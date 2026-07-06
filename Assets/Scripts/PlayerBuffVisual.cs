using UnityEngine;

/// <summary>
/// 버프 VFX 전담 컴포넌트. Player.Network 프리팹 루트에 추가.
///
/// [담당]
///   Shield / SpeedUp 버프 파티클 ON·OFF + 플레이어 고유색 적용.
///
/// [배치 방법]
///   1. Player.Network 루트에 Add Component → PlayerBuffVisual.
///   2. Inspector에서 shieldRoot / speedUpRoot에 Buff/Shield, Buff/SpeedUp 오브젝트 연결.
///   3. 각 파티클 오브젝트는 평소 비활성 + Play On Awake = Off 상태여야 함.
/// </summary>
public class PlayerBuffVisual : MonoBehaviour
{
    [Header("VFX Roots (Buff/Shield, Buff/SpeedUp 오브젝트 연결)")]
    [SerializeField] GameObject shieldRoot;
    [SerializeField] GameObject speedUpRoot;

    // ── 내부 참조 ──────────────────────────────────────────────────

    Player           _player;
    PlayerBuffSystem _buffSystem;

    ParticleSystem[] _shieldParticles;
    ParticleSystem[] _speedUpParticles;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _player     = GetComponent<Player>();
        _buffSystem = GetComponent<PlayerBuffSystem>();

        _shieldParticles  = CollectParticles(shieldRoot);
        _speedUpParticles = CollectParticles(speedUpRoot);

        if (_player     == null) Debug.LogWarning($"[BuffVisual] Player 없음 — {name}", this);
        if (_buffSystem == null) Debug.LogWarning($"[BuffVisual] PlayerBuffSystem 없음 — {name}", this);
        if (shieldRoot  == null) Debug.LogWarning($"[BuffVisual] shieldRoot 미연결 — {name}", this);
        if (speedUpRoot == null) Debug.LogWarning($"[BuffVisual] speedUpRoot 미연결 — {name}", this);
    }

    void OnEnable()
    {
        if (_buffSystem == null) return;
        _buffSystem.OnBuffApplied += OnBuffApplied;
        _buffSystem.OnBuffRemoved += OnBuffRemoved;
    }

    void OnDisable()
    {
        if (_buffSystem == null) return;
        _buffSystem.OnBuffApplied -= OnBuffApplied;
        _buffSystem.OnBuffRemoved -= OnBuffRemoved;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Shield ON")]
    void Test_ShieldOn()  => ActivateVFX(shieldRoot, _shieldParticles);

    [ContextMenu("테스트: Shield OFF")]
    void Test_ShieldOff() => DeactivateVFX(shieldRoot, _shieldParticles);

    [ContextMenu("테스트: SpeedUp ON")]
    void Test_SpeedUpOn()  => ActivateVFX(speedUpRoot, _speedUpParticles);

    [ContextMenu("테스트: SpeedUp OFF")]
    void Test_SpeedUpOff() => DeactivateVFX(speedUpRoot, _speedUpParticles);
#endif

    // ── PlayerBuffSystem 핸들러 ───────────────────────────────────

    void OnBuffApplied(PlayerBuffSystem.BuffType type, float duration)
    {
        switch (type)
        {
            case PlayerBuffSystem.BuffType.Shield:
                ActivateVFX(shieldRoot, _shieldParticles);
                break;
            case PlayerBuffSystem.BuffType.SpeedUp:
                ActivateVFX(speedUpRoot, _speedUpParticles);
                break;
        }
    }

    void OnBuffRemoved(PlayerBuffSystem.BuffType type)
    {
        switch (type)
        {
            case PlayerBuffSystem.BuffType.Shield:
                DeactivateVFX(shieldRoot, _shieldParticles);
                break;
            case PlayerBuffSystem.BuffType.SpeedUp:
                DeactivateVFX(speedUpRoot, _speedUpParticles);
                break;
        }
    }

    // ── VFX 제어 ─────────────────────────────────────────────────

    void ActivateVFX(GameObject root, ParticleSystem[] particles)
    {
        if (root == null) return;

        ApplyColor(particles);
        root.SetActive(true);
        PlayParticles(particles);
    }

    void DeactivateVFX(GameObject root, ParticleSystem[] particles)
    {
        if (root == null) return;

        StopParticles(particles);
        root.SetActive(false);
    }

    void PlayParticles(ParticleSystem[] particles)
    {
        if (particles == null) return;
        for (int i = 0; i < particles.Length; i++)
            if (particles[i] != null) particles[i].Play(withChildren: false);
    }

    void StopParticles(ParticleSystem[] particles)
    {
        if (particles == null) return;
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            particles[i].Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
            particles[i].Clear(withChildren: false);
        }
    }

    // ── 색 적용 ──────────────────────────────────────────────────

    void ApplyColor(ParticleSystem[] particles)
    {
        if (particles == null || _player == null) return;

        // 흑/백 전환과 무관하게 고유색 1개만 사용
        Color c = PlayerColorUtil.GetUniqueColor(_player.playerColorType);

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            var main = particles[i].main;
            main.startColor = new ParticleSystem.MinMaxGradient(c);
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static ParticleSystem[] CollectParticles(GameObject root)
    {
        if (root == null) return System.Array.Empty<ParticleSystem>();
        return root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
    }
}
