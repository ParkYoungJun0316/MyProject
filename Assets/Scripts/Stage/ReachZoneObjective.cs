using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 트리거 존에 진입하면 완료되는 스테이지 목표.
/// 같은 GameObject에 BoxCollider(isTrigger=true)가 있어야 한다.
/// StageManager.OnStageClear → DoorController.Open() 패턴으로 사용.
///
/// [진행 추적]
/// - startPoint : 경로 시작 기준 Transform (Inspector에서 연결)
/// - Progress01 : 활성 플레이어 평균 위치를 start→end 선분에 투영한 0~1 값
/// - OnProgressChanged : 진행률 변화 시 발동. ObjectiveUI가 자동 구독.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReachZoneObjective : StageObjective
{
    [Header("진행 추적")]
    [Tooltip("경로 시작 기준 Transform (스폰 포인트 또는 빈 GameObject)")]
    [SerializeField] Transform startPoint;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("진행률(0~1) 변화 시 호출. ObjectiveUI가 자동 구독.")]
    public UnityEvent<float> OnProgressChanged;

    public float Progress01 { get; private set; }

    bool     _entered;
    float    _lastProgress;
    Player[] _players;

    public override void Begin()
    {
        _players      = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        Progress01    = CalcProgress();
        _lastProgress = Progress01;
        OnProgressChanged?.Invoke(Progress01);
    }

    public override void Tick()
    {
        if (IsCompleted || _players == null) return;

        Progress01 = CalcProgress();
        if (Mathf.Abs(Progress01 - _lastProgress) < 0.005f) return;
        _lastProgress = Progress01;
        OnProgressChanged?.Invoke(Progress01);
    }

    // ── 진행률 계산 ───────────────────────────────────────────────

    float CalcProgress()
    {
        if (startPoint == null || _players == null || _players.Length == 0) return 0f;

        Vector3 start = startPoint.position;
        Vector3 end   = EndPos();
        Vector3 dir   = end - start;
        float   len   = dir.magnitude;
        if (len < 0.001f) return 0f;

        Vector3 avg   = Vector3.zero;
        int     count = 0;
        foreach (var p in _players)
        {
            if (p == null) continue;
            avg += p.transform.position;
            count++;
        }
        if (count == 0) return 0f;

        avg /= count;
        float proj = Vector3.Dot(avg - start, dir.normalized);
        return Mathf.Clamp01(proj / len);
    }

    Vector3 EndPos()
    {
        var col = GetComponent<BoxCollider>();
        return col != null ? transform.TransformPoint(col.center) : transform.position;
    }

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
