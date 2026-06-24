/// <summary>
/// 프로젝트 전체 효과음 목록.
/// SFXLibrary / SFXManager / SFXEventPlayer 에서 공통으로 사용.
/// </summary>
public enum SFXId
{
    // ── Boss ─────────────────────────────────────────────────────
    Boss_PhaseTransition,

    // ── Boulder ──────────────────────────────────────────────────
    Boulder_PlayerCollision,
    Boulder_Roll,

    // ── Breakable ────────────────────────────────────────────────
    Breakable_Destroy,

    // ── Buff ─────────────────────────────────────────────────────
    Buff_Received,
    Buff_SpeedUp,
    Buff_Invincibility,

    // ── Door ─────────────────────────────────────────────────────
    Door_Close,
    Door_Open,

    // ── Minigame / Quiz ──────────────────────────────────────────
    Minigame_OX_Correct,
    Minigame_OX_Wrong,
    Minigame_SequenceRing_Correct,

    // ── Mouth ────────────────────────────────────────────────────
    Mouth_TeethBreak,

    // ── Player ───────────────────────────────────────────────────
    Player_ColorChange,
    Player_Death,
    Player_FallDeath,
    Player_Hit,
    Player_InstantKill,
    Player_Respawn,
    Player_Run,

    // ── Pressure Pad ─────────────────────────────────────────────
    PressurePad_Step,

    // ── Stage / Flow ─────────────────────────────────────────────
    Stage_Clear,
    Stage_Countdown,
    Stage_TransitionEnter,
    Stage_TransitionEsophagus,

    // ── Stage 5 ──────────────────────────────────────────────────
    Stage5_Chaser_Attack,
    Stage5_Chaser_Run,
    Stage5_Runner_Captured,
    Stage5_Runner_Run,

    // ── Trap / Hazard ────────────────────────────────────────────
    Trap_AdvancingWall_Move,
    Trap_AdvancingWall_Telegraph,
    Trap_Arrow,
    Trap_Ceiling,
    Trap_ContactDamage,
    Trap_Drop,
    Trap_SpikeRaise,
    Trap_WallMover,

    // ── UI / Menu ────────────────────────────────────────────────
    UI_Click,
    UI_DropdownChange,

    // ── Wind ─────────────────────────────────────────────────────
    Wind_Pull,
    Wind_Push,
}
