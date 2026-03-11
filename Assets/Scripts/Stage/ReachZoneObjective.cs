using UnityEngine;

/// <summary>
/// 플레이어가 트리거 존에 진입하면 완료되는 스테이지 목표.
/// 같은 GameObject에 BoxCollider(isTrigger=true)가 있어야 한다.
/// StageManager.OnStageClear → DoorController.Open() 패턴으로 사용.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReachZoneObjective : StageObjective
{
    [Header("도달 목표 설정")]
    [Tooltip("감지할 레이어 마스크. 0이면 Player 컴포넌트 존재 여부로 판단")]
    public LayerMask targetLayer;

    bool _entered;

    public override void Begin() { }
    public override void Tick()  { }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_entered || IsCompleted) return;

        bool isPlayer = (targetLayer.value != 0)
            ? ((1 << other.gameObject.layer) & targetLayer.value) != 0
            : other.GetComponentInParent<Player>() != null;

        if (!isPlayer) return;

        _entered = true;
        Complete();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color  = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(col.center),
            transform.rotation,
            transform.lossyScale);
        Gizmos.DrawCube(Vector3.zero, col.size);

        Gizmos.color = IsCompleted ? Color.green : new Color(0f, 0.8f, 0f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, col.size);
    }
#endif
}
