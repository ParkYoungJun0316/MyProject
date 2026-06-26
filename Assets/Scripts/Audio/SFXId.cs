/// <summary>
/// 프로젝트 전체 효과음 목록.
/// SFXLibrary / SFXManager / SFXEventPlayer 에서 공통으로 사용.
/// </summary>
public enum SFXId
{
    // ── Boss ─────────────────────────────────────────────────────
    Boss_PhaseTransition_Mouth,
    Boss_PhaseTransition_Esophagus,

    // ── Boulder ──────────────────────────────────────────────────
    Boulder_PlayerCollision,
    Boulder_Roll,

    // ── Breakable ────────────────────────────────────────────────
    Breakable_Destroy,

    // ── Buff ─────────────────────────────────────────────────────
    Buff_Shield,
    Buff_SpeedUp,

    // ── Door ─────────────────────────────────────────────────────
    Door_Close,
    Door_Open,

    // ── Fruit ────────────────────────────────────────────────────
    FruitPop_1,
    FruitPop_2,

    // ── Minigame / Quiz ──────────────────────────────────────────
    Minigame_OX_Correct,
    Minigame_OX_Wrong,
    Minigame_SequenceRing_Correct,

    // ── Mouth ────────────────────────────────────────────────────
    Mouth_TeethBreak_1,
    Mouth_TeethBreak_2,

    // ── Player ───────────────────────────────────────────────────
    Player_ColorChange,
    Player_Death,
    Player_Hit,
    Player_Respawn,
    Player_Run,

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
    Trap_Arrow_1,
    Trap_Arrow_2,
    Trap_Ceiling,
    Trap_Drop,
    Trap_SpikeRaise,
    Trap_WallMover,

    // ── UI / Menu ────────────────────────────────────────────────
    UI_Click,

    // ── Wind ─────────────────────────────────────────────────────
    Wind_Pull,
    Wind_Push,

    // ── 재생 없음 (Inspector 기본값용) ───────────────────────────
    None,
}
