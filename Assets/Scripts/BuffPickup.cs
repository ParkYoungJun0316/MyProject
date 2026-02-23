using UnityEngine;

/// <summary>
/// 버프 픽업 아이템. 이 스크립트를 버프 아이템 프리팹에 부착합니다.
/// Player가 닿으면 PlayerBuffSystem.ApplyBuff()를 호출하고 아이템을 제거합니다.
/// </summary>
public class BuffPickup : MonoBehaviour
{
    [Header("버프 설정")]
    [Tooltip("적용할 버프 타입")]
    public PlayerBuffSystem.BuffType buffType;

    [Tooltip("0이면 PlayerBuffSystem의 Buff Settings에 설정된 기본값 사용")]
    public float overrideDuration = 0f;

    [Tooltip("SpeedUp 전용 추가 속도. 0이면 Buff Settings 기본값 사용")]
    public float overrideValue = 0f;

    Rigidbody rigid;
    Collider itemCollider;

    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();
    }

    void Update()
    {
        transform.Rotate(Vector3.up * 20f * Time.deltaTime);
    }

    // 바닥에 떨어지면 정지 + Trigger로 전환
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
        {
            if (rigid != null) rigid.isKinematic = true;
            if (itemCollider != null) itemCollider.isTrigger = true;
        }
    }

    // 플레이어가 닿으면 버프 적용
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerBuffSystem buffSystem = other.GetComponent<PlayerBuffSystem>();
        if (buffSystem == null) return;

        if (overrideDuration > 0f || overrideValue > 0f)
            buffSystem.ApplyBuff(buffType, overrideDuration, overrideValue);
        else
            buffSystem.ApplyBuff(buffType);

        Destroy(gameObject);
    }
}
