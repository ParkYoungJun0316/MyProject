using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dodge_Panel에 붙이는 스크립트.
/// actionCooldown 기준으로 fillAmount를 계산해 쿨타임 게이지 표시.
/// 쿨타임 중 → 어둡게 / 사용 가능 → 밝게
/// </summary>
public class DodgeCooldownUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] Player player;

    [Header("UI 참조")]
    [Tooltip("쿨타임 게이지 이미지 (Image Type: Filled, Fill Method: Radial360 또는 Horizontal)")]
    [SerializeField] Image fillImage;
    [Tooltip("아이콘 이미지 (선택)")]
    [SerializeField] Image iconImage;
    [Tooltip("남은 시간 텍스트 (선택)")]
    [SerializeField] TextMeshProUGUI cooldownText;

    [Header("색상")]
    [SerializeField] Color readyColor    = Color.white;
    [SerializeField] Color cooldownColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    void Update()
    {
        if (player == null) return;

        float remaining = player.NextActionTime - Time.time;

        if (remaining <= 0f)
        {
            // 사용 가능 상태
            if (fillImage != null)
            {
                fillImage.fillAmount = 1f;
                SetColor(readyColor);
            }
            if (cooldownText != null) cooldownText.text = string.Empty;
        }
        else
        {
            // 쿨타임 중 - 올림 정수로 표시 (2.9초 → "3")
            if (fillImage != null)
            {
                float ratio = player.actionCooldown > 0f
                    ? 1f - Mathf.Clamp01(remaining / player.actionCooldown)
                    : 1f;
                fillImage.fillAmount = ratio;
                SetColor(cooldownColor);
            }
            if (cooldownText != null)
                cooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    void SetColor(Color c)
    {
        if (fillImage != null) fillImage.color = c;
        if (iconImage != null) iconImage.color = c;
    }
}
