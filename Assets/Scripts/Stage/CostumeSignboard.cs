using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 머리 소품 선택 창을 여닫는 상호작용 표지판 — Tutorial + Interlude. CostumeDesign.md.
/// <see cref="TutorialCheerNameSignboard"/>와 같은 패턴(근접 트리거 + [E], 프롬프트 상시 표시, 순수 로컬).
///
/// [설정 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger) 부착
///    (다른 표지판 트리거와 겹치지 않게 — 겹치면 [E] 한 번에 둘 다 반응)
/// 2. costumePanel에 씬의 CostumePanelUI(CostumePanel) 연결
/// 3. promptRoot에 "[E] 꾸미기" 안내 UI 연결
/// </summary>
[RequireComponent(typeof(Collider))]
public class CostumeSignboard : MonoBehaviour
{
    [Tooltip("씬의 CostumePanel(CostumePanelUI) — 상호작용 시 이걸 열고 닫는다.")]
    [SerializeField] CostumePanelUI costumePanel;

    [Tooltip("항상 보이는 \"[E] 꾸미기\" 프롬프트(창이 열려 있는 동안만 숨김). 비워도 동작.")]
    [SerializeField] GameObject promptRoot;

    bool _localPlayerInRange;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
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
    }

    void Update()
    {
        bool isOpen = CostumePanelUI.IsOpen;

        // 프롬프트는 거리와 무관하게 상시 표시 — 창이 열려있는 동안만 숨긴다(TutorialCheerNameSignboard와 동일).
        if (promptRoot != null && promptRoot.activeSelf != !isOpen)
            promptRoot.SetActive(!isOpen);

        if (!_localPlayerInRange || costumePanel == null) return;

        // 채팅·팀 구호 입력 중 타이핑한 'e'는 상호작용이 아니다 — 열기·닫기 둘 다 양보.
        // (창이 열려 있는 동안엔 채팅 자체가 안 열린다 — InGameChatUI. 여기는 2중 가드.)
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        // 창에 입력칸이 없어 'e' 타이핑 오인이 없다 — 열려 있으면 E로 닫는다.
        if (isOpen)
        {
            costumePanel.Close();
            return;
        }

        // ESC 메뉴 등 다른 커서 UI가 떠 있을 때는 열지 않는다.
        if (CursorUnlockRequestUtil.IsRequested) return;

        costumePanel.Open();
    }
}
