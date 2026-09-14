using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using TMPro;

/// <summary>
/// 시전자(로컬 Owner) 쪽 부활 안내 HUD — DownedReviveSystemDesign.md §6 "부활 안내 (시전자)".
/// 로컬 플레이어의 PlayerReviveInteract / PlayerDownState를 매 프레임 읽기만 한다(네트워크 쓰기 없음).
///
/// [표시 상태]
///  - 안내   : CurrentTarget 있음 → "[E] 부활". 후보 판정은 PlayerReviveInteract와 같은 함수라
///             "안내가 떠 있으면 E 요청이 Host 검증을 통과한다"가 성립한다.
///  - 부활 중 : 내가 요청한 대상의 IsBeingRevived && ReviverClientId == 내 clientId (Host 수락 확인 후).
///  - 캔슬   : 수락됐던 시전이 끝났는데 대상이 여전히 부활 가능(다운 유지) → cancelFlashDuration 동안 표시.
///             대상이 일어났거나(완료) 완전사망했으면 캔슬 문구 없이 숨긴다.
///
/// [구성 (Inspector)]
///  - group : 문구 컨테이너 CanvasGroup (raycast 차단 안 함).
///  - label : TextMeshProUGUI. 화면 중앙 하단 권장.
///  - 문구 LocalizedString 3개 — 비어 있으면 한국어 폴백. [E]는 키보드 표기라 전 언어 영문 고정.
/// </summary>
public class ReviveInteractPromptUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] CanvasGroup group;
    [SerializeField] TextMeshProUGUI label;

    [Header("캔슬 피드백")]
    [SerializeField] float cancelFlashDuration = 1f;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color cancelColor = new Color(1f, 0.45f, 0.45f);

    [Header("문구")]
    [Tooltip("DeathUI 테이블 Revive.Prompt. 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString promptMessage;
    [Tooltip("DeathUI 테이블 Revive.Casting. 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString castingMessage;
    [Tooltip("DeathUI 테이블 Revive.Cancelled. 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString cancelledMessage;

    const string FallbackPrompt    = "[E] 부활";
    const string FallbackCasting   = "부활 중...";
    const string FallbackCancelled = "부활 취소";

    enum State { Hidden, Prompt, Casting, Cancelled }

    PlayerReviveInteract _interact;

    // Host가 수락한 것을 확인한 시전 대상 — 끝났을 때 완료/캔슬 구분용.
    PlayerDownState _acceptedTarget;
    float _cancelFlashEnd;
    State _shown = (State)(-1);

    void Awake()
    {
        if (group != null)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        Show(State.Hidden);
    }

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += Bind;
        if (PlayerSpawnCoordinator.IsReady) Bind();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= Bind;
    }

    void Bind()
    {
        _interact = null;
        _acceptedTarget = null;
        foreach (var p in FindObjectsByType<PlayerReviveInteract>(FindObjectsSortMode.None))
        {
            if (p.IsSpawned && p.IsOwner)
            {
                _interact = p;
                break;
            }
        }
    }

    void Update()
    {
        if (_interact == null)
        {
            Show(State.Hidden);
            return;
        }

        UpdateAcceptedTarget();

        if (_acceptedTarget != null)
            Show(State.Casting);
        else if (Time.unscaledTime < _cancelFlashEnd)
            Show(State.Cancelled);
        else if (_interact.CurrentTarget != null)
            Show(State.Prompt);
        else
            Show(State.Hidden);
    }

    void UpdateAcceptedTarget()
    {
        var nm = NetworkManager.Singleton;
        ulong localId = nm != null ? nm.LocalClientId : ulong.MaxValue;

        if (_acceptedTarget != null)
        {
            if (_acceptedTarget.IsBeingRevived && _acceptedTarget.ReviverClientId == localId) return;

            // 시전 종료: 대상이 아직 누워 있고 다시 시전 가능하면 캔슬, 아니면 완료·사망.
            if (_acceptedTarget.IsRevivable)
                _cancelFlashEnd = Time.unscaledTime + cancelFlashDuration;
            _acceptedTarget = null;
        }

        var cast = _interact.CastTarget;
        if (cast != null && cast.IsBeingRevived && cast.ReviverClientId == localId)
        {
            _acceptedTarget = cast;
            _cancelFlashEnd = 0f;
        }
    }

    void Show(State state)
    {
        if (state == _shown) return;
        _shown = state;

        if (group != null) group.alpha = state == State.Hidden ? 0f : 1f;
        if (label == null || state == State.Hidden) return;

        label.color = state == State.Cancelled ? cancelColor : normalColor;
        label.text = state switch
        {
            State.Prompt  => Localize(promptMessage, FallbackPrompt),
            State.Casting => Localize(castingMessage, FallbackCasting),
            _             => Localize(cancelledMessage, FallbackCancelled),
        };
    }

    static string Localize(LocalizedString localized, string fallback)
    {
        if (localized != null && !localized.IsEmpty)
        {
            string value = localized.GetLocalizedString();
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return fallback;
    }
}
