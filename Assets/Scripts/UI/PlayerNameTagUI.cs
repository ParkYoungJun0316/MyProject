using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터 머리 위에 색 고정 이름(BERRY/GUMA/SOOK/DAN)을 World Space 텍스트로 표시. Player 프리팹에 부착.
///
/// [2026-09-14 부활 — 개인 CheerName 커스텀화 완전 삭제에 따른 재도입]
/// 흑/백 팔레트로 색을 바꾸면 팀원을 시각적으로 구분할 수 없어져서 다시 필요해졌다. 이름 자체는 더 이상
/// 커스텀 입력이 아니라 <see cref="PlayerColorUtil.DefaultCheerNames"/> 고정값이라, 게임 중 값이 바뀔
/// 일이 없다(구버전처럼 CheerName 변경 이벤트를 구독할 필요 없음).
///
/// [느낌표와의 자리 다툼]
/// 팀 응원 창이 열려 있는 동안 <see cref="PlayerCheerHeartsUI"/>가 같은 머리 위 자리에 느낌표를 띄운다.
/// 느낌표가 떠 있으면 이름표를 숨기고, 느낌표가 꺼지면(=이 사람이 통과했거나 창이 닫히면) 이름표를 다시
/// 보여준다 — 매 프레임 <see cref="PlayerCheerHeartsUI.IsMarkVisible"/>를 폴링해서 결정하므로 이벤트
/// 구독/해제 순서를 신경 쓸 필요가 없다.
///
/// [로컬 오너]
/// hideForLocalOwner 기본 true — 자기 머리 위 이름표는 숨김(PlayerHPUI가 "YOU · BERRY" 표시).
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerNameTagUI 추가(텍스트 오브젝트는 코드로 자동 생성됨).
/// 2. offset : 머리 위 텍스트 위치 오프셋 — PlayerCheerHeartsUI의 offset과 겹치지 않게 조정.
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

    Player            _player;
    PlayerEvents      _events;
    PlayerCheerHeartsUI _heartsUI;
    TextMeshPro       _text;
    Transform         _camTransform;

    System.Action<PlayerColorType> _onColorTypeChanged;

    void Awake()
    {
        _player = GetComponent<Player>();
        _events = GetComponent<PlayerEvents>();
        _heartsUI = GetComponent<PlayerCheerHeartsUI>();
    }

    void Start()
    {
        if (hideForLocalOwner && IsLocalOwner())
            return;

        BuildText();

        _onColorTypeChanged = _ => RefreshName();
        if (_events != null)
            _events.OnColorTypeChanged += _onColorTypeChanged;

        // 색 매핑(PlayerSpawnCoordinator NetworkList)이 아직 안 왔을 수 있어 준비 완료 시 재확인.
        PlayerSpawnCoordinator.OnPlayersReady += RefreshName;
        RefreshName();
    }

    void OnDestroy()
    {
        if (_events != null && _onColorTypeChanged != null)
            _events.OnColorTypeChanged -= _onColorTypeChanged;
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshName;
    }

    void LateUpdate()
    {
        if (_text == null) return;

        // 팀 응원 느낌표가 떠 있는 동안은 이름표를 숨긴다 — 같은 자리를 다투지 않게.
        bool heartsVisible = _heartsUI != null && _heartsUI.IsMarkVisible;
        if (_text.gameObject.activeSelf == heartsVisible)
            _text.gameObject.SetActive(!heartsVisible);
        if (heartsVisible) return;

        // Y 축만 카메라를 따라 수평 회전 — X·Z 고정으로 텍스트 항상 수직 유지 (PressurePadCountUI와 동일 패턴)
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform == null) return;
        float yaw = _camTransform.eulerAngles.y;
        _text.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

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
