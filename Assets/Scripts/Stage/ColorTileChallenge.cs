using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 찰린지 매니저.
///
/// [동작]
///  Activate() 호출 시:
///   1. 씬에서 고유색 플레이어를 수집
///   2. spawnPoints 풀에서 무작위로 위치 선택
///   3. 각 플레이어 고유색 타일 생성
///   4. timeLimit 안에 모든 플레이어가 자기 타일 위에 서 있으면 OnSuccess
///   5. 시간 초과 시 OnFail (벽 압축 등 연결 가능)
///
/// [설정]
///  1. 빈 오브젝트들을 타일이 등장할 바닥 위치에 배치 → spawnPoints에 등록
///  2. tilePrefab: ColorTile 스크립트 + Collider(Trigger) + 시각적 메시가 있는 프리팹
///  3. PlayerTriggerZone.OnPlayerEnter → Activate() 연결
/// </summary>
public class ColorTileChallenge : MonoBehaviour
{
    [Header("타일 생성")]
    [Tooltip("타일이 등장할 수 있는 위치 풀 (빈 오브젝트 Transform).\n플레이어 수 이상으로 준비할 것")]
    [SerializeField] Transform[] spawnPoints = new Transform[0];

    [Tooltip("ColorTile 스크립트가 붙은 타일 프리팹")]
    [SerializeField] GameObject tilePrefab;

    [Header("타이머")]
    [Tooltip("모든 플레이어가 타일에 올라서야 하는 제한 시간(초)")]
    [SerializeField] float timeLimit = 10f;

    [Header("이벤트")]
    [Tooltip("시간 안에 모두 성공 시 호출")]
    public UnityEvent OnSuccess;

    [Tooltip("시간 초과 시 호출 (벽 압축 등 연결)")]
    public UnityEvent OnFail;

    [Tooltip("찰린지 시작 시 호출")]
    public UnityEvent OnChallengeStarted;

    [Header("Runtime (확인용)")]
    [SerializeField] float _remainingTime;
    [SerializeField] bool  _isRunning;

    readonly List<ColorTile> _activeTiles = new List<ColorTile>();
    Coroutine _challengeCoroutine;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>찰린지 시작. PlayerTriggerZone.OnPlayerEnter에 연결.</summary>
    public void Activate()
    {
        if (_isRunning) return;
        if (_challengeCoroutine != null) StopCoroutine(_challengeCoroutine);
        _challengeCoroutine = StartCoroutine(ChallengeRoutine());
    }

    /// <summary>찰린지 강제 종료 + 타일 정리.</summary>
    public void Cancel()
    {
        if (_challengeCoroutine != null)
        {
            StopCoroutine(_challengeCoroutine);
            _challengeCoroutine = null;
        }
        _isRunning = false;
        ClearTiles();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator ChallengeRoutine()
    {
        _isRunning = true;

        // 1. 고유색 플레이어 수집
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        var uniquePlayers = new List<Player>();
        foreach (Player p in allPlayers)
        {
            if (!p.IsDead && p.isUniqueColor)
                uniquePlayers.Add(p);
        }

        if (uniquePlayers.Count == 0 || spawnPoints.Length == 0 || tilePrefab == null)
        {
            _isRunning = false;
            yield break;
        }

        // 2. 랜덤 스폰 포인트 선택 (플레이어 수만큼)
        int tileCount = Mathf.Min(uniquePlayers.Count, spawnPoints.Length);
        List<Transform> shuffled = new List<Transform>(spawnPoints);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // 3. 타일 생성
        ClearTiles();
        for (int i = 0; i < tileCount; i++)
        {
            GameObject tileObj = Instantiate(tilePrefab, shuffled[i].position, Quaternion.identity);
            ColorTile tile = tileObj.GetComponent<ColorTile>();
            if (tile == null) tile = tileObj.AddComponent<ColorTile>();
            tile.Setup(uniquePlayers[i].playerColorType);
            _activeTiles.Add(tile);
        }

        OnChallengeStarted?.Invoke();

        // 4. 타이머 루프
        _remainingTime = timeLimit;
        while (_remainingTime > 0f)
        {
            _remainingTime -= Time.deltaTime;

            // 모든 타일 완료 확인
            bool allDone = true;
            foreach (ColorTile t in _activeTiles)
            {
                if (t == null || !t.IsCompleted) { allDone = false; break; }
            }

            if (allDone)
            {
                ClearTiles();
                _isRunning = false;
                _remainingTime = 0f;
                OnSuccess?.Invoke();
                yield break;
            }

            yield return null;
        }

        // 5. 시간 초과
        ClearTiles();
        _isRunning = false;
        OnFail?.Invoke();
    }

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
            if (t != null) Destroy(t.gameObject);
        _activeTiles.Clear();
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
