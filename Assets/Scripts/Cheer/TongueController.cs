using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 혀 함정 — 팀 응원 되돌림 (ITeamCheerRevert). Mouth/Saliva와 같은 머신. 새 RPC 없음.
/// Animation Event <c>SweepBreak(int index)</c> — Animator와 같은 GO에 이 컴포넌트를 붙일 것.
///
/// 4.1 RiseHold: Warning → Rise(가운데) → Hold → 외침 시 Retract 후 복구.
/// 4.2 AttackSweep: Warning → Attack L 또는 R(스윕 10칸) → Hold 클립 없음.
///     안 외치면 칸은 꺼진 채 다음 사이클이 반대쪽. 외침(Warning 또는 Attack 중 큐)이면 꺼진 칸 전부 복구.
/// M.Boss MixedSweep: 창마다 가운데·L·R 중 하나를 시드로 뽑는다. Hold 없음 —
///     가운데도 Rise → 칸 부숨 → Retract로 끝나는 공격이다. 안 외치면 칸이 꺼진 채 남고, 외치면 전부 복구.
/// Rise/Attack이 시작되면 끊지 않음. MouthBG 혀 쓰지 않음.
///
/// 경고 마커: SpikeLaneWarnMarker를 그대로 재사용(신규 컴포넌트 없음). Warning 진입 시 이번 창의
/// 공격 영역을 PeekNextRegion()으로 미리 계산해 해당 마커만 켠다 — Host/Client 모두 같은 시드/스케줄로
/// Warning에 진입하므로(§SSOT: 기존 랜덤 스케줄이 이미 동일) 마커 재생도 별도 네트워크 동기화 없이
/// 로컬 재생만으로 일치한다. 새 RPC/NetworkVariable 없음.
/// </summary>
public class TongueController : MonoBehaviour, ITeamCheerRevert
{
    // 인스펙터 직렬화 값 유지 — 새 패턴은 뒤에만 추가할 것(0=RiseHold, 1=AttackSweep).
    public enum TonguePattern
    {
        RiseHold,
        AttackSweep,
        MixedSweep,
    }

    enum HazardPhase
    {
        Idle,
        Warning,
        Attacking,
        Holding,
        Recovering,
    }

    enum SweepRegion
    {
        None,
        Center,
        Left,
        Right,
    }

    [Header("패턴")]
    [Tooltip("4.1 SequenceRing = RiseHold. 4.2 ArrowTrap = AttackSweep.\n" +
             "M.Boss = MixedSweep — 창마다 가운데/L/R 중 하나. Hold 없음.")]
    [SerializeField] TonguePattern pattern = TonguePattern.RiseHold;

    [Header("참조")]
    [Tooltip("혀 Animator. 비우면 이 GO 또는 자식에서 탐색. SweepBreak 이벤트는 Animator와 같은 GO 필요.")]
    [SerializeField] Animator tongueAnimator = null;

    [Header("Animator 트리거")]
    [SerializeField] string idleTrigger = "doIdle";
    [SerializeField] string riseTrigger = "doRise";
    [SerializeField] string holdTrigger = "doHold";
    [SerializeField] string retractTrigger = "doRetract";
    [SerializeField] string attackLTrigger = "doAttackL";
    [SerializeField] string attackRTrigger = "doAttackR";

    [Header("클립 길이 (초) — 클립 Length와 맞출 것")]
    [Tooltip("TongueRise. 예) 24fps 160프레임 ≈ 6.67")]
    [SerializeField] float riseClipLength = 0f;

    [Tooltip("TongueAttack_L / _R. 예) 24fps 120프레임 = 5")]
    [SerializeField] float attackClipLength = 0f;

    [Tooltip("TongueRetract. 4.1만. 예) 24fps 40프레임 ≈ 1.67")]
    [SerializeField] float retractClipLength = 0f;

    [Header("1×1 타일 (배열 순서 = 스윕 순서. 가운데 1×5는 넣지 말 것)")]
    [Tooltip("4.1 = 가운데 큰 판 1칸. MixedSweep(보스) = 가운데 3×3 9칸")]
    [SerializeField] GameObject[] centerTiles = new GameObject[0];

