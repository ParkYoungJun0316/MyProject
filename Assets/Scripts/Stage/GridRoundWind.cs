using System.Collections;
using UnityEngine;

/// <summary>
/// M.Stage5 전용 — WindTrap을 스테이지 시계가 아니라 GridChallenge 라운드에 맞춰 발동한다 (2026-09-22).
///
/// [타이밍] firstRound부터 everyNRounds 라운드마다(M.Stage5: 1, 3, 5 … 격라운드). 바람은 **붕괴 + 공개 내내**:
/// 힘 시작 = 선행 시작 + forceStartAfterPreReveal, 끝 = 정산 순간(지속 시간을 FireNow에 넘김).
/// 시작·끝은 WindTrap.rampSeconds로 부드럽게 — 갑자기 밀리는 느낌(멀미) 완화.
/// 방향은 WindTrap의 Random 모드가 발동마다 뽑는다(라운드마다 랜덤, 전 머신 동일 시드).
///
/// [입 충전] 힘보다 WindTrap.WindChargeTime(입 연출 2초 등)만큼 먼저 FireNow()해야 한다 — 그 시각은 선행 시작
/// 이전(휴식 끝부분)이라 OnRoundPreReveal로는 늦는다. 그래서 **직전 라운드 정산(OnRoundSettled)** 을 받는 순간
/// "멈춤 + 복구 + 휴식" 뒤의 선행 시작을 계산해 예약한다. 정산은 Client에 ClientRpc로 오므로 Host보다 전송
/// 지연만큼 늦게 발동하지만, 바람 힘은 원래 Owner 물리로 각 피어가 자기 플레이어에 로컬 적용하는 구조라
/// 판정이 갈리지 않는다. 예약을 못 한 라운드(첫 발동 라운드가 0 등)는 OnRoundPreReveal에서 즉시 발동한다.
///
/// [네트워크] 새 RPC/NV 없음. 입 연출은 기존대로 Host 이벤트 → ClientRpc로 동기화된다.
/// [설정] WindTrap.fireAtSeconds를 비워 둘 것 — 비어 있어야 스스로 불지 않는다. WindTrap.windDuration은
/// 쓰이지 않는다(라운드마다 붕괴 + 공개 시간으로 계산해 넘김).
/// </summary>
public class GridRoundWind : MonoBehaviour
{
    [SerializeField] GridChallenge grid;
    [SerializeField] WindTrap wind;

    [Header("발동 라운드")]
    [Tooltip("첫 발동 라운드 인덱스(0부터). 1 = 2번째 라운드부터")]
    [SerializeField] [Min(0)] int firstRound = 1;

    [Tooltip("몇 라운드마다 발동할지. 2 = 격라운드")]
    [SerializeField] [Min(1)] int everyNRounds = 2;

    [Header("타이밍")]
    [Tooltip("붕괴 구간(선행) 시작 후 힘이 걸리기 시작하는 시각(초). 끝은 정산 순간")]
    [SerializeField] float forceStartAfterPreReveal = 0f;

    Coroutine _pending;
    int _armedRound = -1;   // 발동을 예약(또는 실행)한 라운드
    int _windRound = -1;    // 지금 바람이 걸린 라운드 — 정산 때 끌 대상(안전망)
    bool _subscribed;

    void Awake()
    {
        if (grid == null) grid = GetComponent<GridChallenge>() ?? GetComponentInParent<GridChallenge>();
        if (grid == null)
            Debug.LogWarning($"[GridRoundWind] GridChallenge가 연결되지 않았다 — 바람이 불지 않는다. ({name})", this);
        if (wind == null)
            Debug.LogWarning($"[GridRoundWind] WindTrap이 연결되지 않았다. ({name})", this);
    }

    void OnEnable() => Subscribe();

    // GridChallenge와 같은 이유로 Start가 최초 구독 안전망.
    void Start() => Subscribe();

    void OnDisable()
    {
        Unsubscribe();
        ResetState();
    }

    void Subscribe()
    {
        if (_subscribed || grid == null) return;
        grid.OnRoundSettled.AddListener(HandleRoundSettled);
        grid.OnRoundPreReveal.AddListener(HandleRoundPreReveal);
        grid.OnChallengeComplete.AddListener(ResetState);
        grid.OnChallengeCancelled.AddListener(ResetState);
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (grid != null)
        {
            grid.OnRoundSettled.RemoveListener(HandleRoundSettled);
            grid.OnRoundPreReveal.RemoveListener(HandleRoundPreReveal);
            grid.OnChallengeComplete.RemoveListener(ResetState);
            grid.OnChallengeCancelled.RemoveListener(ResetState);
        }
        _subscribed = false;
    }

    bool IsWindRound(int round) =>
        wind != null && round >= firstRound && (round - firstRound) % Mathf.Max(1, everyNRounds) == 0;

    // 직전 라운드 정산 → 다음 라운드 선행 시작까지 남은 시간이 정해진다(멈춤 + 복구 + 휴식).
    void HandleRoundSettled(int round, bool success)
    {
        // 이번 라운드 바람은 정산 순간 끝나도록 지속 시간을 넘겼지만, 머신별 타이밍 오차 안전망으로 한 번 더 끈다.
        CancelPending();
        if (_windRound == round && wind != null) wind.StopWind();
        _windRound = -1;

        int next = round + 1;
        if (next >= grid.TotalRounds || !IsWindRound(next)) return;

        float untilPreReveal = grid.SettleHoldSeconds + grid.RestoreSeconds + grid.RestSeconds;
        float delay = untilPreReveal + forceStartAfterPreReveal - wind.WindChargeTime;
        Arm(next, delay);
    }

    // 예약을 못 받은 라운드 보충(첫 라운드가 발동 라운드이거나, 정산 이벤트를 놓친 경우) — 충전만큼 늦게 분다.
    void HandleRoundPreReveal(int round)
    {
        if (_armedRound == round || !IsWindRound(round)) return;
        Arm(round, Mathf.Max(0f, forceStartAfterPreReveal - wind.WindChargeTime));
    }

    void Arm(int round, float delay)
    {
        CancelPending();
        _armedRound = round;
        _pending = StartCoroutine(FireAfter(round, delay));
    }

    IEnumerator FireAfter(int round, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        _pending = null;
        _windRound = round;
        // 힘 지속 = 힘 시작(선행 시작 + forceStartAfterPreReveal) ~ 정산. 이 라운드 공개 시간은 라운드별로 다르다.
        float duration = grid.PreRevealSeconds + grid.RoundDurationFor(round) - forceStartAfterPreReveal;
        wind.FireNow(duration);
    }

    void CancelPending()
    {
        if (_pending != null) { StopCoroutine(_pending); _pending = null; }
    }

    void ResetState()
    {
        CancelPending();
        if (_windRound >= 0 && wind != null) wind.StopWind();
        _armedRound = -1;
        _windRound = -1;
    }
}
