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
///   StageManager.OnStageClear → SFXEventPlayer.Play()  (sfxId = Stage_Clear)
///   DoorController.OnOpened  → SFXEventPlayer.Play()  (sfxId = Door_Open)
///   DoorController.OnClosed  → SFXEventPlayer.Play()  (sfxId = Door_Close)
///   WallMover.OnMoveStarted  → SFXEventPlayer.Play()  (sfxId = Trap_WallMover)
///   PhaseData.onPhaseEnter   → SFXEventPlayer.Play()  (sfxId = Boss_PhaseTransition_Mouth / Boss_PhaseTransition_Esophagus)
///
/// [3D 재생]
///   위치 기반 사운드가 필요하면 Play3D() 를 연결하면 됨.
///   이 컴포넌트가 붙은 오브젝트의 위치에서 재생됨.
/// </summary>
public class SFXEventPlayer : MonoBehaviour
{
    [Tooltip("Play() 호출 시 재생할 SFX ID")]
    [SerializeField] SFXId sfxId = SFXId.UI_Click;

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
}
