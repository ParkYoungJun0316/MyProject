using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// T키로 이모트 선택 패널을 열고 닫고, 숫자 1~6 또는 버튼 클릭으로 이모트 애니메이션을 재생.
/// 로컬 오너 Player를 찾아 그 Animator에 직접 SetBool/SetTrigger — NetworkAnimator(Owner Authority)가
/// 자동으로 다른 클라이언트에 동기화한다 (Player.cs의 doHit/doDie, isRun과 동일한 방식).
///
/// [번호 배정]
/// 1 Yes, 2 No, 3 Thanks, 4 Wing, 5 SplitLeg, 6 Bat
///
/// [루프 vs 원샷]
/// Yes/No/Wing/Bat: 루프 클립 → Bool 파라미터(isYes/isNo/isWing/isBat)로 재생, 이동 입력이 들어오면 즉시 취소.
/// Thanks/SplitLeg: 원샷 클립 → Trigger 파라미터(doThanks/doSplitLeg) 그대로 사용.
///
/// [배치]
/// UI.prefab(로컬 HUD) 빈 오브젝트에 부착.
/// emoteMenuPanel: 6개 버튼이 든 패널 GameObject 연결.
/// 각 버튼 OnClick() → PlayYes()/PlayNo()/PlayThanks()/PlayWing()/PlaySplitLeg()/PlayBat() 연결.
/// </summary>
public class PlayerEmoteMenuUI : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("T키로 열고 닫을 이모트 선택 패널 (6개 버튼 포함)")]
    [SerializeField] GameObject emoteMenuPanel;

    [Header("커서")]
    [Tooltip("메뉴 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] bool lockCursorOnClose = true;

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
    }

    void Update()
    {
        if (_player == null || Keyboard.current == null) return;

        // 루프 이모트 재생 중 이동 입력이 들어오면 즉시 취소 (메뉴/채팅 상태와 무관하게 항상 체크)
        if (_activeLoopParam != null && _player.moveInput.sqrMagnitude > 0.0001f)
            CancelActiveLoop();

        if (InGameChatUI.IsChatOpen) return;

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

        if (Keyboard.current.digit1Key.wasPressedThisFrame) PlayYes();
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) PlayNo();
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) PlayThanks();
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) PlayWing();
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) PlaySplitLeg();
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) PlayBat();
    }

    void OpenMenu()
    {
        _isOpen = true;
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseMenu()
    {
        _isOpen = false;
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(false);

        if (lockCursorOnClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ── 버튼 OnClick 전용 (숫자키 핸들러도 동일 메서드 재사용) ─────────────

    /// <summary>1번 / Yes 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayYes() => PlayLoopEmote("isYes");
    /// <summary>2번 / No 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayNo() => PlayLoopEmote("isNo");
    /// <summary>3번 / Thanks 버튼 (원샷).</summary>
    public void PlayThanks() => PlayOneShotEmote("doThanks");
    /// <summary>4번 / Wing 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayWing() => PlayLoopEmote("isWing");
    /// <summary>5번 / SplitLeg 버튼 (원샷).</summary>
    public void PlaySplitLeg() => PlayOneShotEmote("doSplitLeg");
    /// <summary>6번 / Bat 버튼 (루프 — 이동 시 자동 취소).</summary>
    public void PlayBat() => PlayLoopEmote("isBat");

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
