using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 혀 함정 — 팀 응원 되돌림 (ITeamCheerRevert). Mouth/Saliva와 같은 머신. 새 RPC 없음.
/// Animation Event <c>SweepBreak(int index)</c> — Animator와 같은 GO에 이 컴포넌트를 붙일 것.
///
/// 4.1 RiseHold: Warning → Rise(스윕 9칸) → Hold → 외침 시 Retract 후 복구.
/// 4.2 AttackSweep: Warning → Attack L 또는 R(스윕 10칸) → Hold 클립 없음.
///     안 외치면 칸은 꺼진 채 다음 사이클이 반대쪽. 외침(Warning 또는 Attack 중 큐)이면 꺼진 칸 전부 복구.
/// Rise/Attack이 시작되면 끊지 않음. MouthBG 혀 쓰지 않음.
/// </summary>
public class TongueController : MonoBehaviour, ITeamCheerRevert
{
    public enum TonguePattern
    {
        RiseHold,
        AttackSweep,
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
    [Tooltip("4.1 SequenceRing = RiseHold. 4.2 ArrowTrap = AttackSweep.")]
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
    [Tooltip("4.1 가운데 3×3 = 9칸")]
    [SerializeField] GameObject[] centerTiles = new GameObject[0];

    [Tooltip("4.2 왼쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] leftTiles = new GameObject[0];

    [Tooltip("4.2 오른쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] rightTiles = new GameObject[0];

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

    HazardPhase _phase = HazardPhase.Idle;
    SweepRegion _sweepRegion = SweepRegion.None;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    double _resyncDeadline = -1d;
    int _cycleCount;
    int _syncGeneration;
    bool _attackLeft;
    bool _sideInitialized;

    public bool IsAvailable => _available;

    void Awake()
    {
        if (tongueAnimator == null)
            tongueAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
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

    public void Revert()
    {
        if (!_available) return;

        _syncGeneration++;
        _resyncDeadline = GetServerTime() + PickSeededInterval(_syncGeneration);

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
                break;
        }
    }

    /// <summary>클립 Animation Event. 배열 인덱스 = 스윕 순서 (0부터).</summary>
    public void SweepBreak(int index)
    {
        BreakTile(CurrentSweepArray(), index);
    }

    IEnumerator HazardCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitUntilServerTime(_resyncDeadline);
                _resyncDeadline = -1d;
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount));
                _cycleCount++;
            }

            EnsureAttackSide();
            _prevented = false;
            _recoverQueued = false;
            _phase = HazardPhase.Warning;
            _available = true;
            CheerService.Instance?.NotifyHazardWindow(true);

            float warnElapsed = 0f;
            float warn = Mathf.Max(0f, warnDuration);
            while (warnElapsed < warn && !_prevented)
            {
                warnElapsed += Time.deltaTime;
                yield return null;
            }

            if (_prevented)
            {
                _prevented = false;
                RestoreAll();
                TriggerIdle();
                AdvanceAttackSide();
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
                    continue;
                }

                _phase = HazardPhase.Holding;
                FireTrigger(holdTrigger);
                while (!_recoverQueued)
                    yield return null;

                _recoverQueued = false;
                yield return RecoverRoutine();
                continue;
            }

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return RecoverRoutine();
            }
            else
            {
                EndWindow();
                TriggerIdle();
                _phase = HazardPhase.Idle;
            }

            AdvanceAttackSide();
        }
    }

    IEnumerator AttackRoutine()
    {
        _phase = HazardPhase.Attacking;
        if (pattern == TonguePattern.RiseHold)
        {
            _sweepRegion = SweepRegion.Center;
            FireTrigger(riseTrigger);
            if (riseClipLength > 0f)
                yield return new WaitForSeconds(riseClipLength);
            yield break;
        }

        if (_attackLeft)
        {
            _sweepRegion = SweepRegion.Left;
            FireTrigger(attackLTrigger);
        }
        else
        {
            _sweepRegion = SweepRegion.Right;
            FireTrigger(attackRTrigger);
        }

        if (attackClipLength > 0f)
            yield return new WaitForSeconds(attackClipLength);
    }

    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();
        _sweepRegion = SweepRegion.None;

        if (pattern == TonguePattern.RiseHold)
        {
            FireTrigger(retractTrigger);
            if (retractClipLength > 0f)
                yield return new WaitForSeconds(retractClipLength);
        }

        RestoreAll();
        TriggerIdle();
        _phase = HazardPhase.Idle;
    }

    IEnumerator WaitUntilServerTime(double deadline)
    {
        while (GetServerTime() < deadline)
            yield return null;
    }

    float PickSeededInterval(int generation)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491);
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        return Random.Range(min, max);
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

    void AdvanceAttackSide()
    {
        if (pattern != TonguePattern.AttackSweep) return;
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
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _resyncDeadline = -1d;
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
            tile.SetActive(false);
    }

    void BreakRemaining()
    {
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
    }

    static void RestoreArray(GameObject[] tiles)
    {
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
        {
            GameObject tile = tiles[i];
            if (tile != null && !tile.activeSelf)
                tile.SetActive(true);
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
