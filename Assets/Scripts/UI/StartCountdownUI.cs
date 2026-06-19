using UnityEngine;
using TMPro;

/// <summary>
/// StageStartGate 카운트다운 전용 UI.
/// 전원 점유 시 남은 초를 정수로 표시하고, 대기·이탈·완료 시 자동 숨김.
///
/// [Inspector 연결]
/// StageStartGate 이벤트에 아래 순서로 연결:
///   OnCountdownTick     → StartCountdownUI.SetRemaining   (dynamic float)
///   OnCountdownReset    → StartCountdownUI.Hide
///   OnCountdownComplete → StartCountdownUI.Hide
///
/// ※ OnCountdownReset 직후 StageStartGate가 발행하는 Tick 하나는 내부에서 자동 무시.
///   덕분에 "대기 상태 Tick = countdownDuration" 이 UI에 나타나지 않음.
/// </summary>
public class StartCountdownUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] TextMeshProUGUI countdownText;

    // OnCountdownReset 뒤에 뒤따라오는 Tick 한 회를 무시하기 위한 플래그
    bool _suppressNextTick;

    void Awake()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// OnCountdownTick(float)에 연결. 남은 시간을 올림 정수로 표시.
    /// 0 이하이면 즉시 숨김 (0은 화면에 표시하지 않음).
    /// </summary>
    public void SetRemaining(float remaining)
    {
        if (_suppressNextTick)
        {
            _suppressNextTick = false;
            return;
        }

        int display = Mathf.CeilToInt(remaining);
        if (display <= 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (countdownText != null)
            countdownText.text = display.ToString();
    }

    /// <summary>
    /// OnCountdownReset / OnCountdownComplete에 연결.
    /// UI를 숨기고, Reset 계열 직후 발행되는 Tick 한 회를 자동 무시.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        _suppressNextTick = true;
    }
}
