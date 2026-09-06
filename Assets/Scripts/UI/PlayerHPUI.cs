using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP_Panel에 붙이는 스크립트.
/// maxHeart 수만큼 하트 Image를 자동 생성.
/// [ExecuteAlways] 로 씬 모드에서도 미리보기 가능.
///
/// [네트워크 모드]
/// player 필드가 비어 있으면 Start()에서 로컬 오너 플레이어를 자동으로 탐색.
/// PlayerSpawnManager가 스폰 완료 전일 경우를 대비해 짧은 대기 후 재시도.
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

    [Header("CheerName 표시")]
    [Tooltip("하트 위/옆에 배치할 이름 텍스트 (Prefab에서 연결). 비워두면 표시 안 함.")]
    [SerializeField] TextMeshProUGUI selfNameLabel;
    [Tooltip("이름 앞에 붙는 접두어. 비워두면 이름만 표시됨.")]
    [SerializeField] string selfNamePrefix = "YOU · ";

    Image[] heartImages;
    PlayerEvents _events;

    // 델리게이트 필드로 보관 — 익명 람다는 OnDestroy에서 구독 해제가 불가능하므로
    // (TeamStatusUI.ColorSlot과 동일 패턴) 반드시 필드에 담아 해제한다.
    System.Action _onDamaged;
    System.Action       _onHealed;
    System.Action       _onRespawned;
    System.Action<PlayerColorType> _onColorTypeChanged;

    void Start()
    {
        BuildHearts();

        if (!Application.isPlaying) return;

        PlayerCheerNameSync.OnAnyCheerNameChanged += HandleAnyCheerNameChanged;

        if (player != null)
        {
            SubscribeAndRefresh();
            return;
        }

        // player가 Inspector에 연결되지 않은 경우 (네트워크 스폰 대기)
        PlayerSpawnCoordinator.OnPlayersReady += FindAndSubscribe;
        if (PlayerSpawnCoordinator.IsReady) FindAndSubscribe();
    }

    void FindAndSubscribe()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndSubscribe;

        player = FindLocalOwnerPlayer();
        if (player == null)
        {
            Debug.LogWarning("[PlayerHPUI] OnPlayersReady 시점에도 로컬 오너 플레이어를 찾지 못했습니다.");
            return;
        }

        BuildHearts();
        SubscribeAndRefresh();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndSubscribe;
        PlayerCheerNameSync.OnAnyCheerNameChanged -= HandleAnyCheerNameChanged;
        UnsubscribeEvents();
    }

    /// <summary>
    /// 씬에서 로컬 오너 플레이어를 찾는다.
    /// 오프라인: isOwnerControlled=true인 첫 번째 Player
    /// 네트워크: NetworkObject.IsOwner인 Player
    /// </summary>
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

    void SubscribeAndRefresh()
    {
        UnsubscribeEvents(); // player 재탐색(FindAndSubscribe 재호출) 시 이전 구독 잔존 방지

        _events = player.GetComponent<PlayerEvents>();
        if (_events != null)
        {
            _onDamaged          = RefreshHearts;
            _onHealed           = RefreshHearts;
            _onRespawned        = RefreshHearts;
            _onColorTypeChanged = _ => { RefreshHearts(); RefreshSelfName(); };

            _events.OnDamaged          += _onDamaged;
            _events.OnHealed           += _onHealed;
            _events.OnRespawned        += _onRespawned;
            _events.OnColorTypeChanged += _onColorTypeChanged;
        }

        // Tutorial CheerName NV가 바뀌면(자기/타인 무관) "YOU · 이름"을 실시간 값으로 다시 읽는다.
        // OnSubmitResult는 NV보다 먼저 올 수 있어 기본값(berry 등)을 다시 찍으므로 쓰지 않는다.
        RefreshHearts();
        RefreshSelfName();
    }

    void HandleAnyCheerNameChanged()
    {
        if (player != null) RefreshSelfName();
    }

    void UnsubscribeEvents()
    {
        if (_events == null) return;
        _events.OnDamaged          -= _onDamaged;
        _events.OnHealed           -= _onHealed;
        _events.OnRespawned        -= _onRespawned;
        _events.OnColorTypeChanged -= _onColorTypeChanged;
        _events = null;
    }

    /// <summary>selfNameLabel에 "YOU · BERRY" 형태 텍스트를 반영.</summary>
    void RefreshSelfName()
    {
        if (selfNameLabel == null || player == null) return;
        int ci = System.Array.IndexOf(PlayerColorUtil.ColorOrder, player.playerColorType);
        string name = CheerService.GetCheerName(ci);
        selfNameLabel.text = selfNamePrefix + (string.IsNullOrEmpty(name) ? "???" : name.ToUpper());
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