    [Tooltip("4.2 왼쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] leftTiles = new GameObject[0];

    [Tooltip("4.2 오른쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] rightTiles = new GameObject[0];

    [Header("파편 연출 (TileDebrisUtil 공용 — 신규 컴포넌트 없음)")]
    [Tooltip("타일 파괴 시 스폰할 파편 프리팹. 비우면 파편 생략(기존처럼 SFX+비활성만).\n" +
             "크기는 타일 월드 크기와 같게 만들 것 — FloorTile 기준 5 × 1 × 5.")]
    [SerializeField] GameObject tileDebrisPrefab = null;

    [Tooltip("파편 자동 소멸 시간(초). 0이면 자동 소멸 안 함.")]
    [SerializeField] float tileDebrisLifetime = 2f;

    [Tooltip("파편에 가할 임펄스 힘 최소값.")]
    [SerializeField] float tileDebrisImpulseMin = 2f;

    [Tooltip("파편에 가할 임펄스 힘 최대값.")]
    [SerializeField] float tileDebrisImpulseMax = 5f;

    [Header("복구 연출 (선택 — 판정은 항상 즉시, 이건 시각 연출만. TileRestorePopGroup 공용)")]
    [Tooltip("복구 시 살짝 부풀었다 가라앉는 연출 시간(초). 0이면 연출 없음(기존과 동일, 즉시 복구만).")]
    [SerializeField] float restorePopDuration = 0f;

    [Tooltip("부풀어 오르는 최대 배율(0.06 = 최대 106%). 0이면 연출 없음과 동일.\n" +
             "콜라이더도 같이 커진다 — 0.06이면 타일 윗면이 약 3cm 올라가고 좌우로 0.15m씩 이웃 칸을 " +
             "침범해, 복구 순간 근처에 선 플레이어가 살짝 들리거나 밀릴 수 있다.")]
    [SerializeField] float restorePopAmplitude = 0.06f;

    [Tooltip("여러 타일이 한꺼번에 복구될 때 조금씩 다르게 보이도록 지속시간에 주는 랜덤 폭(초, ±). 0이면 랜덤 없음.")]
    [SerializeField] float restorePopJitter = 0f;

    [Header("경고 마커 (SpikeLaneWarnMarker 재사용 — 신규 컴포넌트 없음)")]
    [Tooltip("가운데 영역 경고. RiseHold 전부 + MixedSweep 가운데에 사용. 비우면 경고 생략.")]
    [SerializeField] SpikeLaneWarnMarker centerWarnMarker = null;

    [Tooltip("왼쪽 영역 경고. AttackSweep + MixedSweep 왼쪽에 사용.")]
    [SerializeField] SpikeLaneWarnMarker leftWarnMarker = null;

    [Tooltip("오른쪽 영역 경고. AttackSweep + MixedSweep 오른쪽에 사용.")]
    [SerializeField] SpikeLaneWarnMarker rightWarnMarker = null;

    [Header("랜덤 스케줄")]
    [SerializeField] float randomIntervalMin = 5f;
    [SerializeField] float randomIntervalMax = 15f;
    [SerializeField] float initialDelay = 0f;
    [SerializeField] bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("Attack 전 Warning (초). 수치는 나중에 튜닝.")]
    [SerializeField] float warnDuration = 2f;

    [Header("네트워크 시드")]
    [Tooltip("Mouth 0x4D4F5554 / Saliva 0x53504954 와 겹치지 않게.")]
    [SerializeField] int seedSalt = 0x544F4E47;

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    // 스폰해 둔 파편. 응원 성공으로 칸이 복구되면 lifetime을 기다리지 않고 즉시 치워야
    // "멀쩡한 타일 위에 파편이 뒹구는" 그림이 안 나온다. 페이즈 전환(OnDisable)에서도 정리.
    readonly List<GameObject> _spawnedDebris = new();

    // 복구 연출(스케일 팝). 타일별 원래 스케일·코루틴 추적은 전부 여기가 들고 있다
    // (MouthBossJawSmash와 공용 — 같은 부기를 두 함정이 복제하지 않는다).
    TileRestorePopGroup _restorePop;

    HazardPhase _phase = HazardPhase.Idle;
    SweepRegion _sweepRegion = SweepRegion.None;
    // Warning 진입 시 PeekNextRegion()으로 미리 뽑아 경고 마커를 켠 영역. AttackRoutine이 재추첨 없이
    // 그대로 재사용(_sweepRegion에 대입)한다 — 경고가 켜진 곳과 실제 공격 영역이 어긋나면 안 됨.
    SweepRegion _warnRegion = SweepRegion.None;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;
    int _cycleCount;
    int _syncGeneration;
    bool _attackLeft;
    bool _sideInitialized;

    // 소비한 창 수. 스킵·차단·완주 어느 경로로 끝나든 창당 정확히 1회 올라가므로 전 머신이
    // 같은 값을 갖는다 — MixedSweep의 영역 추첨 키.
    int _attackCount;

    // PhaseStartServerTime(Host가 Phase 진입 직전에 찍는 절대 시각)이 전파될 때까지 기다리는 한도.
    // 그 안에 안 오면 앵커가 없는 씬으로 보고 예전처럼 로컬 시각으로 폴백한다.
    const float AnchorWaitTimeout = 3f;

    public bool IsAvailable => _available;

    void Awake()
    {
        if (tongueAnimator == null)
            tongueAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        _restorePop = new TileRestorePopGroup(this);
    }

    void OnEnable()
    {
        ResetHazardFlags();
        RestoreAll();
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
        RestoreAll();
        // StopAllCoroutines()가 진행 중이던 팝 코루틴을 자체 정리(스케일 원복) 없이 죽였을 수
        // 있다 — 타일이 부푼 채로 멈춰 있는 그림을 막기 위해 명시적으로 되돌린다.
        _restorePop.ResetAll();
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        if (startOnAwake)
            StartCycle();
    }

    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(HazardCycle());
    }

    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        RestoreAll();
        TriggerIdle();
    }

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + PickSeededInterval(generation, RevertAxis);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return;   // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

        switch (_phase)
        {
            case HazardPhase.Warning:
                _prevented = true;
                EndWindow();
                break;
            case HazardPhase.Attacking:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                // 응원 성공 순간 이미 깨진 칸이 있으면(SweepBreak 애니메이션 이벤트가 클립 재생 중
                // 실시간으로 깨움) 클립이 끝나길 기다리지 않고 그 즉시 복구한다(2026-09-07 추가).
                // 안 그러면 응원에 성공했는데도 남은 클립 재생 시간 동안 바닥이 깨진 채로 남는다 —
                // SweepBreak()/BreakRemaining()도 아래에서 _recoverQueued를 봐서 더 이상 새로
                // 깨지 않게 게이트했으니, 이 시점 이후로는 단 한 칸도 깨지지 않는다.
                RestoreAll();
                break;
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 창을 열지 않았다(씬 로드 시각 차이 등). 예전엔 여기서 명령을
                // 통째로 버려서 혼자 뒤늦게 스윕이 돌고 꺼진 1×1이 남았다.
                // 대기 중인 창을 열지 않고 건너뛰어 Host가 준 다음 예약에 위상을 맞춘다.
                // 건너뛰는 분기도 Host와 같이 창을 1회 소비한다(AdvanceAttack) — 안 그러면
                // 4.2 좌/우 순서와 MixedSweep 영역 추첨이 Host와 어긋난다.
                _skipNextWindow = true;
                break;
            // Recovering: 직전 창을 되돌리는 중 — 이번 창은 애초에 열지 않았으므로
            // 위에서 받은 _resyncDeadline만 따라가면 위상이 맞는다.
        }
    }

