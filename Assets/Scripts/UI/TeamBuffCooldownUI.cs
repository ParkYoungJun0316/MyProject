using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팀 버프 공용 쿨을 Mute 위 스탬프로 표시.
/// 발동 → 글자 어두움 → 아래→위로 색이 차오름 → 다 차면 pop 1회.
/// 숫자 없음. 상시 숨쉬기 없음. 가운데 TeamBuffBannerUI(발동 축하)와는 별개.
///
/// CheerService.OnTeamCooldownClockChanged + TeamCooldownEndServerTime(ServerTime 종료 시각).
/// Inspector에서 stamp 스프라이트를 연결해야 보인다. 미배치·미스프라이트면 쿨 HUD만 없고 Heal은 그대로 적용된다.
/// </summary>
public class TeamBuffCooldownUI : MonoBehaviour
{
    [Header("이미지")]
    [Tooltip("TeamBuffBannerUI와 같은 TEAM BUFF!!! 스탬프 (투명 배경).")]
    [SerializeField] Sprite stamp;

    [Header("레이아웃")]
    [SerializeField] Vector2 stampSize = new Vector2(180f, 100f);

    [Header("쿨 비주얼")]
    [Tooltip("쿨 직후(아직 안 찬) 글자 색. 순수 검정은 어두운 씬에서 실루엣이 사라진다.")]
    [SerializeField] Color emptyColor = new Color(0.18f, 0.18f, 0.18f, 1f);

    [Header("준비 pop")]
    [SerializeField] float punchScale    = 1.12f;
    [SerializeField] float punchInDuration  = 0.12f;
    [SerializeField] float punchOutDuration = 0.16f;

    Image       _emptyImage;
    Image       _fillImage;
    Coroutine   _waitSubscribe;
    Coroutine   _popRoutine;
    Vector3     _restScale = Vector3.one;

    bool   _wasCooling;
    double _endServerTime;
    float  _cooldownDuration = 120f;

    void Awake() => BuildVisual();

    void BuildVisual()
    {
        CanvasGroup group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable   = false;

        RectTransform selfRt = GetComponent<RectTransform>();
        if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.sizeDelta = stampSize;

        _emptyImage = FindOrCreateImage("Empty");
        _emptyImage.sprite         = stamp;
        _emptyImage.color          = emptyColor;
        _emptyImage.preserveAspect = true;
        _emptyImage.raycastTarget  = false;
        _emptyImage.type           = Image.Type.Simple;
        _emptyImage.enabled        = stamp != null;

        _fillImage = FindOrCreateImage("Fill");
        _fillImage.sprite          = stamp;
        _fillImage.color           = Color.white;
        _fillImage.preserveAspect  = true;
        _fillImage.raycastTarget   = false;
        _fillImage.type            = Image.Type.Filled;
        _fillImage.fillMethod      = Image.FillMethod.Vertical;
        _fillImage.fillOrigin      = (int)Image.OriginVertical.Bottom;
        _fillImage.fillAmount      = 1f;
        _fillImage.enabled         = stamp != null;

        _restScale = transform.localScale;
        if (_restScale.sqrMagnitude < 0.0001f) _restScale = Vector3.one;
        transform.localScale = _restScale;
    }

    Image FindOrCreateImage(string childName)
    {
        Transform existing = transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        if (existing == null)
            go.transform.SetParent(transform, false);

        Image img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale       = Vector3.one;
        rt.localRotation    = Quaternion.identity;
        return img;
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
        svc.OnTeamCooldownClockChanged -= HandleClockChanged;
        svc.OnTeamCooldownClockChanged += HandleClockChanged;
        HandleClockChanged();
    }

    void Unsubscribe()
    {
        if (_waitSubscribe != null)
        {
            StopCoroutine(_waitSubscribe);
            _waitSubscribe = null;
        }
        if (CheerService.Instance != null)
            CheerService.Instance.OnTeamCooldownClockChanged -= HandleClockChanged;
    }

    void HandleClockChanged()
    {
        var svc = CheerService.Instance;
        if (svc != null)
        {
            _endServerTime     = svc.TeamCooldownEndServerTime;
            _cooldownDuration  = Mathf.Max(0.01f, svc.TeamCooldownDuration);
        }

        if (stamp == null || _fillImage == null) return;
        if (Remaining() > 0f)
        {
            if (_popRoutine != null)
            {
                StopCoroutine(_popRoutine);
                _popRoutine = null;
            }
            transform.localScale = _restScale;
            _wasCooling = true;
        }
        ApplyFill(popIfFinished: false);
    }

    static double GetServerTime()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
            return nm.ServerTime.Time;
        return Time.timeAsDouble;
    }

    float Remaining() => (float)(_endServerTime - GetServerTime());

    void ApplyFill(bool popIfFinished)
    {
        if (_fillImage == null) return;

        float remaining = Remaining();
        float total     = Mathf.Max(0.01f, _cooldownDuration);

        if (remaining > 0f)
        {
            _wasCooling = true;
            _fillImage.fillAmount = Mathf.Clamp01((total - remaining) / total);
            return;
        }

        _fillImage.fillAmount = 1f;
        if (popIfFinished && _wasCooling)
        {
            _wasCooling = false;
            PlayReadyPop();
        }
        else
        {
            _wasCooling = false;
        }
    }

    void Update()
    {
        if (_fillImage == null) return;
        ApplyFill(popIfFinished: true);
    }

    void PlayReadyPop()
    {
        if (!isActiveAndEnabled) return;
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        yield return ScaleTo(_restScale * punchScale, punchInDuration);
        yield return ScaleTo(_restScale, punchOutDuration);
        transform.localScale = _restScale;
        _popRoutine = null;
    }

    IEnumerator ScaleTo(Vector3 to, float duration)
    {
        Vector3 from = transform.localScale;
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float k = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.LerpUnclamped(from, to, k);
            yield return null;
        }

        transform.localScale = to;
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 쿨다운 시작")]
    void Debug_StartCooldown()
    {
        float seconds = CheerService.Instance != null
            ? CheerService.Instance.TeamCooldownDuration
            : 120f;
        _cooldownDuration = Mathf.Max(0.01f, seconds);
        _endServerTime    = GetServerTime() + _cooldownDuration;
        _wasCooling       = true;
        if (_popRoutine != null)
        {
            StopCoroutine(_popRoutine);
            _popRoutine = null;
        }
        transform.localScale = _restScale;
        ApplyFill(popIfFinished: false);
    }

    [ContextMenu("테스트: 준비 pop")]
    void Debug_Pop() => PlayReadyPop();
#endif
}
