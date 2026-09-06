using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Slider 값(절대값, 예: 채팅 글자 크기 10~24)을 옆 텍스트에 "14" 형태로 표시하고,
/// 그 숫자를 직접 편집하면 슬라이더 값과 양방향으로 연동한다.
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderValueIntegerLabel : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    Slider _slider;
    TMP_InputField _input;

    void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(Refresh);
        BindInput();
        RefreshNow();
    }

    void OnEnable() => RefreshNow();

    /// <summary>
    /// slider.interactable은 setter가 onValueChanged를 안 쏘기 때문에, 이 컴포넌트가
    /// OnEnable(RefreshNow)로 값을 캐싱한 시점 이후 다른 코드가 slider.interactable만
    /// 바꾸면 _input.interactable이 못 따라갈 수 있음 — 매 프레임 재동기화해서
    /// "호출 순서/누락"에 의존하지 않게 한다(§1 pull 원칙과 동일 이유).
    /// </summary>
    void Update()
    {
        if (_input != null && _slider != null)
            _input.interactable = _slider.interactable;
    }

    void OnDestroy()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(Refresh);
        UnbindInput();
    }

    public void RefreshNow()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        Refresh(_slider.value);
    }

    void BindInput()
    {
        _input = SliderValueEditableField.Ensure(label, 3);
        if (_input == null) return;
        _input.onEndEdit.AddListener(OnInputEndEdit);
    }

    void UnbindInput()
    {
        if (_input == null) return;
        _input.onEndEdit.RemoveListener(OnInputEndEdit);
    }

    void Refresh(float value)
    {
        if (_input != null)
            _input.interactable = _slider != null && _slider.interactable;

        if (_input != null && _input.isFocused)
            return;

        string text = Mathf.RoundToInt(value).ToString();
        if (_input != null)
            _input.SetTextWithoutNotify(text);
        else if (label != null)
            label.text = text;
    }

    void OnInputEndEdit(string raw)
    {
        if (_slider == null || !_slider.interactable)
        {
            RefreshNow();
            return;
        }

        if (string.IsNullOrWhiteSpace(raw) ||
            !int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            RefreshNow();
            return;
        }

        int min = Mathf.RoundToInt(_slider.minValue);
        int max = Mathf.RoundToInt(_slider.maxValue);
        _slider.value = Mathf.Clamp(parsed, min, max);
        RefreshNow();
    }
}
