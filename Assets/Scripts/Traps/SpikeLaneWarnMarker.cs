using System.Collections;
using UnityEngine;

/// <summary>
/// SpikeLane 경고 마커 — 레인 전체 길이를 덮는 긴 데칼(라인 메시)에 부착.
/// DropWarnMarker(_Fill 채움)와 달리 채움이 아니라 진행도 0→1에 따라 노란색→빨간색으로
/// 색 자체를 보간한다. 1(완전 빨강)에 도달하는 순간이 곧 SpikeLaneField가 이 레인을
/// 발동시키는 시점(SetPreFireChargeTime(warningDuration)으로 스케줄에 반영됨).
///
/// [설정 방법]
/// 1. SpikeLane 자식으로 배치, 그 레인의 SpikeTrap 타일들 전체 길이를 덮도록 직접 스케일 조절
/// 2. targetRenderer에 마커 메시 Renderer 연결 (비워두면 자식에서 자동 탐색)
/// 3. 전용 머티리얼(URP Lit, _BaseColor 사용) 연결 — 실제 색은 MaterialPropertyBlock으로 덮어써서
///    보간하므로 머티리얼 자체의 기본 색은 의미 없음
/// </summary>
public class SpikeLaneWarnMarker : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("색을 입힐 Renderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Renderer targetRenderer = null;

    [Header("색상 보간 (0=경고 시작, 1=발동)")]
    [Tooltip("Renderer 머티리얼의 색 셰이더 프로퍼티 이름")]
    [SerializeField] private string colorProperty = "_BaseColor";

    [SerializeField] private Color warnStartColor = Color.yellow;
    [SerializeField] private Color warnEndColor = Color.red;

    MaterialPropertyBlock _block;
    int _colorId;
    Coroutine _routine;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _colorId = Shader.PropertyToID(colorProperty);
        _block   = new MaterialPropertyBlock();
        SetVisible(false);
    }

    /// <summary>duration(초) 동안 진행도 0→1(노랑→빨강)로 갱신하며 표시. 완료 후에도 빨간 채로
    /// 계속 보이는 상태를 유지한다 — 언제 끌지는 호출부(SpikeLane.Trigger())가 결정.</summary>
    public void PlayWarning(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        SetVisible(true);
        _routine = StartCoroutine(WarnRoutine(duration));
    }

    /// <summary>발동 즉시(가시가 튀어오르는 순간) 마커를 끈다.</summary>
    public void ResetWarning()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        SetVisible(false);
    }

    IEnumerator WarnRoutine(float duration)
    {
        if (duration <= 0f)
        {
            SetProgress(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / duration);
            yield return null;
        }
        SetProgress(1f);
    }

    void SetProgress(float t)
    {
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(_block);
        _block.SetColor(_colorId, Color.Lerp(warnStartColor, warnEndColor, Mathf.Clamp01(t)));
        targetRenderer.SetPropertyBlock(_block);
    }

    void SetVisible(bool visible)
    {
        if (targetRenderer != null) targetRenderer.enabled = visible;
    }
}
