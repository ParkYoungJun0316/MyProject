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
        Refresh(_slider.value);
    }

    void OnDestroy()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(Refresh);
    }

    void Refresh(float value)
    {
        if (label != null)
            label.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
