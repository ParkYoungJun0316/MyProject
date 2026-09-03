using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팀 응원 120초 쿨 HUD. 쿨은 폐기 — 프리팹에 남아 있어도 숨긴다.
/// 직렬화 필드는 기존 인스펙터 값을 깨지 않기 위해 유지.
/// </summary>
public class TeamBuffCooldownUI : MonoBehaviour
{
    [Header("이미지")]
    [SerializeField] Sprite stamp;

    [Header("레이아웃")]
    [SerializeField] Vector2 stampSize = new Vector2(180f, 100f);

    [Header("쿨 비주얼")]
    [SerializeField] Color emptyColor = new Color(0.18f, 0.18f, 0.18f, 1f);

    [Header("준비 pop")]
    [SerializeField] float punchScale = 1.12f;
    [SerializeField] float punchInDuration = 0.12f;
    [SerializeField] float punchOutDuration = 0.16f;

    void Awake() => HideRetired();
    void OnEnable() => HideRetired();

    void HideRetired()
    {
        CanvasGroup group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            images[i].enabled = false;
    }
}
