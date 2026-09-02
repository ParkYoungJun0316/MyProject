using UnityEngine;

/// <summary>
/// 버프 VFX 전담 컴포넌트. Player.Network 프리팹 루트에 추가.
///
/// [담당]
///   Shield / SpeedUp 버프 VFX ON·OFF만. 색은 각 머티리얼에 고정된 값을 그대로 쓰고
///   플레이어 고유색/흑백을 따라가지 않는다.
///
///   (2026-09-02: 이전에는 Shield 막 색을 플레이어 색에 맞췄으나 —
///   Shield는 Additive 블렌드라 검정(RGB 0)일 때 안 보이고, MaterialPropertyBlock으로
///   DstBlend를 바꿔도 GPU 블렌드 스테이트는 실제로 안 바뀌어 우회가 먹히지 않았음.
///   SpeedUp도 Additive ColorMode + 밝은 베이스 텍스처와 겹치면 어떤 tint를 넣어도
///   흰색으로 saturate돼 색이 안 먹혔음. 두 문제의 근본 원인이 같아 색 추종 자체를
///   제거함. Shield를 또렷하게 보이려면 ShieldBubble.mat의 Surface Type을
///   Alpha Blend로 바꿔야 함 — 에셋 수정은 Inspector에서 사용자가 직접.)
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

    PlayerBuffSystem _buffSystem;

    ParticleSystem[] _shieldParticles;
    ParticleSystem[] _speedUpParticles;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _buffSystem = GetComponent<PlayerBuffSystem>();

        _shieldParticles  = CollectParticles(shieldRoot);
        _speedUpParticles = CollectParticles(speedUpRoot);

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

    static ParticleSystem[] CollectParticles(GameObject root)
    {
        if (root == null) return System.Array.Empty<ParticleSystem>();
        return root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
    }
}
