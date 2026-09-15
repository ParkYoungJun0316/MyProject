using UnityEngine;

/// <summary>
/// 부활 시전 중 다운된 대상의 입 위로 꿀물이 떨어지는 연출. 프리팹에 미리 둔 비활성 VFX를
/// IsBeingRevived NV를 읽어 ON/OFF만 한다(PlayerBuffVisual 패턴, 네트워크 쓰기 없음).
/// 위치·지속시간 규칙 SSOT: DownedReviveSystemDesign.md §6 "부활 진행 중".
/// </summary>
[RequireComponent(typeof(PlayerDownState))]
public class ReviveHoneyVfx : MonoBehaviour
{
    [Header("VFX Root (Head/ReviveHoneyMist 오브젝트 연결)")]
    [SerializeField] GameObject honeyMistRoot;

    PlayerDownState _down;
    ParticleSystem[] _particles;
    bool _active;

    void Awake()
    {
        _down = GetComponent<PlayerDownState>();
        _particles = CollectParticles(honeyMistRoot);

        if (honeyMistRoot == null) Debug.LogWarning($"[ReviveHoneyVfx] honeyMistRoot 미연결 — {name}", this);
    }

    void OnDisable()
    {
        if (_active) Deactivate();
    }

    void LateUpdate()
    {
        if (_down == null || !_down.IsSpawned) return;

        bool reviving = _down.IsBeingRevived;
        if (reviving && !_active) Activate();
        else if (!reviving && _active) Deactivate();
    }

    void Activate()
    {
        if (honeyMistRoot == null) return;

        // Shield/SpeedUp과 동일 — 재생 중 파티클 위에 그냥 Play()하면 안 지워지고 겹친다.
        StopParticles();
        honeyMistRoot.SetActive(true);
        PlayParticles();
        _active = true;
    }

    void Deactivate()
    {
        _active = false;
        if (honeyMistRoot == null) return;

        StopParticles();
        honeyMistRoot.SetActive(false);
    }

    void PlayParticles()
    {
        for (int i = 0; i < _particles.Length; i++)
            if (_particles[i] != null) _particles[i].Play(withChildren: false);
    }

    void StopParticles()
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    static ParticleSystem[] CollectParticles(GameObject root)
    {
        if (root == null) return System.Array.Empty<ParticleSystem>();
        return root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
    }
}
