using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 버프 시스템.
/// 새 버프 추가 방법:
///   1. BuffType enum에 항목 추가
///   2. Inspector의 Buff Settings 배열에 항목 추가 (duration, value 설정)
///   3. Player.cs의 해당 위치에 IsActive(BuffType.XXX) 체크 추가
/// </summary>
public class PlayerBuffSystem : MonoBehaviour
{
    // ── 버프 타입 ────────────────────────────────────────────
    public enum BuffType
    {
        SpeedUp,            // 달리기 속도 + value 만큼 추가
        InfiniteStamina,    // 스테미나 소모 없음
        Invincibility,      // 피격 무시
        // 추가 예시: HealOverTime, DamageUp, DodgeCooldownReduce 등
    }

    // ── Inspector 설정 ───────────────────────────────────────
    [System.Serializable]
    public class BuffSetting
    {
        public BuffType type;
        [Tooltip("버프 지속 시간(초)")]
        public float duration = 0f;
        [Tooltip("SpeedUp: 추가 속도 / InfiniteStamina·Invincibility: 사용 안 함")]
        public float value = 0f;
    }

    [Header("버프 기본 설정 (Inspector에서 각 버프의 지속시간·수치 설정)")]
    public BuffSetting[] buffSettings = new BuffSetting[0];

    // ── 런타임 상태 ──────────────────────────────────────────
    [System.Serializable]
    public class ActiveBuff
    {
        public BuffType type;
        public float remainingTime;
        public float value;
    }

    [Header("활성 버프 (Runtime 확인용)")]
    public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    // ── Update ───────────────────────────────────────────────

    void Update()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].remainingTime -= Time.deltaTime;
            if (activeBuffs[i].remainingTime <= 0f)
                activeBuffs.RemoveAt(i);
        }
    }

    // ── 공개 API ─────────────────────────────────────────────

    /// <summary>
    /// Inspector에 설정된 기본값으로 버프 적용.
    /// 이미 활성 중이면 남은 시간을 기본 duration으로 갱신.
    /// </summary>
    public void ApplyBuff(BuffType type)
    {
        BuffSetting setting = GetSetting(type);
        float dur = setting != null ? setting.duration : 5f;
        float val = setting != null ? setting.value    : 0f;
        ApplyBuff(type, dur, val);
    }

    /// <summary>
    /// duration·value를 직접 지정해 버프 적용 (이벤트·아이템 등에서 커스텀 사용).
    /// 이미 활성 중이면 남은 시간을 새 duration으로 갱신.
    /// </summary>
    public void ApplyBuff(BuffType type, float duration, float value)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].type == type)
            {
                activeBuffs[i].remainingTime = duration;
                activeBuffs[i].value         = value;
                return;
            }
        }
        activeBuffs.Add(new ActiveBuff { type = type, remainingTime = duration, value = value });
    }

    /// <summary> 특정 버프가 현재 활성 중인지 여부 </summary>
    public bool IsActive(BuffType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
            if (activeBuffs[i].type == type) return true;
        return false;
    }

    /// <summary> 활성 버프의 value 반환. 없으면 0 </summary>
    public float GetValue(BuffType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
            if (activeBuffs[i].type == type) return activeBuffs[i].value;
        return 0f;
    }

    /// <summary> 특정 버프 즉시 제거 </summary>
    public void RemoveBuff(BuffType type)
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
            if (activeBuffs[i].type == type)
                activeBuffs.RemoveAt(i);
    }

    // ── 내부 헬퍼 ───────────────────────────────────────────

    BuffSetting GetSetting(BuffType type)
    {
        for (int i = 0; i < buffSettings.Length; i++)
            if (buffSettings[i].type == type) return buffSettings[i];
        return null;
    }
}
