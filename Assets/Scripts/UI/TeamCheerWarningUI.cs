using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팀 응원 가능 구간(Warning ~ Revert) 동안 뜨는 공통 경고.
/// 입 닫힘·침·혀·조임이 같은 CheerService.OnHazardWindowChanged를 쓴다.
/// Canvas 아래 빈 GO에 부착. stamp 미연결이면 표시만 생략.
/// </summary>
public class TeamCheerWarningUI : MonoBehaviour
{
    [Header("이미지")]
    [SerializeField] Sprite stamp;

    [Header("레이아웃")]
    [SerializeField] Vector2 stampSize = new Vector2(520f, 220f);
    [SerializeField] [Range(0f, 1f)] float anchorY = 0.82f;

    CanvasGroup _canvasGroup;
    Image _image;
    Coroutine _waitSubscribe;

    void Awake()
    {
        _canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        RectTransform selfRt = GetComponent<RectTransform>();
        if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.anchorMin = new Vector2(0.5f, anchorY);
        selfRt.anchorMax = new Vector2(0.5f, anchorY);
        selfRt.pivot = new Vector2(0.5f, 0.5f);
        selfRt.sizeDelta = stampSize;
        selfRt.anchoredPosition = Vector2.zero;

        _image = gameObject.GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();
        _image.sprite = stamp;
        _image.preserveAspect = true;
        _image.raycastTarget = false;
        _image.enabled = stamp != null;
    }

    void OnEnable() => TrySubscribe();
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
        svc.OnHazardWindowChanged -= HandleWindow;
        svc.OnHazardWindowChanged += HandleWindow;
        HandleWindow(svc.IsHazardWindowActive);
    }

    void Unsubscribe()
    {
        if (_waitSubscribe != null)
        {
            StopCoroutine(_waitSubscribe);
            _waitSubscribe = null;
        }
        if (CheerService.Instance != null)
            CheerService.Instance.OnHazardWindowChanged -= HandleWindow;
    }

    void HandleWindow(bool active)
    {
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = active && stamp != null ? 1f : 0f;
    }
}
