using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 침이 깔린 발판 트리거. Hold(및 Cover) 중에 안에 있는 Player만 Move() 그립을 낮춘다.
/// 이미 서 있는 채로 침이 깔려도 인식한다 (Enter에만 의존하지 않음).
/// </summary>
[RequireComponent(typeof(Collider))]
public class SalivaVolume : MonoBehaviour
{
    [SerializeField] SalivaHazard hazard;

    readonly HashSet<Player> _inside = new();

    void Awake()
    {
        if (hazard == null)
            hazard = GetComponentInParent<SalivaHazard>();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        RebuildInside();
        if (hazard != null && hazard.IsSlipActive)
            ApplySlipToInside(true);
        hazard?.RefreshLateJoinVisuals();
    }

    void OnDisable()
    {
        if (hazard != null && hazard.IsSlipActive)
            ApplySlipToInside(false);
        _inside.Clear();
    }

    /// <summary>Cover 시작/Recover 시작 때 SalivaHazard가 호출. 이미 발판 위에 있는 플레이어에게 그립을 켜거나 끈다.</summary>
    public void NotifySlipChanged(bool active)
    {
        PruneInside();
        ApplySlipToInside(active);
    }

    void OnTriggerEnter(Collider other)
    {
        Player p = Resolve(other);
        if (p == null || p.IsDead) return;
        if (!_inside.Add(p)) return;
        if (hazard != null && hazard.IsSlipActive)
            p.AddSalivaOverlap();
    }

    void OnTriggerExit(Collider other)
    {
        Player p = Resolve(other);
        if (p == null) return;
        if (!_inside.Remove(p)) return;
        if (hazard != null && hazard.IsSlipActive)
            p.RemoveSalivaOverlap();
    }

    void RebuildInside()
    {
        _inside.Clear();
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Vector3 center;
        Vector3 halfExtents;
        Quaternion rot;
        if (col is BoxCollider box)
        {
            center = box.transform.TransformPoint(box.center);
            Vector3 lossy = box.transform.lossyScale;
            halfExtents = Vector3.Scale(box.size, lossy) * 0.5f;
            halfExtents.x = Mathf.Abs(halfExtents.x);
            halfExtents.y = Mathf.Abs(halfExtents.y);
            halfExtents.z = Mathf.Abs(halfExtents.z);
            rot = box.transform.rotation;
        }
        else
        {
            Bounds b = col.bounds;
            center = b.center;
            halfExtents = b.extents;
            rot = Quaternion.identity;
        }

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rot,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Player p = Resolve(hits[i]);
            if (p == null || p.IsDead) continue;
            _inside.Add(p);
        }
    }

    void ApplySlipToInside(bool active)
    {
        foreach (Player p in _inside)
        {
            if (p == null || p.IsDead) continue;
            if (active) p.AddSalivaOverlap();
            else p.RemoveSalivaOverlap();
        }
    }

    void PruneInside()
    {
        _inside.RemoveWhere(p => p == null || p.IsDead);
    }

    static Player Resolve(Collider other)
    {
        if (other == null) return null;
        return other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
    }

    void OnDrawGizmos()
    {
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Color c = new Color(0.95f, 0.92f, 0.55f, 0.28f);
        Gizmos.color = c;
        Gizmos.DrawCube(Vector3.zero, Vector3.one * 0.9f);
        c.a = 0.9f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = prev;
    }
}
