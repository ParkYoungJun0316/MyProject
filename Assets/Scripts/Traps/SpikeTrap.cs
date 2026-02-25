using System.Collections;
using UnityEngine;

/// <summary>
/// 바닥에 배치하는 가시 함정.
/// activateInterval마다 가시가 올라왔다가 raiseDuration 후 내려감.
/// 
/// [설정 방법]
/// 1. 이 스크립트를 붙인 GameObject에 Collider(Trigger) 추가 → 데미지 판정 영역
/// 2. spikeVisual에 실제 가시 메시 오브젝트(자식) 연결 → 올라갔다 내려가는 비주얼
/// 3. spikeVisual이 없으면 히트박스만 동작 (비주얼 없이도 작동)
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpikeTrap : TrapBase
{
    [Header("Spike Trap")]
    [Tooltip("가시 비주얼 Transform (자식 오브젝트). 없으면 비주얼 이동 생략")]
    [SerializeField] private Transform spikeVisual = null;

    [Tooltip("플레이어에게 입히는 데미지")]
    [SerializeField] private int damage = 0;

    [Tooltip("가시가 완전히 올라와 있는 유지 시간(초)")]
    [SerializeField] private float raiseDuration = 0f;

    [Tooltip("가시가 올라오는 높이 (로컬 Y)")]
    [SerializeField] private float raiseHeight = 0f;

    [Tooltip("가시 이동 속도 (m/s). 0이면 즉시")]
    [SerializeField] private float raiseSpeed = 0f;

    [Tooltip("연속 데미지 간격(초). 0이면 닿을 때마다 즉시")]
    [SerializeField] private float damageInterval = 0f;

    Collider spikeTrigger;
    Vector3 loweredLocalPos;
    bool isRaised;
    float nextDamageTime;

    protected override void Start()
    {
        spikeTrigger = GetComponent<Collider>();
        spikeTrigger.isTrigger = true;
        spikeTrigger.enabled = false;

        if (spikeVisual != null)
            loweredLocalPos = spikeVisual.localPosition;

        base.Start();
    }

    protected override void OnTrapTrigger()
    {
        if (!isRaised) StartCoroutine(RaiseCycle());
    }

    IEnumerator RaiseCycle()
    {
        isRaised = true;
        spikeTrigger.enabled = true;

        yield return MoveSpikeLocal(loweredLocalPos, loweredLocalPos + Vector3.up * raiseHeight);

        yield return new WaitForSeconds(raiseDuration);

        yield return MoveSpikeLocal(loweredLocalPos + Vector3.up * raiseHeight, loweredLocalPos);

        spikeTrigger.enabled = false;
        isRaised = false;
    }

    IEnumerator MoveSpikeLocal(Vector3 from, Vector3 to)
    {
        if (spikeVisual == null) yield break;

        float dist = Vector3.Distance(from, to);
        float duration = (raiseSpeed > 0f) ? dist / raiseSpeed : 0.01f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            spikeVisual.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        spikeVisual.localPosition = to;
    }

    // 가시 위로 걸어 들어올 때
    void OnTriggerEnter(Collider other)
    {
        TryDamagePlayer(other);
    }

    // 가시가 올라올 때 이미 위에 서 있던 경우 + 연속 데미지
    void OnTriggerStay(Collider other)
    {
        TryDamagePlayer(other);
    }

    void TryDamagePlayer(Collider other)
    {
        if (!isRaised) return;
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextDamageTime) return;

        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p == null) return;

        p.TakeDamage(damage, false);
        nextDamageTime = Time.time + Mathf.Max(damageInterval, 0.1f);
    }
}
