using UnityEngine;

/// <summary>
/// OX 퀴즈 경로의 발판 하나.
/// OXQuizManager.Start()에서 quizManager, rowIndex를 자동 주입.
///
/// [상태]
///  Danger  : 미개방 구역 또는 오답. 밟으면 즉사.
///  Pending : 현재 퀴즈 대상 행. 밟으면 O/X 답변 등록.
///  Safe    : 정답 처리된 안전 발판.
///
/// [설정 방법]
///  1. 이 컴포넌트를 발판 오브젝트에 부착
///  2. isOSide: O 발판이면 true, X 발판이면 false
///  3. OXQuizManager.rows[]에 등록하면 rowIndex가 자동 설정됨
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
    [Tooltip("Safe 상태 — 정답, 안전 통과")]
    public Color safeColor    = new Color(0.10f, 0.65f, 0.20f);
    [Tooltip("Danger 상태 — 오답, 밟으면 즉사")]
    public Color dangerColor  = new Color(0.80f, 0.10f, 0.10f);

    [Header("Runtime (확인용)")]
    [SerializeField] TileState _state;

    // OXQuizManager.Start()에서 자동 주입
    [HideInInspector] public OXQuizManager quizManager;
    [HideInInspector] public int           rowIndex;

    public TileState State => _state;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _mpb;
    Renderer              _rend;

    void Awake()
    {
        _rend = GetComponentInChildren<Renderer>();
        _mpb  = new MaterialPropertyBlock();
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

    // ── 물리 충돌 ────────────────────────────────────────────────

    void OnCollisionEnter(Collision col)
    {
        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        switch (_state)
        {
            case TileState.Danger:
                player.KillInstantly();
                break;

            case TileState.Pending:
                quizManager?.OnPlayerAnswer(rowIndex, isOSide, player);
                break;

            // Safe: 아무 처리 없음 (자유 통과)
        }
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
            ? new Color(0.1f, 0.5f, 1.0f, 0.35f)   // O = 파랑
            : new Color(1.0f, 0.4f, 0.1f, 0.35f);   // X = 주황
        Gizmos.color = gc;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.90f);
        gc.a = 1f;
        Gizmos.color = gc;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 0.95f);
    }
}
