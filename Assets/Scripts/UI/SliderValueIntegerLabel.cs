using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Slider 값(절대값, 예: 채팅 글자 크기 10~24)을 옆 텍스트에 "14" 형태로 표시.</summary>
[RequireComponent(typeof(Slider))]
public class SliderValueIntegerLabel : MonoBehaviour
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
            label.text = Mathf.RoundToInt(value).ToString();
    }
}
