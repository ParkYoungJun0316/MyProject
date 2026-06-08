using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 세션 관리자. DontDestroyOnLoad 싱글턴.
/// 인원 수와 활성 플레이어·색 목록을 한곳에서 관리한다.
///
/// [배치 방법]
/// 1. M.Stage1 씬에만 배치. DontDestroyOnLoad로 모든 씬에서 유지됨.
/// 2. playerCount: 2 / 3 / 4 입력
/// 3. allPlayers[]: 씬에 배치된 Player를 색 순서대로 등록
///    (Blue → Purple → Green → Yellow 순)
///
/// [씬 전환 시 플레이어 재수집]
/// 새 씬 로드 시 씬 안의 Player를 playerColorType 순으로 자동 수집·재적용.
/// 각 씬에 Player 오브젝트가 Blue→Purple→Green→Yellow 순으로 배치되어 있어야 함.
/// 수동으로 재적용하려면 Inspector 우클릭 → "테스트: 인원 설정 재적용".
///
/// [다른 스크립트에서 사용]
/// GameSession.Instance.GetActivePlayers()
/// GameSession.Instance.IsColorActive(PlayerColorType.Blue)
/// GameSession.Instance.ActivePlayerCount
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("인원 설정")]
    [Tooltip("실제 플레이 인원. 2 / 3 / 4 중 하나 입력.")]
    [SerializeField, Range(1, 4)] private int playerCount = 4;

    [Header("플레이어 목록 (색 순서대로 등록, M.Stage1 전용)")]
    [Tooltip("M.Stage1 씬의 Player 오브젝트. Blue → Purple → Green → Yellow 순.\n" +
             "다른 씬은 자동 수집됨.")]
    [SerializeField] private Player[] allPlayers;

    // 활성 색 정렬 기준 (앞 N색 활성 규칙에 사용)
    private static readonly PlayerColorType[] ColorOrder =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    // ── 런타임 상태 ───────────────────────────────────────────────

    private readonly List<Player>             _activePlayers = new List<Player>();
    private readonly HashSet<PlayerColorType> _activeColors  = new HashSet<PlayerColorType>();

    // ── 프로퍼티 ──────────────────────────────────────────────────

    public int ActivePlayerCount => _activePlayers.Count;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Apply(allPlayers);
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // 씬 전환 시 새 씬의 Player를 자동 수집해 재적용
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // DontDestroyOnLoad 씬이면 무시
        if (scene.name == "DontDestroyOnLoad") return;

        // 활성 플레이어 참조가 유효하면 Awake에서 이미 올바르게 셋업된 것 → 재수집 불필요
        // (씬 전환/리로드 후에는 오브젝트가 파괴되어 null이 되므로 그때만 재수집)
        if (_activePlayers.Count > 0 && _activePlayers[0] != null) return;

        Player[] found = FindObjectsByType<Player>(FindObjectsSortMode.None);
        if (found.Length == 0) return;

        // playerColorType 순으로 정렬 (ColorOrder 기준)
        System.Array.Sort(found, (a, b) =>
            ColorIndex(a.playerColorType).CompareTo(ColorIndex(b.playerColorType)));

        Apply(found);
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>활성 플레이어 목록 반환.</summary>
    public IReadOnlyList<Player> GetActivePlayers() => _activePlayers;

    /// <summary>활성 색 여부 확인.</summary>
    public bool IsColorActive(PlayerColorType color) => _activeColors.Contains(color);

    // ── 내부 ──────────────────────────────────────────────────────

    void Apply(Player[] players)
    {
        _activePlayers.Clear();
        _activeColors.Clear();

        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("[GameSession] 적용할 Player가 없습니다.");
            return;
        }

        int count = Mathf.Clamp(playerCount, 1, players.Length);

        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            if (p == null) continue;

            bool active = i < count;
            p.gameObject.SetActive(active);

            if (active)
            {
                _activePlayers.Add(p);
                _activeColors.Add(p.playerColorType);
            }
        }

        Debug.Log($"[GameSession] {count}인 모드 적용 — 활성 색: {string.Join(", ", _activeColors)}");
    }

    static int ColorIndex(PlayerColorType type)
    {
        for (int i = 0; i < ColorOrder.Length; i++)
            if (ColorOrder[i] == type) return i;
        return int.MaxValue;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 인원 설정 재적용")]
    void Debug_Apply() => Apply(allPlayers);

    [ContextMenu("테스트: 활성 플레이어 콘솔 출력")]
    void Debug_Print()
    {
        Debug.Log($"[GameSession] ActivePlayerCount = {ActivePlayerCount}");
        foreach (Player p in _activePlayers)
            Debug.Log($"  → {p.name} / {p.playerColorType}");
    }
#endif
}
