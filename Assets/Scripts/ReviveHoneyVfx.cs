using System.Collections;
using UnityEngine;

/// <summary>
/// 부활 시전 중 시전자 → 다운된 대상에게 EffectExamples Waterfall을 노란색 꿀물로 흘린다.
/// SSOT: DownedReviveSystemDesign.md §6. 네트워크 쓰기 없음 — IsBeingRevived NV만 읽는다.
///
/// [연출]
///  - 시전: WaterfallSmallEffect를 시전자 가슴 → 대상 몸통에 맞춰 늘리고 노란 틴트.
///  - 캔슬: StopEmittingAndClear로 즉시 끊김.
///  - 완료: 이미 나온 입자는 잠깐 남기고 방출만 멈춤.
///
/// [배치]
/// Player 프리팹(Kkultteok)에 PlayerDownState와 함께 부착.
/// waterfallPrefab ← Assets/UnityAssets/EffectExamples/WaterEffects/Prefabs/WaterfallSmallEffect
/// </summary>
[RequireComponent(typeof(PlayerDownState))]
public class ReviveHoneyVfx : MonoBehaviour
{
    [Header("폭포")]
    [Tooltip("Menu 씬 Waterfall과 같은 프리팹. WaterfallSmallEffect.")]
    [SerializeField] GameObject waterfallPrefab;

    [Tooltip("꿀물 색. 원본 물 그라디언트를 이 색으로 덮는다.")]
    [SerializeField] Color honeyColor = new Color(1f, 0.72f, 0.12f, 0.9f);

    [Header("위치")]
    [Tooltip("시전자 로컬 오프셋 — 가슴/손 높이에서 붓는다.")]
    [SerializeField] Vector3 casterOffset = new Vector3(0f, 1.15f, 0.2f);

    [Tooltip("다운된 대상 로컬 오프셋 — 누운 몸통.")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 0.4f, 0f);

    [SerializeField] float minScale = 0.08f;
    [SerializeField] float maxScale = 0.28f;

    [Tooltip("완료 시 남은 입자가 사라질 때까지 기다리는 시간.")]
    [SerializeField] float completeLinger = 0.35f;

    PlayerDownState _down;
    GameObject _instance;
    ParticleSystem[] _systems;
    Vector3 _sourceLocal;
    Vector3 _flowLocal;
    bool _playing;
    bool _wasReviving;
    Coroutine _hideRoutine;
    bool _loggedMissingPrefab;

    void Awake()
    {
        _down = GetComponent<PlayerDownState>();
    }

    void OnDisable()
    {
        StopStream(clear: true);
    }

    void OnDestroy()
    {
        if (_instance != null)
            Destroy(_instance);
    }

    void LateUpdate()
    {
        if (_down == null || !_down.IsSpawned) return;

        bool reviving = _down.IsBeingRevived;
        if (reviving)
        {
            if (!_playing) StartStream();
            AlignStream();
        }
        else if (_wasReviving)
        {
            bool cancelled = _down.IsDowned;
            StopStream(clear: cancelled);
        }

        _wasReviving = reviving;
    }

    void StartStream()
    {
        if (!EnsureInstance()) return;

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        _instance.SetActive(true);
        for (int i = 0; i < _systems.Length; i++)
        {
            if (_systems[i] == null) continue;
            _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _systems[i].Play(true);
        }

        _playing = true;
        AlignStream();
    }

    void StopStream(bool clear)
    {
        if (_instance == null)
        {
            _playing = false;
            return;
        }

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        var mode = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        if (_systems != null)
        {
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null) continue;
                _systems[i].Stop(true, mode);
            }
        }

        _playing = false;

        if (clear)
            _instance.SetActive(false);
        else
            _hideRoutine = StartCoroutine(HideAfterLinger());
    }

    IEnumerator HideAfterLinger()
    {
        yield return new WaitForSeconds(completeLinger);
        if (_instance != null && !_playing)
            _instance.SetActive(false);
        _hideRoutine = null;
    }

    void AlignStream()
    {
        if (_instance == null || !_playing) return;

        Transform reviver = FindReviver();
        if (reviver == null) return;

        Vector3 from = reviver.TransformPoint(casterOffset);
        Vector3 to = transform.TransformPoint(targetOffset);
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.05f) return;

        float flowLen = _flowLocal.magnitude;
        if (flowLen < 0.01f) return;

        float scale = Mathf.Clamp(dist / flowLen, minScale, maxScale);
        Quaternion rot = Quaternion.FromToRotation(_flowLocal / flowLen, delta / dist);
        Vector3 pos = from - rot * (_sourceLocal * scale);

        _instance.transform.SetPositionAndRotation(pos, rot);
        _instance.transform.localScale = Vector3.one * scale;
    }

    Transform FindReviver()
    {
        ulong id = _down.ReviverClientId;
        if (id == ulong.MaxValue) return null;

        var spawned = PlayerDownState.AllSpawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            PlayerDownState other = spawned[i];
            if (other != null && other.IsSpawned && other.OwnerClientId == id)
                return other.transform;
        }

        return null;
    }

    bool EnsureInstance()
    {
        if (_instance != null) return true;
        if (waterfallPrefab == null)
        {
            if (!_loggedMissingPrefab)
            {
                Debug.LogWarning("[ReviveHoneyVfx] waterfallPrefab이 비어 있습니다. WaterfallSmallEffect를 연결하세요.", this);
                _loggedMissingPrefab = true;
            }
            return false;
        }

        _instance = Instantiate(waterfallPrefab);
        _instance.name = "ReviveHoneyWaterfall";
        _instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _instance.transform.localScale = Vector3.one;

        CacheAnchors();
        _systems = _instance.GetComponentsInChildren<ParticleSystem>(true);
        ApplyHoneyLook();

        _instance.SetActive(false);
        return true;
    }

    void CacheAnchors()
    {
        Transform root = _instance.transform;
        Transform source = FindDeep(root, "WaterfallSmall");
        Transform splash = FindDeep(root, "WaterRipples");

        _sourceLocal = source != null
            ? root.InverseTransformPoint(source.position)
            : Vector3.zero;
        Vector3 splashLocal = splash != null
            ? root.InverseTransformPoint(splash.position)
            : _sourceLocal + Vector3.down * 11f;

        _flowLocal = splashLocal - _sourceLocal;
        if (_flowLocal.sqrMagnitude < 0.0001f)
            _flowLocal = Vector3.down;
    }

    void ApplyHoneyLook()
    {
        var honey = new ParticleSystem.MinMaxGradient(honeyColor);
        var honeyFade = BuildHoneyFade();

        for (int i = 0; i < _systems.Length; i++)
        {
            ParticleSystem ps = _systems[i];
            if (ps == null) continue;

            var main = ps.main;
            main.playOnAwake = false;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = honey;

            var col = ps.colorOverLifetime;
            if (col.enabled)
                col.color = honeyFade;

            var trails = ps.trails;
            if (trails.enabled)
            {
                trails.colorOverLifetime = honey;
                trails.colorOverTrail = honey;
            }
        }
    }

    ParticleSystem.MinMaxGradient BuildHoneyFade()
    {
        var g = new Gradient();
        Color bright = honeyColor;
        Color deep = honeyColor * 0.85f;
        deep.a = honeyColor.a;
        g.SetKeys(
            new[]
            {
                new GradientColorKey(bright, 0f),
                new GradientColorKey(deep, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(honeyColor.a, 0.25f),
                new GradientAlphaKey(honeyColor.a, 0.85f),
                new GradientAlphaKey(0f, 1f),
            });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
