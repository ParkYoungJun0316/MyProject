using UnityEngine;

/// <summary>
/// SpikeTrap 발동 순간(TrapBase.OnFiring)에 파티클을 재생한다.
/// spikeVisual 메시 없이 파티클만으로 연출할 때 사용.
///
/// [설정]
/// 1. SpikeTrap과 같은 GameObject에 부착
/// 2. raiseParticle에 1회성(burst, non-loop) ParticleSystem 연결
/// 3. SpikeTrap.spikeVisual은 비워둬도 됨 (콜라이더만으로 판정)
/// </summary>
[RequireComponent(typeof(SpikeTrap))]
public class SpikeTrapVFX : MonoBehaviour
{
    [Tooltip("발동 시 재생할 파티클. 비워두면 자식에서 자동 탐색")]
    [SerializeField] ParticleSystem raiseParticle = null;

    TrapBase _trap;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();

        if (raiseParticle == null)
            raiseParticle = GetComponentInChildren<ParticleSystem>(true);
    }

    void OnEnable()
    {
        if (_trap != null)
            _trap.OnFiring += HandleFiring;
    }

    void OnDisable()
    {
        if (_trap != null)
            _trap.OnFiring -= HandleFiring;

        // UnityEngine.Object fake-null 대비: ?. 대신 != null
        if (raiseParticle != null)
            raiseParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void HandleFiring()
    {
        if (raiseParticle == null) return;
        raiseParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        raiseParticle.Play();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: Raise Particle Play")]
    void TestPlay()
    {
        if (raiseParticle == null)
            raiseParticle = GetComponentInChildren<ParticleSystem>(true);
        if (raiseParticle != null) raiseParticle.Play();
    }
#endif
}
