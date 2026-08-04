using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 기억 경로 스테이지 인트로 오케스트레이터.
///
/// [담당 흐름]
///  StageStartGate 카운트다운 완료
///   → 탑다운 카메라 전환 (pivot 고정)
///   → cameraLeadInTime 대기
///   → 모든 경로 StartPreview()
///   → 전체 경로 Challenge 진입 완료
///   → 베리어 Open + 카메라 복귀 + StartStage()
///
/// [씬 설정 방법]
///  1. 빈 GameObject에 이 컴포넌트 추가 (구역마다 1개)
///  2. StageStartGate.OnCountdownComplete → BeginIntro() 연결
///     ⚠ StageStartGate의 stageManager 필드는 비워둘 것 (오케스트레이터가 대신 호출)
///  3. 각 경로의 startOnAwake = false 확인
///  4. Inspector 필드 모두 연결
/// </summary>
public class MemoryPathIntroController : MonoBehaviour
{
    [Header("카메라")]
    [Tooltip("비워두면 LocalPlayerCamera.Instance를 자동 사용 (C안 표준).")]
    [FormerlySerializedAs("topDownCamera")]
    [SerializeField] ThirdPersonCamera thirdPersonCamera;

    /// <summary>Inspector 값 우선, 없으면 로컬 카메라 싱글턴 사용.</summary>
    ThirdPersonCamera ActiveCamera => thirdPersonCamera != null ? thirdPersonCamera : LocalPlayerCamera.Instance?.ThirdPersonCam;

    [Tooltip("프리뷰 동안 카메라가 고정될 지점. 경로 발판들의 중앙에 배치.")]
    [SerializeField] Transform previewPivot;

    [Header("타이밍")]
    [Tooltip("카메라 블렌드 완료 후, StartPreview() 전 추가 대기(초).\n" +
             "플레이어가 화면을 파악할 시간. 1~2초 권장.")]
    [SerializeField] float cameraLeadInTime = 1.5f;

    [Header("경로")]
    [Tooltip("이 구역의 MemoryPath 목록. startOnAwake = false 필수.")]
    [SerializeField] MemoryPath[] memoryPaths;

    [Tooltip("이 구역의 ColoredMemoryPath 목록. startOnAwake = false 필수.")]
    [SerializeField] ColoredMemoryPath[] coloredMemoryPaths;

    [Tooltip("이 구역의 PioneerPathManager 목록. startOnAwake = false 필수.")]
    [SerializeField] PioneerPathManager[] pioneerPathManagers;

    [Header("베리어")]
    [Tooltip("Challenge 시작 시 Open할 DoorController. 여러 개 등록 가능.")]
    [SerializeField] DoorController[] barrierDoors;

    [Header("스테이지")]
    [Tooltip("Challenge 시작 시 StartStage()를 호출할 StageManager.\n" +
             "StageStartGate의 stageManager 필드는 비워둘 것.")]
    [SerializeField] StageManager stageManager;

    int _totalPaths;
    int _challengeReadyCount;
    bool _isRunning;

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>
    /// StageStartGate.OnCountdownComplete에 연결.
    /// 실행 중에는 중복 호출을 무시함.
    /// </summary>
    public void BeginIntro()
    {
        if (_isRunning) return;
        _isRunning = true;
        StartCoroutine(IntroRoutine());
    }

    /// <summary>
    /// 스테이지 실패·리셋 시 초기화.
    /// StageResetOnPlayerDeath 등에서 호출하거나 Inspector 이벤트에 연결.
    /// </summary>
    public void ResetIntro()
    {
        StopAllCoroutines();
        UnsubscribeAllPaths();
        _challengeReadyCount = 0;
        _isRunning = false;
    }

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator IntroRoutine()
    {
        // 1. 모든 경로의 OnChallengeStart 구독 + 총 경로 수 집계
        _totalPaths = 0;
        _challengeReadyCount = 0;

        foreach (MemoryPath p in memoryPaths)
        {
            if (p == null) continue;
            _totalPaths++;
            p.OnChallengeStart.AddListener(OnPathChallengeStarted);
        }
        foreach (ColoredMemoryPath cp in coloredMemoryPaths)
        {
            if (cp == null) continue;
            _totalPaths++;
            cp.OnChallengeStart.AddListener(OnPathChallengeStarted);
        }
        foreach (PioneerPathManager pp in pioneerPathManagers)
        {
            if (pp == null) continue;
            _totalPaths++;
            pp.OnChallengeStart.AddListener(OnPathChallengeStarted);
        }

        // 2. 카메라 탑다운으로 전환 (pivot 고정)
        var cam = ActiveCamera;
        if (cam != null && previewPivot != null)
            cam.EnterPreviewView(previewPivot);

        // 3. 카메라 블렌드 + 리드인 대기
        float blendTime = cam != null ? cam.PreviewBlendTime : 0f;
        yield return new WaitForSeconds(blendTime + cameraLeadInTime);

        // 4. 모든 경로 미리보기 동시 시작
        foreach (MemoryPath p in memoryPaths)
            if (p != null) p.StartPreview();
        foreach (ColoredMemoryPath cp in coloredMemoryPaths)
            if (cp != null) cp.StartPreview();
        foreach (PioneerPathManager pp in pioneerPathManagers)
            if (pp != null) pp.StartPreview();

        // 5. 전체 경로가 Challenge에 진입할 때까지 대기
        //    경로가 없으면 즉시 통과
        if (_totalPaths > 0)
            yield return new WaitUntil(() => _challengeReadyCount >= _totalPaths);

        UnsubscribeAllPaths();

        // 6. 베리어 Open
        foreach (DoorController door in barrierDoors)
            door?.Open();

        // 7. 카메라 게임플레이 시점 복귀
        ActiveCamera?.ExitPreviewView();

        // 8. 스테이지 시작 (함정·목표 활성화)
        stageManager?.StartStage();

        _isRunning = false;
    }

    void OnPathChallengeStarted()
    {
        _challengeReadyCount++;
    }

    void UnsubscribeAllPaths()
    {
        foreach (MemoryPath p in memoryPaths)
            if (p != null) p.OnChallengeStart.RemoveListener(OnPathChallengeStarted);
        foreach (ColoredMemoryPath cp in coloredMemoryPaths)
            if (cp != null) cp.OnChallengeStart.RemoveListener(OnPathChallengeStarted);
        foreach (PioneerPathManager pp in pioneerPathManagers)
            if (pp != null) pp.OnChallengeStart.RemoveListener(OnPathChallengeStarted);
    }

    // ── 에디터 테스트 ──────────────────────────────────────────────

    [ContextMenu("테스트: 인트로 시작")]
    void Debug_Begin() => BeginIntro();

    [ContextMenu("테스트: 리셋")]
    void Debug_Reset() => ResetIntro();
}
