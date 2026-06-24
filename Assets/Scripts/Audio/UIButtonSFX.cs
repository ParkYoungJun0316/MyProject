using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI 버튼 클릭 시 SFX 를 자동 재생하는 경량 컴포넌트.
/// TitleMenuController / LobbyMenuController 를 수정하지 않고,
/// 버튼 GameObject 에 이 컴포넌트를 추가하기만 하면 동작한다.
///
/// [사용 방법]
///   1. 사운드를 재생할 Button GameObject 선택.
///   2. Add Component → UIButtonSFX.
///   3. sfxId 를 원하는 ID 로 변경 (기본: UI_Click).
///      - 일반 버튼  : UI_Click
///      - 드롭다운   : UI_DropdownChange
///   4. SFXManager 가 씬에 있어야 함.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSFX : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("클릭 시 재생할 SFX ID")]
    [SerializeField] SFXId sfxId = SFXId.UI_Click;

    public void OnPointerClick(PointerEventData eventData)
    {
        SFXManager.Instance?.Play(sfxId);
    }
}
