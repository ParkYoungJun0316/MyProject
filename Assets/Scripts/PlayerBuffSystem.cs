using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어 버프: BuffType / buffSettings 추가 후 Player 등에서 IsActive·GetValue 연동.</summary>
public class PlayerBuffSystem : MonoBehaviour
{
    public enum BuffType
    {
        SpeedUp,            // 달리기 속도 + value 만큼 추가
        Invincibility,      // 피격 무시
    }

    [System.Serializable]
    public class BuffSetting
    {
        public BuffType type;
        [Tooltip("버프 지속 시간(초)")]
        public float duration = 0f;
        [Tooltip("SpeedUp: 추가 속도 / Invincibility: 사용 안 함")]
        public float value = 0f;
    }

    [Header("버프 기본 설정 (Inspector에서 각 버프의 지속시간·수치 설정)")]
    public BuffSetting[] buffSettings = new BuffSetting[0];

    [System.Serializable]
    public class ActiveBuff
    {
        public BuffType type;
        public float remainingTime;
        public float value;
    }

    /// <summary>버프가 새로 적용되거나 갱신될 때 발생. (BuffType, 전체 지속시간)</summary>
    public event System.Action<BuffType, float> OnBuffApplied;

    List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    void Update()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].remainingTime -= Time.deltaTime;
            if (activeBuffs[i].remainingTime <= 0f)
                activeBuffs.RemoveAt(i);
        }
    }

    /// <summary>buffSettings 기본값 적용. 활성 중이면 남은 시간 갱신.</summary>
    public void ApplyBuff(BuffType type)
    {
        BuffSetting setting = GetSetting(type);
        float dur = setting != null ? setting.duration : 5f;
        float val = setting != null ? setting.value    : 0f;
        ApplyBuff(type, dur, val);
    }

    public void ApplyBuff(BuffType type, float duration, float value)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            if (activeBuffs[i].type == type)
            {
                activeBuffs[i].remainingTime = duration;
                activeBuffs[i].value         = value;
                OnBuffApplied?.Invoke(type, duration);
                return;
            }
        }
        activeBuffs.Add(new ActiveBuff { type = type, remainingTime = duration, value = value });
        OnBuffApplied?.Invoke(type, duration);
    }

    public bool IsActive(BuffType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
            if (activeBuffs[i].type == type) return true;
        return false;
    }

    public float GetValue(BuffType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
            if (activeBuffs[i].type == type) return activeBuffs[i].value;
        return 0f;
    }

    public float GetRemainingTime(BuffType type)
    {
        for (int i = 0; i < activeBuffs.Count; i++)
            if (activeBuffs[i].type == type) return activeBuffs[i].remainingTime;
        return 0f;
    }

    public List<ActiveBuff> GetActiveBuffs() => activeBuffs;

    public void RemoveBuff(BuffType type)
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
            if (activeBuffs[i].type == type)
                activeBuffs.RemoveAt(i);
    }

    public BuffSetting GetSetting(BuffType type)
    {
        for (int i = 0; i < buffSettings.Length; i++)
            if (buffSettings[i].type == type) return buffSettings[i];
        return null;
    }
}
