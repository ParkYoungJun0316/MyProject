using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팀 응원 가능 구간(Warning ~ Revert) 동안 뜨는 공통 경고.
/// 입 닫힘·침·혀·조임이 같은 CheerService.OnHazardWindowChanged를 쓴다.
/// 스프라이트·크기·위치는 이 패널의 Image / RectTransform에서 맞춘다. 이 스크립트는 켜고 끄기만 한다.
/// </summary>
public class TeamCheerWarningUI : MonoBehaviour
{
    CanvasGroup _canvasGroup;
    Image _image;
    Coroutine _waitSubscribe;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _image = GetComponent<Image>();
        if (_image == null) _image = GetComponentInChildren<Image>(true);
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
        bool show = active && _image != null && _image.sprite != null;
        _canvasGroup.alpha = show ? 1f : 0f;
    }
}
