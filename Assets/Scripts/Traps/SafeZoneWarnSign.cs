using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 보스 크로스파이어 페이즈 전용 — 발사 사이클마다 "이번엔 여기가 안전하다"를 미리 표시하는
/// 독립 스케줄 컴포넌트. ArrowTrap 등 함정 스케줄과는 완전히 별개로 동작한다 — 트랩들은 각자
/// 자기 fireAtSeconds로 그대로 발사하고, 여기서는 그 발사 시각에 맞춰 안전 지점만 표시한다
/// (트랩 쪽 코드는 전혀 건드리지 않음 — 두 스케줄은 작성자가 같은 시각 값으로 나란히 맞춰서
/// 입력한다).
///
/// [사용법]
/// 1. cycles[]에 발사 시각(그 사이클에 쏘는 ArrowTrap들의 fireAtSeconds와 같은 기준 시각)과,
///    이번 사이클에 안전한 위치(바닥 타일 등의 Transform)를 순서대로 등록
/// 2. markerVisual에 표시할 마커 오브젝트 연결 — 표시될 때 해당 Transform 위치로 옮긴 뒤 활성화
/// 3. warnParticle(선택)에 순간 파티클 연결 — 표시 시작 시 1회 재생 (주목 유도용)
///
/// [동기화 방식 — DropWarnMarker와 동일 패턴]
/// Host만 스케줄 코루틴을 돌며 표시/숨김을 로컬 적용 + StageNetworkState RPC로 Client에 전달.
/// Client는 스케줄을 스스로 계산하지 않고 RPC 수신으로만 반응한다 — 순수 연출이라 예측 불필요.
/// </summary>
public class SafeZoneWarnSign : MonoBehaviour
{
    [System.Serializable]
    public class SafeZoneCycle
    {
        [Tooltip("발사 시각 (Phase 시작 기준, 초) — 이번 사이클에 쏘는 ArrowTrap들의 fireAtSeconds와 동일한 값으로 입력")]
        public float fireAtSeconds;

        [Tooltip("이 사이클에서 안전한 위치 (바닥 타일 등의 Transform)")]
        public Transform safeSpot;
    }

    public static SafeZoneWarnSign Instance { get; private set; }

    [Header("스케줄 (해당 사이클 ArrowTrap들의 fireAtSeconds와 같은 기준 시각으로 입력)")]
    [SerializeField] private SafeZoneCycle[] cycles = new SafeZoneCycle[0];

    [Header("표시")]
    [Tooltip("안전 지점에 표시할 마커 오브젝트. 사이클마다 safeSpot 위치로 옮긴 뒤 활성화")]
    [SerializeField] private GameObject markerVisual = null;

    [Tooltip("표시 시작 시 1회 재생할 파티클 (선택 — 순간 주목 유도용, 없으면 생략)")]
    [SerializeField] private ParticleSystem warnParticle = null;

    [Tooltip("safeSpot 위치에서 마커를 띄울 Y축 오프셋(m). 타일과 완전히 겹치면 z-fighting으로 " +
             "안 보일 수 있어 기본적으로 살짝 띄운다")]
    [SerializeField] private float markerHeightOffset = 1f;

    [Header("타이밍")]
    [Tooltip("발사 전 안전 지점을 미리 보여줄 시간(초)")]
    [SerializeField] private float warnLeadTime = 1.5f;

    [Tooltip("발사 시각 기준 마커를 숨길 오프셋(초). 0이면 발사 즉시 숨김, 양수면 발사 후에도 유지, " +
             "음수면 발사 전에 미리 숨김(예: warnLeadTime=2, holdAfterFire=-1 → 발사 2초 전에 표시, " +
             "1초 전에 숨김 → 1초간만 노출). warnLeadTime + holdAfterFire가 음수가 되지 않게만 입력할 것 " +
             "(표시 시작 시각보다 먼저 숨기는 건 불가능 — 자동으로 0에서 클램프됨).")]
    [SerializeField] private float holdAfterFire = 0f;

    Coroutine _scheduleCoroutine;

    void Awake()
    {
        Instance = this;
        HideMarkerLocal();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Stage SetActive(false → true) 사이클 시 자동 재시작 (TrapBase.OnEnable과 동일 원칙)
    void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        // Client는 스스로 스케줄을 돌리지 않음 — Host가 보내는 RPC로만 표시/숨김 (순수 연출)
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        _scheduleCoroutine = StartCoroutine(ScheduleLoop());
    }

    void OnDisable()
    {
        if (_scheduleCoroutine != null)
        {
            StopCoroutine(_scheduleCoroutine);
            _scheduleCoroutine = null;
        }
        HideMarkerLocal();
    }

    IEnumerator ScheduleLoop()
    {
        var   nm = NetworkManager.Singleton;
        float baseTime;

        // ArrowTrap/DropTrap과 동일한 앵커 — PhaseStartServerTime 기준으로 Host/Client가
        // 같은 절대 시각을 기준점으로 쓴다 (StageNetworkState가 없는 씬은 로컬 시각 폴백).
        if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
        {
            baseTime = (float)StageNetworkState.Instance.PhaseStartServerTime;
            while (nm != null && (float)nm.ServerTime.Time < baseTime)
                yield return null;
        }
        else
        {
            baseTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        }

        foreach (SafeZoneCycle cycle in cycles)
        {
            if (cycle.safeSpot == null) continue;

            float targetTime = baseTime + cycle.fireAtSeconds;
            if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;

            float now      = nm != null ? (float)nm.ServerTime.Time : Time.time;
            float waitTime = Mathf.Max(0f, targetTime - now - warnLeadTime);
            yield return new WaitForSeconds(waitTime);

            Vector3 pos = cycle.safeSpot.position;
            ShowMarkerLocal(pos);
            StageNetworkState.Instance?.SyncSafeZoneWarnClientRpc(pos);

            yield return new WaitForSeconds(Mathf.Max(0f, warnLeadTime + holdAfterFire));

            HideMarkerLocal();
            StageNetworkState.Instance?.SyncSafeZoneHideClientRpc();
        }
    }

    // ── 재생 진입점 (Host: 위 코루틴에서 직접 호출 / Client: StageNetworkState RPC 수신으로 호출) ──

    public void PlayWarnFromNetwork(Vector3 position) => ShowMarkerLocal(position);

    public void PlayHideFromNetwork() => HideMarkerLocal();

    void ShowMarkerLocal(Vector3 position)
    {
        Vector3 raisedPosition = position + Vector3.up * markerHeightOffset;

        if (markerVisual != null)
        {
            markerVisual.transform.position = raisedPosition;
            markerVisual.SetActive(true);
        }
        if (warnParticle != null)
        {
            warnParticle.transform.position = raisedPosition;
            warnParticle.Play();
        }
    }

    void HideMarkerLocal()
    {
        if (markerVisual != null) markerVisual.SetActive(false);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 첫 사이클 위치에 표시")]
    void TestShowFirst()
    {
        if (cycles == null || cycles.Length == 0 || cycles[0].safeSpot == null) return;
        ShowMarkerLocal(cycles[0].safeSpot.position);
    }

    [ContextMenu("테스트: 숨김")]
    void TestHide() => HideMarkerLocal();
}
