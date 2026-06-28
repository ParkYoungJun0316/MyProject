using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 세션 관리자. DontDestroyOnLoad 싱글턴.
/// 활성 색 목록을 기준으로 플레이어 on/off 및 색 오브젝트 필터링 기반을 제공한다.
///
/// [배치 방법]
/// 1. 0.Title 씬에 배치. DontDestroyOnLoad로 모든 씬에서 유지됨.
/// 2. activeColorSlots[]: Inspector 기본값은 참고용. 실제 적용은 아래 방법으로.
///    - 솔로  : LobbyMenuController.ApplySoloColor() → SetActiveColors(1색)
///    - 멀티  : 로비 Ready 완료 후 NGO 스폰 전 → SetActiveColors(접속 색 목록)
/// 3. allPlayers[]: 비워둘 것. 씬 로드 시 자동 수집.
///
/// [씬 전환 시 플레이어 재수집]
/// 새 씬 로드 시 씬 안의 Player를 playerColorType 순으로 자동 수집·재적용.
/// 수동 재적용: Inspector 우클릭 → "테스트: 인원 설정 재적용".
///
/// [다른 스크립트에서 사용]
/// GameSession.Instance.GetActivePlayers()
/// GameSession.Instance.GetActiveColors()
/// GameSession.Instance.IsColorActive(PlayerColorType.Blue)
/// GameSession.Instance.ActivePlayerCount
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("활성 색 (이번 판 참가 색만 선택)")]
    [Tooltip("이번 판에 참가하는 플레이어 색을 모두 등록.\n" +
             "예) Green + Yellow → 2인 Green/Yellow 모드\n" +
             "중복 등록 시 무시됨.")]
    [SerializeField] private PlayerColorType[] activeColorSlots =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    [Header("플레이어 목록")]
    [Tooltip("비워둘 것. 씬 로드 시 자동 수집됨.\n" +
             "에디터에서 M.Stage1 직접 Play할 때만 임시로 등록 가능.")]
    [SerializeField] private Player[] allPlayers;

    // 정렬 기준 (로그·UI 표시용)
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

    /// <summary>활성 색 목록 반환 (ColorOrder 기준 정렬).</summary>
    public IReadOnlyList<PlayerColorType> GetActiveColors()
    {
        var sorted = new List<PlayerColorType>(_activeColors);
        sorted.Sort((a, b) => ColorIndex(a).CompareTo(ColorIndex(b)));
        return sorted;
    }

    /// <summary>활성 색 여부 확인.</summary>
    public bool IsColorActive(PlayerColorType color) => _activeColors.Contains(color);

    /// <summary>
    /// 픽창 또는 외부에서 활성 색을 바꿀 때 호출.
    /// 씬 안의 플레이어에 즉시 재적용된다.
    /// </summary>
    public void SetActiveColors(PlayerColorType[] colors)
    {
        activeColorSlots = colors;
        Player[] found = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Apply(found);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void Apply(Player[] players)
    {
        _activePlayers.Clear();
        _activeColors.Clear();

        // ① activeColorSlots → _activeColors 즉시 반영 (플레이어 유무 무관)
        //    ColoredStartZone 등 다른 시스템이 IsColorActive()를 신뢰할 수 있도록 먼저 채움
        if (activeColorSlots != null)
            foreach (PlayerColorType c in activeColorSlots)
                if (c != PlayerColorType.Common && c != PlayerColorType.Danger)
                    _activeColors.Add(c);

        if (players == null || players.Length == 0)
        {
            Debug.Log($"[GameSession] Player 없음 — 활성 색 등록만 완료: {string.Join(", ", _activeColors)}");
            return;
        }

        foreach (Player p in players)
        {
            if (p == null) continue;

            bool active = _activeColors.Contains(p.playerColorType);

            // ② NetworkObject가 붙은 플레이어는 PlayerSpawnManager가 스폰을 담당.
            //    SetActive 제어를 GameSession이 하면 정상 플레이어가 꺼질 수 있으므로 제외.
            bool isNetworkPlayer = p.GetComponent<Unity.Netcode.NetworkObject>() != null;
            if (!isNetworkPlayer)
                p.gameObject.SetActive(active);

            if (active)
                _activePlayers.Add(p);
        }

        Debug.Log($"[GameSession] {_activePlayers.Count}인 모드 적용 — 활성 색: {string.Join(", ", _activeColors)}");
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

    [ContextMenu("테스트: 2인 Green+Yellow")]
    void Debug_2P_GreenYellow() =>
        SetActiveColors(new[] { PlayerColorType.Green, PlayerColorType.Yellow });

    [ContextMenu("테스트: 2인 Blue+Purple")]
    void Debug_2P_BluePurple() =>
        SetActiveColors(new[] { PlayerColorType.Blue, PlayerColorType.Purple });

    [ContextMenu("테스트: 4인 전체")]
    void Debug_4P() =>
        SetActiveColors(new[] { PlayerColorType.Blue, PlayerColorType.Purple,
                                PlayerColorType.Green, PlayerColorType.Yellow });
#endif
}
