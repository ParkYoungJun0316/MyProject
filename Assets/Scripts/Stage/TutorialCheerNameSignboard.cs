using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tutorial 구역 2(CheerName 설정) — 상호작용 표지판. NetworkDesign.md §6B.7 P6 UI 파트,
/// CheerAndTutorialDesign.md §9.2 구역 2.
///
/// [왜 상시 패널 대신 상호작용인가 — 2026-08-19 사용자 결정]
/// 항상 화면에 떠 있는 패널은 계속 화면을 가리고, DialogueUI식 "그 순간 지나면 다시 못 봄" 노출은
/// 나중에 이름을 바꾸고 싶어도 타이밍을 놓칠 수 있다. 표지판 상호작용은 게이트 통과 전까지
/// 언제든 원하는 시점에 다시 열 수 있다(§3.4 "재확정 언제든 가능"과 자연스럽게 맞음).
///
/// [순수 로컬 — 네트워크 판정 없음]
/// 이 스크립트는 "내(로컬) 캐릭터가 표지판 근처에 있는가"만 본다 — TutorialGatherZone처럼 여러
/// 클라이언트의 점유를 서버가 판정할 필요가 없다(각자 자기 화면의 이름 입력 UI를 자기가 여닫을
/// 뿐이므로 충돌 자체가 없음). 그래서 NetworkBehaviour가 아니라 순수 MonoBehaviour.
///
/// [설정 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger) 부착, Tutorial 구역 2에 배치
/// 2. cheerNameUI에 씬의 TutorialCheerNameUI(CheerNamePanel) 연결
/// 3. promptRoot에 "[E] 이름 설정" 안내 UI(World Space 또는 화면 고정) 연결 — 기본 비활성 권장
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialCheerNameSignboard : MonoBehaviour
{
    [Tooltip("씬의 CheerNamePanel(TutorialCheerNameUI) — 상호작용 시 이걸 열고 닫는다.")]
    [SerializeField] TutorialCheerNameUI cheerNameUI;

    [Tooltip("근처에 있을 때만 보이는 \"[E] 이름 설정\" 프롬프트. 비워도 동작(프롬프트 없이 상호작용만).")]
    [SerializeField] GameObject promptRoot;

    bool _localPlayerInRange;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetPromptVisible(false);
    }

    void OnTriggerEnter(Collider other) => TrySetRange(other, true);

    void OnTriggerExit(Collider other) => TrySetRange(other, false);

    void TrySetRange(Collider other, bool inRange)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;

        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwner) return; // 남의 캐릭터는 내 화면 프롬프트와 무관

        _localPlayerInRange = inRange;
        if (!inRange) SetPromptVisible(false);
    }

    void Update()
    {
        if (!_localPlayerInRange || cheerNameUI == null) return;

        bool isOpen = TutorialCheerNameUI.IsOpen;

        // 패널이 열려있는 동안엔 프롬프트를 숨겨 중복 안내를 피하고, 닫히면 다시 보이게 한다.
        SetPromptVisible(!isOpen);

        // 패널이 열려있으면 E 키 감지를 건너뛴다 — 그대로 두면 이름에 'e'가 들어간 단어를
        // 타이핑할 때마다 이 표지판이 그 키 입력을 상호작용으로도 오인해 Toggle() → Close()가
        // 되어버린다(입력창이 포커스를 가져도 전역 Keyboard 폴링은 걸러지지 않음, 2026-08-22 수정).
        // 닫기는 Esc/확정 성공/닫기 버튼이 이미 담당하므로 여기서 막아도 닫을 방법이 없어지지 않는다.
        if (isOpen) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            cheerNameUI.Toggle();
    }

    void SetPromptVisible(bool visible)
    {
        if (promptRoot != null) promptRoot.SetActive(visible);
    }
}
