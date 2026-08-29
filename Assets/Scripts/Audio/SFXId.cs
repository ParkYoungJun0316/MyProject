/// <summary>
/// 프로젝트 전체 효과음 목록.
/// SFXLibrary / SFXManager / SFXEventPlayer 에서 공통으로 사용.
///
/// [번호 고정 이유]
/// 씬/프리팹의 SFXEventPlayer.sfxId, ArrowTrap.fireSfxId 등은 이 enum 값을 정수로 직렬화해서
/// 저장한다. 번호를 자동 부여(선언 순서)에 맡기면 중간 항목을 삭제/추가할 때 그 뒤 항목들의
/// 번호가 전부 밀려서, 이미 씬에 저장된 다른 연결까지 엉뚱한 값으로 깨질 수 있다. 그래서 각
/// 항목에 번호를 명시로 고정한다 — 새 항목은 끝에 추가하고, 삭제 시 그 번호는 비워두고
/// 나머지는 그대로 둔다. 전면 재번호는 씬/프리팹 SFX 연결을 전부 다시 잡을 때만 한다.
/// </summary>
public enum SFXId
{
    None = 0,

    // ── Boss ─────────────────────────────────────────────────────
    Boss_PhaseTransition_Mouth     = 1,
    Boss_PhaseTransition_Esophagus = 2,
    Boss_Die_Mouth                 = 3,
    Boss_Die_Esophagus             = 4,

    // ── Boulder ──────────────────────────────────────────────────
    Boulder_Roll                   = 5,

    // ── Breakable ────────────────────────────────────────────────
    Breakable_Destroy              = 6,

    // ── Buff ─────────────────────────────────────────────────────
    Buff                           = 7,

    // ── Door ─────────────────────────────────────────────────────
    Door_Close                     = 8,
    Door_Open                      = 9,

    // ── Minigame / Quiz ──────────────────────────────────────────
    Minigame_SequenceRing_Correct  = 10,
    Minigame_SequenceRing_Wrong    = 11,

    // ── Player ───────────────────────────────────────────────────
    Player_ColorChange             = 12,
    Player_Death                   = 13,
    Player_Hit                     = 14,
    Player_Punch                   = 15,
    Player_PunchHit                = 16,
    Player_Run                     = 17,

    // ── Stage / Flow ─────────────────────────────────────────────
    // M.* 씬(Mouth 구역) 진입 시 SceneFlowManager.OnSceneLoaded()에서 자동 재생.
    Stage_TransitionMouth          = 18,

    // ── Stage 5 ──────────────────────────────────────────────────
    Stage5_Chaser_Attack           = 19,
    Stage5_Chaser_Run              = 20,
    Stage5_Runner_Captured         = 21,
    Stage5_Runner_Run              = 22,

    // ── Trap / Hazard ────────────────────────────────────────────
    Trap_AdvancingWall_Move        = 23,
    Trap_AdvancingWall_Telegraph   = 24,
    Trap_Arrow                     = 25,
    Trap_Drop                      = 26,
    Trap_SpikeRaise                = 27,

    // ── UI / Menu ────────────────────────────────────────────────
    UI_Click                       = 28,

    // ── Wind ─────────────────────────────────────────────────────
    Wind_Pull                      = 29,
    Wind_Push                      = 30,

    // ── Stage / Flow (추가) ──────────────────────────────────────
    Stage_TransitionEsophagus      = 31,
}
