using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 SFX 전담 컴포넌트.
/// Player 프리팹 루트에 추가한다.
///
/// [담당 SFX]
///   개인(Owner 2D): Player_Hit / Player_Death / Player_ColorChange / Player_Run / Buff
///   월드(전 클라 3D): Player_Punch / Player_PunchHit
///
/// [배치 방법]
///   1. Player.G / Player.B 등 프리팹 루트에 Add Component → PlayerAudio.
///   2. runVolume · Punch 3D 거리를 Inspector 에서 설정.
///   3. SFXManager 가 씬에 있어야 함.
/// </summary>
[RequireComponent(typeof(PlayerEvents))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Run 루프")]
    [Tooltip("달리기 루프 볼륨 (0 ~ 1). 0이면 Inspector에서 미설정 → 1로 처리")]
    [SerializeField] [Range(0f, 1f)] float runVolume = 0f;

    [Header("Punch / PunchHit (3D)")]
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float punchMinDistance = 5f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float punchMaxDistance = 25f;
    [SerializeField] AudioRolloffMode punchRolloffMode = AudioRolloffMode.Logarithmic;

    Player           _player;
    PlayerEvents     _events;
    PlayerBuffSystem _buffSystem;
    NetworkObject    _net;

    AudioSource _runSource;
    bool        _isRunning;

    void Awake()
    {
        _player     = GetComponent<Player>();
        _events     = GetComponent<PlayerEvents>();
        _buffSystem = GetComponent<PlayerBuffSystem>();
        _net        = GetComponent<NetworkObject>();

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
            _events.OnBlackWhiteChanged  -= OnBWChanged;
            _events.OnUniqueColorChanged -= OnUniqueChanged;
        }

        if (_buffSystem != null)
            _buffSystem.OnBuffApplied -= OnBuffApplied;

        StopRun();
    }

    bool IsLocalOwner()
    {
        if (_net != null && _net.IsSpawned) return _net.IsOwner;
        return false;
    }

    // ── 달리기 루프 (Owner 2D) ────────────────────────────────────

    void Update()
    {
        if (_player == null || _player.IsDead || !IsLocalOwner())
        {
            StopRun();
            return;
        }

        bool moving = _player.moveInput.sqrMagnitude > 0.0001f;

        if (moving && !_isRunning)
            StartRun();
        else if (!moving && _isRunning)
            StopRun();

        if (_isRunning && _runSource != null)
        {
            float baseVolume = runVolume > 0f ? runVolume : 1f;
            _runSource.volume = baseVolume * (SFXManager.Instance?.EffectiveVolume ?? 1f);
        }
    }

    void StartRun()
    {
        if (_runSource == null) return;
        AudioClip clip = SFXManager.Instance?.GetClip(SFXId.Player_Run);
        if (clip == null) return;

        _runSource.clip = clip;
        _runSource.Play();
        _isRunning = true;
    }

    void StopRun()
    {
        if (_runSource != null && _runSource.isPlaying)
            _runSource.Stop();
        _isRunning = false;
    }

    // ── 개인 SFX (Owner 2D) ───────────────────────────────────────

    void OnHit()
    {
        if (!IsLocalOwner()) return;
        SFXManager.Instance?.Play(SFXId.Player_Hit);
    }

    void OnDeath()
    {
        if (!IsLocalOwner()) return;
        SFXManager.Instance?.Play(SFXId.Player_Death);
    }

    void OnBWChanged(bool _)
    {
        if (!IsLocalOwner()) return;
        SFXManager.Instance?.Play(SFXId.Player_ColorChange);
    }

    void OnUniqueChanged(int _)
    {
        if (!IsLocalOwner()) return;
        SFXManager.Instance?.Play(SFXId.Player_ColorChange);
    }

    void OnBuffApplied(PlayerBuffSystem.BuffType type, float _)
    {
        if (!IsLocalOwner()) return;
        SFXManager.Instance?.Play(SFXId.Buff);
    }

    // ── 월드 SFX (전 클라 3D) ─────────────────────────────────────

    public void PlayPunch3D()
    {
        SFXManager.Instance?.PlayAtPoint(
            SFXId.Player_Punch, transform.position,
            punchMinDistance, punchMaxDistance, punchRolloffMode);
    }

    public void PlayPunchHit3D()
    {
        SFXManager.Instance?.PlayAtPoint(
            SFXId.Player_PunchHit, transform.position,
            punchMinDistance, punchMaxDistance, punchRolloffMode);
    }
}
