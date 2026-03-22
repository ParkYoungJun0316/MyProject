using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색 기억 경로 매니저.
///
/// [진행 흐름]
///  Idle → StartPreview() 호출
///  → Previewing: colorSequence 순서대로 각 색 타일이 colorPreviewDuration 초 동안 발광
///    (Yellow 경로 표시 → 끄기 → Blue 경로 표시 → 끄기 → ...)
///  → Challenge: 모든 타일이 normalColor로 동일하게 보임
///    - 자기 색에 포함된 타일 → 안전 통과
///    - 자기 색에 포함되지 않은 타일 → 즉사 + Failed
///  → 안전 타일을 모두 밟으면 Complete
///
/// [하나의 타일이 여러 색에 속하는 경우]
///  해당 색 미리보기 때 각각 발광.
///  Challenge 중에는 해당 색 플레이어 모두 안전.
///
/// [설정]
///  1. 이 오브젝트 자식으로 타일 배치
///  2. 각 타일에 ColoredMemoryPathTile + Collider 부착
///  3. 각 타일의 safeColors에 안전 색 목록 설정
///  4. colorSequence로 미리보기 순서 지정
/// </summary>
public class ColoredMemoryPath : MonoBehaviour
{
    public enum PathState { Idle, Previewing, Challenge, Complete, Failed }

    [Header("경로 설정")]
    [Tooltip("게임 시작 시 자동으로 미리보기를 시작할지 여부")]
    public bool startOnAwake = true;

    [Tooltip("각 색 경로를 보여주는 시간(초)")]
    public float colorPreviewDuration = 0f;

    [Tooltip("색 전환 사이 짧은 암전 대기(초). 0이면 바로 전환")]
    public float colorPreviewGap = 0f;

    [Tooltip("실패 후 자동 초기화·재시작까지 대기(초). 0 = 자동 재시작 없음")]
    public float autoResetDelay = 0f;

    [Header("미리보기 색 순서")]
    [Tooltip("이 순서대로 색 경로를 하나씩 표시. 원하는 색만 포함 가능")]
    public PlayerColorType[] colorSequence = {
        PlayerColorType.Yellow,
        PlayerColorType.Blue,
        PlayerColorType.Red,
        PlayerColorType.Green,
    };

    [Header("미리보기 색상 (Inspector에서 조정)")]
    public Color yellowPreviewColor = Color.yellow;
    public Color bluePreviewColor   = Color.blue;
    public Color redPreviewColor    = Color.red;
    public Color greenPreviewColor  = Color.green;

    [Header("이벤트")]
    [Tooltip("Challenge 단계 시작 시 (마지막 색 미리보기가 끝난 직후)")]
    public UnityEvent OnChallengeStart;

    [Tooltip("안전 타일을 모두 통과했을 때")]
    public UnityEvent OnCompleted;

    [Tooltip("잘못된 타일을 밟아 즉사했을 때")]
    public UnityEvent OnFailed;

    [Header("Runtime (확인용 — 수정 불가)")]
    [SerializeField] PathState _state;
    [SerializeField] int _safeStepped;
    [SerializeField] int _safeTotalCount;
    [SerializeField] PlayerColorType _currentPreviewColor;

    public PathState State => _state;

    ColoredMemoryPathTile[] _tiles;

    void Awake() => CollectTiles();

    void Start()
    {
        if (startOnAwake) StartPreview();
    }

