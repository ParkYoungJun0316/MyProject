using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팀 버프 발동 시 화면에 짧게 뜨는 스탬프 이미지 배너.
/// UI Canvas 아래 GameObject에 이 스크립트를 붙이면 된다 — 이미지는 Awake에서 스스로 붙인다.
/// CheerService.OnTeamBuffActivated를 코드에서 직접 구독.
///
/// [트리거]
/// CheerService.BroadcastTeamBuffActivatedClientRpc → OnTeamBuffActivated (Host 로컬 + 전 클라이언트).
/// Inspector에서 stamp 스프라이트를 연결해야 보인다. 미배치·미스프라이트면 배너만 없고 Heal은 그대로 적용된다.
/// </summary>
public class TeamBuffBannerUI : MonoBehaviour
{
    [Header("이미지")]
    [SerializeField] Sprite stamp;

    [Header("레이아웃")]
    [SerializeField] Vector2 stampSize = new Vector2(760f, 420f);
    [SerializeField] [Range(0f, 1f)] float anchorY = 0.72f;

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

    void Awake() => BuildVisual();

    void BuildVisual()
    {
        DestroyLegacyChild("Text");
        DestroyLegacyChild("Shadow");

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
        selfRt.sizeDelta        = stampSize;
        selfRt.anchoredPosition = Vector2.zero;
        selfRt.localRotation    = Quaternion.identity;

        _image = gameObject.GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();
        _image.sprite         = stamp;
        _image.preserveAspect = true;
        _image.raycastTarget  = false;
        _image.color          = Color.white;
        _image.enabled        = stamp != null;

        _restScale = transform.localScale;
        if (_restScale.sqrMagnitude < 0.0001f) _restScale = Vector3.one;
    }

    void DestroyLegacyChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
            Destroy(child.gameObject);
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

#if UNITY_EDITOR
    [ContextMenu("테스트: 배너 표시")]
    void Debug_Show() => Show();
#endif

    void Show()
    {
        if (!isActiveAndEnabled) return;
        if (stamp == null) return;
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
