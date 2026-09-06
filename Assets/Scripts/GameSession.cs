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

    // ── 런타임 상태 ───────────────────────────────────────────────

    private readonly List<Player>             _activePlayers  = new List<Player>();
    private readonly HashSet<PlayerColorType> _activeColors   = new HashSet<PlayerColorType>();

    // 씬 인트로 대화를 이미 본 키 목록 (사망 리로드 후 재표시 방지)
    private readonly HashSet<string> _seenIntroKeys = new HashSet<string>();

    // 이번 판 확정 CheerName. 인덱스 = colorIndex (0=Blue 1=Purple 2=Green 3=Yellow).
    // 미설정 시 PlayerColorUtil.DefaultCheerNames 기본값 반환.
    private string[] _sessionCheerNames;

    /// <summary>Host가 안 건드리면 이 값. CheerService._teamCheerWord 기본값과 동일.</summary>
    public const string DefaultTeamCheerWord = "fighting";

    // 이번 판 확정 TeamCheerWord. null = 미확정(게이트 전) → Get은 DefaultTeamCheerWord 폴백.
    private string _sessionTeamCheerWord;

    // 이번 판 확정 Steam 표시 이름(DisplayName). 인덱스 = colorIndex.
    // 로비 LobbyPlayerState.DisplayName을 게임 시작 시 1회 그대로 옮겨온 것 — 별도 네트워크 갱신 없음.
    private string[] _sessionDisplayNames;

    // 이번 판 확정 Dissonance VoiceId(LocalPlayerName). 인덱스 = colorIndex.
    // 로비 LobbyPlayerState.VoiceId를 게임 시작 시 1회 그대로 옮겨온 것 — DisplayName과 동일 패턴.
    // OptionsTeamVoicePanel이 DissonanceComms.FindPlayer(voiceId)로 팀원 VoicePlayerState를 찾는 키.
    private string[] _sessionVoiceIds;

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

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshPlayersOnReady;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "DontDestroyOnLoad") return;

        // 플레이어가 스폰되는 씬에서만 재수집. 타이틀은 플레이어 참조 불필요.
        // 판정은 PlayerSpawnManager.IsStageScene SSOT — 예전엔 여기 "Stage"/"Boss" 문자열 검사를
        // 따로 들고 있었는데, Interlude처럼 새 씬이 한쪽에만 추가되면 이 씬에서 재수집이 안 돌아
        // 이전 씬의 파괴된 Player 참조가 _activePlayers에 남는다(2026-09-06 리뷰).
        if (!PlayerSpawnManager.IsStageScene(scene.name)) return;

        // Coordinator(DDOL)는 StartGameServerRpc에서 LoadScene보다 먼저 스폰되므로
        // 씬 로드 시점에 이미 올바른 색 데이터를 보유한다 (NGO FIFO 보장).
        // 즉시 읽어서 _activeColors를 확정 → Start()에서 IsColorActive()를 호출하는
        // 모든 UI가 씬 로드 직후부터 올바른 값을 얻을 수 있다.
        var coordColors = PlayerSpawnCoordinator.GetActiveColors();
        if (coordColors.Length > 0)
            SetActiveColors(coordColors);   // activeColorSlots + _activeColors 즉시 확정
        else
        {
            _activePlayers.Clear();
            _activeColors.Clear();
        }

        PlayerSpawnCoordinator.OnPlayersReady -= RefreshPlayersOnReady;
        PlayerSpawnCoordinator.OnPlayersReady += RefreshPlayersOnReady;

        // [버그 수정 2026-07-28] PlayerSpawnCoordinator의 표준 구독 패턴(IsReady 늦은 구독 대비)이
        // 여기만 빠져 있었다. OnPlayersReady가 이 구독보다 먼저 발행돼버리면 _activePlayers가
        // 그 씬 내내 빈 채로 남아 GetActivePlayers() 의존 로직(ColorTileChallenge 등)이 조용히
        // 아무것도 안 하는 채로 실패했다 — M.Stage3 ColorTile 미생성 버그의 근본 원인.
        if (PlayerSpawnCoordinator.IsReady)
            RefreshPlayersOnReady();
    }

    void RefreshPlayersOnReady()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshPlayersOnReady;

        // OnPlayersReady 시점에 PlayerSpawnCoordinator(NetworkList)에서 확정 색을 읽어 재적용.
        var confirmedColors = PlayerSpawnCoordinator.GetActiveColors();
        SetActiveColors(confirmedColors.Length > 0 ? confirmedColors : activeColorSlots);
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

    // ── 세션 CheerName ─────────────────────────────────────────────

    /// <summary>
    /// true = SetSessionCheerNames가 이미 호출됨(Tutorial 게이트 통과 후, §6B.7 P5).
    /// CheerService.GetCheerName/GetColorIndex가 "확정 세션값 vs 게이트 전 실시간값" 중 어느 쪽을
    /// 우선할지 판단하는 용도 — GetSessionCheerName 자체는 미확정 시에도 기본값으로 폴백해버려서
    /// 값만으로는 확정 여부를 구분할 수 없기 때문에 별도 플래그가 필요하다.
    /// </summary>
    public bool HasSessionCheerNames => _sessionCheerNames != null;

    /// <summary>
    /// 이번 판 확정 CheerName 배열 저장.
    /// 인덱스 = colorIndex (0=Blue 1=Purple 2=Green 3=Yellow).
    /// StartGame 직전 Host 로컬·Client 양쪽에서 동일하게 호출.
    /// </summary>
    public void SetSessionCheerNames(string[] names)
    {
        _sessionCheerNames = names;
        Debug.Log($"[GameSession] 세션 CheerName 적용: {string.Join(", ", names)}");
    }

    /// <summary>colorIndex → 이번 판 CheerName. 세션 미설정 시 PlayerColorUtil.DefaultCheerNames 기본값.</summary>
    public string GetSessionCheerName(int colorIndex)
    {
        if (_sessionCheerNames != null && colorIndex >= 0 && colorIndex < _sessionCheerNames.Length)
            return _sessionCheerNames[colorIndex];
        var defaults = PlayerColorUtil.DefaultCheerNames;
        if (colorIndex >= 0 && colorIndex < defaults.Length)
            return defaults[colorIndex];
        return string.Empty;
    }

    /// <summary>이름 → colorIndex. 세션 이름 우선, 없으면 PlayerColorUtil.DefaultCheerNames 기본값. 미매칭 시 -1.</summary>
    public int GetSessionColorIndex(string cheerName)
    {
        string lower = cheerName.Trim().ToLower();
        if (_sessionCheerNames != null)
        {
            for (int i = 0; i < _sessionCheerNames.Length; i++)
                if (_sessionCheerNames[i] == lower) return i;
        }
        return System.Array.IndexOf(PlayerColorUtil.DefaultCheerNames, lower);
    }

    // ── 세션 TeamCheerWord ─────────────────────────────────────────

    /// <summary>
    /// true = SetSessionTeamCheerWord가 이미 호출됨(Tutorial 게이트 통과 후).
    /// CheerService 스폰 시 세션값을 NV에 넣을지, Inspector/기본값("fighting")을 쓸지 구분용.
    /// </summary>
    public bool HasSessionTeamCheerWord => _sessionTeamCheerWord != null;

    /// <summary>
    /// 이번 판 확정 TeamCheerWord 저장. StartGame 직전 Host 로컬·Client 양쪽에서 CheerName과 동일하게 호출.
    /// </summary>
    public void SetSessionTeamCheerWord(string word)
    {
        _sessionTeamCheerWord = string.IsNullOrEmpty(word)
            ? DefaultTeamCheerWord
            : word.Trim().ToLowerInvariant();
        Debug.Log($"[GameSession] 세션 TeamCheerWord 적용: {_sessionTeamCheerWord}");
    }

    /// <summary>이번 판 TeamCheerWord. 세션 미설정 시 DefaultTeamCheerWord.</summary>
    public string GetSessionTeamCheerWord()
        => string.IsNullOrEmpty(_sessionTeamCheerWord) ? DefaultTeamCheerWord : _sessionTeamCheerWord;

    // ── 세션 Steam 표시 이름 ───────────────────────────────────────

    // HasSessionDisplayNames는 삭제됨(2026-09-05). 배열이 있어도 개별 색 슬롯은 빈 값일 수 있어
    // (미보고 플레이어) "확정됨"의 의미가 슬롯 단위가 아니라 배열 단위였고, 그걸 게이트로 쓰면
    // 미보고 슬롯이 실시간 NV 폴백을 못 타고 플레이스홀더로 고착됐다. 이제 표시 이름은
    // GetSessionDisplayName의 반환값이 비었는지로만 판단한다(CheerName 쪽 HasSessionCheerNames와
    // 다른 이유: CheerName은 미확정 폴백이 항상 유효한 색 기본값이라 값만으로 구분이 안 된다).

    /// <summary>
    /// 이번 판 확정 Steam 표시 이름 배열 저장.
    /// 인덱스 = colorIndex (0=Blue 1=Purple 2=Green 3=Yellow).
    /// StartGame 직전 Host 로컬·Client 양쪽에서 CheerName과 동일하게 1회만 호출됨(런타임 갱신 없음).
    /// </summary>
    public void SetSessionDisplayNames(string[] names)
    {
        _sessionDisplayNames = names;
        Debug.Log($"[GameSession] 세션 표시 이름 적용: {string.Join(", ", names)}");
    }

    /// <summary>
    /// colorIndex → 이번 판 Steam 표시 이름. 미설정/빈 값이면 빈 문자열(= 확정값 없음).
    ///
    /// 예전엔 여기서 "Player"를 폴백으로 돌려줬는데, 그게 non-empty라서 호출자의
    /// "확정값 → 없으면 실시간 NV" 폴백 체인(TeamStatusUI.GetPlayerDisplayName)이
    /// 도달 불가한 죽은 코드가 됐다 — 게이트 순간 한 명의 NV 보고가 안 와 있으면 그 판 내내
    /// "Player"로 고착됐다(2026-09-05, Steam 4인 테스트). 플레이스홀더 표기는 UI가 정한다.
    /// </summary>
    public string GetSessionDisplayName(int colorIndex)
    {
        if (_sessionDisplayNames != null && colorIndex >= 0 && colorIndex < _sessionDisplayNames.Length
            && !string.IsNullOrEmpty(_sessionDisplayNames[colorIndex]))
            return _sessionDisplayNames[colorIndex];
        return string.Empty;
    }

    // ── 세션 Dissonance VoiceId ─────────────────────────────────────

    /// <summary>
    /// 이번 판 확정 Dissonance VoiceId 배열 저장.
    /// 인덱스 = colorIndex (0=Blue 1=Purple 2=Green 3=Yellow).
    /// StartGame 직전 Host 로컬·Client 양쪽에서 DisplayName과 동일하게 1회만 호출됨(런타임 갱신 없음).
    /// </summary>
    public void SetSessionVoiceIds(string[] ids)
    {
        _sessionVoiceIds = ids;
        Debug.Log($"[GameSession] 세션 VoiceId 적용 완료 — Blue={(string.IsNullOrEmpty(ids[0]) ? "빈값" : "매칭됨")} Purple={(string.IsNullOrEmpty(ids[1]) ? "빈값" : "매칭됨")} Green={(string.IsNullOrEmpty(ids[2]) ? "빈값" : "매칭됨")} Yellow={(string.IsNullOrEmpty(ids[3]) ? "빈값" : "매칭됨")}");
    }

    /// <summary>colorIndex → 이번 판 Dissonance VoiceId. 미설정/빈 값이면 null(매칭 불가로 취급).</summary>
    public string GetSessionVoiceId(int colorIndex)
    {
        if (_sessionVoiceIds != null && colorIndex >= 0 && colorIndex < _sessionVoiceIds.Length
            && !string.IsNullOrEmpty(_sessionVoiceIds[colorIndex]))
            return _sessionVoiceIds[colorIndex];
        return null;
    }

    // ── 인트로 대화 본 여부 ───────────────────────────────────────

    /// <summary>해당 씬 키의 인트로 대화를 이미 봤는지 확인.</summary>
    public bool IsIntroSeen(string key) => _seenIntroKeys.Contains(key);

    /// <summary>해당 씬 키의 인트로 대화를 봤다고 기록.</summary>
    public void MarkIntroSeen(string key) => _seenIntroKeys.Add(key);

    /// <summary>
    /// 활성 플레이어·색 목록 초기화. TitleReturnFlow에서 호출.
    /// 타이머·채팅 초기화는 TitleReturnFlow가 직접 처리한다.
    /// activeColorSlots는 다음 판의 SetActiveColors()가 덮어씀.
    /// </summary>
    public void ResetSession()
    {
        _activePlayers.Clear();
        _activeColors.Clear();
        _seenIntroKeys.Clear();
        _sessionCheerNames = null;
        _sessionTeamCheerWord = null;
        _sessionDisplayNames = null;
        _sessionVoiceIds = null;

        Debug.Log("[GameSession] 세션 런타임 상태 리셋 완료");
    }

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
                if (PlayerColorUtil.IsUniquePlayerColor(c))
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

        // FindObjectsByType(None)은 순서를 보장하지 않아 Host/Client가 다른 순서로 수집할 수 있다.
        // ColorTileChallenge 등이 "playerColorType 순 결정적"을 전제로 하므로 여기서 정렬해 보장한다.
        _activePlayers.Sort((a, b) => ColorIndex(a.playerColorType).CompareTo(ColorIndex(b.playerColorType)));

        Debug.Log($"[GameSession] {_activePlayers.Count}인 모드 적용 — 활성 색: {string.Join(", ", _activeColors)}");
    }

    static int ColorIndex(PlayerColorType type)
    {
        int i = PlayerColorUtil.ColorTypeToIndex(type);
        return i >= 0 ? i : int.MaxValue;
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
