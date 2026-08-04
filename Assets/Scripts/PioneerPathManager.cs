using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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

    [Header("미리보기 색상 (Inspector에서 조정)")]
    [SerializeField] Color yellowPreviewColor = Color.yellow;
    [SerializeField] Color bluePreviewColor   = Color.blue;
    [SerializeField] Color purplePreviewColor = new Color(0.55f, 0.2f, 0.95f);
    [SerializeField] Color greenPreviewColor  = Color.green;

    [Header("이벤트")]
    [Tooltip("Challenge 단계 시작 시 (미리보기 끝난 직후). MemoryPathIntroController가 구독.")]
    public UnityEvent OnChallengeStart;

    PathState         _state;
    PioneerPathZone[] _zones;

    // 이번 라운드 4구역에 배정된 pioneer 색 (GameSession 활성색 기준 균등 분배)
    PlayerColorType[] _assignedColors;

    // 해금 네트워크 동기화용 — zone 순서 → zone 내 path 타일 순서로 index 배정(계층 순회라
    // Host/Client 항상 동일 순서, StagePressurePadSetup의 이름순 정렬과 동일한 목적).
    // TStageNetworkBoard.md §3.5 버그 수정 참고.
    readonly List<PioneerPathTile> _networkedPathTiles = new();
    StageNetworkState _netState;

    public PathState State => _state;

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Awake()
    {
        _zones = GetComponentsInChildren<PioneerPathZone>(true);
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i] != null)
                _zones[i].Init(this, normalColor, unlockedColor, trapColor);

        BuildPathTileIndexMap();
    }

    void Start()
    {
        ApplyNormalColorsToAll();
        SetupTileNetworkSync();
        if (startOnAwake) StartPreview();
    }

    void OnDestroy()
    {
        if (_netState != null)
            _netState.OnPioneerTileUnlocked -= HandleTileUnlocked;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>미리보기 시작. Idle 상태일 때만 동작.</summary>
    public void StartPreview()
    {
        if (_state != PathState.Idle) return;
        AssignPioneerColors();
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

    // AssignPioneerColors()가 각 머신에서 독립적으로 로컬 실행되므로, 구역-색 배정은
    // 반드시 세션 시드 기반 결정론 RNG를 써야 Host/Client가 같은 결과를 얻는다
    // (ColorWall/GameSessionWallColorRemap과 동일 버그 클래스 — NetworkDesign.md TStageNetworkBoard §3.3 참고).
    const int PioneerColorSeedSalt = 0x504E5254; // "PNRT"

    /// <summary>
    /// GameSession 활성색 기준으로 4구역 pioneer를 셔플 배정한다.
    ///  - 2인 : [A, A, B, B] 셔플 후 구역별 배정
    ///  - 3인 : [A, A, B, C] 여분은 랜덤 색에
    ///  - 4인 : [A, B, C, D]
    /// </summary>
    void AssignPioneerColors()
    {
        if (_zones == null || _zones.Length == 0) return;

        var rng = new System.Random(NetworkSessionData.Seed ^ PioneerColorSeedSalt);
        _assignedColors = GameSessionColorDistribution.Distribute(_zones.Length, rng);

        // 어떤 구역에 어떤 색이 배정될지 셔플 (같은 rng로 결정론 유지)
        PlayerColorType[] shuffled = (PlayerColorType[])_assignedColors.Clone();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int count = Mathf.Min(shuffled.Length, _zones.Length);
        for (int i = 0; i < count; i++)
        {
            if (_zones[i] != null)
                _zones[i].SetEffectivePioneer(shuffled[i], GetPreviewColorForType(shuffled[i]));
        }
    }

    Color GetPreviewColorForType(PlayerColorType colorType)
    {
        switch (colorType)
        {
            case PlayerColorType.Yellow: return yellowPreviewColor;
            case PlayerColorType.Blue:   return bluePreviewColor;
            case PlayerColorType.Purple: return purplePreviewColor;
            case PlayerColorType.Green:  return greenPreviewColor;
            default: return Color.white;
        }
    }

    /// <summary>모든 구역의 모든 타일을 normalColor로 통일.</summary>
    void ApplyNormalColorsToAll()
    {
        if (_zones == null) return;
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i] != null) _zones[i].HidePreview();
    }

    // ── 타일 해금 네트워크 동기화 (TStageNetworkBoard.md §3.5 버그 수정) ──

    /// <summary>zone 순서 → zone 내 path 타일 순서로 전역 network index를 배정한다(계층 순회라 Host/Client 항상 동일).</summary>
    void BuildPathTileIndexMap()
    {
        _networkedPathTiles.Clear();
        if (_zones == null) return;

        for (int zi = 0; zi < _zones.Length; zi++)
        {
            if (_zones[zi] == null) continue;
            PioneerPathTile[] pathTiles = _zones[zi].PathTiles;
            if (pathTiles == null) continue;

            for (int ti = 0; ti < pathTiles.Length; ti++)
            {
                if (pathTiles[ti] == null) continue;
                pathTiles[ti].networkIndex = _networkedPathTiles.Count;
                _networkedPathTiles.Add(pathTiles[ti]);
            }
        }
    }

    /// <summary>
    /// Host: 슬롯 초기화. 전 머신: 해금 신호 구독 — Host가 감지한 해금만 진실로 확정해
    /// 전 머신(Host 포함)에 브로드캐스트한다(PioneerPathTile.OnCollisionEnter 참고).
    /// </summary>
    void SetupTileNetworkSync()
    {
        _netState = StageNetworkState.Instance;
        if (_netState == null)
        {
            Debug.LogWarning("[PioneerPathManager] StageNetworkState를 찾을 수 없어 타일 네트워크 동기화를 건너뜁니다.");
            return;
        }

        if (!IsClientOnly())
            _netState.InitPioneerTiles(_networkedPathTiles.Count);

        _netState.OnPioneerTileUnlocked += HandleTileUnlocked;
    }

    void HandleTileUnlocked(int index)
    {
        if (index < 0 || index >= _networkedPathTiles.Count) return;
        _networkedPathTiles[index]?.Unlock();
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
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
