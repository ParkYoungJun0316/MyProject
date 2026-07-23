using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 다중 스파이크 레인 필드.
/// activeLaneCount개의 레인을 랜덤으로 선택해 동시에 발동.
/// 발동되지 않은 나머지 레인은 항상 비활성 상태를 유지.
/// TrapBase.activateInterval로 반복 주기를 제어.
///
/// [씬 계층 구조]
///  SpikeLaneField (이 스크립트)
///  ├─ Lane_0 (SpikeLane)
///  │   ├─ SpikeTrap 타일 ...
///  ├─ Lane_1 (SpikeLane)
///  │   └─ SpikeTrap 타일 ...
///  └─ Lane_7 (SpikeLane)
///      └─ SpikeTrap 타일 ...
///
/// [설정]
/// 1. 자식 SpikeLane들은 자동 수집 (lanes 필드를 직접 채워도 됨)
/// 2. 각 SpikeLane 아래 SpikeTrap의 startActive = false, activateInterval = 0 으로 설정
/// 3. activeLaneCount로 동시에 발동할 레인 수 설정
/// 4. activateInterval(TrapBase)로 발동 반복 주기 설정
/// </summary>
public class SpikeLaneField : TrapBase
{
    [Header("Lane Field")]
    [Tooltip("발동 대상 레인 목록. 비워두면 자식 SpikeLane을 자동 수집.")]
    [SerializeField] SpikeLane[] lanes;

    [Tooltip("한 번에 발동할 레인 수. lanes 개수를 넘을 수 없음.")]
    [SerializeField, Range(1, 8)] int activeLaneCount = 1;

    [Tooltip("true: 직전에 발동됐던 레인은 다음 발동 후보에서 제외 (연속 방지)")]
    [SerializeField] bool excludeLastLanes = false;

    [Header("경고 연출 (추후 구현)")]
#pragma warning disable CS0414
    [Tooltip("경고 비주얼 프리팹 — 추후 연결")]
    [SerializeField] GameObject warningPrefab = null;

    [Tooltip("경고 표시 지속 시간(초)")]
    [SerializeField] float warningDuration = 0f;

    [Tooltip("경고 오브젝트를 생성할 부모 Transform — 추후 연결")]
    [SerializeField] Transform warningParent = null;

    [Tooltip("경고 오디오 클립 — 추후 연결")]
    [SerializeField] AudioClip warningSound = null;
#pragma warning restore CS0414

    int[] _lastSelected;

    // Host/Client가 같은 레인을 뽑도록 OnTrapTrigger 발동 횟수를 시드 salt로 사용.
    // TrapLoop()가 ServerTime에 앵커링돼 있어 이 값은 Host/Client에서 항상 같은 시점에 같은 값으로 증가함.
    int _fireCount;

    protected override void Awake()
    {
        base.Awake();
        if (lanes == null || lanes.Length == 0)
            lanes = GetComponentsInChildren<SpikeLane>(true);
    }

    // ── 스케줄 기준 시각 결정 ─────────────────────────────────────────
    // [버그 수정 2026-07-24] ArrowTrap/DropTrap/WindTrap과 동일한 이유로 PhaseStartServerTime
    // (Host가 이 Phase 진입 직전에 기록한 절대 ServerTime)을 앵커로 사용 — Host/Client가 동일한
    // 절대 시각을 기준으로 삼아, Client의 Activate() 호출이 Phase NV 전파 지연만큼 늦게 와도
    // 스케줄이 밀리지 않는다. StageStartServerTime이 아니라 별도 슬롯인 PhaseStartServerTime을
    // 쓴다 — StageStartGate가 그 값을 "이 방 게이트 완료" 1회성 신호로 배타적으로 쓰므로 같이
    // 쓰면 안 된다. StageNetworkState가 없는 씬(테스트 등)에서는 로컬 Activate() 시각으로 폴백.
    // 아래 OnTrapTrigger()의 시드 동기화(Host/Client 발동 횟수 일치)는 이 앵커 교체와 무관하게 유지됨.
    protected override IEnumerator TrapLoop()
    {
        var nm = NetworkManager.Singleton;
        float scheduleStartTime;

        if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
        {
            scheduleStartTime = (float)StageNetworkState.Instance.PhaseStartServerTime + initialDelay;
            while (nm != null && (float)nm.ServerTime.Time < scheduleStartTime)
                yield return null;
        }
        else
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        }

        int cycle = 0;
        while (isRunning)
        {
            yield return StartCoroutine(FireWithCharge());

            if (activateInterval <= 0f)
            {
                isRunning = false;
                yield break;
            }

            cycle++;
            float targetTime = scheduleStartTime + cycle * activateInterval;
            float now         = nm != null ? (float)nm.ServerTime.Time : Time.time;
            yield return new WaitForSeconds(Mathf.Max(0f, targetTime - now));
        }
    }

    protected override void OnTrapTrigger()
    {
        if (lanes == null || lanes.Length == 0) return;

        // Host/Client가 동일한 레인을 뽑도록 공유 세션 시드 + 발동 횟수로 로컬 RNG를 동기화.
        // StagePressurePadSetup.ApplySeedAndColors()와 동일한 "Seed ^ salt" 관례를 따름.
        const int seedSalt = 0x5B1DE000;
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (_fireCount * 0x2545F491);
        UnityEngine.Random.InitState(mixedSeed);
        _fireCount++;

        int count = Mathf.Clamp(activeLaneCount, 1, lanes.Length);
        int[] selected = PickRandomIndices(count);

        for (int i = 0; i < selected.Length; i++)
            if (lanes[selected[i]] != null) lanes[selected[i]].Trigger();

        _lastSelected = selected;
    }

    /// <summary>count개의 레인 인덱스를 중복 없이 랜덤 선택.</summary>
    int[] PickRandomIndices(int count)
    {
        List<int> pool = new List<int>(lanes.Length);

        for (int i = 0; i < lanes.Length; i++)
        {
            if (excludeLastLanes && _lastSelected != null && count < lanes.Length)
            {
                bool wasLast = false;
                for (int j = 0; j < _lastSelected.Length; j++)
                    if (_lastSelected[j] == i) { wasLast = true; break; }
                if (wasLast) continue;
            }
            pool.Add(i);
        }

        // 제외 후 풀이 부족하면 전체 풀로 폴백
        if (pool.Count < count)
        {
            pool.Clear();
            for (int i = 0; i < lanes.Length; i++) pool.Add(i);
        }

        // Fisher-Yates 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        int[] result = new int[count];
        for (int i = 0; i < count; i++) result[i] = pool[i];
        return result;
    }

    protected override void OnDeactivated()
    {
        _fireCount = 0;
        if (lanes == null) return;
        for (int i = 0; i < lanes.Length; i++)
            if (lanes[i] != null) lanes[i].ForceDeactivate();
    }

    // SetActive(false) 경로: OnDisable()만 불리고 OnDeactivated()는 안 불림 (DropTrap.OnDisable과 동일 이유)
    protected override void OnDisable()
    {
        base.OnDisable();
        _fireCount = 0;
    }
}
