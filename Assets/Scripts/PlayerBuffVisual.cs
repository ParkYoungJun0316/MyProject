using UnityEngine;

/// <summary>
/// 버프 VFX 전담 컴포넌트. Player.Network 프리팹 루트에 추가.
///
/// [담당]
///   Shield / SpeedUp 버프 VFX ON·OFF.
///
///   Shield : Buff/Shield의 ShieldBubbleFx(투명막 + Sparkles + BreakShards).
///            막·파편 색 = Player.uniqueColor (흑백 모드와 무관하게 고유색 고정).
///            버프 제거(피격으로 charge 소진 / 시간 만료 공통) 시 Break — 막이 사라지고 파편이 흩어짐.
///            파편이 보여야 하므로 Shield 루트는 제거 시 비활성화하지 않는다.
///   SpeedUp: Buff/SpeedUp의 흰 먼지(Dust = 달릴 때 거리 비례, DustIdle = 서 있을 때 구름). 색 고정.
///
///   (2026-09-23: 흰 Additive 구체 → ShieldMembrane 셰이더(알파 블렌드 프레넬)로 교체.
///   예전 색 추종이 검정에서 안 보이던 원인은 Additive 블렌드였고, 알파 블렌드라 해결됨.)
///
/// [배치 방법]
///   1. Player.Network 루트에 Add Component → PlayerBuffVisual.
///   2. Inspector에서 shieldRoot / speedUpRoot에 Buff/Shield, Buff/SpeedUp 오브젝트 연결.
///   3. 각 오브젝트는 평소 비활성 + 파티클 Play On Awake = Off 상태여야 함.
/// </summary>
public class PlayerBuffVisual : MonoBehaviour
{
    [Header("VFX Roots (Buff/Shield, Buff/SpeedUp 오브젝트 연결)")]
    [SerializeField] GameObject shieldRoot;
    [SerializeField] GameObject speedUpRoot;

    // ── 내부 참조 ──────────────────────────────────────────────────

    PlayerBuffSystem _buffSystem;
    Player _player;
    ShieldBubbleFx _shieldFx;

    // 루프 파티클만 ON/OFF 대상. 1회성(BreakShards)은 ShieldBubbleFx가 직접 재생.
    ParticleSystem[] _shieldParticles;
    ParticleSystem[] _speedUpParticles;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _buffSystem = GetComponent<PlayerBuffSystem>();
        _player = GetComponent<Player>();
        if (shieldRoot != null) _shieldFx = shieldRoot.GetComponent<ShieldBubbleFx>();

        _shieldParticles  = CollectLoopParticles(shieldRoot);
        _speedUpParticles = CollectLoopParticles(speedUpRoot);

        if (_buffSystem == null) Debug.LogWarning($"[BuffVisual] PlayerBuffSystem 없음 — {name}", this);
        if (_player == null)     Debug.LogWarning($"[BuffVisual] Player 없음 — {name}", this);
        if (shieldRoot  == null) Debug.LogWarning($"[BuffVisual] shieldRoot 미연결 — {name}", this);
        if (speedUpRoot == null) Debug.LogWarning($"[BuffVisual] speedUpRoot 미연결 — {name}", this);
        if (shieldRoot != null && _shieldFx == null) Debug.LogWarning($"[BuffVisual] Buff/Shield에 ShieldBubbleFx 없음 — {name}", this);
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
    void Test_ShieldOn()  => ShowShield();

    [ContextMenu("테스트: Shield OFF (깨짐)")]
    void Test_ShieldOff() => BreakShield();

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
                ShowShield();
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
                BreakShield();
                break;
            case PlayerBuffSystem.BuffType.SpeedUp:
                DeactivateVFX(speedUpRoot, _speedUpParticles);
                break;
        }
    }

    // ── Shield ───────────────────────────────────────────────────

    void ShowShield()
    {
        if (shieldRoot == null) return;
        ActivateVFX(shieldRoot, _shieldParticles);
        if (_shieldFx == null) return;
        if (_player != null) _shieldFx.SetColor(_player.uniqueColor);
        _shieldFx.Show();
    }

    void BreakShield()
    {
        if (shieldRoot == null) return;
        StopParticles(_shieldParticles);
        if (_shieldFx != null) _shieldFx.Break();
        else shieldRoot.SetActive(false);
    }

    // ── VFX 제어 ─────────────────────────────────────────────────

    void ActivateVFX(GameObject root, ParticleSystem[] particles)
    {
        if (root == null) return;

        // 이미 재생 중인 파티클 위에 그냥 Play()하면 기존 파티클이 안 지워지고 겹쳐서
        // 다중 막처럼 보인다(예: Shield 3중 겹침). 재생 여부와 무관하게 항상 먼저 정리 후 재생.
        StopParticles(particles);
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

    // ── 유틸 ─────────────────────────────────────────────────────

    static ParticleSystem[] CollectLoopParticles(GameObject root)
    {
        if (root == null) return System.Array.Empty<ParticleSystem>();
        var all = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        return System.Array.FindAll(all, ps => ps.main.loop);
    }
}
