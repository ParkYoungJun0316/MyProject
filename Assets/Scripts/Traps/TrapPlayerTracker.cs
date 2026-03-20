using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어를 추적하여 함정을 조준시키는 범용 컴포넌트.
///
/// [스텔스 연동]
/// - playerVisibleLayer 에 "Player" 레이어만 등록
/// - 플레이어가 스텔스(PlayerStealth 레이어) 상태이면 타겟에서 자동 제외 → 공격 안 함
/// - 스텔스 해제(Player 레이어)로 돌아오면 자동으로 다시 타겟 포함 → 공격 재개
/// - TrapStealthSensor 없이 이 컴포넌트 단독으로 스텔스 감지까지 처리함
///
/// [ArrowTrap과 사용 시]
/// - rotateToTarget = true 로 설정
/// - 오브젝트(또는 firePoint)를 보이는 플레이어 방향으로 회전 → 발사 방향 자동 조준
/// - ArrowTrap의 fireAtSeconds 스케줄은 그대로 유지됨 (타이밍은 스케줄이, 방향은 이 컴포넌트가 제어)
/// - 보이는 플레이어가 없으면 회전을 멈추고 마지막 방향 유지
///
/// [DropTrap과 사용 시]
/// - dropInterval 에 발사 주기 입력 (0이면 비활성)
/// - dropInterval 마다 보이는 플레이어 위치로 FireAt() 자동 호출
/// - 보이는 플레이어가 없으면 해당 사이클을 건너뜀 (발사 안 함)
/// - DropTrap의 fireAtSeconds 는 비워둘 것 (이 컴포넌트가 발사 타이밍 전담)
///
/// [공통]
/// - activateDelay 이후에 추적 시작
/// - controlTrapActivation = true 시, activateDelay 후 TrapBase.Activate() 도 자동 호출
///   (PhaseManager가 Activate를 이미 제어하고 있으면 false 로 유지)
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class TrapPlayerTracker : MonoBehaviour
{
    public enum TargetMode
    {
        Nearest,  // 가장 가까운 플레이어 1명
        Random,   // 살아있는 플레이어 중 랜덤 1명
        All,      // 살아있는 플레이어 전원 (DropTrap 전용)
    }

    [Header("활성화")]
    [Tooltip("추적 시작까지 대기 시간 (초). 0 = 즉시")]
    [SerializeField] float activateDelay = 0f;

    [Tooltip("true: activateDelay 후 TrapBase.Activate() 도 자동 호출\n" +
             "false: 함정 활성화는 TrapBase.startActive 또는 PhaseManager 가 제어 (권장)")]
    [SerializeField] bool controlTrapActivation = false;

    [Header("타겟 설정")]
    [Tooltip("어떤 플레이어를 노릴지")]
    [SerializeField] TargetMode targetMode = TargetMode.Nearest;

    [Tooltip("공격 대상으로 인식할 플레이어 레이어 마스크.\n" +
             "Player 레이어만 선택 → 스텔스(PlayerStealth) 상태면 자동 제외됨.\n" +
             "0(없음)이면 스텔스 무시하고 모든 살아있는 플레이어를 타겟으로 삼음.")]
    [SerializeField] LayerMask playerVisibleLayer;

    [Header("DropTrap 전용 — 발사 주기")]
    [Tooltip("DropTrap 사용 시 플레이어 위치로 낙하를 호출하는 주기 (초). 0 = 비활성")]
    [SerializeField] float dropInterval = 0f;

    [Header("회전 추적 (ArrowTrap 전용)")]
    [Tooltip("플레이어 방향으로 오브젝트를 회전. false = 회전 없음")]
    [SerializeField] bool rotateToTarget = false;

    [Tooltip("회전 속도 (도/초). 0 = 즉시 전환")]
    [SerializeField] float rotateSpeed = 0f;

    [Tooltip("Y축만 회전 (수평 조준). true 권장")]
    [SerializeField] bool rotateYAxisOnly = true;

    TrapBase _trap;
    DropTrap _dropTrap;
    Player[] _players;
    bool     _activated;

    void Awake()
    {
        _trap     = GetComponent<TrapBase>();
        _dropTrap = GetComponent<DropTrap>();
    }

    void Start()
    {
        RefreshPlayerCache();

        if (activateDelay > 0f)
            StartCoroutine(DelayedActivate());
        else
            BeginTracking();
    }

    IEnumerator DelayedActivate()
    {
        yield return new WaitForSeconds(activateDelay);
        BeginTracking();
    }

    void BeginTracking()
    {
        _activated = true;

        if (controlTrapActivation)
            _trap.Activate();

        if (_dropTrap != null && dropInterval > 0f)
            StartCoroutine(DropLoop());
    }

    // ── DropTrap 발사 루프 ─────────────────────────────────────────────

    IEnumerator DropLoop()
    {
        while (_activated)
        {
            FireAtPlayers();
            yield return new WaitForSeconds(dropInterval);
        }
    }

    void FireAtPlayers()
    {
        if (_dropTrap == null) return;

        if (targetMode == TargetMode.All)
        {
            foreach (Player p in _players)
            {
                if (p == null || p.IsDead || !IsVisible(p)) continue;
                _dropTrap.FireAt(p.transform.position);
            }
        }
        else
        {
            Player target = GetSingleTarget();
            if (target != null)
                _dropTrap.FireAt(target.transform.position);
        }
    }

    // ── ArrowTrap 방향 회전 ─────────────────────────────────────────────

    void Update()
    {
        if (!_activated) return;
        if (!rotateToTarget) return;
        if (_dropTrap != null) return; // DropTrap은 회전 불필요

        Player target = GetSingleTarget();
        if (target == null) return;

        Vector3 dir = target.transform.position - transform.position;
        if (rotateYAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        if (rotateSpeed <= 0f)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    // ── 타겟 선택 ──────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 공격 가능한 상태인지 확인.
    /// playerVisibleLayer가 설정된 경우, 해당 레이어(Player)에 있어야만 타겟으로 인정.
    /// PlayerStealth 레이어이면 false → 공격 제외.
    /// playerVisibleLayer가 0이면 스텔스 무시하고 항상 true.
    /// </summary>
    bool IsVisible(Player p)
    {
        if (playerVisibleLayer.value == 0) return true;
        return (playerVisibleLayer.value & (1 << p.gameObject.layer)) != 0;
    }

    Player GetSingleTarget()
    {
        if (_players == null || _players.Length == 0) return null;

        if (targetMode == TargetMode.Random)
        {
            int validCount = 0;
            foreach (Player p in _players)
                if (p != null && !p.IsDead && IsVisible(p)) validCount++;

            if (validCount == 0) return null;

            int pick = Random.Range(0, validCount);
            int idx  = 0;
            foreach (Player p in _players)
            {
                if (p == null || p.IsDead || !IsVisible(p)) continue;
                if (idx == pick) return p;
                idx++;
            }
            return null;
        }

        // Nearest (All 모드에서 단일 타겟이 필요한 경우도 Nearest 사용)
        Player nearest = null;
        float  minSqr  = float.MaxValue;
        foreach (Player p in _players)
        {
            if (p == null || p.IsDead || !IsVisible(p)) continue;
            float sqr = (p.transform.position - transform.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr  = sqr;
                nearest = p;
            }
        }
        return nearest;
    }

    // ── 외부 API ───────────────────────────────────────────────────────

    /// <summary>
    /// 씬에서 플레이어가 추가 / 제거된 경우 외부에서 호출하여 캐시 갱신.
    /// TrapStealthSensor.RefreshPlayerCache() 와 동일한 패턴.
    /// </summary>
    public void RefreshPlayerCache()
    {
        _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
    }

    /// <summary>외부(PhaseManager 이벤트 등)에서 추적을 즉시 중단할 때 호출.</summary>
    public void StopTracking() => _activated = false;

    /// <summary>외부에서 추적을 재개할 때 호출.</summary>
    public void ResumeTracking() => _activated = true;
}
