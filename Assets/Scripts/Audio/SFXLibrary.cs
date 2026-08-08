using System;
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
/// [1/2 교차 재생]
///   Mouth_TeethBreak, Trap_Arrow 는 SFXManager.PlayAlternating() 으로 번갈아 재생.
///
/// [클립별 볼륨 보정 — Volume Overrides]
///   원본 음원끼리 소리 크기가 서로 안 맞을 때(다른 곳에서 받아온 음원 등), Audacity로 파일 자체를
///   고치지 않고도 이 에셋(Inspector)에서 특정 SFX만 상대적으로 크게/작게 보정할 수 있음.
///   목록에 없는 SFXId는 보정 없음(1배)으로 처리됨. 0~2배 범위 — 1보다 크면 원본보다 부스트됨
///   (너무 크게 부스트하면 노이즈/클리핑 위험 있으니 소폭 보정 용도로만 사용 권장).
///   최종 소리 크기 = 원본 × 이 보정배율 × 옵션 메뉴 마스터·SFX 볼륨(0~1, GameSettingsManager).
/// </summary>
[CreateAssetMenu(fileName = "SFXLibrary", menuName = "Audio/SFX Library")]
public class SFXLibrary : ScriptableObject
{
    // ── Boss ─────────────────────────────────────────────────────
    [Header("Boss")]
    public AudioClip Boss_PhaseTransition_Mouth;
    public AudioClip Boss_PhaseTransition_Esophagus;

    // ── Boulder ──────────────────────────────────────────────────
    [Header("Boulder")]
    public AudioClip Boulder_PlayerCollision;
    public AudioClip Boulder_Roll;

    // ── Breakable ────────────────────────────────────────────────
    [Header("Breakable")]
    public AudioClip Breakable_Destroy;

    // ── Buff ─────────────────────────────────────────────────────
    [Header("Buff")]
    public AudioClip Buff_Shield;
    public AudioClip Buff_SpeedUp;

    // ── Door ─────────────────────────────────────────────────────
    [Header("Door")]
    public AudioClip Door_Close;
    public AudioClip Door_Open;

    // ── Minigame / Quiz ──────────────────────────────────────────
    [Header("Minigame / Quiz")]
    public AudioClip Minigame_OX_Correct;
    [Tooltip("문제 시작~정답 공개까지 도는 타이머 틱 루프 사운드")]
    public AudioClip Minigame_OX_TimerTick;
    public AudioClip Minigame_OX_Wrong;
    public AudioClip Minigame_SequenceRing_Correct;

    // ── Mouth ────────────────────────────────────────────────────
    [Header("Mouth")]
    public AudioClip Mouth_TeethBreak_1;
    public AudioClip Mouth_TeethBreak_2;

    // ── Player ───────────────────────────────────────────────────
    [Header("Player")]
    public AudioClip Player_ColorChange;
    public AudioClip Player_Death;
    public AudioClip Player_Hit;
    [Tooltip("달리기 루프 클립. 앞뒤를 잘라서 루프가 자연스럽도록 준비할 것")]
    public AudioClip Player_Run;

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
    public AudioClip Trap_Arrow_1;
    public AudioClip Trap_Arrow_2;
    public AudioClip Trap_Ceiling;
    public AudioClip Trap_Drop;
    public AudioClip Trap_SpikeRaise;
    public AudioClip Trap_WallMover;

    // ── UI / Menu ────────────────────────────────────────────────
    [Header("UI / Menu")]
    public AudioClip UI_Click;

    // ── Wind ─────────────────────────────────────────────────────
    [Header("Wind")]
    public AudioClip Wind_Pull;
    public AudioClip Wind_Push;

    // ── 클립별 볼륨 보정 ──────────────────────────────────────────

    [Serializable]
    public class VolumeOverride
    {
        public SFXId id;
        [Range(0f, 2f)]
        [Tooltip("1 = 원본 그대로. 1보다 작으면 감쇠, 크면 부스트(과하게 쓰면 클리핑 위험).")]
        public float volume = 1f;
    }

    [Header("클립별 볼륨 보정 (선택, 목록에 없으면 1배)")]
    [SerializeField] VolumeOverride[] volumeOverrides;

    /// <summary>id에 대한 볼륨 보정 배율. 목록에 없으면 1(보정 없음).</summary>
    public float GetVolumeMultiplier(SFXId id)
    {
        if (volumeOverrides == null) return 1f;
        foreach (VolumeOverride o in volumeOverrides)
            if (o != null && o.id == id) return o.volume;
        return 1f;
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>ID 에 해당하는 AudioClip 을 반환한다. null 이면 재생 스킵.</summary>
    public AudioClip GetClip(SFXId id)
    {
        switch (id)
        {
            case SFXId.Boss_PhaseTransition_Mouth:        return Boss_PhaseTransition_Mouth;
            case SFXId.Boss_PhaseTransition_Esophagus:    return Boss_PhaseTransition_Esophagus;

            case SFXId.Boulder_PlayerCollision:           return Boulder_PlayerCollision;
            case SFXId.Boulder_Roll:                      return Boulder_Roll;

            case SFXId.Breakable_Destroy:                 return Breakable_Destroy;

            case SFXId.Buff_Shield:                       return Buff_Shield;
            case SFXId.Buff_SpeedUp:                      return Buff_SpeedUp;

            case SFXId.Door_Close:                        return Door_Close;
            case SFXId.Door_Open:                         return Door_Open;

            case SFXId.Minigame_OX_Correct:               return Minigame_OX_Correct;
            case SFXId.Minigame_OX_TimerTick:             return Minigame_OX_TimerTick;
            case SFXId.Minigame_OX_Wrong:                 return Minigame_OX_Wrong;
            case SFXId.Minigame_SequenceRing_Correct:     return Minigame_SequenceRing_Correct;

            case SFXId.Mouth_TeethBreak_1:                  return Mouth_TeethBreak_1;
            case SFXId.Mouth_TeethBreak_2:                  return Mouth_TeethBreak_2;

            case SFXId.Player_ColorChange:                return Player_ColorChange;
            case SFXId.Player_Death:                      return Player_Death;
            case SFXId.Player_Hit:                        return Player_Hit;
            case SFXId.Player_Run:                        return Player_Run;

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
            case SFXId.Trap_Arrow_1:                      return Trap_Arrow_1;
            case SFXId.Trap_Arrow_2:                      return Trap_Arrow_2;
            case SFXId.Trap_Ceiling:                      return Trap_Ceiling;
            case SFXId.Trap_Drop:                         return Trap_Drop;
            case SFXId.Trap_SpikeRaise:                   return Trap_SpikeRaise;
            case SFXId.Trap_WallMover:                    return Trap_WallMover;

            case SFXId.UI_Click:                          return UI_Click;

            case SFXId.Wind_Pull:                         return Wind_Pull;
            case SFXId.Wind_Push:                         return Wind_Push;

            default: return null;
        }
    }
}
