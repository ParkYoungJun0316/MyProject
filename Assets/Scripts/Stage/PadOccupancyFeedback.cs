using UnityEngine;

/// <summary>
/// PressurePad / ColorTile 점유 시 PadRipple 파티클을 로컬로 켜고 끈다.
/// 네트워크 동기화 없음 — 발판 트리거가 이미 각 머신에서 점유를 알고 있다.
///
/// [연결]
///  PressurePad : OnCountChanged (current &gt; 0 이면 Play, 0이면 Stop). OnFulfilled 에 걸지 말 것.
///  ColorTile   : OnCompleted → Play, OnUncompleted → Stop.
///  ColorStartZone 에는 붙이지 않는다 (물결 없음).
///
/// [씬 설정]
///  1. 발판/타일 프리팹에 이 컴포넌트 추가
///  2. ripplePrefab 에 Assets/Art/Particle/PadRipple 연결
///     (또는 자손으로 PadRipple 인스턴스를 넣고 ripple 에 연결)
/// </summary>
public class PadOccupancyFeedback : MonoBehaviour
{
    [Header("파티클")]
    [Tooltip("비워두면 자손 ParticleSystem을 쓰고, 그것도 없으면 ripplePrefab을 스폰한다.")]
    [SerializeField] ParticleSystem ripple;

    [Tooltip("자손에 파티클이 없을 때 스폰할 프리팹. PadRipple 권장.")]
    [SerializeField] ParticleSystem ripplePrefab;

    [Tooltip("렌더러 윗면보다 얼마나 위에 둘지 (월드 미터)")]
    [SerializeField] float heightOffset = 0.05f;

    PressurePad _pad;
    ColorTile   _tile;
    bool        _playing;
    bool        _spawnedInstance;
    bool        _ready;

    void Awake()
    {
        EnsureRipple();
        StopRipple();
    }

    void Start()
    {
        _ready = true;
        Subscribe();
        ApplyTint();
        PlaceOnTop();
    }

    void OnEnable()
    {
        if (_ready) Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        StopRipple();
    }

    void OnDestroy()
    {
        Unsubscribe();

        if (_spawnedInstance && ripple != null)
            Destroy(ripple.gameObject);
    }

    // ── 스폰 / 배치 ──────────────────────────────────────────────

    void EnsureRipple()
    {
        if (ripple == null)
            ripple = GetComponentInChildren<ParticleSystem>(true);

        if (ripple == null && ripplePrefab != null)
        {
            ripple = Instantiate(ripplePrefab, transform);
            ripple.gameObject.name = "PadRipple";
            _spawnedInstance = true;
        }

        // PadRipple의 main.scalingMode = Local이라 부모(발판)의 non-uniform scale
        // (PressurePad 5,0.5,5 / ColorTile 2,5,2)은 원래 크기에 영향을 주지 않는다.
        // localScale을 여기서 임의로 건드리지 않는다 — 필요하면 Inspector에서 직접 조정할 것.
    }

    void PlaceOnTop()
    {
        if (ripple == null) return;

        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
            rend = GetComponentInChildren<Renderer>();

        Vector3 pos;
        if (rend != null)
        {
            pos = rend.bounds.center;
            pos.y = rend.bounds.max.y + heightOffset;
        }
        else
        {
            pos = transform.position;
            pos.y += heightOffset;
        }

        ripple.transform.position = pos;
        ripple.transform.rotation = Quaternion.identity;
    }

    void ApplyTint()
    {
        if (ripple == null) return;

        PlayerColorType colorType = PlayerColorType.Common;
        if (_tile != null)
            colorType = _tile.RequiredColorType;
        else if (_pad != null)
            colorType = _pad.EffectiveColor;

        Color c = PlayerColorUtil.GetUniqueColor(colorType);
        c.a = 0.85f;

        var main = ripple.main;
        main.startColor = c;
    }

    // ── 구독 ─────────────────────────────────────────────────────

    void Subscribe()
    {
        Unsubscribe();

        _pad  = GetComponent<PressurePad>();
        _tile = GetComponent<ColorTile>();

        if (_pad != null)
            _pad.OnCountChanged.AddListener(HandleCountChanged);

        if (_tile != null)
        {
            _tile.OnCompleted.AddListener(PlayRipple);
            _tile.OnUncompleted.AddListener(StopRipple);
        }
    }

    void Unsubscribe()
    {
        if (_pad != null)
            _pad.OnCountChanged.RemoveListener(HandleCountChanged);

        if (_tile != null)
        {
            _tile.OnCompleted.RemoveListener(PlayRipple);
            _tile.OnUncompleted.RemoveListener(StopRipple);
        }
    }

    void HandleCountChanged(int current, int _)
    {
        if (current > 0) PlayRipple();
        else             StopRipple();
    }

    // ── Play / Stop ──────────────────────────────────────────────

    void PlayRipple()
    {
        if (ripple == null || _playing) return;

        ApplyTint();
        PlaceOnTop();
        ripple.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ripple.Play();
        _playing = true;
    }

    void StopRipple()
    {
        _playing = false;
        if (ripple == null) return;

        ripple.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
