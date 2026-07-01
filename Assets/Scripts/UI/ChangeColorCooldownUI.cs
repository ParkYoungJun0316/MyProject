using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 색 전환(흑/백/고유색) 쿨다운을 원형 슬롯으로 표시.
/// 좌하단, Buff_Panel 옆에 배치.
///
/// [아이콘 전환 규칙]
///   isUniqueColor = true  → uniqueColorIconMap에서 playerColorType으로 조회
///   isUniqueColor = false, isBlack = true  → blackIcon
///   isUniqueColor = false, isBlack = false → whiteIcon
///
/// [쿨다운 표시]
///   쿨다운 중: fill(시계 반대방향 감소) + 남은 초 숫자, 아이콘 어두움
///   사용 가능: fill 없음, 숫자 없음, 아이콘 밝음
/// </summary>
public class ChangeColorCooldownUI : MonoBehaviour
{
    [System.Serializable]
    public class UniqueColorIconEntry
    {
        public PlayerColorType colorType;
        [Tooltip("해당 고유색의 아이콘 스프라이트")]
        public Sprite icon;
    }

    [Header("연결")]
    [SerializeField] Player player;

    [Header("아이콘 스프라이트")]
    [Tooltip("isBlack = true 상태 아이콘")]
    [SerializeField] Sprite blackIcon;
    [Tooltip("isBlack = false 상태 아이콘")]
    [SerializeField] Sprite whiteIcon;

    [Header("고유색 아이콘 (PlayerColorType별 등록)")]
    [Tooltip("Blue·Purple·Green·Yellow 각 색의 isUniqueColor 활성 아이콘")]
    [SerializeField] UniqueColorIconEntry[] uniqueColorIconMap;

    [Header("배경 스프라이트")]
    [Tooltip("원형 배경 스프라이트. 비우면 bgColor 단색.")]
    [SerializeField] Sprite bgSprite;

    [Header("슬롯 크기")]
    [SerializeField] float slotSize     = 56f;
    [Tooltip("남은 시간 숫자 폰트 크기")]
    [SerializeField] float textFontSize = 20f;

    [Header("색상")]
    [SerializeField] Color bgColor       = new Color(0.9f, 0.87f, 0.75f, 1f);
    [Tooltip("쿨다운 fill 오버레이 색")]
    [SerializeField] Color fillColor     = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("사용 가능 상태 아이콘 색")]
    [SerializeField] Color readyColor    = Color.white;
    [Tooltip("쿨다운 중 아이콘 색")]
    [SerializeField] Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    Image           _iconImage;
    Image           _fillImage;
    TextMeshProUGUI _durationText;
    PlayerEvents    _events;

    void Start()
    {
        if (player != null)
        {
            InitWithPlayer();
            return;
        }
        StartCoroutine(FindLocalPlayerRoutine());
    }

    IEnumerator FindLocalPlayerRoutine()
    {
        float elapsed = 0f;
        while (elapsed < 10f)
        {
            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;

            Player found = FindLocalOwnerPlayer();
            if (found != null)
            {
                player = found;
                InitWithPlayer();
                yield break;
            }
        }
        Debug.LogWarning("[ChangeColorCooldownUI] 10초 내 로컬 오너 플레이어를 찾지 못했습니다.");
    }

    /// <summary>오프라인: isOwnerControlled=true, 온라인: NetworkObject.IsOwner 기준으로 탐색.</summary>
    static Player FindLocalOwnerPlayer()
    {
        var all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
            if (p.isOwnerControlled) return p;
        }
        return null;
    }

    void InitWithPlayer()
    {
        _events = player.GetComponent<PlayerEvents>();

        BuildSlot();

        if (_events != null)
        {
            _events.OnBlackWhiteChanged  += OnBlackWhiteChanged;
            _events.OnUniqueColorChanged += OnUniqueColorChanged;
        }

        RefreshIcon();
    }

    void OnDestroy()
    {
        if (_events != null)
        {
            _events.OnBlackWhiteChanged  -= OnBlackWhiteChanged;
            _events.OnUniqueColorChanged -= OnUniqueColorChanged;
        }
    }

    void BuildSlot()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(slotSize, slotSize);

        // 배경
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(transform, false);
        Image bgImg          = bg.AddComponent<Image>();
        bgImg.sprite         = bgSprite;
        bgImg.color          = bgColor;
        bgImg.preserveAspect = false;
        RectTransform bgRt   = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 쿨다운 fill (Radial360, 시계 반대방향 감소)
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(transform, false);
        _fillImage               = fill.AddComponent<Image>();
        _fillImage.color         = fillColor;
        _fillImage.type          = Image.Type.Filled;
        _fillImage.fillMethod    = Image.FillMethod.Radial360;
        _fillImage.fillOrigin    = (int)Image.Origin360.Top;
        _fillImage.fillClockwise = false;
        _fillImage.fillAmount    = 0f;
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        // 아이콘
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(transform, false);
        _iconImage               = iconObj.AddComponent<Image>();
        _iconImage.preserveAspect = true;
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.1f, 0.1f);
        iconRt.anchorMax = new Vector2(0.9f, 0.9f);
        iconRt.sizeDelta = Vector2.zero;

        // 남은 시간 텍스트
        GameObject textObj = new GameObject("CooldownText");
        textObj.transform.SetParent(transform, false);
        _durationText           = textObj.AddComponent<TextMeshProUGUI>();
        _durationText.fontSize  = textFontSize;
        _durationText.fontStyle = FontStyles.Bold;
        _durationText.alignment = TextAlignmentOptions.Center;
        _durationText.color     = Color.white;
        _durationText.text      = string.Empty;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
    }

    void Update()
    {
        if (player == null || _fillImage == null) return;

        float remaining  = player.GetBWCooldownRemaining();
        float total      = player.bwCooldown;
        bool  onCooldown = remaining > 0f;

        _fillImage.fillAmount = (total > 0f && onCooldown) ? remaining / total : 0f;

        if (_iconImage != null)
            _iconImage.color = onCooldown ? cooldownColor : readyColor;

        if (_durationText != null)
            _durationText.text = onCooldown
                ? Mathf.CeilToInt(remaining).ToString()
                : string.Empty;
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────

    void OnBlackWhiteChanged(bool isBlack) => RefreshIcon();

    void OnUniqueColorChanged(int _)       => RefreshIcon();

    void RefreshIcon()
    {
        if (_iconImage == null || player == null) return;

        if (player.isUniqueColor)
            _iconImage.sprite = GetUniqueColorIcon(player.playerColorType);
        else
            _iconImage.sprite = player.isBlack ? blackIcon : whiteIcon;
    }

    Sprite GetUniqueColorIcon(PlayerColorType colorType)
    {
        if (uniqueColorIconMap != null)
            foreach (var entry in uniqueColorIconMap)
                if (entry.colorType == colorType)
                    return entry.icon;
        return null;
    }
}
