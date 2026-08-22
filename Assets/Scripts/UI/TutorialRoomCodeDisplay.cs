using TMPro;
using UnityEngine;

/// <summary>
/// Tutorial 씬 룸코드 표시 — 로컬 개발(①ParrelSync/②Dev Build) 전용, 최소 구현.
/// NetworkDesign.md §6B.7 P3 "룸코드 NV" 항목의 간소화 버전: 룸코드는 Steam 정식 배포(§4.2)에서
/// 폐지될 예정이라 NetworkVariable 브로드캐스트를 새로 만들지 않는다 — Host가 StartHost() 시점에
/// 이미 로컬로 들고 있는 NetworkManagerSetup.RoomCode를 그대로 읽어 보여주는 게 전부.
/// Client의 RoomCode는 항상 빈 문자열(StartClient()가 값을 설정하지 않음)이라 이 컴포넌트가
/// 자동으로 숨겨진다 — Client는 원래 자기가 입력해서 들어온 값이므로 표시 대상이 아님(§6B.5).
///
/// [배치] Tutorial 씬 상시 HUD의 부모 GameObject(계속 활성 상태 유지)에 부착 —
/// roomCodeText는 그 자식의 TMP_Text를 연결(코드 없을 때 이 자식만 숨김).
///
/// [Steam 경로와의 상호배타, §6B.5]
/// Steam(④ 정식 릴리스) 경로는 룸코드 UI 자체가 없다 — <see cref="TutorialSteamInviteUI"/>(Invite 버튼)만 보인다.
/// <see cref="NetworkManagerSetup.UseLocalNetworkPath"/>가 false면 항상 빈 코드로 취급해 텍스트를 숨긴다.
/// </summary>
public class TutorialRoomCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text roomCodeText;

    string _lastCode;

    void Update()
    {
        if (roomCodeText == null) return;

        string code = (NetworkManagerSetup.UseLocalNetworkPath && NetworkManagerSetup.Instance != null)
            ? NetworkManagerSetup.Instance.RoomCode
            : string.Empty;
        if (code == _lastCode) return;
        _lastCode = code;

        bool has = !string.IsNullOrEmpty(code);
        roomCodeText.gameObject.SetActive(has);
        // 개발 전용 표시라 마스킹(LanDiscovery.FormatDisplayCode) 안 씀 — Client 입력창에
        // 그대로 옮겨 적어야 하므로 6자리 전부 노출.
        if (has) roomCodeText.text = code;
    }
}
