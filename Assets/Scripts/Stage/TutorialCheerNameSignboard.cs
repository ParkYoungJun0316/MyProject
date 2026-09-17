using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 팀 키워드(TeamCheerWord) 설정 패널을 여닫는 상호작용 표지판 — Tutorial 구역 2 + Interlude(2차 변경).
/// NetworkDesign.md §6B.7 P6 UI 파트, CheerAndTutorialDesign.md §9.2 구역 2.
/// 클래스 이름의 "CheerName"은 씬 연결 호환용으로 남긴 옛 이름 — 개인 CheerName 커스텀화는 2026-09-14 삭제됐다.
///
/// [왜 상시 패널 대신 상호작용인가 — 2026-08-19 사용자 결정]
/// 항상 화면에 떠 있는 패널은 계속 화면을 가리고, DialogueUI식 "그 순간 지나면 다시 못 봄" 노출은
/// 나중에 팀 키워드를 바꾸고 싶어도 타이밍을 놓칠 수 있다. 표지판 상호작용은 게이트 통과 전까지
/// 언제든 원하는 시점에 다시 열 수 있다(§3.4 "재확정 언제든 가능"과 자연스럽게 맞음).
///
/// [순수 로컬 — 네트워크 판정 없음]
/// 이 스크립트는 "내(로컬) 캐릭터가 표지판 근처에 있는가"만 본다 — TutorialGatherZone처럼 여러
/// 클라이언트의 점유를 서버가 판정할 필요가 없다(각자 자기 화면의 팀 키워드 패널을 자기가 여닫을
/// 뿐이므로 충돌 자체가 없음). 그래서 NetworkBehaviour가 아니라 순수 MonoBehaviour.
///
/// [설정 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger) 부착, Tutorial 구역 2 / Interlude에 배치
///    (같은 씬의 TutorialTeamCheerTestSignboard 트리거와 겹치지 않게 — 겹치면 [E] 한 번에 둘 다 반응)
/// 2. cheerNameUI에 씬의 TutorialCheerNameUI(CheerNamePanel) 연결
/// 3. promptRoot에 "[E] 팀 키워드 설정"(Tutorial.Prompt.CheerName) 안내 UI(World Space 또는 화면 고정) 연결 — 기본 비활성 권장
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialCheerNameSignboard : MonoBehaviour
{
    [Tooltip("씬의 CheerNamePanel(TutorialCheerNameUI) — 상호작용 시 이걸 열고 닫는다.")]
    [SerializeField] TutorialCheerNameUI cheerNameUI;

    [Tooltip("근처에 있을 때만 보이는 \"[E] 팀 키워드 설정\" 프롬프트. 비워도 동작(프롬프트 없이 상호작용만).")]
    [SerializeField] GameObject promptRoot;

    [Tooltip("Host만 보이는 표시(왕관/리본 등). 비-Host 머신에선 숨김. 비워도 동작.")]
    [SerializeField] GameObject hostBadge;

    bool _localPlayerInRange;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetPromptVisible(false);
        RefreshHostBadge();
    }

    void OnTriggerEnter(Collider other) => TrySetRange(other, true);

    void OnTriggerExit(Collider other) => TrySetRange(other, false);

    void TrySetRange(Collider other, bool inRange)
    {
        Player p = other.GetComponent<Player>();
        if (p == null) return;

        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwner) return; // 남의 캐릭터는 내 화면 프롬프트와 무관

        _localPlayerInRange = inRange;
        if (!inRange) SetPromptVisible(false);
    }

    void Update()
    {
        RefreshHostBadge();

        if (!_localPlayerInRange || cheerNameUI == null) return;

        bool isOpen = TutorialCheerNameUI.IsOpen;

        // 패널이 열려있는 동안엔 프롬프트를 숨겨 중복 안내를 피하고, 닫히면 다시 보이게 한다.
        SetPromptVisible(!isOpen);

        // 패널이 열려있으면 E 키 감지를 건너뛴다 — 그대로 두면 이름에 'e'가 들어간 단어를
        // 타이핑할 때마다 이 표지판이 그 키 입력을 상호작용으로도 오인해 Toggle() → Close()가
        // 되어버린다(입력창이 포커스를 가져도 전역 Keyboard 폴링은 걸러지지 않음, 2026-08-22 수정).
        // 닫기는 Esc/확정 성공/닫기 버튼이 이미 담당하므로 여기서 막아도 닫을 방법이 없어지지 않는다.
        if (isOpen)
        {
            // [2026-09-14] 비-Host 패널엔 입력칸이 없어 'e' 타이핑 오인이 없다 — E로 닫기 허용.
            if (!IsLocalServer() && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                cheerNameUI.Close();
            return;
        }

        // 채팅 입력창도 같은 이유로 양보한다 — 채팅에 'e'가 든 단어를 치면 이 표지판이 그 입력을
        // 상호작용으로 오인해 이름 패널을 열어버린다(CheerDigitInput·MicMuteHotkeyUI와 동일 게이팅).
        if (InGameChatUI.IsChatOpen) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            cheerNameUI.Toggle();
    }

    static bool IsLocalServer()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer;
    }

    void RefreshHostBadge()
    {
        if (hostBadge == null) return;
        bool host = IsLocalServer();
        if (hostBadge.activeSelf != host)
            hostBadge.SetActive(host);
    }

    void SetPromptVisible(bool visible)
    {
        if (promptRoot != null) promptRoot.SetActive(visible);
    }
}
