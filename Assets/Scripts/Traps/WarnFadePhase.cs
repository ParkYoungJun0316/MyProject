using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 경고 사인 공용 — 시간 경과에 따라 경고 사인이 점점 흐려지는 난이도 단계.
/// SpeedPhase(속도 단계상승, 계단식)와 달리 afterSeconds 지점들 사이를 선형 보간한다 —
/// "30초까지 잘 보이고 60초까지 서서히 흐려지다 그 뒤로 안 보임" 같은 그라데이션 페이드를
/// 표현하려면 계단식보다 보간이 자연스럽다.
/// afterSeconds 오름차순으로 여러 단계를 입력. 범위 밖(첫 단계 이전/마지막 단계 이후)은
/// 각각 첫/마지막 alphaMultiplier로 고정(clamp)된다.
/// </summary>
[System.Serializable]
public class WarnFadePhase
{
    [Tooltip("Phase 시작(PhaseStartServerTime) 후 이 초가 지난 시점의 알파 배율 (오름차순 입력)")]
    public float afterSeconds = 0f;

    [Tooltip("경고 사인 알파(불투명도) 배율. 1=원래 밝기, 0=완전히 안 보임")]
    [Range(0f, 1f)]
    public float alphaMultiplier = 1f;

    // PhaseStartServerTime(동기화된 NV) + ServerTime 기준이라 Host/Client가 별도 RPC 없이 같은 값을 계산한다.
    public static float Evaluate(WarnFadePhase[] phases)
    {
        if (phases == null || phases.Length == 0) return 1f;

        var nm = NetworkManager.Singleton;
        var state = StageNetworkState.Instance;
        if (nm == null || state == null) return 1f;

        double phaseStart = state.PhaseStartServerTime;
        if (phaseStart <= 0) return 1f;

        float elapsed = (float)(nm.ServerTime.Time - phaseStart);

        if (elapsed <= phases[0].afterSeconds) return phases[0].alphaMultiplier;

        for (int i = 0; i < phases.Length - 1; i++)
        {
            WarnFadePhase a = phases[i];
            WarnFadePhase b = phases[i + 1];
            if (elapsed > b.afterSeconds) continue;

            float span = b.afterSeconds - a.afterSeconds;
            float u = span > 0f ? (elapsed - a.afterSeconds) / span : 1f;
            return Mathf.Lerp(a.alphaMultiplier, b.alphaMultiplier, u);
        }

        return phases[phases.Length - 1].alphaMultiplier;
    }
}
