using System.Collections;
using UnityEngine;

/// <summary>
/// SpikeLane 경고 마커 — 레인 전체 길이를 덮는 긴 데칼(라인 메시)에 부착.
/// 가시 레인뿐 아니라 BreakTile / GridTileCollapse / MouthBossJawSmash / TongueController의
/// 칸형 경고도 전부 이 컴포넌트를 쓴다. 채움 없이 진행도 0→1에 따라 WarnPalette.Start(탠저린)→
/// End(진홍)로 색 자체를 보간한다(WarnMarker 셰이더 _FillMode=1, 발동 직전 펄스는 _Progress 기준). 1(완전 빨강)에 도달하는 순간이 곧 SpikeLaneField가 이 레인을
/// 발동시키는 시점(SetPreFireChargeTime(warningDuration)으로 스케줄에 반영됨).
///
/// [설정 방법]
/// 1. SpikeLane 자식으로 배치, 그 레인의 SpikeTrap 타일들 전체 길이를 덮도록 직접 스케일 조절
/// 2. targetRenderer에 마커 메시 Renderer 연결 (비워두면 자식에서 자동 탐색)
/// 3. Assets/Mat/Traps/WarnMarkerTile.mat(WarnMarker 셰이더, _FillMode=1) 연결 — 실제 색은
///    MaterialPropertyBlock으로 덮어써서 보간하므로 머티리얼 자체의 기본 색은 의미 없음
/// </summary>
public class SpikeLaneWarnMarker : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("색을 입힐 Renderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Renderer targetRenderer = null;

    [Header("색 (값은 WarnPalette 공용 — 0=경고 시작, 1=발동)")]
    [Tooltip("Renderer 머티리얼의 색 셰이더 프로퍼티 이름")]
    [SerializeField] private string colorProperty = "_BaseColor";

    WarnMarkerColorFx _fx;
    Coroutine _routine;

    void Awake() => EnsureInit();

    /// <summary>
    /// _fx 지연 초기화. 원래는 Awake에서만 만들었는데, 이 마커를 소유하지 않은 다른 컴포넌트가
    /// (예: TongueController.ResetAllWarnings) 자기 OnEnable에서 곧바로 PlayWarning/ResetWarning을
    /// 호출하면 이 GO의 Awake가 아직 안 돈 경우 _fx가 null이라 NRE가 났다(Unity는 GameObject 간
    /// Awake 순서를 보장하지 않음). Awake/PlayWarning/ResetWarning 세 곳 모두 이걸 거쳐 몇 번을
    /// 호출해도 안전하고 순서에 의존하지 않는다.
    /// </summary>
    void EnsureInit()
    {
        if (_fx != null) return;
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _fx = new WarnMarkerColorFx(targetRenderer, colorProperty, WarnPalette.Start, WarnPalette.End, null, "_Progress");
        SetVisible(false);
    }

    /// <summary>duration(초) 동안 진행도 0→1(탠저린→진홍)로 갱신하며 표시. 완료 후에도 진홍 채로
    /// 계속 보이는 상태를 유지한다 — 언제 끌지는 호출부(SpikeLane.Trigger())가 결정.</summary>
    public void PlayWarning(float duration)
    {
        EnsureInit();
        if (_routine != null) StopCoroutine(_routine);
        SetVisible(true);
        _routine = StartCoroutine(WarnRoutine(duration));
    }

    /// <summary>발동 즉시(가시가 튀어오르는 순간) 마커를 끈다.</summary>
    public void ResetWarning()
    {
        EnsureInit();
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

    void SetProgress(float t) => _fx.SetProgress(t);

    void SetVisible(bool visible) => _fx.SetRendererVisible(visible);
}
