using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// M.Boss 마지막 페이즈(P4) 전용 — 입 닫힘이 무조건 발동하고, 암전 중 이빨이 바닥 타일을 부수는
/// 보스 전용 팀 응원 되돌림 + 페이즈 완료 판정. CoopStageAudit.M.md §7 P4(2026-09-08 확정).
///
/// 기존 <see cref="MouthController"/>.teamCheerHazard 로는 이 인과관계를 표현할 수 없다 —
/// 응원이 Close를 막는 게 아니라, Close→Open이 끝난 뒤에만 사후 복구 창이 열린다(지금까지의
/// 닫힘 M1·M3·P2와 정반대). 그래서 별도 클래스로 분리했다. 이 컴포넌트가 이 페이즈의 입
/// 애니메이터(Close/Open)를 직접 구동한다 — 같은 아레나의 MouthController는 붙이지 않거나
/// teamCheerHazard=false인 순수 연출용으로만 둘 것(둘 다 CheerService에 RegisterRevert하면
/// "씬당 하나" 계약 위반 경고가 뜬다).
///
/// [머신 (총 6회 반복)]
/// 1. Warning  — 이번 회차에 부술 타일에 마커 표시. 응원 없음, 예고만.
/// 2. Closing  — 무조건 발동(응원으로 못 막음). 암전.
/// 3. Breaking — 암전 중 이빨이 내려와 경고된 타일을 부순다(1타일=1이빨, 연출).
/// 4. Opening  — 암전 걷힘.
/// 5. CheerWindow — Open이 끝난 시점부터 열리는 사후 복구 창. 성공하면 바닥 전체 원상복구,
///    타임아웃(실패)이면 깨진 채로 다음 회차로.
/// 6회 완료(성공/실패 무관, 팀이 살아있으면) → OnChallengeComplete
///    (→ BossFightObjective.NotifyPhaseCleared() 연결, 기존 챌린지들과 동일 연결 방식).
///
/// [파괴 수 누적] 회차 N의 목표 파괴 수 = tilesPerCycleStep × N(§7 확정값 4). 직전 회차가
/// 복구됐으면 이번에 그 개수를 전부 새로 뽑고, 복구 안 됐으면 이미 깨진 타일 수를 뺀 나머지만
/// 새로 뽑아 목표를 채운다. 남은 칸이 목표보다 적으면 남은 칸 전부를 깨는 걸로 캡.
///
/// [동기화] ITeamCheerRevert — CheerService의 기존 되돌림 채널(생성 세대 + 재개 ServerTime)을
/// 그대로 쓴다. 새 RPC 없음. 회차 진행 자체는 랜덤 스케줄이 아니라 고정 길이 구간의 연속이라,
/// 첫 진입만 PhaseStartServerTime 앵커에 걸면 이후 각 구간(Warning/Closing/Breaking/Opening)은
/// 전 머신이 같은 지속시간을 로컬로 흘려도 어긋나지 않는다. 팀 응원 성공(=되돌림)만 Host가
/// 정한 세대/재개시각을 전 머신에 실어 보내 위상을 맞춘다(Mouth/Tongue와 동일 원칙).
/// 타일 추첨은 NetworkSessionData.Seed + 회차 번호로 결정 — 클라이언트마다 로컬 Random 없음
/// (TongueController.PickSeededRegion과 동일 패턴).
/// </summary>
public class MouthBossJawSmash : MonoBehaviour, ITeamCheerRevert
{
    enum HazardPhase
    {
        Idle,
        Warning,
        Closing,
        Breaking,
        Opening,
        CheerWindow,
    }

    [Header("타일 (인스펙터 배열 순서 = 픽 인덱스. §7 기본 25칸)")]
    [Tooltip("바닥 타일 전체. 부서지면 SetActive(false), 복구되면 true.")]
    [SerializeField] GameObject[] floorTiles = new GameObject[0];

    [Tooltip("floorTiles와 같은 인덱스로 매칭되는 이빨 프롭. 그 타일이 부서진 동안만 활성화.\n" +
             "비워두면 이빨 연출 생략(타일만 꺼짐).")]
    [SerializeField] GameObject[] toothProps = new GameObject[0];

    [Tooltip("floorTiles와 같은 인덱스로 매칭되는 경고 마커. Warning 중 이번 회차에 부술 타일만 켜짐.\n" +
             "비워두면 경고 연출 생략.")]
    [SerializeField] GameObject[] warnMarkers = new GameObject[0];

