using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 슬라이더 옆 TMP_Text를 런타임에 TMP_InputField로 올려 클릭·숫자 입력이 되게 한다.
/// 기존 Setting_Panel 프리팹은 Value가 Text라서, 씬/프리팹을 건드리지 않고 연동한다.
/// </summary>
static class SliderValueEditableField
{
    public static TMP_InputField Ensure(TMP_Text label, int characterLimit)
    {
        if (label == null) return null;

        var go = label.gameObject;
        var input = go.GetComponent<TMP_InputField>();
        bool created = false;
        if (input == null)
        {
            input = go.AddComponent<TMP_InputField>();
            input.enabled = false;
            created = true;
        }

        label.raycastTarget = true;

        input.textComponent = label;
        input.textViewport = go.GetComponent<RectTransform>();
        input.targetGraphic = label;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = characterLimit;
        input.onFocusSelectAll = true;
        input.restoreOriginalTextOnEscape = true;
        input.richText = false;
        input.transition = Selectable.Transition.None;

        if (created)
            input.enabled = true;

        return input;
    }
}
