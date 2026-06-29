using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 슬롯 1개의 UI를 완전히 담당.
/// Slot0~Slot3 각 GameObject에 부착.
/// LobbyMenuController.RefreshAllSlots()에서 Refresh / SetEmpty 호출.
///
/// [Inspector 연결 — 전부 연결할 것]
/// - slotContentRoot    : 슬롯 내 모든 UI를 묶은 부모 (Empty 시 통째로 숨김)
/// - portrait           : 캐릭터 초상화 Image
/// - nameText           : 캐릭터 이름 TMP_Text (BERRY / GUMA / SSUK / DANHO)
/// - characterDropdown  : 캐릭터 선택 TMP_Dropdown (로컬 플레이어 슬롯에서만 표시)
/// - statusText         : READY / WAITING TMP_Text
/// - readyIndicator     : 체크 아이콘 GameObject
/// - hostIndicator      : 별표/왕관 아이콘 (Host 슬롯에서만 표시)
/// - kickButtonRoot     : Kick 버튼 부모 (Host만, 타인 슬롯에서만 표시)
///
/// [Kick 버튼 OnClick]
/// Kick 버튼 → OnClickKick()
/// </summary>
public class LobbySlotUI : MonoBehaviour
{
    [Tooltip("빈 슬롯 전용 비주얼 (발판 이미지 등). 플레이어 없을 때만 표시됨.")]
    [SerializeField] private GameObject   emptyVisualRoot;

    [Tooltip("점유 슬롯 전용 비주얼 (플레이어 + 이름 + 드롭다운 등). 플레이어 있을 때만 표시됨.")]
    [SerializeField] private GameObject   slotContentRoot;

    [SerializeField] private Image        portrait;

    [Tooltip("캐릭터 이름 TMP_Text (BERRY / GUMA / SSUK / DANHO)")]
    [SerializeField] private TMP_Text     nameText;

    [Tooltip("캐릭터 선택 드롭다운. 로컬 플레이어 슬롯에서만 표시됨.")]
    [SerializeField] private TMP_Dropdown characterDropdown;

    [Tooltip("READY / WAITING 상태 TMP_Text")]
    [SerializeField] private TMP_Text     statusText;

    [Tooltip("체크 아이콘 (Ready=활성, Waiting=비활성)")]
    [SerializeField] private GameObject   readyIndicator;

    [Tooltip("별표/왕관 아이콘 (Host 슬롯에서만 표시)")]
    [SerializeField] private GameObject   hostIndicator;

    [SerializeField] private GameObject   kickButtonRoot;

    // 캐릭터 이름 (ColorIndex 순)
    static readonly string[] CharacterNames = { "BERRY", "GUMA", "SSUK", "DANHO" };

    private ulong _assignedClientId  = ulong.MaxValue;
    private bool  _dropdownListening = false;

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 슬롯 내용을 갱신.
    /// </summary>
    /// <param name="state">슬롯 네트워크 상태</param>
    /// <param name="portraitSprite">색상 초상화 Sprite</param>
    /// <param name="canKick">Kick 버튼 표시 (Host + 타인 슬롯)</param>
    /// <param name="isHostSlot">이 슬롯이 Host인지</param>
    /// <param name="isLocalSlot">이 슬롯이 로컬 플레이어인지</param>
    public void Refresh(LobbyPlayerState state, Sprite portraitSprite,
                        bool canKick, bool isHostSlot = false, bool isLocalSlot = false)
    {
        _assignedClientId = state.ClientId;

        // 점유 상태: 플레이어 비주얼 표시, 빈 비주얼 숨김
        if (emptyVisualRoot  != null) emptyVisualRoot.SetActive(false);
        if (slotContentRoot  != null) slotContentRoot.SetActive(true);

        // 초상화
        if (portrait != null)
        {
            portrait.gameObject.SetActive(true);
            if (portraitSprite != null) portrait.sprite = portraitSprite;
        }

        // 캐릭터 이름
        if (nameText != null)
        {
            int ci = Mathf.Clamp(state.ColorIndex, 0, CharacterNames.Length - 1);
            nameText.text = CharacterNames[ci];
        }

        // 드롭다운: 로컬 플레이어만 표시 + 네트워크 값으로 맞춤
        if (characterDropdown != null)
        {
            characterDropdown.gameObject.SetActive(isLocalSlot);
            if (isLocalSlot)
            {
                // 네트워크 상태와 드롭다운 값 동기화 (이벤트 없이 조용히)
                SetDropdownSilent(state.ColorIndex);
                SubscribeDropdown();
            }
            else
            {
                UnsubscribeDropdown();
            }
        }

        // 상태 텍스트
        if (statusText != null)
            statusText.text = state.IsReady ? "READY" : "WAITING";

        // 체크 아이콘
        if (readyIndicator != null)
            readyIndicator.SetActive(state.IsReady);

        // 호스트 별표
        if (hostIndicator != null)
            hostIndicator.SetActive(isHostSlot);

        // Kick 버튼
        if (kickButtonRoot != null)
            kickButtonRoot.SetActive(canKick);
    }

    /// <summary>빈 슬롯으로 표시 — 발판(emptyVisualRoot)만 보이고 플레이어 UI는 숨김.</summary>
    public void SetEmpty()
    {
        _assignedClientId = ulong.MaxValue;
        UnsubscribeDropdown();

        // 빈 비주얼(발판 등) 표시
        if (emptyVisualRoot != null) emptyVisualRoot.SetActive(true);

        // 플레이어 UI 전체 숨김
        if (slotContentRoot != null)
        {
            slotContentRoot.SetActive(false);
            return;
        }

        // slotContentRoot 미연결 시 개별 처리
        if (portrait          != null) portrait.gameObject.SetActive(false);
        if (nameText          != null) nameText.text = "";
        if (characterDropdown != null) characterDropdown.gameObject.SetActive(false);
        if (statusText        != null) statusText.text = "";
        if (readyIndicator    != null) readyIndicator.SetActive(false);
        if (hostIndicator     != null) hostIndicator.SetActive(false);
        if (kickButtonRoot    != null) kickButtonRoot.SetActive(false);
    }

    // ── Kick 버튼 OnClick ─────────────────────────────────────────

    /// <summary>Kick 버튼 OnClick에 연결.</summary>
    public void OnClickKick()
    {
        if (_assignedClientId == ulong.MaxValue) return;
        LobbyNetworkManager.Instance?.KickPlayerServerRpc(_assignedClientId);
    }

    // ── 드롭다운 이벤트 ───────────────────────────────────────────

    void SubscribeDropdown()
    {
        if (_dropdownListening || characterDropdown == null) return;
        characterDropdown.onValueChanged.AddListener(OnDropdownChanged);
        _dropdownListening = true;
    }

    void UnsubscribeDropdown()
    {
        if (!_dropdownListening || characterDropdown == null) return;
        characterDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        _dropdownListening = false;
    }

    void OnDropdownChanged(int index)
    {
        // 로컬 플레이어가 캐릭터를 바꿀 때 서버에 알림
        LobbyNetworkManager.Instance?.SetColorServerRpc(index);
    }

    void SetDropdownSilent(int value)
    {
        if (characterDropdown == null) return;
        // 이벤트 없이 값만 변경 (피드백 루프 방지)
        characterDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        characterDropdown.value = value;
        if (_dropdownListening)
            characterDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDestroy() => UnsubscribeDropdown();
}