    [Header("입 애니메이터 (이 페이즈 전용 — MouthController와 별개, 응원 없이 무조건 발동)")]
    [Tooltip("비워두면 자식에서 자동 탐색")]
    [SerializeField] Animator mouthAnimator = null;
    [SerializeField] string openTrigger = "doOpen";
    [SerializeField] string closeTrigger = "doClose";
    [SerializeField] string idleTrigger = "doIdle";

    [Header("클립 길이 (초) — Animator 클립 Length와 맞출 것")]
    [SerializeField] float closeClipLength = 0f;
    [SerializeField] float openClipLength = 0f;

    [Header("타이밍 (초 — 나중에 튜닝)")]
    [Tooltip("Close 전 경고 시간. 응원 없이 예고만 — 놓쳐도 Close는 그대로 진행됨.")]
    [SerializeField] float warnDuration = 2f;

    [Tooltip("암전(Closing 종료) 후 이빨이 타일을 부수는 연출 시간. 지난 뒤 Opening 시작.")]
    [SerializeField] float toothBreakDuration = 1f;

    [Tooltip("Open이 끝난 뒤 응원이 열려 있는 시간. 이 안에 팀 전원 외침 성공하면 즉시 복구,\n" +
             "타임아웃이면 깨진 채로 다음 회차.")]
    [SerializeField] float cheerWindowSeconds = 6f;

    [Tooltip("한 회차가 끝나고 다음 회차 Warning이 시작되기 전 여유(초).")]
    [SerializeField] float interCycleGap = 1f;

    [Header("암전 연동 (선택)")]
    [SerializeField] ScreenFader screenFader = null;

    [Header("회차 (§7 확정값: 6회, 회차당 4개씩 누적)")]
    [Tooltip("총 반복 회차 수.")]
    [SerializeField] int totalCycles = 6;

    [Tooltip("회차 N의 목표 파괴 수 = 이 값 × N.")]
    [SerializeField] int tilesPerCycleStep = 4;

    [Header("네트워크 시드")]
    [Tooltip("Mouth 0x4D4F5554 / Saliva 0x53504954 / Tongue 0x544F4E47 와 겹치지 않게.")]
    [SerializeField] int seedSalt = 0x4A415753;

    [Header("이벤트")]
    [Tooltip("6회차 완료 시 호출(성공/실패 무관) → BossFightObjective.NotifyPhaseCleared() 연결")]
    public UnityEvent OnChallengeComplete;

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    HazardPhase _phase = HazardPhase.Idle;
    bool _available;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;
    int _cycleIndex; // 1부터 시작하는 회차 번호(N). RunCycles의 for 변수를 그대로 공유.
    int _syncGeneration;

    readonly HashSet<int> _brokenIndices = new();

    const float AnchorWaitTimeout = 3f;
    const int RevertAxis = 1;
    const int TileAxis = 2;

    public bool IsAvailable => _available;

    void Awake()
    {
        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        ResetHazardFlags();
        RestoreAllTiles();
        TriggerIdle();
        _bindRoutine = StartCoroutine(BindAndStartHazard());
    }

    void OnDisable()
    {
        if (CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);
        StopAllCoroutines();
        _cycleCoroutine = null;
        _bindRoutine = null;
        ResetHazardFlags();
        RestoreAllTiles();
        // 페이드는 ScreenFader 자기 코루틴이라 위 StopAllCoroutines로 안 멈춘다(MouthController와 동일 이유).
        if (screenFader != null)
            screenFader.FadeIn(0f);
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        StartCycle();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>회차 진행 시작. OnEnable이 자동으로 호출하므로 보통 직접 부를 필요 없음.</summary>
    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleIndex = 0;
        _brokenIndices.Clear();
        _cycleCoroutine = StartCoroutine(RunCycles());
    }

