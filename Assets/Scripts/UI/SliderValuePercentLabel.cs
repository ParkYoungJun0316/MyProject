using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Slider 값(0~1)을 옆 텍스트에 "70%" 형태로 표시.</summary>
[RequireComponent(typeof(Slider))]
public class SliderValuePercentLabel : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    Slider _slider;

    void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(Refresh);
        RefreshNow();
    }

    void OnEnable() => RefreshNow();

    void OnDestroy()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(Refresh);
    }

    /// <summary>SetValueWithoutNotify 이후처럼 onValueChanged가 안 올 때 라벨만 맞춘다.</summary>
    public void RefreshNow()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        Refresh(_slider.value);
    }

    void Refresh(float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
