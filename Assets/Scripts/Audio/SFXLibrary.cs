using UnityEngine;

/// <summary>
/// 프로젝트 전체 효과음 클립 목록 (ScriptableObject).
///
/// [생성 방법]
///   Project 창 우클릭 → Create → Audio → SFX Library
///
/// [사용 방법]
///   1. 생성된 SFXLibrary 에셋을 선택한다.
///   2. 각 슬롯에 해당 AudioClip 을 드래그한다.
///      - 아직 없는 SFX 는 비워 둔다 (null 이면 재생 스킵됨).
///   3. SFXManager Inspector 의 Library 필드에 이 에셋을 연결한다.
///
/// [Fallback 규칙]
///   - Buff_SpeedUp / Buff_Invincibility 가 null → Buff_Received 로 대체
///   - Player_FallDeath / Player_InstantKill 이 null → Player_Death 로 대체
///   - UI_DropdownChange 가 null → UI_Click 으로 대체
/// </summary>
[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Audio/SFX Library")]
public class SFXLibrary : ScriptableObject
{
    // ── Boss ─────────────────────────────────────────────────────
    [Header("Boss")]
    public AudioClip Boss_PhaseTransition;

    // ── Boulder ──────────────────────────────────────────────────
    [Header("Boulder")]
    public AudioClip Boulder_PlayerCollision;
    public AudioClip Boulder_Roll;

    // ── Breakable ────────────────────────────────────────────────
    [Header("Breakable")]
    public AudioClip Breakable_Destroy;

    // ── Buff ─────────────────────────────────────────────────────
    [Header("Buff")]
    [Tooltip("SpeedUp / Invincibility 가 null 일 때 재생되는 공통 버프음")]
    public AudioClip Buff_Received;
    [Tooltip("null 이면 Buff_Received 로 대체")]
    public AudioClip Buff_SpeedUp;
    [Tooltip("null 이면 Buff_Received 로 대체")]
    public AudioClip Buff_Invincibility;

    // ── Door ─────────────────────────────────────────────────────
    [Header("Door")]
    public AudioClip Door_Close;
    public AudioClip Door_Open;

    // ── Minigame / Quiz ──────────────────────────────────────────
    [Header("Minigame / Quiz")]
    public AudioClip Minigame_OX_Correct;
    public AudioClip Minigame_OX_Wrong;
    public AudioClip Minigame_SequenceRing_Correct;

    // ── Mouth ────────────────────────────────────────────────────
    [Header("Mouth")]
    public AudioClip Mouth_TeethBreak;

    // ── Player ───────────────────────────────────────────────────
    [Header("Player")]
    public AudioClip Player_ColorChange;
    public AudioClip Player_Death;
    [Tooltip("null 이면 Player_Death 로 대체")]
    public AudioClip Player_FallDeath;
    public AudioClip Player_Hit;
    [Tooltip("null 이면 Player_Death 로 대체")]
    public AudioClip Player_InstantKill;
    public AudioClip Player_Respawn;
    [Tooltip("달리기 루프 클립. 앞뒤를 잘라서 루프가 자연스럽도록 준비할 것")]
    public AudioClip Player_Run;

    // ── Pressure Pad ─────────────────────────────────────────────
    [Header("Pressure Pad")]
    public AudioClip PressurePad_Step;

    // ── Stage / Flow ─────────────────────────────────────────────
    [Header("Stage / Flow")]
    public AudioClip Stage_Clear;
    public AudioClip Stage_Countdown;
    public AudioClip Stage_TransitionEnter;
    public AudioClip Stage_TransitionEsophagus;

    // ── Stage 5 ──────────────────────────────────────────────────
    [Header("Stage 5")]
    public AudioClip Stage5_Chaser_Attack;
    public AudioClip Stage5_Chaser_Run;
    public AudioClip Stage5_Runner_Captured;
    public AudioClip Stage5_Runner_Run;

    // ── Trap / Hazard ────────────────────────────────────────────
    [Header("Trap / Hazard")]
    public AudioClip Trap_AdvancingWall_Move;
    public AudioClip Trap_AdvancingWall_Telegraph;
    public AudioClip Trap_Arrow;
    public AudioClip Trap_Ceiling;
    public AudioClip Trap_ContactDamage;
    public AudioClip Trap_Drop;
    public AudioClip Trap_SpikeRaise;
    public AudioClip Trap_WallMover;

    // ── UI / Menu ────────────────────────────────────────────────
    [Header("UI / Menu")]
    public AudioClip UI_Click;
    [Tooltip("null 이면 UI_Click 으로 대체")]
    public AudioClip UI_DropdownChange;

    // ── Wind ─────────────────────────────────────────────────────
    [Header("Wind")]
    public AudioClip Wind_Pull;
    public AudioClip Wind_Push;

    // ─────────────────────────────────────────────────────────────

    /// <summary>ID 에 해당하는 AudioClip 을 반환한다. null 이면 재생 스킵.</summary>
    public AudioClip GetClip(SFXId id)
    {
        switch (id)
        {
            case SFXId.Boss_PhaseTransition:              return Boss_PhaseTransition;

            case SFXId.Boulder_PlayerCollision:           return Boulder_PlayerCollision;
            case SFXId.Boulder_Roll:                      return Boulder_Roll;

            case SFXId.Breakable_Destroy:                 return Breakable_Destroy;

            case SFXId.Buff_Received:                     return Buff_Received;
            case SFXId.Buff_SpeedUp:                      return Buff_SpeedUp != null ? Buff_SpeedUp : Buff_Received;
            case SFXId.Buff_Invincibility:                return Buff_Invincibility != null ? Buff_Invincibility : Buff_Received;

            case SFXId.Door_Close:                        return Door_Close;
            case SFXId.Door_Open:                         return Door_Open;

            case SFXId.Minigame_OX_Correct:               return Minigame_OX_Correct;
            case SFXId.Minigame_OX_Wrong:                 return Minigame_OX_Wrong;
            case SFXId.Minigame_SequenceRing_Correct:     return Minigame_SequenceRing_Correct;

            case SFXId.Mouth_TeethBreak:                  return Mouth_TeethBreak;

            case SFXId.Player_ColorChange:                return Player_ColorChange;
            case SFXId.Player_Death:                      return Player_Death;
            case SFXId.Player_FallDeath:                  return Player_FallDeath != null ? Player_FallDeath : Player_Death;
            case SFXId.Player_Hit:                        return Player_Hit;
            case SFXId.Player_InstantKill:                return Player_InstantKill != null ? Player_InstantKill : Player_Death;
            case SFXId.Player_Respawn:                    return Player_Respawn;
            case SFXId.Player_Run:                        return Player_Run;

            case SFXId.PressurePad_Step:                  return PressurePad_Step;

            case SFXId.Stage_Clear:                       return Stage_Clear;
            case SFXId.Stage_Countdown:                   return Stage_Countdown;
            case SFXId.Stage_TransitionEnter:             return Stage_TransitionEnter;
            case SFXId.Stage_TransitionEsophagus:         return Stage_TransitionEsophagus;

            case SFXId.Stage5_Chaser_Attack:              return Stage5_Chaser_Attack;
            case SFXId.Stage5_Chaser_Run:                 return Stage5_Chaser_Run;
            case SFXId.Stage5_Runner_Captured:            return Stage5_Runner_Captured;
            case SFXId.Stage5_Runner_Run:                 return Stage5_Runner_Run;

            case SFXId.Trap_AdvancingWall_Move:           return Trap_AdvancingWall_Move;
            case SFXId.Trap_AdvancingWall_Telegraph:      return Trap_AdvancingWall_Telegraph;
            case SFXId.Trap_Arrow:                        return Trap_Arrow;
            case SFXId.Trap_Ceiling:                      return Trap_Ceiling;
            case SFXId.Trap_ContactDamage:                return Trap_ContactDamage;
            case SFXId.Trap_Drop:                         return Trap_Drop;
            case SFXId.Trap_SpikeRaise:                   return Trap_SpikeRaise;
            case SFXId.Trap_WallMover:                    return Trap_WallMover;

            case SFXId.UI_Click:                          return UI_Click;
            case SFXId.UI_DropdownChange:                 return UI_DropdownChange != null ? UI_DropdownChange : UI_Click;

            case SFXId.Wind_Pull:                         return Wind_Pull;
            case SFXId.Wind_Push:                         return Wind_Push;

            default: return null;
        }
    }
}
