using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지/Phase 클리어 시 스탬프를 짧게 보여 준다.
/// 스프라이트·크기·위치는 이 패널의 Image / RectTransform에서 맞춘다. 이 스크립트는 타이밍만 담당.
///
/// [트리거]
/// StageNetworkState.OnAnyStageClearedPulse (Host 로컬 + ClientRpc).
/// StageManager.OnStageClear를 직접 구독하지 않는다 — 그 판정은 Host 레인만이라 Client에서는 안 뜬다.
/// Image 미연결·미스프라이트면 배너만 없다.
/// </summary>
public class StageClearBannerUI : MonoBehaviour
{
    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration  = 0.12f;
    [SerializeField] float holdDuration    = 1.6f;
    [SerializeField] float fadeOutDuration = 0.25f;
    [SerializeField] float punchScale      = 1.12f;

    CanvasGroup _canvasGroup;
    Image       _image;
    Coroutine   _playRoutine;
    Coroutine   _waitSubscribe;
    Vector3     _restScale = Vector3.one;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        _image = GetComponent<Image>();
        if (_image == null) _image = GetComponentInChildren<Image>(true);

        _restScale = transform.localScale;
        if (_restScale.sqrMagnitude < 0.0001f) _restScale = Vector3.one;
    }

    void OnEnable()  => TrySubscribe();
    void OnDisable() => Unsubscribe();

    void TrySubscribe()
    {
        if (StageNetworkState.Instance != null)
        {
            Subscribe();
            return;
        }
        if (_waitSubscribe != null) return;
        _waitSubscribe = StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (StageNetworkState.Instance == null)
            yield return null;
        _waitSubscribe = null;
        if (isActiveAndEnabled)
            Subscribe();
    }

    void Subscribe()
    {
        var state = StageNetworkState.Instance;
        if (state == null) return;
        state.OnAnyStageClearedPulse -= Show;
        state.OnAnyStageClearedPulse += Show;
    }

    void Unsubscribe()
    {
        if (_waitSubscribe != null)
        {
            StopCoroutine(_waitSubscribe);
            _waitSubscribe = null;
        }
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.OnAnyStageClearedPulse -= Show;
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 배너 표시")]
    void Debug_Show() => Show();
#endif

    void Show()
    {
        if (!isActiveAndEnabled) return;
        if (_image == null || _image.sprite == null) return;
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        transform.SetAsLastSibling();
        transform.localScale = _restScale * 0.55f;
        yield return FadeScale(0f, 1f, 0.55f, punchScale, fadeInDuration);
        yield return ScaleTo(1f, 0.1f);
        yield return new WaitForSeconds(holdDuration);
        yield return FadeScale(1f, 0f, 1f, 0.92f, fadeOutDuration);
        transform.localScale = _restScale;
        _playRoutine = null;
    }

    IEnumerator FadeScale(float fromAlpha, float toAlpha, float fromMul, float toMul, float duration)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = toAlpha;
            transform.localScale = _restScale * toMul;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float k = 1f - (1f - t) * (1f - t);
            _canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
            transform.localScale = _restScale * Mathf.Lerp(fromMul, toMul, k);
            yield return null;
        }

        _canvasGroup.alpha = toAlpha;
        transform.localScale = _restScale * toMul;
    }

    IEnumerator ScaleTo(float mul, float duration)
    {
        Vector3 from = transform.localScale;
        Vector3 to = _restScale * mul;
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localScale = to;
    }
}
