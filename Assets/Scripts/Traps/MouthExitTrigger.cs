using UnityEngine;

/// <summary>
/// 입 출구에 배치하는 발사체 탈출 감지 트리거.
/// 발사체(TrapProjectile)가 이 콜라이더를 통과하면
/// MouthTrapAnimatorAnim.NotifyProjectileExited()를 호출해 Hold → Close로 전환시킨다.
///
/// [인스펙터 설정]
///   1. 입 앞에 빈 GameObject 생성
///   2. BoxCollider 추가 → Is Trigger = true
///   3. 이 컴포넌트 추가 → mouthAnim 연결
///   4. 콜라이더 배치: 입 출구 바로 앞, 두께 0.1~0.3 정도의 얇은 판
///   5. Layer: 발사체 레이어와 Physics Matrix에서 충돌 허용 여부 확인
/// </summary>
[RequireComponent(typeof(Collider))]
public class MouthExitTrigger : MonoBehaviour
{
    [Tooltip("연결할 MouthTrapAnimatorAnim 컴포넌트")]
    [SerializeField] private MouthTrapAnimatorAnim mouthAnim = null;

    void Awake()
    {
        // Is Trigger 자동 강제
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[MouthExitTrigger] {name}: Collider의 Is Trigger가 false였습니다. 자동으로 true로 설정했습니다.", this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (mouthAnim == null) return;
        if (other.GetComponent<TrapProjectile>() == null) return;

        mouthAnim.NotifyProjectileExited();
    }
}
