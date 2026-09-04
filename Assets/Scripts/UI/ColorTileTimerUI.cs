using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ColorTileChallenge 점수 진행 + 클리어 결과 표시.
///
/// OnQuotaChanged로 `진행/할당`을 갱신한다. 라운드 타이머는 없다.
/// </summary>
public class ColorTileTimerUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("UI 참조")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("결과 텍스트 (String Table 엔트리 연결 — SideSplit/Result.Success 재사용 권장)")]
    [SerializeField] LocalizedString successText;

    [Header("결과 표시 시간(초)")]
    [SerializeField] float resultDisplayDuration = 1.5f;

    [Header("텍스트 색상")]
    [SerializeField] Color normalColor  = Color.white;
    [SerializeField] Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);

    Coroutine _sequence;

    void Awake()
    {
        RegisterListeners();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        UnregisterListeners();
    }

    void RegisterListeners()
    {
        if (challenge == null) return;

        challenge.OnChallengeStarted.AddListener(Show);
        challenge.OnSuccess.AddListener(ShowSuccess);
        challenge.OnQuotaChanged.AddListener(SetQuota);
    }

    void UnregisterListeners()
    {
        if (challenge == null) return;

        challenge.OnChallengeStarted.RemoveListener(Show);
        challenge.OnSuccess.RemoveListener(ShowSuccess);
        challenge.OnQuotaChanged.RemoveListener(SetQuota);
    }

    void Show()
    {
        StopSequence();
        gameObject.SetActive(true);
        if (timerText != null) timerText.color = normalColor;
        SetQuota();
    }

    void SetQuota()
    {
        if (timerText == null || challenge == null) return;
        timerText.text = $"{challenge.QuotaProgress}/{challenge.QuotaRequired}";
        timerText.color = normalColor;
    }

    void ShowSuccess()
    {
        StopSequence();
        _sequence = StartCoroutine(ResultSequence());
    }

    IEnumerator ResultSequence()
    {
        if (timerText != null)
        {
            timerText.text  = successText.GetLocalizedString();
            timerText.color = successColor;
        }

        yield return new WaitForSeconds(resultDisplayDuration);

        gameObject.SetActive(false);
        _sequence = null;
    }

    void StopSequence()
    {
        if (_sequence == null) return;
        StopCoroutine(_sequence);
        _sequence = null;
    }
}