    /// <summary>회차 진행 중지. 현재 진행 중인 회차는 즉시 중단되고 바닥이 복구된다.</summary>
    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        RestoreAllTiles();
        TriggerIdle();
        if (screenFader != null)
            screenFader.FadeIn(0f);
    }

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + Mathf.Max(0f, interCycleGap);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return; // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

        switch (_phase)
        {
            case HazardPhase.CheerWindow:
                _recoverQueued = true;
                EndWindow();
                RestoreAllTiles();
                break;
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 회차를 시작하지 않았다(씬 로드 시각 차이 등) — 열지 않고
                // 건너뛰어 Host가 준 다음 예약(_resyncDeadline)에 위상을 맞춘다.
                _skipNextWindow = true;
                break;
            // Warning/Closing/Breaking/Opening: §7 "무조건 발동" 구간 — 응원으로 못 막고, 이
            // 창은 애초에 열려 있지 않았으므로 여기 도착한 표는 조용히 무시한다.
        }
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator RunCycles()
    {
        yield return ResolveFirstAnchor();

        for (_cycleIndex = 1; _cycleIndex <= totalCycles; _cycleIndex++)
        {
            if (_resyncDeadline > 0d)
                yield return WaitForResyncDeadline();
            else if (_cycleIndex > 1)
                yield return new WaitForSeconds(Mathf.Max(0f, interCycleGap));

            if (_skipNextWindow)
            {
                _skipNextWindow = false;
                RestoreAllTiles();
                TriggerIdle();
                continue;
            }

            yield return RunSingleCycle(_cycleIndex);
        }

        _phase = HazardPhase.Idle;
        // BossFightObjective.NotifyPhaseCleared()가 자기 안에서도 Host 레인 가드를 하지만,
        // 기존 챌린지(PhaseSurviveChallenge 등)와 동일하게 호출부에서도 한 번 더 막는다.
        if (!IsClientOnly())
            OnChallengeComplete?.Invoke();
    }

    IEnumerator RunSingleCycle(int cycleIndex)
    {
        List<int> targets = PickTargets(cycleIndex);

        // 1. Warning — 응원 없음, 예고만.
        _phase = HazardPhase.Warning;
        ShowWarnMarkers(targets, true);
        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);
        ShowWarnMarkers(targets, false);

        // 2. Closing — 무조건.
        _phase = HazardPhase.Closing;
        TriggerSafe(closeTrigger, openTrigger, idleTrigger);
        screenFader?.FadeOut(closeClipLength > 0f ? closeClipLength : 0f);
        if (closeClipLength > 0f)
            yield return new WaitForSeconds(closeClipLength);

        // 3. Breaking — 암전 중 이빨이 타일 파괴.
        _phase = HazardPhase.Breaking;
        BreakTiles(targets);
        if (toothBreakDuration > 0f)
            yield return new WaitForSeconds(toothBreakDuration);

        // 4. Opening.
        _phase = HazardPhase.Opening;
        TriggerSafe(openTrigger, closeTrigger, idleTrigger);
        screenFader?.FadeIn(openClipLength > 0f ? openClipLength : 0f);
        if (openClipLength > 0f)
            yield return new WaitForSeconds(openClipLength);
        TriggerIdle();

        // 5. CheerWindow — 이 시점부터만 응원이 유효(§7 인과관계 반전).
        _phase = HazardPhase.CheerWindow;
        _recoverQueued = false;
        _available = true;
        CheerService.Instance?.NotifyHazardWindow(true);

        float elapsed = 0f;
        float window = Mathf.Max(0f, cheerWindowSeconds);
        while (elapsed < window && !_recoverQueued)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!_recoverQueued)
            EndWindow(); // 타임아웃 — 깨진 채로 다음 회차. (성공 시엔 Revert()가 이미 EndWindow 처리)
        _recoverQueued = false;

        _phase = HazardPhase.Idle;
    }

    /// <summary>
    /// 첫 회차를 Host/Client 공통 절대 시각에 건다 — Mouth/Tongue와 동일한 앵커
    /// (PhaseStartServerTime). 앵커가 없는 씬(테스트 등)에서는 로컬 시각 그대로 진행.
    /// </summary>
    IEnumerator ResolveFirstAnchor()
    {
        // PhaseManager.EnterPhase()는 objectsToEnable.SetActive(true) 다음에야 MarkAndSyncPhase()를
        // 찍는다 — 한 프레임 양보해 같은 EnterPhase의 최신 앵커를 읽는다(Mouth/Tongue와 동일 이유).
        yield return null;

        double anchor = -1d;
        float waited = 0f;
        while (waited < AnchorWaitTimeout)
        {
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.PhaseStartServerTime > 0d)
            {
                anchor = sns.PhaseStartServerTime;
                break;
            }
            waited += Time.deltaTime;
            yield return null;
        }

        if (anchor > 0d)
            _resyncDeadline = anchor + Mathf.Max(0f, interCycleGap);
    }

    /// <summary>
    /// 예약된 재개 시각까지 대기. 대기 중에 Revert가 예약을 갱신할 수 있으므로 매 프레임 필드를
    /// 다시 읽는다(Mouth/Tongue와 동일 원칙).
    /// </summary>
    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    // ── 타일 픽 / 파괴 / 복구 ──────────────────────────────────────

    List<int> PickTargets(int cycleIndex)
    {
        int target = tilesPerCycleStep * cycleIndex;
        int needed = Mathf.Max(0, target - _brokenIndices.Count);

        var candidates = new List<int>();
        for (int i = 0; i < floorTiles.Length; i++)
            if (!_brokenIndices.Contains(i)) candidates.Add(i);

        needed = Mathf.Min(needed, candidates.Count);
        if (needed <= 0) return new List<int>();

        ShuffleSeeded(candidates, cycleIndex);
        return candidates.GetRange(0, needed);
    }

    void ShuffleSeeded(List<int> list, int cycleIndex)
    {
        // InitState는 전역 RNG를 갈아엎는다 — 뽑고 나서 되돌려야 같은 씬의 다른 시스템이
        // 이 시드 스트림을 물려받지 않는다(Mouth/Tongue와 동일 원칙).
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(MixSeed(cycleIndex, TileAxis));
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        UnityEngine.Random.state = prevState;
    }

    int MixSeed(int index, int axis)
        => NetworkSessionData.Seed ^ seedSalt ^ (index * 0x2545F491) ^ (axis * 0x27220A95);

    void BreakTiles(List<int> targets)
    {
        foreach (int i in targets)
        {
            _brokenIndices.Add(i);
            SetTileActive(i, false);
            SetToothActive(i, true);
        }
    }

    void RestoreAllTiles()
    {
        for (int i = 0; i < floorTiles.Length; i++)
        {
            SetTileActive(i, true);
            SetToothActive(i, false);
        }
        _brokenIndices.Clear();
    }

    void SetTileActive(int index, bool active)
    {
        if (index < 0 || index >= floorTiles.Length) return;
        if (floorTiles[index] != null) floorTiles[index].SetActive(active);
    }

    void SetToothActive(int index, bool active)
    {
        if (index < 0 || index >= toothProps.Length) return;
        if (toothProps[index] != null) toothProps[index].SetActive(active);
    }

    void ShowWarnMarkers(List<int> targets, bool show)
    {
        if (show)
        {
            foreach (int i in targets)
                if (i >= 0 && i < warnMarkers.Length && warnMarkers[i] != null)
                    warnMarkers[i].SetActive(true);
            return;
        }

        for (int i = 0; i < warnMarkers.Length; i++)
            if (warnMarkers[i] != null) warnMarkers[i].SetActive(false);
    }

    // ── 엔딩 연출 ─────────────────────────────────────────────────

    /// <summary>
    /// 클리어 후 대화 컷신에서 호출 — 남은 마지막 칸까지 전부 부순다
    /// (§7 엔딩: "마지막 남은 1칸까지 이빨이 부수는 연출 → T로 전환").
    /// Timeline/대화 종료 UnityEvent 등에서 직접 연결.
    /// </summary>
    public void ForceBreakAllTilesForEnding()
    {
        for (int i = 0; i < floorTiles.Length; i++)
        {
            _brokenIndices.Add(i);
            SetTileActive(i, false);
            SetToothActive(i, true);
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _available = false;
        _recoverQueued = false;
        _skipNextWindow = false;
        _resyncDeadline = -1d;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void TriggerIdle() => TriggerSafe(idleTrigger, openTrigger, closeTrigger);

    void TriggerSafe(string trigger, string r1 = null, string r2 = null)
    {
        if (mouthAnimator == null || string.IsNullOrEmpty(trigger)) return;
        if (!string.IsNullOrEmpty(r1)) mouthAnimator.ResetTrigger(r1);
        if (!string.IsNullOrEmpty(r2)) mouthAnimator.ResetTrigger(r2);
        mouthAnimator.SetTrigger(trigger);
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 회차 진행 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 회차 진행 중지 + 복구")]
    void TestStopCycle() => StopCycle();

    [ContextMenu("테스트: 전부 복구")]
    void TestRestoreAll() => RestoreAllTiles();

    [ContextMenu("테스트: 6회 완료 강제 호출")]
    void TestForceComplete() => OnChallengeComplete?.Invoke();
}
