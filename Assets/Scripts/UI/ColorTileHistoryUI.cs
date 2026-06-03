using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// ColorTileRoundObjective의 라운드별 O/X 결과를 텍스트로 표시.
///
/// [표시 예시]
///  ○ ○ × ▶ — — —    (완료 라운드 / 현재 진행 중 / 미진행)
///  ○ ○ × ○ ○ ○ ○    (클리어 시 전부 기록)
///
/// [Inspector 설정]
///  - objective    : ColorTileRoundObjective 연결
///  - historyText  : TextMeshProUGUI 연결
///  - 문자/색상은 Inspector에서 조정 가능
///
/// [이벤트 연동]
///  OnHistoryUpdated 자동 구독. 별도 연결 불필요.
/// </summary>
public class ColorTileHistoryUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("감시할 ColorTileRoundObjective")]
    [SerializeField] ColorTileRoundObjective objective;

    [Tooltip("결과 문자열을 표시할 TextMeshProUGUI")]
    [SerializeField] TextMeshProUGUI historyText;

    [Header("표시 문자")]
    [Tooltip("성공(O) 라운드 표시 문자")]
    [SerializeField] string successChar = "○";

    [Tooltip("실패(X) 라운드 표시 문자")]
    [SerializeField] string failChar    = "×";

    [Tooltip("아직 진행되지 않은 라운드 표시 문자")]
    [SerializeField] string pendingChar = "—";

    [Tooltip("현재 진행 중인 라운드 표시 문자")]
    [SerializeField] string activeChar  = "▶";

    [Header("색상")]
    [Tooltip("성공(O) 색상")]
    [SerializeField] Color successColor = new Color(0.27f, 1f, 0.27f);

    [Tooltip("실패(X) 색상")]
    [SerializeField] Color failColor    = new Color(1f, 0.27f, 0.27f);

    [Tooltip("미진행 라운드 색상")]
    [SerializeField] Color pendingColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("현재 진행 중 라운드 색상")]
    [SerializeField] Color activeColor  = new Color(1f, 0.9f, 0.2f);

    [Header("레이아웃")]
    [Tooltip("라운드 사이 구분자")]
    [SerializeField] string separator = " ";

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Start()
    {
        if (objective == null)
        {
            Debug.LogWarning($"[ColorTileHistoryUI] objective가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        objective.OnHistoryUpdated.AddListener(Refresh);
        Refresh();
    }

    void OnDestroy()
    {
        if (objective == null) return;
        objective.OnHistoryUpdated.RemoveListener(Refresh);
    }

    // ── 갱신 ──────────────────────────────────────────────────────

    void Refresh()
    {
        if (historyText == null || objective == null) return;

        bool[] history    = objective.History;
        int    played     = objective.PlayedRounds;
        int    total      = objective.TotalRounds;
        bool   isComplete = objective.IsCompleted;

        if (total == 0)
        {
            historyText.text = string.Empty;
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < total; i++)
        {
            if (i > 0) sb.Append(separator);

            if (i < played)
            {
                // 결과가 기록된 라운드
                if (history != null && i < history.Length && history[i])
                    sb.Append(ColorTag(successChar, successColor));
                else
                    sb.Append(ColorTag(failChar,    failColor));
            }
            else if (!isComplete && i == played)
            {
                // 현재 진행 중 (아직 결과 없는 첫 번째 칸)
                sb.Append(ColorTag(activeChar, activeColor));
            }
            else
            {
                // 미진행
                sb.Append(ColorTag(pendingChar, pendingColor));
            }
        }

        historyText.text = sb.ToString();
    }

    static string ColorTag(string text, Color color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hex}>{text}</color>";
    }
}
