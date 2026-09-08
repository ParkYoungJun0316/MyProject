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
/// 꺼진 칸 낙사 = 방 리셋(M4 혀와 동일 규약) — 낙사 판정은 Player.enableFallDeath /
/// fallDeathY(Owner 신고 → Host 적용)가 담당하므로 여기엔 사망 코드가 없다.
///
/// [파괴 수 누적] 회차 N의 목표 파괴 수 = tilesPerCycleStep × N(§7 확정값 4). 직전 회차가
/// 복구됐으면 이번에 그 개수를 전부 새로 뽑고, 복구 안 됐으면 이미 깨진 타일 수를 뺀 나머지만
/// 새로 뽑아 목표를 채운다. 남은 칸이 목표보다 적으면 남은 칸 전부를 깨는 걸로 캡.
///
/// [동기화] 새 RPC·NV 없음. ITeamCheerRevert로 CheerService의 기존 되돌림 채널만 쓴다.
/// · 시각: 회차·구간 경계를 전부 <b>절대 ServerTime</b>으로 계산한다(앵커 = PhaseStartServerTime).
///   WaitForSeconds를 이어 붙이면 프레임 양자화가 구간마다 쌓여(회차당 5~6회 × 6회차) 저프레임
///   머신과 Host 사이가 수백 ms 벌어지고, 그 드리프트가 응원 창의 종료 시점과 타일 추첨
///   (PickTargets가 "지금 깨진 수"에 의존)을 머신마다 갈라놓는다. 절대 시각이면 남는 오차는
///   프레임 한 겹뿐이고 누적되지 않는다.
/// · 되돌림: 이 컴포넌트의 Revert는 "막기"가 아니라 "사후 복구" 하나뿐이라, 창 안/밖 어디서
///   받아도 하는 일이 같다(Revert() 주석 참고).
/// · 추첨: NetworkSessionData.Seed + 회차 번호로 결정 — 클라이언트마다 로컬 Random 없음
///   (TongueController.PickSeededRegion과 동일 패턴).
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
    [Tooltip("비워두면 이 GO 또는 자식(비활성 포함)에서 자동 탐색")]
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

    [Tooltip("한 회차가 끝나고 다음 회차 Warning이 시작되기 전 여유(초).\n" +
             "회차 리듬은 고정 — 응원을 일찍 성공해도 다음 회차가 앞당겨지지 않는다.")]
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

    // 엔딩 연출(ForceBreakAllTilesForEnding)로 일부러 전부 부순 상태. 이 뒤로는 정리 경로
    // (StopCycle / OnDisable)에서 바닥을 되살리지 않는다 — T로 넘어가는 컷신 중에 부서진 바닥이
    // 다시 켜져 보이면 안 된다.
    bool _endingBroken;

    // 전 머신 공통 스케줄 기준 절대 시각. 회차·구간 경계를 전부 여기서 계산한다.
    double _scheduleAnchor = -1d;

    int _cycleIndex; // 1부터 시작하는 회차 번호(N). RunCycles의 for 변수를 그대로 공유.
    int _syncGeneration;

    readonly HashSet<int> _brokenIndices = new();

    const float AnchorWaitTimeout = 3f;
    const int TileAxis = 2;

    public bool IsAvailable => _available;

    /// <summary>한 회차의 총 길이(초). 회차 시작 시각을 앵커에서 곧바로 역산하는 근거.</summary>
    float CycleDuration =>
        Mathf.Max(0f, warnDuration)
        + Mathf.Max(0f, closeClipLength)
        + Mathf.Max(0f, toothBreakDuration)
        + Mathf.Max(0f, openClipLength)
        + Mathf.Max(0f, cheerWindowSeconds)
        + Mathf.Max(0f, interCycleGap);

    /// <summary>회차 N(1부터)의 Warning이 시작되는 절대 ServerTime.</summary>
    double CycleStartTime(int cycleIndex) =>
        _scheduleAnchor + Mathf.Max(0f, interCycleGap) + (cycleIndex - 1) * (double)CycleDuration;

    void Awake()
    {
        if (mouthAnimator == null)
            mouthAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        ValidateWiring();
    }

    void OnEnable()
    {
        _endingBroken = false;
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
        if (!_endingBroken)
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
        _endingBroken = false;
        // 집합만 비우면 실제 SetActive 상태와 갈라진다 — 복구까지 같이 해서 둘을 한 번에 맞춘다.
        RestoreAllTiles();
        _cycleCoroutine = StartCoroutine(RunCycles());
    }

    /// <summary>
    /// 회차 진행 중지 + 바닥·암전 복구. SceneFlowManager.FreezeAllHazardsNow()가 씬 종료
    /// 클리어에서 호출한다(Mouth/Tongue/Saliva와 동일 계약). 엔딩 연출로 이미 부순 뒤라면
    /// 바닥은 그대로 둔다.
    /// </summary>
    public void StopCycle() => StopCycleInternal(restoreTiles: !_endingBroken);

    void StopCycleInternal(bool restoreTiles)
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        if (restoreTiles)
            RestoreAllTiles();
        TriggerIdle();
        if (screenFader != null)
            screenFader.FadeIn(0f);
    }

    /// <summary>
    /// Host 전용 — 세대 번호 발급. 회차 시각이 앵커에서 완전히 결정되므로 전 머신이 다음 회차
    /// 시작 시각을 독립적으로 같은 값으로 계산한다. resumeAt은 계약상 그 값을 그대로 채워
    /// 보내되 Revert()는 스케줄에 쓰지 않는다 — 랜덤 간격이 있는 Mouth/Tongue과 달리 여기엔
    /// 재동기화할 위상이 없다.
    /// </summary>
    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = _scheduleAnchor > 0d ? CycleStartTime(_cycleIndex + 1) : GetServerTime();
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return; // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;

        // 이 컴포넌트의 되돌림은 "막기"가 아니라 "사후 복구"다(§7 인과관계 반전). 그래서 Mouth/Tongue의
        // "창 밖이면 다음 창을 건너뛴다(_skipNextWindow)" 관용구를 여기에 쓰면 안 된다 — 저쪽은 응원이
        // 공격을 막는 구조라 Host도 그 창을 같이 건너뛰어 대칭이 맞지만, 여기서 Host는 복구만 하고 다음
        // 회차를 정상 진행한다. 건너뛰는 머신만 바닥이 멀쩡한 채로 남아 낙사 판정이 머신마다 갈린다.
        // 창 안이든(정상) 밖이든(RTT로 늦게 도착) 하는 일은 "전체 복구" 하나뿐이고, 6회차는 §7대로
        // 성공/실패와 무관하게 전부 돈다.
        if (_phase == HazardPhase.CheerWindow)
        {
            _recoverQueued = true; // 창 루프를 즉시 끝냄
            EndWindow();
        }

        RestoreAllTiles();
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator RunCycles()
    {
        yield return ResolveScheduleAnchor();

        for (_cycleIndex = 1; _cycleIndex <= totalCycles; _cycleIndex++)
        {
            yield return WaitUntilServerTime(CycleStartTime(_cycleIndex));
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
        // 구간 경계 전부 절대 시각 — 누적 드리프트 없음(클래스 주석 [동기화] 참고).
        double cycleStart = CycleStartTime(cycleIndex);
        double warnEnd    = cycleStart + Mathf.Max(0f, warnDuration);
        double closeEnd   = warnEnd    + Mathf.Max(0f, closeClipLength);
        double breakEnd   = closeEnd   + Mathf.Max(0f, toothBreakDuration);
        double openEnd    = breakEnd   + Mathf.Max(0f, openClipLength);
        double windowEnd  = openEnd    + Mathf.Max(0f, cheerWindowSeconds);

        List<int> targets = PickTargets(cycleIndex);

        // 1. Warning — 응원 없음, 예고만.
        _phase = HazardPhase.Warning;
        ShowWarnMarkers(targets, true);
        yield return WaitUntilServerTime(warnEnd);
        ShowWarnMarkers(targets, false);

        // 2. Closing — 무조건.
        _phase = HazardPhase.Closing;
        TriggerSafe(closeTrigger, openTrigger, idleTrigger);
        screenFader?.FadeOut(Mathf.Max(0f, closeClipLength));
        yield return WaitUntilServerTime(closeEnd);

        // 3. Breaking — 암전 중 이빨이 타일 파괴.
        _phase = HazardPhase.Breaking;
        BreakTiles(targets);
        yield return WaitUntilServerTime(breakEnd);

        // 4. Opening.
        _phase = HazardPhase.Opening;
        TriggerSafe(openTrigger, closeTrigger, idleTrigger);
        screenFader?.FadeIn(Mathf.Max(0f, openClipLength));
        yield return WaitUntilServerTime(openEnd);
        TriggerIdle();

        // 5. CheerWindow — 이 시점부터만 응원이 유효(§7 인과관계 반전).
        _phase = HazardPhase.CheerWindow;
        _recoverQueued = false;
        _available = true;
        CheerService.Instance?.NotifyHazardWindow(true);

        // 창의 끝도 절대 시각 — 전 머신이 같은 순간에 닫아야 "Host는 성공 처리, 나는 이미 실패"가
        // 안 생긴다. 성공(Revert)하면 그 즉시 복구되고 창만 먼저 닫힌다(다음 회차는 고정 리듬대로).
        while (GetServerTime() < windowEnd && !_recoverQueued)
            yield return null;

        if (!_recoverQueued)
            EndWindow(); // 타임아웃 — 깨진 채로 다음 회차. (성공 시엔 Revert()가 이미 EndWindow 처리)
        _recoverQueued = false;

        _phase = HazardPhase.Idle;
    }

    /// <summary>
    /// 스케줄 기준 절대 시각을 잡는다 — Mouth/Tongue와 같은 앵커(PhaseStartServerTime).
    /// 앵커가 없는 씬(단독 테스트 등)은 로컬 시각으로 폴백한다(그 경우 머신이 하나뿐이라
    /// 어긋날 대상이 없다).
    /// </summary>
    IEnumerator ResolveScheduleAnchor()
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

        _scheduleAnchor = anchor > 0d ? anchor : GetServerTime();
    }

    /// <summary>절대 ServerTime까지 대기. 이미 지난 시각이면 즉시 통과.</summary>
    IEnumerator WaitUntilServerTime(double serverTime)
    {
        while (GetServerTime() < serverTime)
            yield return null;
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
    /// 회차 루프·응원 창이 아직 살아있어도 같이 정리한다(오배선 방어 — 컷신 중 입이 또 닫히면 안 된다).
    /// 이 호출 이후에는 StopCycle/OnDisable에서도 바닥을 복구하지 않는다.
    /// Timeline/대화 종료 UnityEvent 등에서 직접 연결.
    /// </summary>
    public void ForceBreakAllTilesForEnding()
    {
        _endingBroken = true;
        StopCycleInternal(restoreTiles: false);

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
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    /// <summary>
    /// 조용한 오설정을 콘솔에 드러낸다(SalivaHazard의 배선 경고와 동일 관례) — 타일이 안 부서지거나
    /// 이빨/예고가 일부만 뜨는 증상은 인스펙터 배선에서 오는 경우가 대부분이다.
    /// </summary>
    void ValidateWiring()
    {
        if (floorTiles == null || floorTiles.Length == 0)
        {
            Debug.LogWarning($"[MouthBossJawSmash] floorTiles가 비어 있습니다 — 부술 타일이 없어 회차가 헛돕니다. ({name})", this);
            return;
        }

        if (toothProps != null && toothProps.Length != 0 && toothProps.Length != floorTiles.Length)
            Debug.LogWarning($"[MouthBossJawSmash] toothProps({toothProps.Length})와 floorTiles({floorTiles.Length}) 길이가 다릅니다 — " +
                             $"인덱스가 큰 타일은 이빨 연출이 빠집니다. ({name})", this);

        if (warnMarkers != null && warnMarkers.Length != 0 && warnMarkers.Length != floorTiles.Length)
            Debug.LogWarning($"[MouthBossJawSmash] warnMarkers({warnMarkers.Length})와 floorTiles({floorTiles.Length}) 길이가 다릅니다 — " +
                             $"인덱스가 큰 타일은 예고가 빠집니다. ({name})", this);

        // §7 불변식: 총 타일 = (회차당 증가분 × 총 회차) + 1 → "마지막 1칸"이 남는 결말이 보장된다.
        int expected = tilesPerCycleStep * totalCycles + 1;
        if (floorTiles.Length != expected)
            Debug.LogWarning($"[MouthBossJawSmash] floorTiles가 {floorTiles.Length}개인데 §7 불변식({tilesPerCycleStep}×{totalCycles}+1)은 " +
                             $"{expected}개를 요구합니다 — \"마지막 1칸\" 엔딩이 성립하지 않습니다. ({name})", this);
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

    [ContextMenu("테스트: 엔딩 연출(남은 칸 전부 파괴)")]
    void TestEndingBreak() => ForceBreakAllTilesForEnding();

    [ContextMenu("테스트: 6회 완료 강제 호출")]
    void TestForceComplete() => OnChallengeComplete?.Invoke();
}
