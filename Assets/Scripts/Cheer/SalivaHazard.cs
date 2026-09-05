using System.Collections;
using System.Collections.Generic;
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
    bool _skipNextWindow;
    bool _slipActive;
    double _resyncDeadline = -1d;

    // PhaseStartServerTime(Host가 Phase 진입 직전에 찍는 절대 시각)이 전파될 때까지 기다리는 한도.
    // 그 안에 안 오면 앵커가 없는 씬으로 보고 예전처럼 로컬 시각으로 폴백한다.
    const float AnchorWaitTimeout = 3f;
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
        WarnIfNoVolumes();
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

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + PickSeededInterval(generation, RevertAxis);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return;   // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

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
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 창을 열지 않았다(씬 로드 시각 차이 등). 예전엔 여기서 명령을
                // 통째로 버려서 혼자 뒤늦게 바닥이 젖고 다음 성공까지 미끄럼이 유지됐다.
                // 대기 중인 창을 열지 않고 건너뛰어 Host가 준 다음 예약에 위상을 맞춘다.
                _skipNextWindow = true;
                break;
            // Recovering: 직전 창을 되돌리는 중 — 이번 창은 애초에 열지 않았으므로
            // 위에서 받은 _resyncDeadline만 따라가면 위상이 맞는다.
        }
    }

    /// <summary>2.2가 늦게 켜질 때 등 — 수면 알파를 현재 상태로 다시 맞춘다.</summary>
    public void RefreshLateJoinVisuals()
    {
        ApplyCoverAlpha(_coverVisualAlpha);
    }

    IEnumerator HazardCycle()
    {
        yield return ResolveFirstWindow();

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitForResyncDeadline();
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount, ScheduleAxis));
                _cycleCount++;
            }

            if (_skipNextWindow)
            {
                // 팀이 이미 되돌린 창 — 열지 않고 다음 예약으로 넘어간다.
                _skipNextWindow = false;
                _phase = HazardPhase.Idle;
                continue;
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
                _phase = HazardPhase.Idle;
                continue;
            }

            _phase = HazardPhase.Holding;
            while (!_recoverQueued)
                yield return null;

            _recoverQueued = false;
            yield return RecoverRoutine();
            _phase = HazardPhase.Idle;
        }
    }

    /// <summary>
    /// 첫 창을 Host/Client 공통 절대 시각에 건다. 예전엔 로컬 OnEnable + WaitForSeconds라 씬 로드
    /// 시각 차이만큼 첫 Warning 창이 어긋났고, 창 밖에서 외친 표는 Host에서 조용히 버려졌다.
    /// 앵커는 WindTrap/ArrowTrap과 같은 PhaseStartServerTime — 앵커가 없는 씬에서는 로컬 폴백.
    /// </summary>
    IEnumerator ResolveFirstWindow()
    {
        // PhaseManager.EnterPhase()는 objectsToEnable.SetActive(true) 다음에야 MarkAndSyncPhase()를
        // 찍는다. Phase가 이 함정을 켜주는 경우 OnEnable에서 곧바로 읽으면 Host가 직전 Phase의 낡은
        // 앵커를 잡아 Client와 첫 창이 어긋난다(SafeZoneWarnSign과 같은 이유). 한 프레임 양보하면
        // 같은 EnterPhase의 MarkAndSyncPhase가 끝난 뒤 새 앵커를 읽는다.
        yield return null;

        double anchor = -1d;
        float waited = 0f;
        while (waited < AnchorWaitTimeout)
        {
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.PhaseStartServerTime > 0d)
            {
                anchor = sns.PhaseStartServerTime;
                break;
            }
            waited += Time.deltaTime;
            yield return null;
        }

        if (anchor > 0d)
        {
            _resyncDeadline = anchor + initialDelay + PickSeededInterval(_cycleCount, ScheduleAxis);
            _cycleCount++;
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
    }

    IEnumerator CoverRoutine()
    {
        _phase = HazardPhase.Covering;
        ShowCoverRoots(true);
        SetSlipActive(true);

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

    // _phase = Idle 은 호출부(HazardCycle)가 찍는다 — Idle이 "다음 창을 기다리는 중"만 뜻해야
    // Revert가 "창 밖이라 건너뛸 머신"과 "직전 창을 되돌리는 중인 머신"을 구분할 수 있다.
    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();
        SetSlipActive(false);

        float dur = Mathf.Max(0f, recoverDuration);
        float from = _coverVisualAlpha;
        if (dur <= 0f)
        {
            HideCoverImmediate();
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
    }

    /// <summary>
    /// 예약된 재개 시각까지 대기. 대기 중에 Revert가 예약을 갱신할 수 있으므로 매 프레임 필드를
    /// 다시 읽는다 — 인자로 붙잡아 두면 갱신된 예약이 대기 종료 직후 지워져 로컬 랜덤으로 샌다.
    /// </summary>
    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    // 간격을 뽑는 축이 둘이다 — 로컬 스케줄(_cycleCount)과 되돌림 세대(_syncGeneration).
    // 둘 다 1,2,3…으로 올라가므로 축을 안 섞으면 같은 정수 → 같은 간격이 그대로 반복된다.
    const int ScheduleAxis = 0;
    const int RevertAxis   = 1;

    float PickSeededInterval(int generation, int axis)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491) ^ (axis * 0x27220A95);
        // InitState는 전역 RNG를 갈아엎는다 — 뽑고 나서 되돌려야 같은 씬의 다른 시스템
        // (Drop 트랩, VFX 등)이 이 시드 스트림을 물려받지 않는다. 결정성은 그대로.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        float interval = Random.Range(min, max);
        UnityEngine.Random.state = prevState;
        return interval;
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
        _skipNextWindow = false;
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

    /// <summary>
    /// 볼륨이 하나도 없으면 침이 깔리는 연출만 나오고 미끄럼은 전혀 안 걸린다 — 조용히 새는 대신 알린다.
    /// (M.Boss처럼 침을 복습으로 얹을 때 배선을 빠뜨리기 쉬움)
    /// </summary>
    void WarnIfNoVolumes()
    {
        if (volumes != null && volumes.Length > 0) return;
        Debug.LogWarning(
            $"[SalivaHazard] '{name}'에 SalivaVolume이 하나도 없다 — 침이 깔려도 미끄럼이 안 걸린다. " +
            "인스펙터 volumes에 발판 트리거를 연결할 것.", this);
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

    void HideCoverImmediate()
    {
        ApplyCoverAlpha(0f);
        ShowCoverRoots(false);
    }

    // coverRoots는 비주얼 전용. SalivaVolume은 여기에 넣지 말 것(꺼지면 트리거가 사라짐).

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