    /// <summary>클립 Animation Event. 배열 인덱스 = 스윕 순서 (0부터).</summary>
    public void SweepBreak(int index)
    {
        // 응원 성공 이후(Revert가 _recoverQueued를 세운 뒤) 늦게 도착하는 이벤트는 무시 —
        // 클립은 끝까지 재생되지만 이 시점부터는 칸이 더 깨지면 안 된다(2026-09-07).
        if (_recoverQueued) return;
        BreakTile(CurrentSweepArray(), index);
    }

    IEnumerator HazardCycle()
    {
        yield return ResolveFirstWindow();

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitForResyncDeadline();
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount, ScheduleAxis));
                _cycleCount++;
            }

            if (_skipNextWindow)
            {
                // 팀이 이미 되돌린 창 — 열지 않고 다음 예약으로 넘어간다.
                // Host의 Warning 차단 경로와 같은 뒷정리(복구·방향 소비)를 해야 꺼진 칸과
                // 다음 공격 방향(4.2)이 Host와 어긋나지 않는다.
                _skipNextWindow = false;
                EnsureAttackSide();
                RestoreAll();
                TriggerIdle();
                AdvanceAttack();
                _phase = HazardPhase.Idle;
                continue;
            }

            EnsureAttackSide();
            _prevented = false;
            _recoverQueued = false;
            _phase = HazardPhase.Warning;
            _available = true;
            CheerService.Instance?.NotifyHazardWindow(true);

            float warnElapsed = 0f;
            float warn = Mathf.Max(0f, warnDuration);
            _warnRegion = PeekNextRegion();
            PlayRegionWarning(_warnRegion, warn);
            while (warnElapsed < warn && !_prevented)
            {
                warnElapsed += Time.deltaTime;
                yield return null;
            }

            if (_prevented)
            {
                _prevented = false;
                ResetRegionWarning(_warnRegion);
                RestoreAll();
                TriggerIdle();
                AdvanceAttack();
                _phase = HazardPhase.Idle;
                continue;
            }

            yield return AttackRoutine();
            BreakRemaining();

            if (pattern == TonguePattern.RiseHold)
            {
                if (_recoverQueued)
                {
                    _recoverQueued = false;
                    yield return RecoverRoutine();
                    _phase = HazardPhase.Idle;
                    continue;
                }

                _phase = HazardPhase.Holding;
                FireTrigger(holdTrigger);
                while (!_recoverQueued)
                    yield return null;

                _recoverQueued = false;
                yield return RecoverRoutine();
                _phase = HazardPhase.Idle;
                continue;
            }

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return RecoverRoutine();
                _phase = HazardPhase.Idle;
            }
            else
            {
                EndWindow();

                // 가운데는 혀를 내리되 칸은 복구하지 않는다 — 안 외친 대가는 L/R과 같다.
                SweepRegion region = _sweepRegion;
                _sweepRegion = SweepRegion.None;
                yield return RetractIfCenter(region);

                // 여기까지 _phase는 Attacking이라 늦게 도착한 명령도 _recoverQueued로 잡힌다.
                // Host가 창 마지막 순간에 표를 받아들이면 명령이 RTT만큼 늦게 오는데, 그걸 흘리면
                // Host는 칸을 복구했는데 이 머신만 꺼진 채 남아 혼자 낙사한다. 한 프레임 더 본다.
                yield return null;
                if (_recoverQueued)
                {
                    _recoverQueued = false;
                    RestoreAll();
                }

                TriggerIdle();
                _phase = HazardPhase.Idle;
            }

            AdvanceAttack();
        }
    }

    IEnumerator AttackRoutine()
    {
        _phase = HazardPhase.Attacking;
        // 가시가 튀어오르는 순간 규칙과 동일 — 공격이 시작되면 경고 마커는 즉시 끈다.
        ResetRegionWarning(_warnRegion);

        if (pattern == TonguePattern.RiseHold)
        {
            _sweepRegion = SweepRegion.Center;
            FireTrigger(riseTrigger);
            if (riseClipLength > 0f)
                yield return new WaitForSeconds(riseClipLength);
            yield break;
        }

        // Warning 시작 시 PeekNextRegion()으로 이미 뽑은 값과 같아야 하므로 재추첨하지 않고 재사용.
        _sweepRegion = _warnRegion;

        if (_sweepRegion == SweepRegion.Center)
        {
            // MixedSweep의 가운데 — Rise를 공격으로 쓴다. Hold 없이 부수고 Retract로 내려간다.
            FireTrigger(riseTrigger);
            if (riseClipLength > 0f)
                yield return new WaitForSeconds(riseClipLength);
            yield break;
        }

        FireTrigger(_sweepRegion == SweepRegion.Left ? attackLTrigger : attackRTrigger);
        if (attackClipLength > 0f)
            yield return new WaitForSeconds(attackClipLength);
    }

    /// <summary>
    /// 가운데 스윕(4.1 Rise / MixedSweep 가운데)은 클립이 혀가 올라간 채 끝나므로 Retract로 내린다.
    /// L/R 클립은 스스로 내려가므로 Retract 없음(4.2 잠금).
    /// </summary>
    IEnumerator RetractIfCenter(SweepRegion region)
    {
        if (region != SweepRegion.Center) yield break;
        FireTrigger(retractTrigger);
        if (retractClipLength > 0f)
            yield return new WaitForSeconds(retractClipLength);
    }

    // _phase = Idle 은 호출부(HazardCycle)가 찍는다 — Idle이 "다음 창을 기다리는 중"만 뜻해야
    // Revert가 "창 밖이라 건너뛸 머신"과 "직전 창을 되돌리는 중인 머신"을 구분할 수 있다.
    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();
        SweepRegion region = _sweepRegion;
        _sweepRegion = SweepRegion.None;

        yield return RetractIfCenter(region);

        RestoreAll();
        TriggerIdle();
    }

    /// <summary>
    /// 첫 창을 Host/Client 공통 절대 시각에 건다. 예전엔 로컬 OnEnable + WaitForSeconds라 씬 로드
    /// 시각 차이만큼 첫 Warning 창이 어긋났고, 창 밖에서 외친 표는 Host에서 조용히 버려졌다.
    /// 앵커는 WindTrap/ArrowTrap과 같은 PhaseStartServerTime — 앵커가 없는 씬에서는 로컬 폴백.
    /// </summary>
    IEnumerator ResolveFirstWindow()
    {
        // PhaseManager.EnterPhase()는 objectsToEnable.SetActive(true) 다음에야 MarkAndSyncPhase()를
        // 찍는다. Phase가 이 함정을 켜주는 경우(4.1↔4.2 전환) OnEnable에서 곧바로 읽으면 Host가
        // 직전 Phase의 낡은 앵커를 잡아 Client와 첫 창이 어긋난다(SafeZoneWarnSign과 같은 이유).
        // 한 프레임 양보하면 같은 EnterPhase의 MarkAndSyncPhase가 끝난 뒤 새 앵커를 읽는다.
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
        {
            _resyncDeadline = anchor + initialDelay + PickSeededInterval(_cycleCount, ScheduleAxis);
            _cycleCount++;
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
    }

    /// <summary>
    /// 예약된 재개 시각까지 대기. 대기 중에 Revert가 예약을 갱신할 수 있으므로 매 프레임 필드를
    /// 다시 읽는다 — 인자로 붙잡아 두면 갱신된 예약이 대기 종료 직후 지워져 로컬 랜덤으로 샌다.
    /// </summary>
    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    // 시드를 뽑는 축이 셋이다 — 로컬 스케줄(_cycleCount) / 되돌림 세대(_syncGeneration) /
    // 영역 추첨(_attackCount). 전부 1,2,3…으로 올라가므로 축을 안 섞으면 같은 정수가 같은 값을 준다.
    const int ScheduleAxis = 0;
    const int RevertAxis   = 1;
    const int RegionAxis   = 2;
    const int DebrisAxis   = 3;

    /// <summary>
    /// 파편 임펄스 시드. 전 머신이 같은 값을 뽑아야 파편이 똑같이 튄다(로컬 Random 금지 원칙).
    /// 창마다 모양이 바뀌도록 창 번호를 섞는데, 축은 전 머신이 반드시 같은 값을 갖는 둘만 쓴다 —
    /// _attackCount(창당 정확히 1회 증가) + _syncGeneration(Host가 준 되돌림 세대).
    /// RiseHold는 AdvanceAttack을 지나지 않아 _attackCount가 고정이므로 _syncGeneration이 변화를 준다.
    /// _cycleCount는 예약 재개(_resyncDeadline) 경로에서 증가하지 않아 머신마다 달라질 수 있으므로 금지.
    /// 창 안에서 이 값이 바뀌는 일은 없다 — Revert가 세대를 올리는 순간 _recoverQueued가 서고,
    /// 그 뒤로는 SweepBreak/BreakRemaining이 단 한 칸도 깨지 않는다.
    /// </summary>
    int DebrisSeed(int index) => MixSeed(index + (_attackCount + _syncGeneration) * 101, DebrisAxis);

    int MixSeed(int index, int axis)
        => NetworkSessionData.Seed ^ seedSalt ^ (index * 0x2545F491) ^ (axis * 0x27220A95);

    float PickSeededInterval(int generation, int axis)
    {
        // InitState는 전역 RNG를 갈아엎는다 — 뽑고 나서 되돌려야 같은 씬의 다른 시스템이
        // 이 시드 스트림을 물려받지 않는다. 결정성은 그대로.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(MixSeed(generation, axis));
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        float interval = Random.Range(min, max);
        UnityEngine.Random.state = prevState;
        return interval;
    }

    static readonly SweepRegion[] MixedRegions =
    {
        SweepRegion.Center,
        SweepRegion.Left,
        SweepRegion.Right,
    };

    /// <summary>
    /// MixedSweep의 이번 창 영역. 전 머신이 같은 값을 뽑아야 꺼진 칸이 어긋나지 않으므로
    /// 로컬 Random을 쓰지 않고 세션 시드 + 창 번호로 결정한다(§7 "클라이언트마다 Random 없음").
    /// 이미 부서진 영역이 또 나오는 것은 허용 — 그 창은 헛방이 된다.
    /// </summary>
    SweepRegion PickSeededRegion(int index)
    {
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(MixSeed(index, RegionAxis));
        int pick = Random.Range(0, MixedRegions.Length);
        UnityEngine.Random.state = prevState;
        return MixedRegions[pick];
    }

    /// <summary>
    /// 이번 창의 공격 영역을 부작용 없이 미리 계산 — Warning 진입 시 경고 마커를 어디에 켤지
    /// 결정하는 용도. AttackRoutine의 영역 결정 로직과 반드시 같은 값을 내야 하므로 여기 한 곳에서만
    /// 계산하고 AttackRoutine은 이 값(_warnRegion)을 그대로 재사용한다(재추첨 금지).
    /// </summary>
    SweepRegion PeekNextRegion()
    {
        if (pattern == TonguePattern.RiseHold) return SweepRegion.Center;
        if (pattern == TonguePattern.MixedSweep) return PickSeededRegion(_attackCount);
        return _attackLeft ? SweepRegion.Left : SweepRegion.Right;
    }

    SpikeLaneWarnMarker MarkerFor(SweepRegion region)
    {
        switch (region)
        {
            case SweepRegion.Center: return centerWarnMarker;
            case SweepRegion.Left: return leftWarnMarker;
            case SweepRegion.Right: return rightWarnMarker;
            default: return null;
        }
    }

    void PlayRegionWarning(SweepRegion region, float duration) => MarkerFor(region)?.PlayWarning(duration);

    void ResetRegionWarning(SweepRegion region) => MarkerFor(region)?.ResetWarning();

    void ResetAllWarnings()
    {
        centerWarnMarker?.ResetWarning();
        leftWarnMarker?.ResetWarning();
        rightWarnMarker?.ResetWarning();
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void EnsureAttackSide()
    {
        if (pattern != TonguePattern.AttackSweep || _sideInitialized) return;
        _attackLeft = ((NetworkSessionData.Seed ^ seedSalt) & 1) == 0;
        _sideInitialized = true;
    }

    /// <summary>
    /// 창 하나를 소비했다는 표시. 스킵·차단·완주 세 경로에서 **정확히 1회씩** 불려야
    /// Host/Client의 공격 방향(4.2)과 영역 추첨(MixedSweep)이 어긋나지 않는다.
    /// </summary>
    void AdvanceAttack()
    {
        _attackCount++;
        if (pattern == TonguePattern.AttackSweep)
            _attackLeft = !_attackLeft;
    }

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _sweepRegion = SweepRegion.None;
        _warnRegion = SweepRegion.None;
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _skipNextWindow = false;
        _resyncDeadline = -1d;
        ResetAllWarnings();
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    GameObject[] CurrentSweepArray()
    {
        switch (_sweepRegion)
        {
            case SweepRegion.Center: return centerTiles;
            case SweepRegion.Left: return leftTiles;
            case SweepRegion.Right: return rightTiles;
            default: return null;
        }
    }

    void BreakTile(GameObject[] tiles, int index)
    {
        if (tiles == null || index < 0 || index >= tiles.Length) return;
        GameObject tile = tiles[index];
        if (tile != null && tile.activeSelf)
        {
            // 원래 스케일 캐시 + 아직 도는 팝 접기(복구 직후 같은 칸이 다시 부서지는 경우 방어).
            _restorePop.OnTileBroken(tile);

            // Breakable.DoBreakVisuals()와 동일 SFX 재생 패턴 재사용 — 새 SFXId 추가 없음.
            SFXManager.Instance?.PlayAtPoint(SFXId.Breakable_Destroy, tile.transform.position, 5f, 50f, AudioRolloffMode.Logarithmic);
            // 파편은 tile의 현재 Renderer.sharedMaterial(Black/White/Reveal)을 그대로 입는다 —
            // 흑/백 전용 파편 프리팹을 따로 만들 필요 없이 항상 실시간 색과 일치.
            GameObject debris = TileDebrisUtil.BreakTile(tile, tileDebrisPrefab, tileDebrisLifetime,
                                                         tileDebrisImpulseMin, tileDebrisImpulseMax, DebrisSeed(index));
            if (debris != null)
                _spawnedDebris.Add(debris);
            tile.SetActive(false);
        }
    }

    void BreakRemaining()
    {
        // 응원 성공 후엔 놓친 칸까지 마무리로 깨는 이 catch-all도 건너뛴다(2026-09-07) — 위
        // SweepBreak 게이트와 같은 이유.
        if (_recoverQueued) return;
        GameObject[] tiles = CurrentSweepArray();
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
            BreakTile(tiles, i);
    }

    void RestoreAll()
    {
        RestoreArray(centerTiles);
        RestoreArray(leftTiles);
        RestoreArray(rightTiles);
        ClearDebris();
    }

    /// <summary>
    /// 남아 있는 파편을 즉시 삭제. RestoreAll 한 곳에서만 부르므로(복구·스킵·OnEnable/OnDisable
    /// 전부 RestoreAll을 지난다) 정리 경로가 갈라지지 않는다.
    /// </summary>
    void ClearDebris()
    {
        for (int i = 0; i < _spawnedDebris.Count; i++)
        {
            GameObject debris = _spawnedDebris[i];
            if (debris != null)
                Destroy(debris);
        }
        _spawnedDebris.Clear();
    }

    void RestoreArray(GameObject[] tiles)
    {
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
        {
            GameObject tile = tiles[i];
            if (tile != null && !tile.activeSelf)
            {
                // 판정(SetActive)은 항상 먼저 즉시 끝낸다 — 팝은 그 뒤에 얹히는 순수 시각 연출.
                tile.SetActive(true);
                _restorePop.Play(tile, restorePopDuration, restorePopAmplitude, restorePopJitter);
            }
        }
    }

    void TriggerIdle() => FireTrigger(idleTrigger);

    void FireTrigger(string trigger)
    {
        if (tongueAnimator == null || string.IsNullOrEmpty(trigger)) return;
        if (!string.IsNullOrEmpty(idleTrigger)) tongueAnimator.ResetTrigger(idleTrigger);
        if (!string.IsNullOrEmpty(riseTrigger)) tongueAnimator.ResetTrigger(riseTrigger);
        if (!string.IsNullOrEmpty(holdTrigger)) tongueAnimator.ResetTrigger(holdTrigger);
        if (!string.IsNullOrEmpty(retractTrigger)) tongueAnimator.ResetTrigger(retractTrigger);
        if (!string.IsNullOrEmpty(attackLTrigger)) tongueAnimator.ResetTrigger(attackLTrigger);
        if (!string.IsNullOrEmpty(attackRTrigger)) tongueAnimator.ResetTrigger(attackRTrigger);
        tongueAnimator.SetTrigger(trigger);
    }

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
