using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좌/우 분기 미니게임의 판정 볼륨 하나 (왼쪽 또는 오른쪽).
/// SideSplitChallenge가 타이머 종료 시점에 이 볼륨의 점유 인원을 물리 오버랩으로 스냅샷 판정한다.
///
/// [설계 원칙 — OXQuizTile과 동일]
///  - 판정은 트리거 이벤트가 아니라 SideSplitChallenge가 타이머 종료 시 호출하는
///    GetPlayersInVolume()의 물리 오버랩으로 수행 (이미 영역 안에 있어도 인식).
///  - OnTriggerEnter/Exit는 참고용 occupant 목록만 유지, 판정에는 사용하지 않음.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SideSplitZone : MonoBehaviour
{
    public enum VisualState { Neutral, Success, Fail }

    [Header("색상 (Inspector에서 조정)")]
    [Tooltip("판정 전 기본 상태.")]
    public Color neutralColor = Color.white;
    [Tooltip("판정 성공 시 잠깐 표시.")]
    public Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [Tooltip("판정 실패 시 잠깐 표시.")]
    public Color failColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    readonly List<Player> _occupants = new List<Player>();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _mpb;
    Renderer              _rend;

    void Awake()
    {
        _rend = GetComponentInChildren<Renderer>();
        _mpb  = new MaterialPropertyBlock();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        ApplyColor(neutralColor);
    }

    // ── 공개 API ─────────────────────────────────────────────────

    public void SetState(VisualState state)
    {
        Color c = state switch
        {
            VisualState.Success => successColor,
            VisualState.Fail    => failColor,
            _                   => neutralColor,
        };
        ApplyColor(c);
    }

    /// <summary>
    /// 타이머 종료 시점 기준, 이 볼륨 Collider.bounds 와 겹치는 살아있는 Player 목록.
    /// 트리거 이벤트에 의존하지 않음 (이미 영역 안에 있어도 인식) — OXQuizTile.GetPlayersInVolume과 동일 원칙.
    /// </summary>
    public List<Player> GetPlayersInVolume(LayerMask playerLayers)
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return new List<Player>();

        Bounds b = col.bounds;
        Collider[] hits = Physics.OverlapBox(
            b.center,
            b.extents,
            Quaternion.identity,
            playerLayers,
            QueryTriggerInteraction.Ignore);

        var list = new List<Player>();
        var seen = new HashSet<Player>();
        for (int i = 0; i < hits.Length; i++)
        {
            Player p = hits[i].GetComponentInParent<Player>();
            if (p == null || p.IsDead) continue;
            if (seen.Add(p))
                list.Add(p);
        }

        return list;
    }

    /// <summary>트리거 기반 목록 (판정에는 사용하지 않음, 디버그/보조용).</summary>
    public List<Player> GetOccupants()
    {
        for (int i = _occupants.Count - 1; i >= 0; i--)
            if (_occupants[i] == null || _occupants[i].IsDead)
                _occupants.RemoveAt(i);

        return _occupants;
    }

    // ── Trigger 감지 (참고용) ────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        if (!_occupants.Contains(p)) _occupants.Add(p);
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;
        _occupants.Remove(p);
    }

    // ── 내부 ─────────────────────────────────────────────────────

    void ApplyColor(Color color)
    {
        if (_rend == null) return;
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, color);
        _mpb.SetColor(ColorId,     color);
        _rend.SetPropertyBlock(_mpb);
    }

    // ── 에디터 Gizmo ─────────────────────────────────────────────

    [Tooltip("Gizmo 색 구분용 — 실제 판정과 무관, 왼쪽/오른쪽 구분 표시만.")]
    public bool isLeftSide = true;

    void OnDrawGizmos()
    {
        Color gc = isLeftSide
            ? new Color(0.1f, 0.5f, 1.0f, 0.35f)
            : new Color(1.0f, 0.4f, 0.1f, 0.35f);
        Gizmos.color = gc;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.90f);
        gc.a = 1f;
        Gizmos.color = gc;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 0.95f);
    }
}
