using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 찰린지 매니저 (4인용).
///
/// [동작]
///  스케줄(activateAtSeconds)에 따라 자동 발동:
///   1. 씬의 플레이어 4명을 playerColorType 기준으로 수집
///   2. spawnPoints 풀에서 무작위 4곳 선택
///   3. 각 플레이어 고유색에 맞는 프리팹으로 타일 동시 생성
///   4. timeLimit 안에 4명 모두 자기 타일 위에 서 있으면 OnSuccess
///   5. 시간 초과 시 OnFail (벽 압축 등 연결 가능)
///
/// [스케줄 설정]
///  activateAtSeconds = [10, 30, 60] → 10초, 30초, 60초에 발동
///  loopSchedule = true + schedulePeriod = 90 → 90초 주기로 반복
///
/// [씬 설정]
///  1. 빈 오브젝트들을 타일이 등장할 바닥 위치에 배치 → spawnPoints 등록 (4개 이상)
///  2. tilePrefabs: Blue/Red/Green/Yellow 각 색상별 프리팹 4개 등록
/// </summary>
public class ColorTileChallenge : MonoBehaviour
{
    /// <summary>색상별 프리팹 매핑 항목.</summary>
    [System.Serializable]
    public class ColorTilePrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType = PlayerColorType.Blue;

