using UnityEngine;

/// <summary>
/// UnityEvent → SFXManager 범용 브릿지.
/// 기존 스크립트(StageManager, DoorController 등)를 수정하지 않고
/// Inspector 의 UnityEvent 에서 SFX 를 재생할 때 사용한다.
///
/// [사용 방법]
///   1. 재생이 필요한 오브젝트(Door, StageManager 등)에 Add Component → SFXEventPlayer.
///   2. sfxId 를 원하는 ID 로 설정.
///   3. 해당 컴포넌트의 UnityEvent → SFXEventPlayer.Play() 를 연결.
///
/// [예시 연결]
///   DoorController.OnOpened  → SFXEventPlayer.Play()  (sfxId = Door_Open)
///   DoorController.OnClosed  → SFXEventPlayer.Play()  (sfxId = Door_Close)
///   WallMover.OnMoveStarted  → SFXEventPlayer.Play()  (sfxId = Trap_WallMover)
///   PhaseData.onPhaseEnter   → SFXEventPlayer.Play()  (sfxId = Boss_PhaseTransition_Mouth / Boss_PhaseTransition_Esophagus)
///
/// [3D 재생]
///   위치 기반 사운드가 필요하면 Play3D() 를 연결하면 됨.
///   이 컴포넌트가 붙은 오브젝트의 위치에서 재생됨.
///
/// [중간에 멈춰야 하는 재생 — 시작/종료 이벤트가 둘 다 있는 경우]
///   Play()는 PlayOneShot 방식이라 한 번 쏘면 클립이 끝날 때까지 멈출 방법이 없다(클립이
///   실제 재생 시간보다 길면 다음 상황에서도 계속 들림). 시작 이벤트보다 먼저 끝내야 하는
///   경우(예: 타이머 사운드가 정답 공개 전에 끝나야 함)는 PlayUntilStopped()/Stop() 사용.
///   예: OXQuizManager.OnQuestionReady → PlayUntilStopped()  (sfxId = 타이머 사운드)
///       OXQuizManager.OnAnswerRevealed → Stop()
///   PlayUntilStopped() 중복 호출은 무시됨(이미 재생 중이면 재시작 안 함).
///   이 오브젝트가 비활성화/파괴되면(씬 리로드, Phase 전환 등) 자동으로 Stop() 처리되어
///   AudioSource가 고아로 남지 않음.
/// </summary>
public class SFXEventPlayer : MonoBehaviour
{
    [Tooltip("Play() / PlayUntilStopped() 호출 시 재생할 SFX ID")]
    [SerializeField] SFXId sfxId = SFXId.UI_Click;

    AudioSource _stoppableSource;

    /// <summary>2D 재생. UnityEvent (no parameter) 에 연결.</summary>
    public void Play()
    {
        SFXManager.Instance?.Play(sfxId);
    }

    /// <summary>3D 재생 (이 오브젝트 월드 위치). 함정·오브젝트 이벤트 연결용.</summary>
    public void Play3D()
    {
        SFXManager.Instance?.Play(sfxId, transform.position);
    }

    /// <summary>중간에 Stop()으로 끊을 수 있는 재생 시작. 이미 재생 중이면 무시. "시작" 이벤트에 연결.</summary>
    public void PlayUntilStopped()
    {
        if (_stoppableSource != null) return;
        _stoppableSource = SFXManager.Instance?.PlayLoop(sfxId);
    }

    /// <summary>PlayUntilStopped() 으로 시작한 재생을 정지. "종료" 이벤트에 연결.</summary>
    public void Stop()
    {
        if (_stoppableSource == null) return;
        SFXManager.Instance?.StopLoop(_stoppableSource);
        _stoppableSource = null;
    }

    void OnDisable()
    {
        // 오브젝트 비활성화/파괴 시(씬 리로드, 사망 리로드, Phase 전환 등) 재생 중인 사운드가
        // DontDestroyOnLoad 상태로 고아처럼 계속 재생되는 것을 방지.
        Stop();
    }
}
