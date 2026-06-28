using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 슬롯 1개의 UI 담당.
/// Slot0~Slot3 각 GameObject에 부착.
/// LobbyMenuController.RefreshAllSlots()에서 Refresh / SetEmpty 호출.
///
/// [Inspector 연결]
/// - portrait        : 캐릭터 초상화 Image
/// - statusText      : 상태 TMP_Text (EMPTY / WAITING / READY)
/// - kickButtonRoot  : Kick 버튼 부모 GameObject
///                     (Host + 본인이 아닌 슬롯에서만 표시)
///
/// [Kick 버튼 OnClick]
/// Kick 버튼 → OnClickKick()
/// </summary>
public class LobbySlotUI : MonoBehaviour
{
    [SerializeField] private Image      portrait;
    [SerializeField] private TMP_Text   statusText;
    [SerializeField] private GameObject kickButtonRoot;

    private ulong _assignedClientId = ulong.MaxValue;

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 슬롯 내용을 갱신.
    /// </summary>
    /// <param name="state">슬롯 상태</param>
    /// <param name="portraitSprite">이 슬롯 플레이어의 색상 초상화</param>
    /// <param name="canKick">Kick 버튼 표시 여부 (Host + 타인 슬롯)</param>
    public void Refresh(LobbyPlayerState state, Sprite portraitSprite, bool canKick)
    {
        _assignedClientId = state.ClientId;

        if (portrait != null)
        {
            portrait.gameObject.SetActive(true);
            if (portraitSprite != null) portrait.sprite = portraitSprite;
        }

        if (statusText != null)
            statusText.text = state.IsReady ? "READY" : "WAITING";

        if (kickButtonRoot != null)
            kickButtonRoot.SetActive(canKick);
    }

    /// <summary>빈 슬롯으로 표시.</summary>
    public void SetEmpty()
    {
        _assignedClientId = ulong.MaxValue;

        if (portrait      != null) portrait.gameObject.SetActive(false);
        if (statusText    != null) statusText.text = "EMPTY";
        if (kickButtonRoot != null) kickButtonRoot.SetActive(false);
    }

    // ── 버튼 OnClick ──────────────────────────────────────────────

    /// <summary>Kick 버튼 OnClick에 연결.</summary>
    public void OnClickKick()
    {
        if (_assignedClientId == ulong.MaxValue) return;
        LobbyNetworkManager.Instance?.KickPlayerServerRpc(_assignedClientId);
    }
}
