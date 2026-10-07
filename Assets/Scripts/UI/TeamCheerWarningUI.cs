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
    [Header("T키 힌트 (§14.8)")]
    [Tooltip("창 동안 외침이 3회 연속 기준 미달이면 켜는 안내 — \"설정 → 'T키로 응원하기'를 켜면 T키로도 응원할 수 있어요\". 비워도 동작.")]
    [SerializeField] GameObject tKeyHintRoot;

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

    void OnEnable()
    {
        CheerKeywordEngine.OnVoiceAttemptFailed += HandleAttemptFailed;
        SetHint(false);
        TrySubscribe();
    }

    void OnDisable()
    {
        CheerKeywordEngine.OnVoiceAttemptFailed -= HandleAttemptFailed;
        Unsubscribe();
    }

    /// <summary>외쳤는데 기준 미달이 창 안에서 3회 연속 — T키가 아직 안 켜진 사람에게만 설정 위치 안내(§14.8).</summary>
    void HandleAttemptFailed(int windowStreak, bool practice)
    {
        if (windowStreak < CheerSoundLocalState.InGameFailHint) return;
        if (CheerDigitInput.IsTKeyEnabled) return;
        SetHint(true);
    }

    void SetHint(bool on)
    {
        if (tKeyHintRoot != null && tKeyHintRoot.activeSelf != on) tKeyHintRoot.SetActive(on);
    }

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
        if (!active) SetHint(false);
    }
}
