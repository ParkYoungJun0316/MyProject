using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터 머리 위에 CheerName(응원 호출명, 예: BERRY)을 World Space 텍스트로 표시.
/// Player 프리팹에 부착.
///
/// [왜 CheerName만 표시하나 — Steam 표시 이름 아님]
/// 팀원이 이 캐릭터의 개인 버프를 위해 외칠 단어(CheerName)를 머리 위에 바로 띄운다.
/// Steam 표시 이름(실제 친구 닉네임)은 TeamStatusUI 코너 패널에 있다.
///
/// [네트워크 불필요]
/// CheerName은 게이트에서 GameSession에 배포되거나, 게이트 전엔 PlayerCheerNameSync NV로 읽는다.
/// 이 컴포넌트는 CheerService.GetCheerName()으로 로컬에서 읽기만 한다.
///
/// [로컬 오너]
/// hideForLocalOwner 기본 true — 자기 머리 위 이름표는 숨김 (PlayerHPUI가 "YOU · BERRY" 표시).
/// 구 "지금 응원 중인 대상" 표시는 cross-targeting 삭제로 제거됨.
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerNameTagUI 추가 (텍스트 오브젝트는 코드로 자동 생성됨).
/// 2. offset : 머리 위 텍스트 위치 오프셋 — 캐릭터 키에 맞게 조정 (예: 0, 2.2, 0).
/// 3. hideForLocalOwner : 기본 true.
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

    System.Action<PlayerColorType> _onColorTypeChanged;

    void Awake()
    {
        _player = GetComponent<Player>();
        _events = GetComponent<PlayerEvents>();
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
        // CheerName NV 변경은 OnAnyCheerNameChanged로 즉시 반영 (게이트 세션 스냅샷을 기다리지 않음).
        PlayerSpawnCoordinator.OnPlayersReady += RefreshName;
        PlayerCheerNameSync.OnAnyCheerNameChanged += RefreshName;
        RefreshName();
    }

    void OnDestroy()
    {
        if (_events != null && _onColorTypeChanged != null)
            _events.OnColorTypeChanged -= _onColorTypeChanged;
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshName;
        PlayerCheerNameSync.OnAnyCheerNameChanged -= RefreshName;
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
