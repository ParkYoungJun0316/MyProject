/// <summary>
/// 프로젝트 전체 효과음 목록.
/// SFXLibrary / SFXManager / SFXEventPlayer 에서 공통으로 사용.
///
/// [번호 고정 이유]
/// 씬/프리팹의 SFXEventPlayer.sfxId, ArrowTrap.fireSfxId 등은 이 enum 값을 정수로 직렬화해서
/// 저장한다. 번호를 자동 부여(선언 순서)에 맡기면 중간 항목을 삭제/추가할 때 그 뒤 항목들의
/// 번호가 전부 밀려서, 이미 씬에 저장된 다른 연결까지 엉뚱한 값으로 깨질 수 있다. 그래서 각
/// 항목에 번호를 명시로 고정한다 — 새 항목은 끝에 추가하거나 안 쓰는 번호를 재사용, 삭제 시
/// 그 번호는 그냥 비워두고 나머지는 그대로 둔다.
/// </summary>
public enum SFXId
{
    // ── Boss ─────────────────────────────────────────────────────
    Boss_PhaseTransition_Mouth    = 0,
    Boss_PhaseTransition_Esophagus = 1,
    Boss_Die_Mouth                 = 41,
    Boss_Die_Esophagus             = 42,

    // ── Boulder ──────────────────────────────────────────────────
    Boulder_Roll                  = 3,

    // ── Breakable ────────────────────────────────────────────────
    Breakable_Destroy              = 4,

    // ── Buff ─────────────────────────────────────────────────────
    Buff                           = 5,

    // ── Door ─────────────────────────────────────────────────────
    Door_Close                     = 7,
    Door_Open                      = 8,

    // ── Minigame / Quiz ──────────────────────────────────────────
    // 9, 10: 삭제됨 (Minigame_OX_Correct / Minigame_OX_TimerTick) — 번호 재사용 금지
    Minigame_SequenceRing_Correct  = 12,
    Minigame_SequenceRing_Wrong    = 39,

    // ── Mouth ────────────────────────────────────────────────────
    Mouth_TeethBreak_1             = 13,

    // ── Player ───────────────────────────────────────────────────
    Player_ColorChange              = 15,
    Player_Death                    = 16,
    Player_Hit                      = 17,
    Player_Punch                    = 43,
    Player_Run                      = 18,

    // ── Stage / Flow ─────────────────────────────────────────────
    // Stage_TransitionMouth: M.* 씬(Mouth 구역) 진입 시. Stage_TransitionEsophagus: T.* 씬(Esophagus 구역) 진입 시.
    // SceneFlowManager.OnSceneLoaded() 에서 씬 이름 접두사로 자동 재생 (코드 연결, 인스펙터 연결 아님).
    Stage_TransitionMouth           = 21,
    Stage_TransitionEsophagus       = 22,

    // ── Stage 5 ──────────────────────────────────────────────────
    Stage5_Chaser_Attack            = 23,
    Stage5_Chaser_Run                = 24,
    Stage5_Runner_Captured           = 25,
    Stage5_Runner_Run                = 26,

    // ── Trap / Hazard ────────────────────────────────────────────
    Trap_AdvancingWall_Move          = 27,
    Trap_AdvancingWall_Telegraph      = 28,
    Trap_Arrow                       = 29,
    Trap_Ceiling                     = 31,
    Trap_Drop                        = 32,
    Trap_SpikeRaise                  = 33,

    // ── UI / Menu ────────────────────────────────────────────────
    UI_Click                         = 35,

    // ── Wind ─────────────────────────────────────────────────────
    Wind_Pull                        = 36,
    Wind_Push                        = 37,

    // ── 재생 없음 (Inspector 기본값용) ───────────────────────────
    None                             = 38,

    // ── Trap / Hazard (추가분) ────────────────────────────────────
    Trap_SpikeWarn                   = 40,
}
