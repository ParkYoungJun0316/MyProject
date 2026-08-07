using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 머리 위에 "지금 이 캐릭터를 응원 중인 팀원" 하트 아이콘을 World Space로 표시.
/// Player 프리팹에 부착 (PlayerNameTagUI와 나란히).
///
/// [무엇을 보여주나]
/// TeamStatusUI 코너 패널의 "Cheering" 라벨과 완전히 같은 데이터(CheerService.OnCheerersChanged /
/// OnVoteReset)를 쓰되, 텍스트 대신 팀원 색깔별 하트 아이콘(Assets/Figma/Ingame/Heart)으로 표시한다.
/// 예: 노랑·파랑이 이 캐릭터를 응원 중이면 노랑 하트 + 파랑 하트 2개만 켜짐(나머지 자리는 숨김).
/// 필요 표수(팀원 수 - 1)를 채워 버프가 발동하면 CheerService.ApplyBuff → ResetVotes가
/// OnVoteReset을 발행해 자동으로 전부 꺼진다.
///
/// [네트워크 불필요 · 전원에게 보임]
/// CheerService가 이미 ClientRpc로 "누가 누구를 응원 중인지"를 전 클라이언트에 브로드캐스트하므로,
/// 이 컴포넌트는 그 이벤트를 자기 colorIndex 기준으로 필터링해 로컬에서 그리기만 한다.
/// 응원받는 본인 화면은 물론, 다른 팀원·관전 상황에서도 동일하게 보인다(추가 통신 없음).
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerCheerHeartsUI 추가 (UI는 코드로 자동 생성됨).
/// 2. colorHeartMap : PlayerColorType별 하트 스프라이트 연결
///    (Assets/Figma/Ingame/Heart/BlueHeart.png 등 — TeamStatusUI colorHeartMap과 동일 에셋 재사용 가능).
/// 3. offset : 머리 위 위치 오프셋. PlayerNameTagUI 이름표보다 위쪽 권장(예: 0, 2.6, 0).
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
    [Tooltip("머리 위 하트 행 위치 오프셋 (이름표보다 위쪽 권장)")]
    [SerializeField] Vector3 offset = new Vector3(0f, 2.6f, 0f);
    [Tooltip("World Space Canvas 축소 비율 — 픽셀 단위로 만든 UI를 월드 크기로 줄임")]
    [SerializeField] Vector3 worldScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("아이콘")]
    [Tooltip("PlayerColorType별 하트 스프라이트 (Figma Heart 폴더)")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;
    [SerializeField] float heartSize    = 40f;
    [SerializeField] float heartSpacing = 6f;

    Player    _player;
    int       _myColorIndex = -1;
    Image[]   _heartIcons;
    Transform _canvasTransform;
    Transform _camTransform;

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
        SubscribeCheerService();
    }

    /// <summary>ColorOrder 인덱스(0=berry …). TeamStatusUI.ResolveColorIndex와 동일 로직.</summary>
    int ResolveColorIndex()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, sessionColor);
        return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, _player.playerColorType);
    }

    // ── CheerService 구독 (TeamStatusUI와 동일 패턴) ───────────────

    void SubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnVoteReset       -= HandleVoteReset;
        svc.OnCheerersChanged += HandleCheerersChanged;
        svc.OnVoteReset       += HandleVoteReset;
    }

    void UnsubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnVoteReset       -= HandleVoteReset;
    }

    /// <summary>서버/솔로에서 응원자 목록이 바뀔 때 호출. 이 캐릭터가 타겟일 때만 반영.</summary>
    void HandleCheerersChanged(int targetIdx, int[] cheererColorIndices)
    {
        if (_myColorIndex < 0) _myColorIndex = ResolveColorIndex();
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;
        RefreshHearts(cheererColorIndices);
    }

    /// <summary>표가 초기화(타임아웃·버프 발동)되면 하트 전부 숨김.</summary>
    void HandleVoteReset(int targetIdx)
    {
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;
        RefreshHearts(System.Array.Empty<int>());
    }

    // ── UI 갱신 ──────────────────────────────────────────────────

    void RefreshHearts(int[] cheererColorIndices)
    {
        if (_heartIcons == null) return;

        var set = new HashSet<int>(cheererColorIndices);
        int shown = 0;
        for (int ci = 0; ci < LobbyNetworkManager.ColorOrder.Length; ci++)
        {
            if (!set.Contains(ci)) continue;
            if (shown >= _heartIcons.Length) break; // 풀 초과 방어 (최대 팀원 수만큼만 존재)

            _heartIcons[shown].sprite = GetHeartSprite(LobbyNetworkManager.ColorOrder[ci]);
            _heartIcons[shown].gameObject.SetActive(true);
            shown++;
        }
        for (int i = shown; i < _heartIcons.Length; i++)
            _heartIcons[i].gameObject.SetActive(false);
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
        canvasRt.sizeDelta = new Vector2(200f, 60f);

        var rowGo = new GameObject("Row");
        rowGo.transform.SetParent(canvasGo.transform, false);
        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = heartSpacing;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;

        // 최대 팀원 수(자기 자신 제외) = ColorOrder 전체 - 1
        int maxIcons = LobbyNetworkManager.ColorOrder.Length - 1;
        _heartIcons = new Image[maxIcons];
        for (int i = 0; i < maxIcons; i++)
        {
            var iconGo = new GameObject($"Heart{i}");
            iconGo.transform.SetParent(rowGo.transform, false);
            var img = iconGo.AddComponent<Image>();
            img.preserveAspect = true;
            iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(heartSize, heartSize);
            iconGo.SetActive(false);
            _heartIcons[i] = img;
        }
    }
}
