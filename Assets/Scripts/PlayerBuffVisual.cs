using UnityEngine;

/// <summary>
/// 버프 VFX 전담 컴포넌트. Player.Network 프리팹 루트에 추가.
///
/// [담당]
///   Shield / SpeedUp 버프 VFX ON·OFF + 플레이어 고유색 적용.
///   Shield는 메쉬 막(MeshRenderer) + 보조 파티클.
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

    [Header("Shield 막 투명도")]
    [SerializeField, Range(0f, 1f)] float shieldAlpha = 0.35f;

    // ── 내부 참조 ──────────────────────────────────────────────────

    Player           _player;
    PlayerBuffSystem _buffSystem;

    ParticleSystem[] _shieldParticles;
    ParticleSystem[] _speedUpParticles;
    MeshRenderer[]   _shieldMeshes;
    MaterialPropertyBlock _mpb;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _player     = GetComponent<Player>();
        _buffSystem = GetComponent<PlayerBuffSystem>();

        _shieldParticles  = CollectParticles(shieldRoot);
        _speedUpParticles = CollectParticles(speedUpRoot);
        _shieldMeshes     = CollectMeshes(shieldRoot);
        _mpb              = new MaterialPropertyBlock();

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
    void Test_ShieldOn()  => ActivateVFX(shieldRoot, _shieldParticles, _shieldMeshes);

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
                ActivateVFX(shieldRoot, _shieldParticles, _shieldMeshes);
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

    void ActivateVFX(GameObject root, ParticleSystem[] particles, MeshRenderer[] meshes = null)
    {
        if (root == null) return;

        // 이미 재생 중인 파티클 위에 그냥 Play()하면 기존 파티클이 안 지워지고 겹쳐서
        // 다중 막처럼 보인다(예: Shield 3중 겹침). 재생 여부와 무관하게 항상 먼저 정리 후 재생.
        StopParticles(particles);
        ApplyColor(particles, meshes);
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

    void ApplyColor(ParticleSystem[] particles, MeshRenderer[] meshes)
    {
        if (_player == null) return;

        // 흑/백 전환과 무관하게 고유색 1개만 사용. 막은 반투명 유지.
        Color c = PlayerColorUtil.GetUniqueColor(_player.playerColorType);
        Color meshColor = c;
        meshColor.a = shieldAlpha;

        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var main = particles[i].main;
                main.startColor = new ParticleSystem.MinMaxGradient(c);
            }
        }

        if (meshes == null) return;
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null) continue;
            // ShieldBubble.mat = URP Lit → _BaseColor만 읽음. 다른 셰이더로 교체 시 여기도 맞춰 갱신.
            meshes[i].GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", meshColor);
            meshes[i].SetPropertyBlock(_mpb);
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static ParticleSystem[] CollectParticles(GameObject root)
    {
        if (root == null) return System.Array.Empty<ParticleSystem>();
        return root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
    }

    static MeshRenderer[] CollectMeshes(GameObject root)
    {
        if (root == null) return System.Array.Empty<MeshRenderer>();
        return root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
    }
}
