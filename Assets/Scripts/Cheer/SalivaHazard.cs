using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 침 함정 — 팀 응원 되돌림 대상 (ITeamCheerRevert).
/// MouthController.teamCheerHazard 와 같은 머신. 새 RPC 없음.
///
/// Idle (응원 무시)
/// → Warning (UI. 이때부터 응원)
///      ├─ 외침: Cover 안 넣음. Idle
///      └─ 없음: Cover 끝까지 → Hold (바닥이 젖은 채) → 외침 시 Recover → Idle
/// Cover가 시작되면 끊지 않음. 자동 복구 없음.
///
/// 미끄럼은 PhysicMaterial이 아니라 Player.Move() 얼음 가속/코스트 + SalivaVolume.
/// </summary>
public class SalivaHazard : MonoBehaviour, ITeamCheerRevert
{
    enum HazardPhase
    {
        Idle,
        Warning,
        Covering,
        Holding,
        Recovering,
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    [Header("볼륨 / 비주얼")]
    [Tooltip("발판 위 트리거. 비우면 자식에서 수집.")]
    [SerializeField] SalivaVolume[] volumes = null;

    [Tooltip("침 수면(WaterPlane 등). Cover~Hold 동안 켜고 Recover 후 끈다. SalivaVolume은 여기 자식으로 넣지 말 것.")]
    [SerializeField] GameObject[] coverRoots = null;

    [Tooltip("알파 페이드 대상. 비우면 coverRoots의 Renderer를 수집.")]
    [SerializeField] Renderer[] coverRenderers = null;

    [Tooltip("깔릴 때 재생. Recover 때 정지.")]
    [SerializeField] ParticleSystem[] coverParticles = null;

    [Tooltip("완전 덮였을 때 수면 알파. 수치는 스테이지 때.")]
    [SerializeField] [Range(0.05f, 1f)] float coverAlpha = 0.7f;

    [Header("클립 길이 (초) — 수치는 스테이지 때")]
    [Tooltip("바닥을 덮는 시간. Cover가 시작되면 이 길이는 끊지 않음.")]
    [SerializeField] float coverDuration = 0.6f;

    [Tooltip("지워지는 페이드 시간.")]
    [SerializeField] float recoverDuration = 0.6f;