        [Tooltip("해당 색상의 ColorTile 프리팹 (머티리얼·시각 포함)")]
        public GameObject prefab;
    }

    [Header("타일 프리팹 (색상별 4개 등록)")]
    [Tooltip("Blue / Red / Green / Yellow 각각의 ColorTile 프리팹을 등록.\n" +
             "런타임에 플레이어 고유색을 감지해 해당 프리팹을 사용.")]
    [SerializeField] ColorTilePrefabEntry[] tilePrefabs = new ColorTilePrefabEntry[0];

    [Header("타일 생성")]
    [Tooltip("타일이 등장할 수 있는 위치 풀 (빈 오브젝트 Transform).\n4개 이상 준비 권장 (랜덤성)")]
    [SerializeField] Transform[] spawnPoints = new Transform[0];

    [Header("타이머")]
    [Tooltip("모든 플레이어가 타일에 올라서야 하는 제한 시간(초)")]
    [SerializeField] float timeLimit = 10f;

    [Header("발동 스케줄 (초 단위)")]
    [Tooltip("씬 시작 기준으로 이 초에 찰린지 발동. 예: [10, 30, 60]\n" +
             "비워두면 Activate()를 외부에서 직접 호출해야 함")]
    [SerializeField] float[] activateAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이(초). loopSchedule=true일 때만 사용")]
    [SerializeField] float schedulePeriod = 90f;

    [Header("실패 패널티 — 벽 영구 접근")]
    [Tooltip("찰린지 실패 시 영구적으로 접근할 AdvancingWall 목록.\n" +
             "North / South / West / East 벽 4개 등록 권장.")]
    [SerializeField] AdvancingWall[] penaltyWalls = new AdvancingWall[0];

    [Tooltip("실패 1회당 각 벽이 영구 접근하는 거리(m).\n" +
             "스케줄 이동과 별개로 적용됨.\n" +
             "ex) 스케줄 10 전진 + 패널티 5 → 총 15 전진")]
    [SerializeField] float penaltyAdvanceDistance = 5f;

    [Header("이벤트")]
    [Tooltip("찰린지 시작 시 호출")]
    public UnityEvent OnChallengeStarted;

    [Tooltip("시간 안에 모두 성공 시 호출")]
    public UnityEvent OnSuccess;

    [Tooltip("시간 초과 시 호출 (추가 연출 등 연결 가능)")]
    public UnityEvent OnFail;

    float _remainingTime;
    bool  _isRunning;
    int   _failCount;

    readonly List<ColorTile> _activeTiles = new List<ColorTile>();
    Coroutine _challengeCoroutine;
    Coroutine _scheduleCoroutine;
    float     _scheduleStartTime;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Start()
    {
        if (activateAtSeconds != null && activateAtSeconds.Length > 0)
            StartSchedule();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>찰린지 즉시 시작. 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        if (_isRunning) return;
        if (_challengeCoroutine != null) StopCoroutine(_challengeCoroutine);
        _challengeCoroutine = StartCoroutine(ChallengeRoutine());
    }

    /// <summary>스케줄 수동 시작 (Start에서 자동 호출됨).</summary>
    public void StartSchedule()
    {
        if (_scheduleCoroutine != null) StopCoroutine(_scheduleCoroutine);
        _scheduleStartTime = Time.time;
        _scheduleCoroutine = StartCoroutine(ScheduleRoutine());
    }

    /// <summary>찰린지 강제 종료 + 타일 정리.</summary>
    public void Cancel()
    {
        if (_challengeCoroutine != null) { StopCoroutine(_challengeCoroutine); _challengeCoroutine = null; }
        _isRunning = false;
        ClearTiles();
    }

    // ── 스케줄 ───────────────────────────────────────────────────

    IEnumerator ScheduleRoutine()
    {
        if (activateAtSeconds == null || activateAtSeconds.Length == 0) yield break;

        float cycleOffset = 0f;

        do
        {
            foreach (float t in activateAtSeconds)
            {
                float targetTime = _scheduleStartTime + cycleOffset + t;
                float waitTime   = targetTime - Time.time;

                if (waitTime > 0f)
                    yield return new WaitForSeconds(waitTime);

                Activate();

                // 찰린지가 끝날 때까지 대기 (겹치기 방지)
                while (_isRunning)
                    yield return null;
            }

            cycleOffset += schedulePeriod;

        } while (loopSchedule);
    }

    // ── 찰린지 ───────────────────────────────────────────────────

    IEnumerator ChallengeRoutine()
    {
        _isRunning = true;

        // 1. 플레이어 4명 수집 (색상 모드 무관, playerColorType 기준)
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        var players = new List<Player>();
        foreach (Player p in allPlayers)
        {
            if (!p.IsDead)
                players.Add(p);
        }

        if (players.Count == 0 || spawnPoints.Length == 0 || tilePrefabs.Length == 0)
        {
            _isRunning = false;
            yield break;
        }

        // 2. 스폰 포인트 셔플 (Fisher-Yates)
        int tileCount = Mathf.Min(players.Count, spawnPoints.Length);
        List<Transform> shuffled = new List<Transform>(spawnPoints);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // 3. 색상별 프리팹으로 타일 4개 동시 생성
        ClearTiles();
        for (int i = 0; i < tileCount; i++)
        {
            PlayerColorType colorType = players[i].playerColorType;
            GameObject prefab = GetPrefabForColor(colorType);

            if (prefab == null)
            {
                Debug.LogWarning($"[ColorTileChallenge] {colorType} 색상 프리팹이 등록되지 않았습니다.");
                continue;
            }

            GameObject tileObj = Instantiate(prefab, shuffled[i].position, Quaternion.identity);
            ColorTile tile = tileObj.GetComponent<ColorTile>();
            if (tile == null) tile = tileObj.AddComponent<ColorTile>();
            tile.Setup(colorType);
            _activeTiles.Add(tile);
        }

        // 타일이 하나도 생성되지 않으면 챌린지 진행 불가
        if (_activeTiles.Count == 0)
        {
            Debug.LogWarning("[ColorTileChallenge] 타일이 생성되지 않았습니다. tilePrefabs 등록을 확인하세요.");
            _isRunning = false;
            yield break;
        }

        OnChallengeStarted?.Invoke();


        // 4. 타이머 루프
        _remainingTime = timeLimit;
        while (_remainingTime > 0f)
        {
            _remainingTime -= Time.deltaTime;

            // 모든 타일이 완료됐는지 확인 (타일이 있을 때만 성공 가능)
            bool allDone = _activeTiles.Count > 0;
            foreach (ColorTile t in _activeTiles)
            {
                if (t == null || !t.IsCompleted) { allDone = false; break; }
            }

            if (allDone)
            {
                ClearTiles();
                _remainingTime = 0f;
                _isRunning = false;
                OnSuccess?.Invoke();
                yield break;
            }

            yield return null;
        }

        // 5. 시간 초과 → 타일 즉시 제거 후 패널티
        ClearTiles();
        _isRunning = false;
        _failCount++;
        ApplyPenalty();
        OnFail?.Invoke();
    }

    void OnDisable()
    {
        // 오브젝트 비활성화 시 남아있는 타일 강제 정리
        ClearTiles();
    }

    /// <summary>
    /// 실패 패널티: penaltyWalls 에 등록된 모든 벽을 penaltyAdvanceDistance 만큼 영구 전진.
    /// 스케줄 이동과 별개로 동작하며, 벽이 현재 이동 중이면 완료 후 실행됨.
    /// </summary>
    void ApplyPenalty()
    {
        if (penaltyWalls == null || penaltyWalls.Length == 0) return;
        if (penaltyAdvanceDistance <= 0f) return;

        foreach (AdvancingWall wall in penaltyWalls)
        {
            if (wall != null)
                wall.PermanentAdvance(penaltyAdvanceDistance);
        }

        Debug.Log($"[ColorTileChallenge] 실패 #{_failCount} — 패널티 적용: {penaltyWalls.Length}개 벽 {penaltyAdvanceDistance}m 영구 전진");
    }

    /// <summary>실패 누적 횟수.</summary>
    public int FailCount => _failCount;

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
        {
            if (t == null) continue;
            t.gameObject.SetActive(false); // 즉시 시각적으로 숨김
            Destroy(t.gameObject);         // 프레임 끝에 메모리 해제
        }
        _activeTiles.Clear();
    }

    /// <summary>colorType에 맞는 프리팹 반환. 없으면 null.</summary>
    GameObject GetPrefabForColor(PlayerColorType colorType)
    {
        foreach (ColorTilePrefabEntry entry in tilePrefabs)
        {
            if (entry.colorType == colorType)
                return entry.prefab;
        }
        return null;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 찰린지 시작")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 찰린지 취소")]
    void Debug_Cancel() => Cancel();

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireCube(sp.position, Vector3.one * 0.8f);
        }
    }
}
