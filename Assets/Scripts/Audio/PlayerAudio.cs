using UnityEngine;

/// <summary>
/// 플레이어 SFX 전담 컴포넌트.
/// Player 프리팹 루트에 추가한다.
///
/// [담당 SFX]
///   Player_Hit / Player_Death / Player_InstantKill / Player_FallDeath
///   Player_Respawn / Player_ColorChange / Player_Run (루프)
///   Buff_SpeedUp / Buff_Invincibility / Buff_Received
///
/// [배치 방법]
///   1. Player.G / Player.B 등 프리팹 루트에 Add Component → PlayerAudio.
///   2. runVolume 을 Inspector 에서 설정 (기본 0 → Inspector 에서 조정).
///   3. SFXManager 가 씬에 있어야 함.
///
/// [Player_Run 루프]
///   이 컴포넌트가 자체 AudioSource 를 생성해서 관리함.
///   SFXManager 의 루프 채널을 쓰지 않으므로 플레이어별로 독립됨.
/// </summary>
[RequireComponent(typeof(PlayerEvents))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Run 루프")]
    [Tooltip("달리기 루프 볼륨 (0 ~ 1). 0이면 Inspector에서 미설정 → 1로 처리")]
    [SerializeField] [Range(0f, 1f)] float runVolume = 0f;

    // ── 내부 참조 ─────────────────────────────────────────────────

    Player           _player;
    PlayerEvents     _events;
    PlayerBuffSystem _buffSystem;

    AudioSource _runSource;
    bool        _isRunning;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _player     = GetComponent<Player>();
        _events     = GetComponent<PlayerEvents>();
        _buffSystem = GetComponent<PlayerBuffSystem>();

        // 달리기 전용 AudioSource (Play 상태를 완전히 이 컴포넌트가 제어)
        _runSource              = gameObject.AddComponent<AudioSource>();
        _runSource.playOnAwake  = false;
        _runSource.loop         = true;
        _runSource.spatialBlend = 0f;
    }

    void OnEnable()
    {
        if (_events != null)
        {
            _events.OnDamaged            += OnHit;
            _events.OnDied               += OnDeath;
            _events.OnInstantKilled      += OnInstantKill;
            _events.OnFallDeath          += OnFallDeath;
            _events.OnRespawned          += OnRespawn;
            _events.OnBlackWhiteChanged  += OnBWChanged;
            _events.OnUniqueColorChanged += OnUniqueChanged;
        }

        if (_buffSystem != null)
            _buffSystem.OnBuffApplied += OnBuffApplied;
    }

    void OnDisable()
    {
        if (_events != null)
        {
            _events.OnDamaged            -= OnHit;
            _events.OnDied               -= OnDeath;
            _events.OnInstantKilled      -= OnInstantKill;
            _events.OnFallDeath          -= OnFallDeath;
            _events.OnRespawned          -= OnRespawn;
            _events.OnBlackWhiteChanged  -= OnBWChanged;
            _events.OnUniqueColorChanged -= OnUniqueChanged;
        }

        if (_buffSystem != null)
            _buffSystem.OnBuffApplied -= OnBuffApplied;

        StopRun();
    }

    // ── 달리기 루프 (매 프레임 감지) ──────────────────────────────

    void Update()
    {
        if (_player == null || _player.IsDead)
        {
            StopRun();
            return;
        }

        bool moving = _player.moveInput.sqrMagnitude > 0.0001f;

        if (moving && !_isRunning)
            StartRun();
        else if (!moving && _isRunning)
            StopRun();
    }

    void StartRun()
    {
        if (_runSource == null) return;
        AudioClip clip = SFXManager.Instance?.GetClip(SFXId.Player_Run);
        if (clip == null) return;

        _runSource.clip   = clip;
        _runSource.volume = runVolume > 0f ? runVolume : 1f;
        _runSource.Play();
        _isRunning = true;
    }

    void StopRun()
    {
        if (_runSource != null && _runSource.isPlaying)
            _runSource.Stop();
        _isRunning = false;
    }

    // ── PlayerEvents 핸들러 ───────────────────────────────────────

    void OnHit(bool _)               => SFXManager.Instance?.Play(SFXId.Player_Hit);
    void OnDeath()                   => SFXManager.Instance?.Play(SFXId.Player_Death);
    void OnInstantKill()             => SFXManager.Instance?.Play(SFXId.Player_InstantKill);
    void OnFallDeath()               => SFXManager.Instance?.Play(SFXId.Player_FallDeath);
    void OnRespawn()                 => SFXManager.Instance?.Play(SFXId.Player_Respawn);
    void OnBWChanged(bool _)         => SFXManager.Instance?.Play(SFXId.Player_ColorChange);
    void OnUniqueChanged(int _)      => SFXManager.Instance?.Play(SFXId.Player_ColorChange);

    // ── PlayerBuffSystem 핸들러 ───────────────────────────────────

    void OnBuffApplied(PlayerBuffSystem.BuffType type, float _)
    {
        SFXId id = type switch
        {
            PlayerBuffSystem.BuffType.SpeedUp       => SFXId.Buff_SpeedUp,
            PlayerBuffSystem.BuffType.Invincibility => SFXId.Buff_Invincibility,
            _                                       => SFXId.Buff_Received,
        };
        SFXManager.Instance?.Play(id);
    }
}
