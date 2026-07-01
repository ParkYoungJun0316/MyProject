using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 벽 순서 이동 시퀀서. (NGO 네트워크 동기화)
///
/// [동작]
///  플레이어가 트리거 존에 진입(또는 외부에서 Activate() 호출)하면
///  wallEntries 목록의 벽들이 순서대로 이동 시작.
///  각 항목의 delayAfterPrevious로 간격 조정.
///
/// [네트워크]
///  Host만 트리거를 처리 → ServerTime 기준 시작 시각을 ClientRpc로 전달
///  → Host/Client 독립 실행, 동일 ServerTime 기준이므로 타이밍 일치.
///  WallMover에 NetworkObject 불필요.
///
/// [사용법]
///  1. 빈 GameObject 생성 → NetworkObject + 이 컴포넌트 추가
///  2. Collider 추가 → Is Trigger = true (트리거존으로 사용 시)
///  3. wallEntries에 WallMover 오브젝트와 딜레이 설정
///  4. activateOnPlayerTrigger = true이면 플레이어 진입 시 자동 실행
///     false이면 Activate()를 PhaseManager UnityEvent 등에 연결
///
/// [예시 구성]
///  Entry 0: wall_Left,  delayAfterPrevious = 0    → 즉시 시작
///  Entry 1: wall_Right, delayAfterPrevious = 1.5  → 0번 시작 1.5초 후 시작
///  Entry 2: wall_Top,   delayAfterPrevious = 2.0  → 1번 시작 2.0초 후 시작
/// </summary>
public class WallMoverSequencer : NetworkBehaviour
{
    [System.Serializable]
    public struct WallEntry
    {
        [Tooltip("이동시킬 WallMover 컴포넌트")]
        public WallMover wall;

        [Tooltip("이전 벽이 이동 시작한 후 이 벽이 시작할 때까지 대기(초). 0 = 이전 벽과 동시")]
        public float delayAfterPrevious;
    }

    [Header("벽 시퀀스")]
    [Tooltip("순서대로 실행할 벽 목록. 위에서부터 순서대로 실행됨")]
    public WallEntry[] wallEntries = new WallEntry[0];

    [Header("트리거 설정")]
    [Tooltip("true: 플레이어가 Collider에 진입하면 자동 실행\n" +
             "false: Activate()를 외부(PhaseManager 등)에서 직접 호출")]
    public bool activateOnPlayerTrigger = true;

    [Tooltip("한 번만 트리거 허용. false이면 플레이어가 재진입 시 재실행")]
    public bool activateOnce = true;

    [Header("이벤트")]
    [Tooltip("시퀀스 시작 시 호출")]
    public UnityEvent OnSequenceStarted;

    [Tooltip("모든 벽 이동이 완료됐을 때 호출")]
    public UnityEvent OnSequenceCompleted;

    bool _isRunning;
    bool _hasActivated;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>시퀀스 시작. 온라인에서는 Host에서만 유효. 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        if (_isRunning) return;
        if (activateOnce && _hasActivated) return;

        var  nm       = NetworkManager.Singleton;
        bool isOnline = nm != null && nm.IsListening;

        // 온라인: Host만 시작 가능 (Client는 ClientRpc로 수신)
        if (isOnline && !nm.IsServer) return;

        _hasActivated = true;

        if (isOnline)
        {
            double startTime = nm.ServerTime.Time;
            StartSequenceClientRpc(startTime);
            StartCoroutine(SequenceRoutine(startTime, useServerTime: true));
        }
        else
        {
            StartCoroutine(SequenceRoutine(0, useServerTime: false));
        }
    }

    /// <summary>모든 벽을 시작 위치로 리셋하고 재사용 가능 상태로 복귀.</summary>
    public void ResetAll()
    {
        StopAllCoroutines();
        _isRunning = false;

        if (wallEntries == null) return;
        for (int i = 0; i < wallEntries.Length; i++)
            if (wallEntries[i].wall != null)
                wallEntries[i].wall.ResetToStart();

        if (!activateOnce) _hasActivated = false;
    }

    // ── 트리거 감지 ──────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnPlayerTrigger) return;

        var  nm       = NetworkManager.Singleton;
        bool isOnline = nm != null && nm.IsListening;

        // 온라인: Host만 처리. Client 트리거 무시.
        if (isOnline && !nm.IsServer) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        Activate();
    }

    // ── ClientRpc ────────────────────────────────────────────────

    /// <summary>Host → 전체 Client: 시퀀스 시작 ServerTime 전달.</summary>
    [ClientRpc]
    void StartSequenceClientRpc(double sequenceStartServerTime)
    {
        if (IsServer) return;  // Host는 Activate()에서 이미 시작
        if (_isRunning) return; // 중복 수신 방지

        _hasActivated = true;
        StartCoroutine(SequenceRoutine(sequenceStartServerTime, useServerTime: true));
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator SequenceRoutine(double startServerTime, bool useServerTime)
    {
        _isRunning = true;
        OnSequenceStarted?.Invoke();

        var nm = NetworkManager.Singleton;
        double cumulativeDelay = 0;

        if (wallEntries != null)
        {
            for (int i = 0; i < wallEntries.Length; i++)
            {
                cumulativeDelay += wallEntries[i].delayAfterPrevious;

                if (useServerTime && nm != null)
                {
                    double target = startServerTime + cumulativeDelay;
                    while (nm.ServerTime.Time < target)
                        yield return null;
                }
                else if (wallEntries[i].delayAfterPrevious > 0f)
                {
                    yield return new WaitForSeconds(wallEntries[i].delayAfterPrevious);
                }

                wallEntries[i].wall?.Activate();
            }
        }

        // 마지막 벽 이동 완료까지 대기 후 OnSequenceCompleted 발동
        if (wallEntries != null && wallEntries.Length > 0)
        {
            WallMover lastWall = wallEntries[wallEntries.Length - 1].wall;
            if (lastWall != null)
            {
                if (useServerTime && nm != null)
                {
                    double completionTarget = startServerTime + cumulativeDelay + lastWall.moveDuration;
                    while (nm.ServerTime.Time < completionTarget)
                        yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(lastWall.moveDuration);
                }
            }
        }

        _isRunning = false;
        OnSequenceCompleted?.Invoke();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 시퀀스 시작")]
    void Debug_Activate()
    {
        _hasActivated = false;
        Activate();
    }

    [ContextMenu("테스트: 전체 리셋")]
    void Debug_Reset() => ResetAll();
}
