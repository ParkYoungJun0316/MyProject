using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 전용 단축키(F)로 응원 버프 선택(Shield ↔ SpeedUp)을 토글하는 입력 컴포넌트.
/// 기존 "M스테이지=Shield/T스테이지=SpeedUp 고정" 방식을 플레이어 개인 선택으로 대체.
///
/// [동작]
/// F 키 → 로컬에서 먼저 "지금 내 버프 활성 중?" 확인(즉시 피드백, 서버 왕복 불필요) →
/// 활성 중이 아니면 NetworkPlayerSetup.RequestToggleBuffTypeServerRpc() 호출 →
/// Host가 다시 한 번 검증(CheerService.IsBuffActive) 후 NetworkVariable 갱신 → 전원 동기화.
/// 로컬 선검증은 UX용 즉시 피드백일 뿐이며, 최종 판정은 항상 Host(RequestToggleBuffTypeServerRpc)가 한다.
///
/// [게이팅]
/// InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen 중엔 다른 UI가 키를 쓰므로 무시
/// (CheerDigitInput / PlayerEmoteMenuUI와 동일 패턴).
///
/// [배치]
/// HUD Canvas(InGameChatUI 등이 붙어있는 오브젝트) 아무 곳에나 부착. NetworkObject 불필요 —
/// 로컬 플레이어의 NetworkPlayerSetup public RPC를 직접 호출하는 일반 MonoBehaviour.
/// </summary>
public class BuffSelectHotkeyInput : MonoBehaviour
{
    [Header("선택 안내 토스트")]
    [SerializeField] float   toastDuration    = 1.2f;
    [SerializeField] float   toastFontSize    = 24f;
    [SerializeField] Color   toastColor       = Color.white;
    [SerializeField] Vector2 toastAnchoredPos = new Vector2(0f, 160f);

    TextMeshProUGUI _toastText;
    Coroutine       _toastCoroutine;

    PlayerBuffSystem    _localBuffSystem;
    NetworkPlayerSetup  _localSetup;

    // ── 초기화 ────────────────────────────────────────────────────

    void Start()
    {
        BuildToastUI();
        PlayerSpawnCoordinator.OnPlayersReady += FindLocalRefs;
        if (PlayerSpawnCoordinator.IsReady) FindLocalRefs();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindLocalRefs;
    }

    void FindLocalRefs()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net      = p.GetComponent<NetworkObject>();
            bool isOwner = (net != null && net.IsOwner) || p.isOwnerControlled;
            if (!isOwner) continue;

            _localBuffSystem = p.GetComponent<PlayerBuffSystem>();
            _localSetup      = p.GetComponent<NetworkPlayerSetup>();
            break;
        }
    }

    // ── 입력 ──────────────────────────────────────────────────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;
        if (!kb.fKey.wasPressedThisFrame) return;

        if (_localSetup == null) FindLocalRefs();
        if (_localSetup == null) return;

        // 로컬 선(先)검증 — 내 버프가 지금 활성 중이면 전환 요청 자체를 보내지 않는다.
        // (권위 있는 최종 판정은 여전히 Host의 RequestToggleBuffTypeServerRpc.)
        bool isActive = _localBuffSystem != null &&
            (_localBuffSystem.IsActive(PlayerBuffSystem.BuffType.Shield) ||
             _localBuffSystem.IsActive(PlayerBuffSystem.BuffType.SpeedUp));

        if (isActive)
        {
            ShowToast("버프 활성 중엔 전환할 수 없습니다");
            return;
        }

        var next = _localSetup.SelectedBuffType == PlayerBuffSystem.BuffType.Shield
            ? PlayerBuffSystem.BuffType.SpeedUp
            : PlayerBuffSystem.BuffType.Shield;

        _localSetup.RequestToggleBuffTypeServerRpc();
        ShowToast(GetLabel(next) + " 선택됨");
    }

    static string GetLabel(PlayerBuffSystem.BuffType type) => type switch
    {
        PlayerBuffSystem.BuffType.Shield  => "쉴드",
        PlayerBuffSystem.BuffType.SpeedUp => "스피드업",
        _ => type.ToString()
    };

    // ── 토스트 UI (단축키 피드백 전용, CheerProgressUI 상태머신과 무관) ─────

    void BuildToastUI()
    {
        var obj = new GameObject("BuffSelectToast");
        obj.transform.SetParent(transform, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(420f, 50f);
        rt.anchoredPosition = toastAnchoredPos;

        _toastText           = obj.AddComponent<TextMeshProUGUI>();
        _toastText.fontSize  = toastFontSize;
        _toastText.color     = toastColor;
        _toastText.alignment = TextAlignmentOptions.Center;
        _toastText.fontStyle = FontStyles.Bold;
        _toastText.text      = string.Empty;

        obj.SetActive(false);
    }

    void ShowToast(string message)
    {
        if (_toastText == null) return;
        _toastText.text = message;
        _toastText.gameObject.SetActive(true);

        if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
        _toastCoroutine = StartCoroutine(HideToastAfter(toastDuration));
    }

    IEnumerator HideToastAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (_toastText != null) _toastText.gameObject.SetActive(false);
        _toastCoroutine = null;
    }
}
