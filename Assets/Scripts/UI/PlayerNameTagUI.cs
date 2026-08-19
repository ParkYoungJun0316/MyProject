using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터 머리 위에 CheerName(응원 호출명, 예: BERRY)을 World Space 텍스트로 표시.
/// Player 프리팹에 부착.
///
/// [왜 CheerName만 표시하나 — Steam 표시 이름 아님]
/// 응원할 때 필요한 매칭은 "지금 보는 캐릭터 → 외칠 단어"이므로, 그 단어(CheerName)를
/// 캐릭터 위에 바로 띄워야 헷갈림이 준다. Steam 표시 이름(실제 친구 닉네임)은 실시간
/// 판단에 안 쓰이므로 TeamStatusUI 코너 패널(원래 CheerName 있던 자리)로 옮겼다.
///
/// [네트워크 불필요]
/// CheerName은 구 LobbyNetworkManager.StartGameServerRpc → SyncCheerNamesClientRpc로
/// 로비→스테이지 전환 시 이미 전원에게 1회 배포되어 GameSession에 저장돼 있다.
/// 이 컴포넌트는 CheerService.GetCheerName()으로 그 값을 로컬에서 읽기만 한다 —
/// 별도 NetworkVariable/RPC 없이 각 클라이언트가 각자 알아서 표시.
///
/// [내 캐릭터 머리 위 슬롯 재사용 — "내가 지금 누굴 응원 중"]
/// 응원 중엔 화면(자기 캐릭터 주변)만 보고 음성으로만 외치기 때문에, 응원 대상 머리 위에 뜨는
/// PlayerCheerHeartsUI/TeamStatusUI 피드백은 응원하는 사람 본인에게는 사실상 안 보인다.
/// 반면 내 캐릭터 이름표 자리는 원래 hideForLocalOwner로 비워두던 자리라(PlayerHPUI가 이미
/// "YOU · BERRY" 표시 중) 항상 시야에 있으므로, 여기에 "지금 응원 중인 대상의 CheerName"을
/// 그 대상 색으로 표시한다. CheerService.OnCheerersChanged에서 내 colorIndex가 낀 target을
/// 찾아 표시하고, 더 이상 안 끼거나 OnVoteReset이 오면 숨긴다. 응원 안 할 때는 완전히 숨김.
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerNameTagUI 추가 (텍스트 오브젝트는 코드로 자동 생성됨).
/// 2. offset : 머리 위 텍스트 위치 오프셋 — 캐릭터 키에 맞게 조정 (예: 0, 2.2, 0).
/// 3. hideForLocalOwner : 기본 true — 내 캐릭터 위에는 CheerName 대신 "응원 중 대상" 표시로 대체.
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerNameTagUI : MonoBehaviour
{
    [Header("표시 위치")]
    [Tooltip("머리 위 텍스트 위치 오프셋")]
    [SerializeField] Vector3 offset = new Vector3(0f, 2.2f, 0f);

    [Header("텍스트")]
    [Tooltip("World Space 텍스트 폰트 크기")]
    [SerializeField] float fontSize = 3f;
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color outlineColor = new Color32(0, 0, 0, 255);
    [SerializeField] float outlineWidth = 0.2f;

    [Header("옵션")]
    [Tooltip("내 캐릭터 위에는 이름표를 표시하지 않음 (자기 자신 위치는 이미 알고 있으므로 화면 클러터 방지)")]
    [SerializeField] bool hideForLocalOwner = true;

    Player       _player;
    PlayerEvents _events;
    TextMeshPro  _text;
    Transform    _camTransform;

    int _myColorIndex    = -1;
    int _selfCheerTarget = -1;

    System.Action<PlayerColorType> _onColorTypeChanged;

    void Awake()
    {
        _player = GetComponent<Player>();
        _events = GetComponent<PlayerEvents>();
    }

    void Start()
    {
        if (hideForLocalOwner && IsLocalOwner())
        {
            BuildText();
            _text.gameObject.SetActive(false); // 응원 중일 때만 표시

            PlayerSpawnCoordinator.OnPlayersReady += HandleSelfPlayersReady;
            HandleSelfPlayersReady();
            return;
        }

        BuildText();

        _onColorTypeChanged = _ => RefreshName();
        if (_events != null)
            _events.OnColorTypeChanged += _onColorTypeChanged;

        // 색 매핑(PlayerSpawnCoordinator NetworkList)이 아직 안 왔을 수 있어 준비 완료 시 재확인.
        // 지금 당장도 한 번 시도 — 로컬 폴백(playerColorType) 경로나 이미 준비된 경우 즉시 반영됨.
        PlayerSpawnCoordinator.OnPlayersReady += RefreshName;
        RefreshName();
    }

    void OnDestroy()
    {
        if (_events != null && _onColorTypeChanged != null)
            _events.OnColorTypeChanged -= _onColorTypeChanged;
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshName;

        PlayerSpawnCoordinator.OnPlayersReady -= HandleSelfPlayersReady;
        UnsubscribeSelfCheerService();
    }

    void LateUpdate()
    {
        if (_text == null) return;
        // Y 축만 카메라를 따라 수평 회전 — X·Z 고정으로 텍스트 항상 수직 유지 (PressurePadCountUI와 동일 패턴)
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform == null) return;
        float yaw = _camTransform.eulerAngles.y;
        _text.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ── 내부 ────────────────────────────────────────────────────

    bool IsLocalOwner()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null) return net.IsOwner;
        return _player != null && _player.isOwnerControlled;
    }

    /// <summary>ColorOrder 인덱스(0=berry …). TeamStatusUI.ResolveColorIndex와 동일 로직.</summary>
    int ResolveColorIndex()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return System.Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return System.Array.IndexOf(PlayerColorUtil.ColorOrder, _player.playerColorType);
    }

    void RefreshName()
    {
        if (_text == null) return;
        int ci = ResolveColorIndex();
        string name = CheerService.GetCheerName(ci);
        _text.text = string.IsNullOrEmpty(name) ? "???" : name.ToUpper();
    }

    // ── 내 캐릭터 전용: "지금 누굴 응원 중" 추적 ───────────────────

    void HandleSelfPlayersReady()
    {
        _myColorIndex = ResolveColorIndex();
        SubscribeSelfCheerService();
    }

    void SubscribeSelfCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleSelfCheerersChanged;
        svc.OnVoteReset       -= HandleSelfVoteReset;
        svc.OnCheerersChanged += HandleSelfCheerersChanged;
        svc.OnVoteReset       += HandleSelfVoteReset;
    }

    void UnsubscribeSelfCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleSelfCheerersChanged;
        svc.OnVoteReset       -= HandleSelfVoteReset;
    }

    /// <summary>
    /// 어떤 target의 응원자 목록이 바뀔 때마다 전 클라이언트에 브로드캐스트됨.
    /// 그 목록에 내 colorIndex가 들어있으면 "내가 지금 그 target을 응원 중"으로 표시하고,
    /// 빠지면(취소·타겟 변경) 숨긴다.
    /// </summary>
    void HandleSelfCheerersChanged(int targetIdx, int[] cheererColorIndices)
    {
        if (_myColorIndex < 0) _myColorIndex = ResolveColorIndex();
        if (_myColorIndex < 0) return;

        bool amCheering = System.Array.IndexOf(cheererColorIndices, _myColorIndex) >= 0;
        if (amCheering)
        {
            _selfCheerTarget = targetIdx;
            ShowSelfCheerTarget(targetIdx);
        }
        else if (_selfCheerTarget == targetIdx)
        {
            _selfCheerTarget = -1;
            HideSelfCheerTarget();
        }
    }

    /// <summary>표가 초기화(타임아웃·버프 발동)되면 내가 응원 중이던 표시도 숨김.</summary>
    void HandleSelfVoteReset(int targetIdx)
    {
        if (_selfCheerTarget != targetIdx) return;
        _selfCheerTarget = -1;
        HideSelfCheerTarget();
    }

    void ShowSelfCheerTarget(int targetIdx)
    {
        if (_text == null || targetIdx < 0 || targetIdx >= PlayerColorUtil.ColorOrder.Length) return;
        string name = CheerService.GetCheerName(targetIdx);
        _text.text  = string.IsNullOrEmpty(name) ? "???" : name.ToUpper();
        _text.color = PlayerColorUtil.GetUniqueColor(PlayerColorUtil.ColorOrder[targetIdx]);
        _text.gameObject.SetActive(true);
    }

    void HideSelfCheerTarget()
    {
        if (_text == null) return;
        _text.gameObject.SetActive(false);
    }

    void BuildText()
    {
        var go = new GameObject("NameTag");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        go.transform.localRotation = Quaternion.identity;

        _text            = go.AddComponent<TextMeshPro>();
        _text.alignment  = TextAlignmentOptions.Center;
        _text.color      = textColor;
        _text.fontStyle  = FontStyles.Bold;
        _text.fontSize   = fontSize;
        _text.outlineWidth = outlineWidth;
        _text.outlineColor = outlineColor;
    }
}
