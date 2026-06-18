using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP_Panel에 붙이는 스크립트.
/// maxHeart 수만큼 하트 Image를 자동 생성.
/// [ExecuteAlways] 로 씬 모드에서도 미리보기 가능.
/// </summary>
[ExecuteAlways]
public class PlayerHPUI : MonoBehaviour
{
    [System.Serializable]
    public class ColorHeartEntry
    {
        public PlayerColorType colorType;
        public Sprite fullHeartSprite;
    }

    [Header("연결")]
    [SerializeField] Player player;

    [Header("하트 스프라이트 (색별)")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;

    [Header("하트 스프라이트 (폴백/공통)")]
    [SerializeField] Sprite fullHeartSprite;
    [SerializeField] Sprite emptyHeartSprite;

    [Header("하트 크기/간격")]
    [SerializeField] float heartSize    = 50f;
    [SerializeField] float heartSpacing = 8f;

    Image[] heartImages;

    void Start()
    {
        BuildHearts();

        // 이벤트 구독은 플레이 모드에서만
        if (!Application.isPlaying) return;

        PlayerEvents events = player.GetComponent<PlayerEvents>();
        if (events != null)
        {
            events.OnDamaged   += _ => RefreshHearts();
            events.OnRespawned +=     RefreshHearts;
        }

        RefreshHearts();
    }

    Sprite GetFullHeartSprite()
    {
        if (colorHeartMap != null && player != null)
            foreach (var entry in colorHeartMap)
                if (entry.colorType == player.playerColorType)
                    return entry.fullHeartSprite;
        return fullHeartSprite;
    }

    void BuildHearts()
    {
        if (player == null) return;

        // 기존 자식 전부 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(transform.GetChild(i).gameObject);
            else
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = heartSpacing;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleLeft;

        int count = Mathf.Max(0, player.maxHeart);
        heartImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject obj = new GameObject($"Heart_{i + 1}");
            obj.transform.SetParent(transform, false);

            Image img = obj.AddComponent<Image>();
            img.sprite         = GetFullHeartSprite();
            img.preserveAspect = true;

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(heartSize, heartSize);

            heartImages[i] = img;
        }
    }

    void RefreshHearts()
    {
        if (heartImages == null) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;
            heartImages[i].sprite = i < player.heart ? GetFullHeartSprite() : emptyHeartSprite;
        }
    }

    // Inspector에서 값 바꿀 때마다 씬 뷰 자동 갱신
#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            BuildHearts();
        };
    }
#endif
}
