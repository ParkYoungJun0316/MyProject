using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 팀 버프 발동 시 화면에 짧게 뜨는 "Team Buff!" 텍스트 배너.
/// UI Canvas 아래 빈 GameObject에 이 스크립트를 붙이면 된다 — 비주얼은 Awake에서 스스로 만든다
/// (StageClearBannerUI와 동일 패턴). CheerService.OnTeamBuffActivated를 코드에서 직접 구독.
///
/// [트리거]
/// CheerService.BroadcastTeamBuffActivatedClientRpc → OnTeamBuffActivated (Host 로컬 + 전 클라이언트).
/// 오브젝트 배치는 사용자 에디터. 미배치면 배너만 없고 Heal은 그대로 적용된다.
/// </summary>
public class TeamBuffBannerUI : MonoBehaviour
{
    [Header("문구")]
    [SerializeField] string bannerMessage = "Team Buff!";

    [Header("색상")]
    [SerializeField] Color bgColor   = new Color(0.12f, 0.55f, 0.38f, 0.92f);
    [SerializeField] Color textColor = Color.white;

    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration  = 0.15f;
    [SerializeField] float holdDuration    = 2.5f;
    [SerializeField] float fadeOutDuration = 0.35f;

    [Header("레이아웃")]
    [SerializeField] Vector2 bannerSize = new Vector2(560f, 88f);
    [SerializeField] [Range(0f, 1f)] float anchorY = 0.72f;
    [SerializeField] float fontSize = 42f;

    CanvasGroup     _canvasGroup;
    TextMeshProUGUI _text;
    Coroutine       _playRoutine;
    Coroutine       _waitSubscribe;

    void Awake() => BuildVisual();

    void BuildVisual()
    {
        Transform existingText = transform.Find("Text");
        if (existingText != null)
        {
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            _text = existingText.GetComponent<TextMeshProUGUI>();
            return;
        }

        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        RectTransform selfRt = GetComponent<RectTransform>();
        if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.anchorMin        = new Vector2(0.5f, anchorY);
        selfRt.anchorMax        = new Vector2(0.5f, anchorY);
        selfRt.pivot            = new Vector2(0.5f, 0.5f);
        selfRt.sizeDelta        = bannerSize;
        selfRt.anchoredPosition = Vector2.zero;

        Image bg = gameObject.GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color = bgColor;
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(transform, false);
        _text           = textObj.AddComponent<TextMeshProUGUI>();
        _text.text      = bannerMessage;
        _text.fontSize  = fontSize;
        _text.fontStyle = FontStyles.Bold;
        _text.color     = textColor;
        _text.alignment = TextAlignmentOptions.Center;
        _text.raycastTarget = false;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
    }

    void OnEnable()  => TrySubscribe();
    void OnDisable() => Unsubscribe();

    void TrySubscribe()
    {
        if (CheerService.Instance != null)
        {
            Subscribe();
            return;
        }
        if (_waitSubscribe != null) return;
        _waitSubscribe = StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (CheerService.Instance == null)
            yield return null;
        _waitSubscribe = null;
        if (isActiveAndEnabled)
            Subscribe();
    }

    void Subscribe()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnTeamBuffActivated -= Show;
        svc.OnTeamBuffActivated += Show;
    }

    void Unsubscribe()
    {
        if (_waitSubscribe != null)
        {
            StopCoroutine(_waitSubscribe);
            _waitSubscribe = null;
        }
        if (CheerService.Instance != null)
            CheerService.Instance.OnTeamBuffActivated -= Show;
    }

    void Show()
    {
        if (!isActiveAndEnabled) return;
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        yield return Fade(_canvasGroup.alpha, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
        _playRoutine = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { _canvasGroup.alpha = to; yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }
}
