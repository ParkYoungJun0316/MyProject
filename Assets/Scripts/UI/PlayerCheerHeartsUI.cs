using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 머리 위에 "이번 팀워드 라운드에 이 플레이어가 이미 외쳤는지"를
/// World Space 하트 1개 온/오프로 표시. Player 프리팹에 부착 (PlayerNameTagUI와 나란히).
///
/// [무엇을 보여주나]
/// CheerService.OnTeamVoteChanged의 voterColorIndices에 이 캐릭터 colorIndex가 있으면 하트 ON.
/// 타임아웃·팀 버프 발동 시 Host가 빈 배열로 같은 이벤트를 다시 보내므로 자동으로 OFF.
///
/// [네트워크 불필요 · 전원에게 보임]
/// CheerService가 이미 ClientRpc로 투표 명단을 전 클라이언트에 브로드캐스트하므로,
/// 이 컴포넌트는 자기 colorIndex만 필터링해 로컬에서 그리기만 한다.
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerCheerHeartsUI (UI는 코드로 자동 생성).
/// 2. colorHeartMap : PlayerColorType별 하트 스프라이트
///    (Assets/Figma/Ingame/Heart — TeamStatusUI colorHeartMap과 동일 에셋 재사용 가능).
/// 3. offset : 머리 위 위치. PlayerNameTagUI 이름표보다 위쪽 권장(예: 0, 2.6, 0).
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerCheerHeartsUI : MonoBehaviour
{
    [System.Serializable]
    public class ColorHeartEntry
    {
        public PlayerColorType colorType;
        public Sprite heartSprite;
    }

    [Header("표시 위치")]
    [Tooltip("머리 위 하트 위치 오프셋 (이름표보다 위쪽 권장)")]
    [SerializeField] Vector3 offset = new Vector3(0f, 2.6f, 0f);
    [Tooltip("World Space Canvas 축소 비율 — 픽셀 단위로 만든 UI를 월드 크기로 줄임")]
    [SerializeField] Vector3 worldScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("아이콘")]
    [Tooltip("PlayerColorType별 하트 스프라이트 (Figma Heart 폴더)")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;
    [SerializeField] float heartSize = 40f;

    Player    _player;
    int       _myColorIndex = -1;
    Image     _heartIcon;
    Transform _canvasTransform;
    Transform _camTransform;
    Coroutine _waitSubscribe;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Start()
    {
        BuildUI();

        PlayerSpawnCoordinator.OnPlayersReady += HandlePlayersReady;
        HandlePlayersReady();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;
        UnsubscribeCheerService();
    }

    void LateUpdate()
    {
        if (_canvasTransform == null) return;
        // Y 축만 카메라를 따라 수평 회전 — PlayerNameTagUI / PressurePadCountUI와 동일 패턴
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform == null) return;
        float yaw = _camTransform.eulerAngles.y;
        _canvasTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ── 준비 시점 ────────────────────────────────────────────────

    void HandlePlayersReady()
    {
        _myColorIndex = ResolveColorIndex();
        TrySubscribeCheerService();
    }

    /// <summary>ColorOrder 인덱스(0=berry …). TeamStatusUI.ResolveColorIndex와 동일 로직.</summary>
    int ResolveColorIndex()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return System.Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return System.Array.IndexOf(PlayerColorUtil.ColorOrder, _player.playerColorType);
    }

    // ── CheerService 구독 ─────────────────────────────────────────

    void TrySubscribeCheerService()
    {
        if (CheerService.Instance != null)
        {
            SubscribeCheerService();
            return;
        }
        if (!isActiveAndEnabled || _waitSubscribe != null) return;
        _waitSubscribe = StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (CheerService.Instance == null)
            yield return null;
        _waitSubscribe = null;
        SubscribeCheerService();
    }

    void SubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnTeamVoteChanged -= HandleTeamVoteChanged;
        svc.OnTeamVoteChanged += HandleTeamVoteChanged;
    }

    void UnsubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnTeamVoteChanged -= HandleTeamVoteChanged;
    }

    void HandleTeamVoteChanged(int current, int required, int[] voterColorIndices)
    {
        if (_myColorIndex < 0) _myColorIndex = ResolveColorIndex();
        bool voted = _myColorIndex >= 0
            && voterColorIndices != null
            && System.Array.IndexOf(voterColorIndices, _myColorIndex) >= 0;
        SetHeartVisible(voted);
    }

    // ── UI 갱신 ──────────────────────────────────────────────────

    void SetHeartVisible(bool visible)
    {
        if (_heartIcon == null) return;
        if (visible)
        {
            PlayerColorType color = _myColorIndex >= 0 && _myColorIndex < PlayerColorUtil.ColorOrder.Length
                ? PlayerColorUtil.ColorOrder[_myColorIndex]
                : _player.playerColorType;
            _heartIcon.sprite = GetHeartSprite(color);
            _heartIcon.gameObject.SetActive(true);
        }
        else
        {
            _heartIcon.gameObject.SetActive(false);
        }
    }

    Sprite GetHeartSprite(PlayerColorType colorType)
    {
        if (colorHeartMap != null)
            foreach (var entry in colorHeartMap)
                if (entry.colorType == colorType) return entry.heartSprite;
        return null;
    }

    // ── UI 생성 (전부 코드 생성 — 프리팹 수정 없이 컴포넌트만 붙이면 됨) ─────

    void BuildUI()
    {
        var canvasGo = new GameObject("CheerHearts");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = offset;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale    = worldScale;
        _canvasTransform = canvasGo.transform;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(80f, 60f);

        var iconGo = new GameObject("Heart");
        iconGo.transform.SetParent(canvasGo.transform, false);
        _heartIcon = iconGo.AddComponent<Image>();
        _heartIcon.preserveAspect = true;
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(heartSize, heartSize);
        iconRt.anchoredPosition = Vector2.zero;
        iconGo.SetActive(false);
    }
}
