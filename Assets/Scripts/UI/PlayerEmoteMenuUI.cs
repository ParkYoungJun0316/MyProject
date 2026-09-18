using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 숫자키 `1`~`8`을 누르면 그 즉시 해당 이모트 애니메이션을 발동한다.
/// [2026-09-14 전면 개편] 구 T홀드 도넛 휠 UI(각도 판정·호버·릴리스 확정)를 완전히 폐지 —
/// 마우스 조작·판정 로직 불필요, 숫자키 → Animator 파라미터 직결.
///
/// [루프 vs 원샷]
/// Yes/No/Hide/Point: 루프 → Bool. 이동 입력이 들어오거나 다른 이모트 숫자키가 눌리면 취소/교체.
/// Thanks/Shame/Fly/Surprise: 원샷 → Trigger, NetworkAnimator.SetTrigger로 전송
/// (Animator에 직접 SetTrigger하면 Trigger 타입은 파라미터 폴링 대상이 아니라 원격에 전달되지 않음).
///
/// [매핑 — 구 휠의 12시 오른쪽부터 시계방향 순서를 그대로 숫자 순서에 대입]
/// 1=Yes 2=No 3=Thanks 4=Hide 5=Point 6=Shame 7=Fly 8=Surprise
///
/// [배치]
/// HUD Canvas 아무 곳에나 부착. NetworkObject 불필요.
/// 구 Emote_Panel(도넛 UI 프리팹)은 더 이상 쓰이지 않음 — 삭제는 사용자 에디터 작업.
/// </summary>
public class PlayerEmoteMenuUI : MonoBehaviour
{
    Player _player;
    Animator _anim;
    NetworkAnimator _netAnim;

    /// <summary>현재 재생 중인 루프 이모트 Bool 파라미터 이름. 없으면 null.</summary>
    string _activeLoopParam;

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

    void InitAnimator()
    {
        _anim = _player.GetComponentInChildren<Animator>();
        _netAnim = _player.GetComponentInChildren<NetworkAnimator>();

        if (_netAnim == null)
            Debug.LogWarning("[PlayerEmoteMenuUI] Player에 NetworkAnimator가 없어 원샷 이모트를 보낼 수 없습니다.");
    }

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

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;
        // ESC 메뉴 등 커서를 쓰는 UI가 떠 있으면 숫자키가 이모트로 새지 않게 양보(구 휠과 동일 기준).
        if (CursorUnlockRequestUtil.IsRequested) return;

        if (_player.IsDead)
        {
            CancelActiveLoop();
            return;
        }

        int index = ResolvePressedIndex(Keyboard.current);
        if (index >= 0) PlayByIndex(index);
    }

    static int ResolvePressedIndex(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame) return 3;
        if (kb.digit5Key.wasPressedThisFrame) return 4;
        if (kb.digit6Key.wasPressedThisFrame) return 5;
        if (kb.digit7Key.wasPressedThisFrame) return 6;
        if (kb.digit8Key.wasPressedThisFrame) return 7;
        return -1;
    }

    void PlayByIndex(int index)
    {
        switch (index)
        {
            case 0: PlayLoopEmote("isYes"); break;
            case 1: PlayLoopEmote("isNo"); break;
            case 2: PlayOneShotEmote("doThanks"); break;
            case 3: PlayLoopEmote("isHide"); break;
            case 4: PlayLoopEmote("isPoint"); break;
            case 5: PlayOneShotEmote("doShame"); break;
            case 6: PlayOneShotEmote("doFly"); break;
            case 7: PlayOneShotEmote("doSurprise"); break;
        }
    }

    /// <summary>루프 이모트 시작. 다른 루프가 재생 중이면 먼저 끄고 교체.</summary>
    void PlayLoopEmote(string boolParam)
    {
        CancelActiveLoop();
        if (_anim != null) _anim.SetBool(boolParam, true);
        _activeLoopParam = boolParam;
    }

    /// <summary>원샷 이모트 재생. Owner 권한 NetworkAnimator.SetTrigger로 로컬 즉시 적용 + 서버 큐잉.
    /// 루프 이모트 재생 중이었다면 먼저 꺼서 원샷 종료 후 루프로 되돌아가는 것을 방지.</summary>
    void PlayOneShotEmote(string trigger)
    {
        CancelActiveLoop();
        if (_netAnim != null) _netAnim.SetTrigger(trigger);
    }

    /// <summary>재생 중인 루프 이모트 Bool을 꺼서 Idle로 되돌린다.</summary>
    void CancelActiveLoop()
    {
        if (_activeLoopParam == null) return;
        if (_anim != null) _anim.SetBool(_activeLoopParam, false);
        _activeLoopParam = null;
    }

    /// <summary>로컬 오너 플레이어 — NetworkObject.IsOwner 기준. 솔로도 Host 1인이라 같은 경로다.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
        }
        return null;
    }
}
