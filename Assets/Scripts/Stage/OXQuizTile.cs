using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OX 퀴즈 발판 하나.
/// OXQuizManager.Start()에서 quizManager, rowIndex를 자동 주입.
///
/// [상태]
///  Danger  : 오답 발판 또는 미개방. 밟아도 즉사 없음 (위치 판정은 Manager가 처리).
///  Pending : 현재 문제 진행 중.
///  Safe    : 정답 처리 후 연출용.
///
/// [변경 사항]
///  - Is Trigger = true 강제 설정 (Awake에서 자동 적용)
///  - OnTriggerEnter/Exit로 목록 유지 (선택)
///  - 판정은 OXQuizManager 타이머 종료 시 GetPlayersInVolume() 물리 오버랩으로 수행
///  - OnCollisionEnter 기반 즉시 판정 제거
/// </summary>
[RequireComponent(typeof(Collider))]
public class OXQuizTile : MonoBehaviour
{
    public enum TileState { Danger, Pending, Safe }

    [Header("답변 설정")]
    [Tooltip("true = O 발판, false = X 발판")]
    public bool isOSide = true;

    [Header("색상 (Inspector에서 조정)")]
    [Tooltip("Pending 상태 — 퀴즈 진행 중, 답변 대기")]
    public Color pendingColor = new Color(0.75f, 0.75f, 0.75f);
    [Tooltip("Safe 상태 — 정답, 안전")]
    public Color safeColor    = new Color(0.10f, 0.65f, 0.20f);
    [Tooltip("Danger 상태 — 오답")]
    public Color dangerColor  = new Color(0.80f, 0.10f, 0.10f);

    TileState _state;

    // OXQuizManager.Start()에서 자동 주입
    [HideInInspector] public OXQuizManager quizManager;
    [HideInInspector] public int           rowIndex;

    public TileState State => _state;

    readonly List<Player> _occupants = new List<Player>();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _mpb;
    Renderer              _rend;

    void Awake()
    {
        _rend = GetComponentInChildren<Renderer>();
        _mpb  = new MaterialPropertyBlock();

        // 항상 Trigger로 강제 설정
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        ApplyColor(dangerColor);
    }

    // ── 공개 API ─────────────────────────────────────────────────

    public void SetState(TileState newState)
    {
        _state = newState;
        Color c = newState switch
        {
            TileState.Pending => pendingColor,
            TileState.Safe    => safeColor,
            TileState.Danger  => dangerColor,
            _                 => dangerColor
        };
        ApplyColor(c);
    }

    /// <summary>
    /// 타이머 종료 시점 기준, 이 발판 Collider.bounds 와 겹치는 살아있는 Player 목록.
    /// 트리거 이벤트에 의존하지 않음 (이미 영역 안에 있어도 인식).
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

    /// <summary>
    /// 트리거 기반 목록 (판정에는 사용하지 않음).
    /// </summary>
    public List<Player> GetOccupants()
    {
        for (int i = _occupants.Count - 1; i >= 0; i--)
            if (_occupants[i] == null || _occupants[i].IsDead)
                _occupants.RemoveAt(i);

        return _occupants;
    }

    /// <summary>점유자 목록 초기화. ResetQuiz 시 호출.</summary>
    public void ClearOccupants() => _occupants.Clear();

    // ── Trigger 감지 ─────────────────────────────────────────────

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

    void OnDrawGizmos()
    {
        Color gc = isOSide
            ? new Color(0.1f, 0.5f, 1.0f, 0.35f)
            : new Color(1.0f, 0.4f, 0.1f, 0.35f);
        Gizmos.color = gc;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.90f);
        gc.a = 1f;
        Gizmos.color = gc;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 0.95f);
    }
}
