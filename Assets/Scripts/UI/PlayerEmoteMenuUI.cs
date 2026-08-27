using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// T키로 이모트 선택 패널을 열고 닫고, 버튼 클릭으로 이모트 애니메이션을 재생.
/// 로컬 오너 Player를 찾아 그 Animator에 직접 SetBool/SetTrigger — NetworkAnimator(Owner Authority)가
/// 자동으로 다른 클라이언트에 동기화한다 (Player.cs의 doHit/doDie, isRun과 동일한 방식).
///
/// [클릭 전용 — 2026-08-27 변경]
/// 이전엔 숫자 1~8 키로도 재생 가능했으나, 응원 시스템이 전역 숫자키 1~4(CheerDigitInput,
/// CheerAndTutorialDesign.md §6.2)를 쓰게 되면서 같은 키가 "메뉴 열림 여부"에 따라 다른 의미가
/// 되는 상태 의존 충돌을 없애기 위해 완전히 클릭 전용으로 전환했다. 버튼 OnClick()은 그대로
/// PlayYes()/PlayNo()/... 를 호출하므로 씬 배선은 바꿀 필요 없다.
///
/// [루프 vs 원샷]
/// Yes/No/Hide/Point: 루프 클립 → Bool 파라미터(isYes/isNo/isHide/isPoint)로 재생, 이동 입력이 들어오면 즉시 취소.
/// Thanks/Shame/Fly/Surprise: 원샷 클립 → Trigger 파라미터(doThanks/doShame/doFly/doSurprise) 그대로 사용.
///
/// [배치]
/// UI.prefab(로컬 HUD) 빈 오브젝트에 부착.
/// emoteMenuPanel: 8개 버튼이 든 패널 GameObject 연결.
/// 각 버튼 OnClick() → PlayYes()/PlayNo()/PlayThanks()/PlayHide()/PlayPoint()/PlayShame()/PlayFly()/PlaySurprise() 연결.
/// </summary>
public class PlayerEmoteMenuUI : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("T키로 열고 닫을 이모트 선택 패널 (8개 버튼 포함)")]
    [SerializeField] GameObject emoteMenuPanel;

    [Header("커서")]
    [Tooltip("메뉴 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] bool lockCursorOnClose = true;

    /// <summary>메뉴가 열려있는 동안 true — EmoteHintUI가 "T: 이모트" 힌트를 숨기는 데 사용
    /// (InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen과 동일 패턴).</summary>
    public static bool IsOpen { get; private set; }

    Player _player;
    Animator _anim;
    bool _isOpen;

    /// <summary>현재 재생 중인 루프 이모트 Bool 파라미터 이름. 없으면 null.</summary>
    string _activeLoopParam;

    void Awake()
    {
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(false);
    }

    void Start()
    {
        _player = FindLocalOwnerPlayer();
        if (_player != null) { InitAnimator(); return; }

        PlayerSpawnCoordinator.OnPlayersReady += FindAndInit;
        if (PlayerSpawnCoordinator.IsReady) FindAndInit();
    }

    void FindAndInit()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;

        _player = FindLocalOwnerPlayer();
        if (_player == null)
        {
            Debug.LogWarning("[PlayerEmoteMenuUI] OnPlayersReady 시점에도 로컬 오너 플레이어를 찾지 못했습니다.");
            return;
        }

        InitAnimator();
    }

    void InitAnimator() => _anim = _player.GetComponentInChildren<Animator>();

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;

        // 씬 파괴(TitleReturnFlow의 SceneManager.LoadScene 등) 시 메뉴가 열려 있던 채로
        // 파괴돼도(CloseMenu 없이) 요청 목록에 잔여 참조가 새지 않도록 하는 안전장치.
        // Release가 아니라 Forget을 쓴다 — 여기서 실제 Cursor를 잠그면 TitleReturnFlow가 그
        // 직전에 이미 풀어둔 커서를 도로 잠가 "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다
        // (2026-08-22 수정, EscMenuController와 동일 원인).
        if (_isOpen) CursorUnlockRequestUtil.Forget(this);
        IsOpen = false;
    }

    void Update()
    {
        if (_player == null || Keyboard.current == null) return;

        // 루프 이모트 재생 중 이동 입력이 들어오면 즉시 취소 (메뉴/채팅 상태와 무관하게 항상 체크)
        if (_activeLoopParam != null && _player.moveInput.sqrMagnitude > 0.0001f)
            CancelActiveLoop();

        if (InGameChatUI.IsChatOpen) return;

        // CheerName 설정 패널이 열려 있으면 이모트 메뉴는 완전히 양보한다(우선순위: cheername > 이모트).
        // 이미 열려 있었다면 강제로 닫아 UI 겹침을 없앤다.
        if (TutorialCheerNameUI.IsOpen)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        if (_player.IsDead)
        {
            if (_isOpen) CloseMenu();
            CancelActiveLoop();
            return;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (_isOpen) CloseMenu();
            else OpenMenu();
            return;
        }

        if (!_isOpen) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame) { CloseMenu(); return; }

        // 숫자키 트리거 제거됨(2026-08-27) — 응원 숫자키(1~4)와의 충돌 방지, 클릭 전용.
    }

    void OpenMenu()
    {
        _isOpen = true;
        IsOpen = true;
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(true);

        CursorUnlockRequestUtil.Request(this);
    }

    void CloseMenu()
    {
        _isOpen = false;
        IsOpen = false;
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(false);

        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    // ── 버튼 OnClick 전용 ────────────────────────────────────────

    /// <summary>1번 / Yes 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayYes() => PlayLoopEmote("isYes");
    /// <summary>2번 / No 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayNo() => PlayLoopEmote("isNo");
    /// <summary>3번 / Thanks 버튼 (원샷).</summary>
    public void PlayThanks() => PlayOneShotEmote("doThanks");
    /// <summary>4번 / Hide 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayHide() => PlayLoopEmote("isHide");
    /// <summary>5번 / Point 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayPoint() => PlayLoopEmote("isPoint");
    /// <summary>6번 / Shame 버튼 (원샷).</summary>
    public void PlayShame() => PlayOneShotEmote("doShame");
    /// <summary>7번 / Fly 버튼 (원샷).</summary>
    public void PlayFly() => PlayOneShotEmote("doFly");
    /// <summary>8번 / Surprise 버튼 (원샷).</summary>
    public void PlaySurprise() => PlayOneShotEmote("doSurprise");

    /// <summary>루프 이모트 시작. 다른 루프가 재생 중이면 먼저 끄고 교체.</summary>
    void PlayLoopEmote(string boolParam)
    {
        CancelActiveLoop();
        if (_anim != null) _anim.SetBool(boolParam, true);
        _activeLoopParam = boolParam;
        CloseMenu();
    }

    /// <summary>원샷 이모트 재생. 루프 이모트 재생 중이었다면 먼저 꺼서 원샷 종료 후 루프로 되돌아가는 것을 방지.</summary>
    void PlayOneShotEmote(string trigger)
    {
        CancelActiveLoop();
        if (_anim != null) _anim.SetTrigger(trigger);
        CloseMenu();
    }

    /// <summary>재생 중인 루프 이모트 Bool을 꺼서 Idle로 되돌린다.</summary>
    void CancelActiveLoop()
    {
        if (_activeLoopParam == null) return;
        if (_anim != null) _anim.SetBool(_activeLoopParam, false);
        _activeLoopParam = null;
    }

    /// <summary>오프라인: isOwnerControlled=true, 온라인: NetworkObject.IsOwner 기준으로 탐색.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
            if (p.isOwnerControlled) return p;
        }
        return null;
    }
}
