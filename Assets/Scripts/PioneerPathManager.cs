using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pioneer Path 전체 관리자.
///
/// [개념]
///  10×10 타일을 5×5 구역 4개로 나누고, 구역마다 담당 고유색(pioneer)이 먼저 Path 타일을
///  밟아 개방해야 함. 개방된 타일은 이후 모든 고유색이 통과 가능.
///  Trap 타일은 영구 즉사. 길은 1붓그리기로 이어지므로 구역 순서 강제.
///
/// [진행 흐름]
///  Idle → StartPreview()
///  → Previewing: zones 순서대로 각 구역 Path 타일 색상 표시
///  → Challenge: 모든 타일 normalColor. pioneer가 먼저 밟아야 개방
///  실패(즉사) → 씬 리셋 (StageResetOnPlayerDeath)
///
/// [계층 구조]
///  PioneerPathManager  ← 이 컴포넌트
///  ├── PioneerPathZone (pioneerColor = Green)
///  │   ├── PioneerPathTile (Path)
///  │   └── PioneerPathTile (Trap)
///  ├── PioneerPathZone (pioneerColor = Yellow)
///  ├── PioneerPathZone (pioneerColor = Blue)
///  └── PioneerPathZone (pioneerColor = Purple)
///
/// [설정]
///  1. startOnAwake = false (MemoryPathIntroController가 StartPreview 호출)
///  2. zonePreviewDuration: 각 구역 미리보기 시간(초)
///  3. MemoryPathIntroController.pioneerPathManagers[]에 등록
/// </summary>
public class PioneerPathManager : MonoBehaviour
{
    public enum PathState { Idle, Previewing, Challenge }

    [Header("경로 설정")]
    [Tooltip("씬 시작 시 자동으로 미리보기 시작. MemoryPathIntroController 사용 시 false.")]
    public bool startOnAwake = false;

    [Tooltip("각 구역 Path 타일을 보여주는 시간(초)")]
    public float zonePreviewDuration = 0f;

    [Tooltip("구역 전환 사이 암전 대기(초). 0이면 바로 전환.")]
    public float zonePreviewGap = 0f;

    [Header("공통 색상")]
    [Tooltip("Challenge 중 모든 타일 기본 색")]
    [SerializeField] Color normalColor   = new Color(0.45f, 0.45f, 0.45f);

    [Tooltip("pioneer가 개방한 타일 색")]
    [SerializeField] Color unlockedColor = new Color(0.27f, 1f,    0.27f);

    [Tooltip("Trap 타일이 밟혔을 때 색")]
    [SerializeField] Color trapColor     = new Color(1f,    0.2f,  0.2f);

    [Header("이벤트")]
    [Tooltip("Challenge 단계 시작 시 (미리보기 끝난 직후). MemoryPathIntroController가 구독.")]
    public UnityEvent OnChallengeStart;

    PathState        _state;
    PioneerPathZone[] _zones;

    public PathState State => _state;

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Awake()
    {
        _zones = GetComponentsInChildren<PioneerPathZone>(true);
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i] != null)
                _zones[i].Init(this, normalColor, unlockedColor, trapColor);
    }

    void Start()
    {
        ApplyNormalColorsToAll();
        if (startOnAwake) StartPreview();
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
        _state = PathState.Idle;

        if (_zones == null) return;
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i] != null) _zones[i].Restore();

        ApplyNormalColorsToAll();
    }

    // ── 내부 ─────────────────────────────────────────────────────

    IEnumerator PreviewRoutine()
    {
        _state = PathState.Previewing;

        if (_zones != null)
        {
            for (int zi = 0; zi < _zones.Length; zi++)
            {
                if (_zones[zi] == null) continue;

                // 이 구역만 발광
                _zones[zi].ShowPreview();
                yield return new WaitForSeconds(zonePreviewDuration);

                // 발광 끄고 전체 타일을 normalColor로 통일
                // → 앞 구역 위치가 기억에 남지 않도록
                ApplyNormalColorsToAll();

                if (zi < _zones.Length - 1 && zonePreviewGap > 0f)
                    yield return new WaitForSeconds(zonePreviewGap);
            }
        }

        _state = PathState.Challenge;
        OnChallengeStart?.Invoke();
    }

    // ── 내부 유틸 ────────────────────────────────────────────────

    /// <summary>모든 구역의 모든 타일을 normalColor로 통일.</summary>
    void ApplyNormalColorsToAll()
    {
        if (_zones == null) return;
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i] != null) _zones[i].HidePreview();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 미리보기 시작")]
    void Debug_Start() => StartPreview();

    [ContextMenu("테스트: 초기화")]
    void Debug_Reset() => ResetPath();

    void OnDrawGizmos()
    {
        if (_zones == null) return;
        for (int i = 0; i < _zones.Length; i++)
        {
            if (_zones[i] == null) continue;
            Color gc = _zones[i].previewColor;
            gc.a = 0.1f;
            Gizmos.color = gc;
            Gizmos.DrawCube(_zones[i].transform.position, _zones[i].transform.lossyScale);
            gc.a = 0.5f;
            Gizmos.color = gc;
            Gizmos.DrawWireCube(_zones[i].transform.position, _zones[i].transform.lossyScale);
        }
    }
}
