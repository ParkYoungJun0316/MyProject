using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 기억 경로 매니저.
///
/// [진행 흐름]
///  Idle → StartPreview() 호출
///  → Previewing: Safe 발판이 previewDuration 초 동안 빛남
///  → Challenge: 모든 발판이 동일하게 보임. 플레이어가 경로를 걸어가야 함
///     - Safe 발판 → 통과
///     - Trap 발판 → 사라지고 낙사
///  → 모든 Safe 발판을 밟으면 Complete (OnCompleted 발동)
///
/// [설정]
///  1. 이 오브젝트 자식으로 발판들 배치
///  2. 각 발판에 MemoryPathTile + Collider 부착 후 role(Safe/Trap) 설정
///  3. previewDuration으로 경로 표시 시간 조정
///  4. StartPreview()를 원하는 타이밍에 호출 (트리거, 버튼, UnityEvent 등)
/// </summary>
public class MemoryPath : MonoBehaviour
{
    public enum PathState { Idle, Previewing, Challenge, Complete, Failed }

    [Header("경로 설정")]
    [Tooltip("경로를 보여주는 시간(초). 이 시간이 지나면 발판이 전부 같은 색으로 변함")]
    public float previewDuration = 3f;

    [Tooltip("Safe 발판을 전부 밟지 않아도 됨. true면 Trap만 안 밟으면 Complete")]
    public bool completeOnNoTrap = false;

    [Header("이벤트")]
    [Tooltip("Challenge 단계 시작 시 (미리보기 끝난 직후)")]
    public UnityEvent OnChallengeStart;

    [Tooltip("Safe 발판을 모두 통과했을 때")]
    public UnityEvent OnCompleted;

    [Tooltip("Trap 발판을 밟아 낙사했을 때")]
    public UnityEvent OnFailed;

    [Header("Runtime (확인용)")]
    [SerializeField] PathState _state;
    [SerializeField] int _safeStepped;

    public PathState State => _state;

    List<MemoryPathTile> _safeTiles = new();
    List<MemoryPathTile> _trapTiles = new();

    void Awake()
    {
        CollectTiles();
    }

    void CollectTiles()
    {
        _safeTiles.Clear();
        _trapTiles.Clear();

        var all = GetComponentsInChildren<MemoryPathTile>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            all[i].memoryPath = this;

            if (all[i].role == MemoryPathTile.TileRole.Safe)
                _safeTiles.Add(all[i]);
            else
                _trapTiles.Add(all[i]);
        }
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>경로 미리보기 시작. Idle 상태일 때만 동작.</summary>
    public void StartPreview()
    {
        if (_state != PathState.Idle) return;
        StartCoroutine(PreviewRoutine());
    }

    /// <summary>경로 전체 초기화 (다시 Idle로)</summary>
    public void ResetPath()
    {
        StopAllCoroutines();
        _safeStepped = 0;
        _state       = PathState.Idle;

        for (int i = 0; i < _safeTiles.Count; i++)
            if (_safeTiles[i] != null) _safeTiles[i].Restore();
        for (int i = 0; i < _trapTiles.Count; i++)
            if (_trapTiles[i] != null) _trapTiles[i].Restore();
    }

    // ── MemoryPathTile 콜백 ──────────────────────────────────────

    /// <summary>Safe 발판을 밟았을 때 MemoryPathTile이 호출</summary>
    public void OnSafeTileStepped(MemoryPathTile tile)
    {
        if (_state != PathState.Challenge) return;

        _safeStepped++;
        if (_safeStepped >= _safeTiles.Count)
            Complete();
    }

    /// <summary>Trap 발판을 밟았을 때 MemoryPathTile이 호출</summary>
    public void OnTrapStepped(MemoryPathTile tile)
    {
        if (_state != PathState.Challenge) return;

        _state = PathState.Failed;
        OnFailed?.Invoke();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator PreviewRoutine()
    {
        _state       = PathState.Previewing;
        _safeStepped = 0;

        // Safe 발판만 빛나게
        for (int i = 0; i < _safeTiles.Count; i++)
            if (_safeTiles[i] != null) _safeTiles[i].ShowPreview();

        yield return new WaitForSeconds(previewDuration);

        // 미리보기 종료: 전부 같은 색으로
        for (int i = 0; i < _safeTiles.Count; i++)
            if (_safeTiles[i] != null) _safeTiles[i].HidePreview();

        _state = PathState.Challenge;
        OnChallengeStart?.Invoke();
    }

    void Complete()
    {
        _state = PathState.Complete;
        OnCompleted?.Invoke();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 미리보기 시작")]
    void Debug_Start() => StartPreview();

    [ContextMenu("테스트: 초기화")]
    void Debug_Reset() => ResetPath();

    void OnDrawGizmos()
    {
        // Safe = 노란색, Trap = 빨간색 구분 표시 (에디터에서만)
        var all = GetComponentsInChildren<MemoryPathTile>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            Gizmos.color = all[i].role == MemoryPathTile.TileRole.Safe
                ? new Color(1f, 0.85f, 0f, 0.25f)
                : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawCube(all[i].transform.position, all[i].transform.lossyScale * 0.9f);
        }
    }
}