    void CollectTiles()
    {
        _tiles = GetComponentsInChildren<ColoredMemoryPathTile>(true);
        for (int i = 0; i < _tiles.Length; i++)
            if (_tiles[i] != null) _tiles[i].coloredMemoryPath = this;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>미리보기 시작. Idle 상태일 때만 동작.</summary>
    public void StartPreview()
    {
        if (_state != PathState.Idle) return;
        StartCoroutine(PreviewRoutine());
    }

    /// <summary>전체 초기화 (다시 Idle로)</summary>
    public void ResetPath()
    {
        StopAllCoroutines();
        _state          = PathState.Idle;
        _safeStepped    = 0;
        _safeTotalCount = 0;

        for (int i = 0; i < _tiles.Length; i++)
            if (_tiles[i] != null) _tiles[i].Restore();
    }

    // ── ColoredMemoryPathTile 콜백 ──────────────────────────────

    /// <summary>안전 타일을 올바른 색 플레이어가 밟았을 때 호출</summary>
    public void OnSafeTileStepped(ColoredMemoryPathTile tile, Player player)
    {
        if (_state != PathState.Challenge) return;

        _safeStepped++;
        if (_safeStepped >= _safeTotalCount)
            Complete();
    }

    /// <summary>잘못된 타일을 밟았을 때 호출</summary>
    public void OnWrongTileStepped(ColoredMemoryPathTile tile, Player player)
    {
        if (_state != PathState.Challenge) return;

        _state = PathState.Failed;
        OnFailed?.Invoke();

        if (autoResetDelay > 0f)
            StartCoroutine(AutoResetRoutine());
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator PreviewRoutine()
    {
        _state       = PathState.Previewing;
        _safeStepped = 0;

        if (colorSequence == null || colorSequence.Length == 0)
        {
            EnterChallenge();
            yield break;
        }

        for (int ci = 0; ci < colorSequence.Length; ci++)
        {
            PlayerColorType col = colorSequence[ci];
            _currentPreviewColor = col;
            Color displayColor   = GetDisplayColor(col);

            // 해당 색 타일 발광
            for (int i = 0; i < _tiles.Length; i++)
                if (_tiles[i] != null && _tiles[i].HasColor(col))
                    _tiles[i].ShowForColor(displayColor);

            yield return new WaitForSeconds(colorPreviewDuration);

            // 해당 색 타일 끄기
            for (int i = 0; i < _tiles.Length; i++)
                if (_tiles[i] != null && _tiles[i].HasColor(col))
                    _tiles[i].HidePreview();

            // 마지막 색이 아니면 gap 대기
            if (ci < colorSequence.Length - 1 && colorPreviewGap > 0f)
                yield return new WaitForSeconds(colorPreviewGap);
        }

        EnterChallenge();
    }

    void EnterChallenge()
    {
        // 안전 타일 총수 집계: colorSequence에 포함된 색을 하나라도 가진 타일 수
        _safeTotalCount = 0;
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;

            for (int ci = 0; ci < colorSequence.Length; ci++)
            {
                if (_tiles[i].HasColor(colorSequence[ci]))
                {
                    _safeTotalCount++;
                    break; // 타일 1개당 1번만 카운트
                }
            }
        }

        _state = PathState.Challenge;
        OnChallengeStart?.Invoke();
    }

    IEnumerator AutoResetRoutine()
    {
        yield return new WaitForSeconds(autoResetDelay);
        ResetPath();
        StartPreview();
    }

    void Complete()
    {
        _state = PathState.Complete;
        OnCompleted?.Invoke();
    }

    Color GetDisplayColor(PlayerColorType colorType)
    {
        switch (colorType)
        {
            case PlayerColorType.Yellow: return yellowPreviewColor;
            case PlayerColorType.Blue:   return bluePreviewColor;
            case PlayerColorType.Red:    return redPreviewColor;
            case PlayerColorType.Green:  return greenPreviewColor;
            default: return Color.white;
        }
    }

    /// <summary>에디터 Gizmo용 기본 색상 매핑 (static)</summary>
    public static Color GetDefaultColorFor(PlayerColorType colorType)
    {
        switch (colorType)
        {
            case PlayerColorType.Yellow: return Color.yellow;
            case PlayerColorType.Blue:   return Color.blue;
            case PlayerColorType.Red:    return Color.red;
            case PlayerColorType.Green:  return Color.green;
            case PlayerColorType.Common: return Color.white;
            case PlayerColorType.Danger: return Color.black;
            default: return Color.white;
        }
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 미리보기 시작")]
    void Debug_Start() => StartPreview();

    [ContextMenu("테스트: 초기화")]
    void Debug_Reset() => ResetPath();

    void OnDrawGizmos()
    {
        if (_tiles == null) return;

        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;
            // 타일이 여러 색에 속할 경우 첫 번째 색 기준으로 Gizmo 표시
            var tile = _tiles[i];
            if (tile.safeColors == null || tile.safeColors.Length == 0) continue;

            Color gc = GetDefaultColorFor(tile.safeColors[0]);
            gc.a = 0.2f;
            Gizmos.color = gc;
            Gizmos.DrawCube(tile.transform.position, tile.transform.lossyScale * 0.9f);
        }
    }
}
