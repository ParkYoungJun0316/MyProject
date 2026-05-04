using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// BoulderSpawner(레인)를 셔플 풀 방식으로 순서 없이 중복 없이 선택해 랜덤 간격으로 스폰.
///
/// [동작]
/// BeginSpawning() 호출 → initialDelay 후 → 풀 셔플 → 하나씩 SpawnOne() → 풀 소진되면 재셔플 → 반복
/// 풀 소진 전 같은 레인이 두 번 연속 나오지 않음.
///
/// [스폰 정지]
/// 플레이어가 stopTriggerPoint 반경 안에 처음 들어오면 스폰 루프만 중단.
/// 이미 나간 바위는 SpinRoller·lifetime 설정대로 끝까지 이동.
///
/// [씬 구성]
/// BoulderSpawnManager
/// ├─ BoulderSpawner_Left   (레인 A)
/// └─ BoulderSpawner_Right  (레인 B)
/// </summary>
public class BoulderSpawnManager : MonoBehaviour
{
    [Header("레인 스포너 목록")]
    [Tooltip("레인별 BoulderSpawner를 순서 무관하게 등록. 셔플 풀로 중복 없이 선택")]
    [SerializeField] BoulderSpawner[] spawners = null;

    [Header("스폰 루프")]
    [Tooltip("켜면 OnEnable 시 BeginSpawning() 자동 호출")]
    [SerializeField] bool startSpawningOnEnable = false;

    [Tooltip("첫 스폰 전 대기(초)")]
    [SerializeField] float initialDelay = 0f;

    [Tooltip("스폰 사이 최소 대기(초)")]
    [SerializeField] float intervalMin = 0f;

    [Tooltip("스폰 사이 최대 대기(초). min과 같으면 고정 간격")]
    [SerializeField] float intervalMax = 0f;

    [Header("스폰 정지 — 통과 지점")]
    [Tooltip("플레이어가 이 Transform 반경 안에 처음 들어오면 스폰 루프 정지")]
    [SerializeField] Transform stopTriggerPoint = null;

    [Tooltip("통과 판정 반경(m). 0이면 정지 기능 비활성")]
    [SerializeField] float stopTriggerRadius = 0f;

    [Header("이벤트")]
    public UnityEvent onSpawnLoopStarted;
    public UnityEvent onSpawnStoppedByPlayer;

    Coroutine _loopCoroutine;
    bool      _stopRequested;
    bool      _triggerFired;
    Player[]  _players;

    // 셔플 풀
    int[] _pool;
    int   _poolIndex;

    // ── Unity 생명주기 ─────────────────────────────────────────

    void OnEnable()
    {
        _triggerFired = false;
        if (startSpawningOnEnable)
            BeginSpawning();
    }

    void OnDisable() => StopInternal();

    void Update()
    {
        if (_triggerFired) return;
        if (stopTriggerPoint == null || stopTriggerRadius <= 0f) return;
        if (_loopCoroutine == null) return;

        if (AnyPlayerInTrigger())
        {
            _triggerFired = true;
            StopInternal();
            onSpawnStoppedByPlayer?.Invoke();
        }
    }

    // ── 외부 API ───────────────────────────────────────────────

    /// <summary>랜덤 간격 스폰 루프 시작. 이미 돌고 있으면 무시.</summary>
    public void BeginSpawning()
    {
        if (!isActiveAndEnabled) return;
        if (spawners == null || spawners.Length == 0) return;
        if (_loopCoroutine != null) return;

        _stopRequested = false;
        BuildPool();
        _loopCoroutine = StartCoroutine(SpawnLoop());
        onSpawnLoopStarted?.Invoke();
    }

    /// <summary>BeginSpawning과 동일(UnityEvent 연결용).</summary>
    public void Spawn() => BeginSpawning();

    /// <summary>스폰 루프만 중단. 이미 나간 바위는 그대로.</summary>
    public void StopSpawningLoop() => StopInternal();

    // ── 내부 ───────────────────────────────────────────────────

    void StopInternal()
    {
        _stopRequested = true;
        if (_loopCoroutine != null)
        {
            StopCoroutine(_loopCoroutine);
            _loopCoroutine = null;
        }
    }

    IEnumerator SpawnLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (!_stopRequested)
        {
            int idx = NextPoolIndex();
            if (spawners[idx] != null)
                spawners[idx].SpawnOne();

            float lo   = Mathf.Min(intervalMin, intervalMax);
            float hi   = Mathf.Max(intervalMin, intervalMax);
            float wait = lo < hi ? Random.Range(lo, hi) : lo;

            if (wait > 0f)
                yield return new WaitForSeconds(wait);
        }

        _loopCoroutine = null;
    }

    // ── 셔플 풀 ────────────────────────────────────────────────

    void BuildPool()
    {
        int n = spawners.Length;
        _pool = new int[n];
        for (int i = 0; i < n; i++) _pool[i] = i;
        Shuffle(_pool);
        _poolIndex = 0;
    }

    int NextPoolIndex()
    {
        if (_poolIndex >= _pool.Length)
        {
            Shuffle(_pool);
            _poolIndex = 0;
        }
        return _pool[_poolIndex++];
    }

    static void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = arr[i];
            arr[i] = arr[j];
            arr[j] = tmp;
        }
    }

    // ── 통과 판정 ──────────────────────────────────────────────

    bool AnyPlayerInTrigger()
    {
        RefreshPlayers();
        float rSq = stopTriggerRadius * stopTriggerRadius;
        Vector3 c = stopTriggerPoint.position;

        foreach (Player p in _players)
        {
            if (p == null || p.IsDead) continue;
            Vector3 a = p.transform.position;
            a.y = c.y;
            if ((a - c).sqrMagnitude <= rSq) return true;
        }

        return false;
    }

    void RefreshPlayers()
    {
        if (_players == null || _players.Length == 0)
            _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (stopTriggerPoint == null || stopTriggerRadius <= 0f) return;
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawWireSphere(stopTriggerPoint.position, stopTriggerRadius);
    }
#endif
}
