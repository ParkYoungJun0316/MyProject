using UnityEngine;

/// <summary>
/// 플레이어가 트리거 존에 진입하면 완료되는 스테이지 목표.
/// 같은 GameObject에 BoxCollider(isTrigger=true)가 있어야 한다.
/// StageManager.OnStageClear → DoorController.Open() 패턴으로 사용.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReachZoneObjective : StageObjective
{
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
        if (other.GetComponentInParent<Player>() == null) return;

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