    [Header("랜덤 스케줄")]
    [SerializeField] float randomIntervalMin = 5f;
    [SerializeField] float randomIntervalMax = 15f;
    [SerializeField] float initialDelay = 0f;
    [SerializeField] bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("Cover 전 Warning 유지 시간(초). 수치는 나중에 튜닝.")]
    [SerializeField] float warnDuration = 2f;

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("입 MouthController seedSalt와 겹치지 않게 유지.")]
    [SerializeField] int seedSalt = 0x53504954;

    MaterialPropertyBlock _mpb;
    Color[] _baseColors;
    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    HazardPhase _phase = HazardPhase.Idle;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _slipActive;
    double _resyncDeadline = -1d;
    int _cycleCount;
    int _syncGeneration;
    float _coverVisualAlpha;

    public bool IsAvailable => _available;

    /// <summary>Cover 시작부터 Recover 직전까지 true. SalivaVolume이 그립을 켤지 판단.</summary>
    public bool IsSlipActive => _slipActive;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (volumes == null || volumes.Length == 0)
            volumes = GetComponentsInChildren<SalivaVolume>(true);
        if ((coverRenderers == null || coverRenderers.Length == 0) && coverRoots != null)
            CollectRenderersFromRoots();
        CacheBaseColors();
        WarnIfVolumeUnderCoverRoot();
        HideCoverImmediate();
    }

    void OnEnable()
    {
        ResetHazardFlags();
        HideCoverImmediate();
        _bindRoutine = StartCoroutine(BindAndStartHazard());
    }

    void OnDisable()
    {
        if (CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);
        StopAllCoroutines();
        _cycleCoroutine = null;
        _bindRoutine = null;
        SetSlipActive(false);
        ResetHazardFlags();
        HideCoverImmediate();
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        if (startOnAwake)
            StartCycle();
    }

    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(HazardCycle());
    }

    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        SetSlipActive(false);
        HideCoverImmediate();
    }

    public void Revert()
    {
        if (!_available) return;

        _syncGeneration++;
        _resyncDeadline = GetServerTime() + PickSeededInterval(_syncGeneration);

        switch (_phase)
        {
            case HazardPhase.Warning:
                _prevented = true;
                EndWindow();
                break;
            case HazardPhase.Covering:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                break;
        }
    }

    /// <summary>2.2가 늦게 켜질 때 등 — 비주얼·파티클을 현재 페이즈에 맞춘다.</summary>
    public void RefreshLateJoinVisuals()
    {
        ApplyCoverAlpha(_coverVisualAlpha);
        if (_slipActive)
            PlayCoverParticles();
        else
            StopCoverParticles();
    }

    IEnumerator HazardCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitUntilServerTime(_resyncDeadline);
                _resyncDeadline = -1d;
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount));
                _cycleCount++;
            }

            _prevented = false;
            _recoverQueued = false;
            _phase = HazardPhase.Warning;
            _available = true;
            CheerService.Instance?.NotifyHazardWindow(true);

            float warnElapsed = 0f;
            float warn = Mathf.Max(0f, warnDuration);
            while (warnElapsed < warn && !_prevented)
            {
                warnElapsed += Time.deltaTime;
                yield return null;
            }

            if (_prevented)
            {
                _prevented = false;
                _phase = HazardPhase.Idle;
                continue;
            }

            yield return CoverRoutine();

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return RecoverRoutine();
                continue;
            }

            _phase = HazardPhase.Holding;
            while (!_recoverQueued)
                yield return null;

            _recoverQueued = false;
            yield return RecoverRoutine();
        }
    }

    IEnumerator CoverRoutine()
    {
        _phase = HazardPhase.Covering;
        ShowCoverRoots(true);
        SetSlipActive(true);
        PlayCoverParticles();

        float dur = Mathf.Max(0f, coverDuration);
        if (dur <= 0f)
        {
            ApplyCoverAlpha(1f);
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            ApplyCoverAlpha(Mathf.Clamp01(t / dur));
            yield return null;
        }
        ApplyCoverAlpha(1f);
    }

    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();
        SetSlipActive(false);
        StopCoverParticles();

        float dur = Mathf.Max(0f, recoverDuration);
        float from = _coverVisualAlpha;
        if (dur <= 0f)
        {
            HideCoverImmediate();
            _phase = HazardPhase.Idle;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            ApplyCoverAlpha(Mathf.Lerp(from, 0f, Mathf.Clamp01(t / dur)));
            yield return null;
        }

        HideCoverImmediate();
        _phase = HazardPhase.Idle;
    }

    IEnumerator WaitUntilServerTime(double deadline)
    {
        while (GetServerTime() < deadline)
            yield return null;
    }

    float PickSeededInterval(int generation)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491);
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        return Random.Range(min, max);
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _resyncDeadline = -1d;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void SetSlipActive(bool active)
    {
        if (_slipActive == active) return;
        _slipActive = active;
        if (volumes == null) return;
        for (int i = 0; i < volumes.Length; i++)
            volumes[i]?.NotifySlipChanged(active);
    }

    void CollectRenderersFromRoots()
    {
        if (coverRoots == null) return;
        var list = new System.Collections.Generic.List<Renderer>();
        for (int i = 0; i < coverRoots.Length; i++)
        {
            if (coverRoots[i] == null) continue;
            list.AddRange(coverRoots[i].GetComponentsInChildren<Renderer>(true));
        }
        coverRenderers = list.ToArray();
    }

    void CacheBaseColors()
    {
        if (coverRenderers == null)
        {
            _baseColors = null;
            return;
        }

        _baseColors = new Color[coverRenderers.Length];
        for (int i = 0; i < coverRenderers.Length; i++)
        {
            Renderer r = coverRenderers[i];
            if (r == null || r.sharedMaterial == null)
            {
                _baseColors[i] = Color.white;
                continue;
            }

            Material mat = r.sharedMaterial;
            if (mat.HasProperty("_BaseColor"))
                _baseColors[i] = mat.GetColor(BaseColorId);
            else if (mat.HasProperty("_Color"))
                _baseColors[i] = mat.GetColor(ColorId);
            else
                _baseColors[i] = mat.color;
        }
    }

    void WarnIfVolumeUnderCoverRoot()
    {
        if (volumes == null || coverRoots == null) return;
        for (int i = 0; i < volumes.Length; i++)
        {
            SalivaVolume vol = volumes[i];
            if (vol == null) continue;
            for (int j = 0; j < coverRoots.Length; j++)
            {
                GameObject root = coverRoots[j];
                if (root == null) continue;
                if (vol.transform == root.transform || vol.transform.IsChildOf(root.transform))
                    Debug.LogWarning(
                        "[SalivaHazard] SalivaVolume이 coverRoots 아래 있습니다. Cover가 꺼지면 트리거가 사라집니다. 볼륨은 수면의 형제로 두세요.",
                        vol);
            }
        }
    }

    void ShowCoverRoots(bool on)
    {
        if (coverRoots == null) return;
        for (int i = 0; i < coverRoots.Length; i++)
            if (coverRoots[i] != null)
                coverRoots[i].SetActive(on);
    }

    void ApplyCoverAlpha(float normalized)
    {
        _coverVisualAlpha = normalized;
        if (coverRenderers == null || _mpb == null) return;
        float a = normalized * coverAlpha;
        for (int i = 0; i < coverRenderers.Length; i++)
        {
            Renderer r = coverRenderers[i];
            if (r == null) continue;
            Color c = _baseColors != null && i < _baseColors.Length ? _baseColors[i] : Color.white;
            c.a = a;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    void PlayCoverParticles()
    {
        if (coverParticles == null) return;
        for (int i = 0; i < coverParticles.Length; i++)
        {
            ParticleSystem ps = coverParticles[i];
            if (ps == null || !ps.gameObject.activeInHierarchy) continue;
            ps.Play(true);
        }
    }

    void StopCoverParticles()
    {
        if (coverParticles == null) return;
        for (int i = 0; i < coverParticles.Length; i++)
        {
            ParticleSystem ps = coverParticles[i];
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void HideCoverImmediate()
    {
        ApplyCoverAlpha(0f);
        StopCoverParticles();
        ShowCoverRoots(false);
    }

    // coverRoots는 비주얼 전용. SalivaVolume은 여기에 넣지 말 것(꺼지면 트리거가 사라짐).

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
