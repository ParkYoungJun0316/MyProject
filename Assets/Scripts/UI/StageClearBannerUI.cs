using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스테이지/Phase 클리어 시 화면에 짧게 뜨는 배너.
/// UI.prefab의 Canvas 밑에 빈 GameObject로 배치하고 이 스크립트를 붙이면 된다 — 자식 비주얼은
/// Awake에서 스스로 만든다(ObjectiveUI/TeamStatusUI와 동일 패턴). 추가 Inspector 연결 불필요
/// (StageNetworkState.Instance를 코드에서 직접 구독).
///
/// [배경 — 플레이테스트 피드백]
/// "클리어했는지 모르겠다". 기존 ObjectiveUI.ShowSceneClear()는 씬 전체 클리어
/// (onAllPhasesComplete)에만 연결돼 있고, 그 슬롯 텍스트도 다음 Phase 진입 시 Refresh()로
/// 같은 프레임에 재생성되어 중간 Phase 클리어에는 사실상 보이지 않는다. 이 배너는 슬롯과
/// 무관하게 독립적으로 자기 타이머로 뜨고 사라지므로 중간 Phase 클리어에도 항상 보인다.
///
/// [트리거]
/// StageNetworkState.OnAnyStageClearedPulse (Host 로컬 + ClientRpc로 전 클라이언트 브로드캐스트).
/// StageManager.OnStageClear를 여기서 직접 구독하지 않는 이유: 그 판정은 Host 레인에서만
/// 실행되므로(§11A.0) Client에서는 절대 발동하지 않는다 — 반드시 위 브릿지를 거쳐야 한다.
/// </summary>
public class StageClearBannerUI : MonoBehaviour
{
    [Header("문구")]
    [SerializeField] string clearMessage = "Stage Clear !!";

    [Header("색상")]
    [SerializeField] Color bgColor   = new Color(1f, 0.4f, 0.7f, 0.9f);
    [SerializeField] Color textColor = Color.white;

    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration  = 0.2f;
    [SerializeField] float holdDuration    = 1.0f;
    [SerializeField] float fadeOutDuration = 0.3f;

    [Header("텍스트")]
    [SerializeField] float fontSize = 48f;

    CanvasGroup     _canvasGroup;
    TextMeshProUGUI _text;
    Coroutine       _playRoutine;

    void Awake() => BuildVisual();

    void BuildVisual()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        RectTransform selfRt = GetComponent<RectTransform>();
        if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.anchorMin = Vector2.zero;
        selfRt.anchorMax = Vector2.one;
        selfRt.offsetMin = Vector2.zero;
        selfRt.offsetMax = Vector2.zero;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = bgColor;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(transform, false);
        _text           = textObj.AddComponent<TextMeshProUGUI>();
        _text.text      = clearMessage;
        _text.fontSize  = fontSize;
        _text.fontStyle = FontStyles.Bold;
        _text.color     = textColor;
        _text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
    }

    // ── 구독 ─────────────────────────────────────────────────────

    void OnEnable()  => TrySubscribe();
    void OnDisable() => Unsubscribe();

    void TrySubscribe()
    {
        if (StageNetworkState.Instance != null)
        {
            Subscribe();
            return;
        }
        StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (StageNetworkState.Instance == null)
            yield return null;
        Subscribe();
    }

    void Subscribe()
    {
        StageNetworkState.Instance.OnAnyStageClearedPulse -= Show;
        StageNetworkState.Instance.OnAnyStageClearedPulse += Show;
    }

    void Unsubscribe()
    {
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.OnAnyStageClearedPulse -= Show;
    }

    // ── 연출 ─────────────────────────────────────────────────────

    void Show()
    {
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        yield return Fade(_canvasGroup.alpha, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
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
