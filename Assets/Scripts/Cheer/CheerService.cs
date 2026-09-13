using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 응원 시스템 핵심 로직 (Host 전용).
/// 씬 내 NetworkObject GameObject에 부착. M.Stage1/T.Stage1뿐 아니라 Tutorial 씬에도 배치한다 —
/// TeamCheerWord Host 설정(TrySetTeamCheerWord)이 인스턴스 메서드라 Tutorial 게이트 통과 전
/// Host가 값을 정하려면 이 씬에도 인스턴스가 있어야 한다(Phase D). 게이트 완료 시점에
/// TutorialNetworkManager가 그때의 TeamCheerWord를 GameSession.SetSessionTeamCheerWord로 옮기고,
/// 이후 스테이지의 CheerService.OnNetworkSpawn이 그 세션값을 자기 NV에 복원한다(CheerName과 동일 패턴).
///
/// [역할]
/// - RequestSelfBuffServerRpc → 즉시 개인 버프/쿨 (투표 없음, Space 키 전용 — 2026-09-14)
/// - SubmitTeamCheerServerRpc → 팀 공용 키워드 1회 통과 누적 → 등록된 ITeamCheerRevert 되돌림
///   (힐·120초 쿨 폐기. 타임아웃/표 리셋도 폐기 — 2026-09-14 3차 변경, §2.2. 새 RPC 없음.
///    Idle 외침 무시. Warning~Revert만 유효. 씬당 revert 하나 —
///    입 MouthController / 침 SalivaHazard / 혀 TongueController)
/// - TeamCheerWord NetworkVariable (Host write, Everyone read)
/// - UI 동기화 → ClientRpc (개인 버프 이벤트 + 팀 발동/진행도)
///
/// [CheerName ↔ colorIndex]
/// 0=berry(Blue), 1=guma(Purple), 2=sook(Green), 3=dan(Yellow)
/// </summary>
public class CheerService : NetworkBehaviour
{
    public static CheerService Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────

    [Header("스테이지 기본 버프 (스폰 시 초기 선택값 — 이후 플레이어가 자유 전환 가능, 고정 아님)")]
    [SerializeField] PlayerBuffSystem.BuffType stageBuffType = PlayerBuffSystem.BuffType.Shield;

    [Header("개인 버프")]
    [Tooltip("버프 종료 후 수혜자 쿨타임(초)")]
    [SerializeField] float cheerCooldownSeconds = 15f;

    [Header("팀 버프")]
    [Tooltip("숫자키 응원 연속 입력 최소 간격(초)")]
    [SerializeField] float chatRateLimitSeconds = 0.5f;

    // ── CheerName 매핑 ────────────────────────────────────────────
    // 색 기본 CheerName은 PlayerColorUtil.DefaultCheerNames SSOT를 직접 쓴다 —
    // 예전엔 같은 배열을 여기 따로 들고 있어 두 곳이 갈라질 수 있었다(2026-09-06 리뷰).

    readonly NetworkVariable<FixedString32Bytes> _teamCheerWord = new(
        new FixedString32Bytes(GameSession.DefaultTeamCheerWord),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Host 내부 상태 (개인) ─────────────────────────────────────

    readonly Dictionary<int, double> _cooldownEnd = new();
    readonly Dictionary<int, double> _buffEnd = new();
    readonly Dictionary<ulong, double> _chatRateEnd = new();

    // ── Host 내부 상태 (팀) ───────────────────────────────────────

    readonly HashSet<ulong> _teamVotes = new();

    ITeamCheerRevert _revert;
    bool _hazardWindow;

    // 이번 창을 이미 되돌렸는지. 되돌림 명령이 로컬 함정에 반영되기 전에 들어온 추가 표로
    // 같은 창이 두 번 발동(배너 2회)하는 것을 Host 상태만으로 막는다. 다음 창 시작 시 해제.
    bool _teamWindowConsumed;

    // ── 이벤트 (로컬 — UI 구독용) ─────────────────────────────────

    /// <summary>개인 버프 발동. (colorIndex)</summary>
    public event System.Action<int> OnBuffActivated;

    /// <summary>개인 쿨타임 시작. (colorIndex, 쿨타임초)</summary>
    public event System.Action<int, float> OnCooldownStart;

    /// <summary>팀 응원 성공 (Revert 직후). TeamCheerCleared 구독.</summary>
    public event System.Action OnTeamBuffActivated;

    /// <summary>Warning 시작~Revert 성공. TeamCheerWarningUI 구독.</summary>
    public event System.Action<bool> OnHazardWindowChanged;

    /// <summary>(현재표수, 필요표수, 이미 외친 플레이어 colorIndex 배열). PlayerCheerHeartsUI(머리 위 구)만 구독 — TeamStatusUI 구독 금지.</summary>
    public event System.Action<int, int, int[]> OnTeamVoteChanged;

    /// <summary>TeamCheerWord NV 변경. TeamCheerWordUI 구독. 문법 재빌드는 기존처럼 이 콜백에서 같이 돈다.</summary>
    public event System.Action OnTeamCheerWordChanged;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────

    public float CooldownDuration => cheerCooldownSeconds;
    public PlayerBuffSystem.BuffType StageBuffType => stageBuffType;
    public bool IsHazardWindowActive => _hazardWindow;

    public string TeamCheerWord
    {
        get
        {
            string w = _teamCheerWord.Value.ToString();
            return string.IsNullOrEmpty(w) ? GameSession.DefaultTeamCheerWord : w;
        }
    }

    // ── 라이프사이클 ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // 씨딩을 OnValueChanged 구독보다 먼저 — 그래야 씨딩 write가 로컬 콜백을 띄우지 않아
        // 아래 RebuildOwnerLocalGrammar 호출과 중복되지 않는다 (PlayerCheerNameSync와 동일 패턴).
        if (IsServer && GameSession.Instance != null && GameSession.Instance.HasSessionTeamCheerWord)
        {
            string sessionWord = GameSession.Instance.GetSessionTeamCheerWord();
            if (!string.IsNullOrEmpty(sessionWord))
                _teamCheerWord.Value = new FixedString32Bytes(sessionWord);
        }

        _teamCheerWord.OnValueChanged += HandleTeamCheerWordNv;

        PlayerCheerNameSync.RebuildOwnerLocalGrammar();
    }

    public override void OnNetworkDespawn()
    {
        _teamCheerWord.OnValueChanged -= HandleTeamCheerWordNv;
        _revert = null;

        // NotifyHazardWindow(false)의 표 리셋이 despawn 중에 ClientRpc를 쏘지 않도록 먼저 비운다.
        _teamVotes.Clear();
        _teamWindowConsumed = false;

        NotifyHazardWindow(false);
        if (Instance == this) Instance = null;
    }

    void HandleTeamCheerWordNv(FixedString32Bytes previous, FixedString32Bytes current)
    {
        PlayerCheerNameSync.RebuildOwnerLocalGrammar();
        OnTeamCheerWordChanged?.Invoke();
    }

    void Update()
    {
        if (!IsServer) return;
        var nm = NetworkManager;
        if (nm == null || !nm.IsListening) return;
        double now = nm.ServerTime.Time;
        CheckBuffEnd(now);
    }

    // ── Host-only TeamCheerWord ───────────────────────────────────

    /// <summary>
    /// Host 클라이언트 UI가 IsServer 가드로 직접 호출. RPC 없음.
    /// 실패 사유: "format" / "reserved" / "blocked" / "taken" / "not_server".
    /// </summary>
    public bool TrySetTeamCheerWord(string candidate, out string reason)
    {
        reason = "";
        if (!IsServer)
        {
            reason = "not_server";
            return false;
        }

        string lower = candidate == null ? "" : candidate.Trim().ToLowerInvariant();
        if (!CheerNameValidator.IsValidFormat(lower, out reason))
            return false;
        if (CheerNameValidator.ContainsBlockedWord(lower))
        {
            reason = "blocked";
            return false;
        }

        foreach (var (_, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (name != lower) continue;
            reason = "taken";
            return false;
        }

        if (TeamCheerWord == lower)
            return true;

        ResetTeamVotes();
        _teamCheerWord.Value = new FixedString32Bytes(lower);
        return true;
    }

    public bool MatchesTeamCheerWord(string lower)
    {
        if (string.IsNullOrEmpty(lower)) return false;
        return TeamCheerWord == lower;
    }

    // ── 되돌림 등록 (씬당 하나) ───────────────────────────────────

    public void RegisterRevert(ITeamCheerRevert revert)
    {
        if (revert == null) return;

        // 씬당 하나가 계약인데 등록 순서는 각 함정의 CheerService.Instance 대기 해제 순서라
        // 비결정적이다. 조용히 덮어쓰면 "어느 함정이 되돌림 대상인지"가 실행마다 달라지므로
        // 에디터 오설정(예: M2 입의 teamCheerHazard를 켬)을 여기서 바로 드러낸다.
        if (IsAlive(_revert) && !ReferenceEquals(_revert, revert))
            Debug.LogWarning(
                $"[CheerService] ITeamCheerRevert가 이미 등록돼 있습니다 " +
                $"({_revert.GetType().Name} → {revert.GetType().Name}). 씬당 하나만 두세요.", this);

        _revert = revert;
    }

    public void UnregisterRevert(ITeamCheerRevert revert)
    {
        if (!ReferenceEquals(_revert, revert)) return;
        _revert = null;
        NotifyHazardWindow(false);
    }

    public void NotifyHazardWindow(bool active)
    {
        if (_hazardWindow == active) return;
        _hazardWindow = active;

        if (IsServer)
        {
            if (active)
            {
                _teamWindowConsumed = false;
            }
            else
            {
                // 창이 성공 없이 닫혔을 때(함정 비활성·씬 전환 등) 표가 남아있으면 다음 창을
                // 적은 인원으로 뚫게 된다. 창 단위로 표를 끊는다 — 타임아웃 자동 리셋은
                // 없지만(2026-09-14) 창 자체가 닫힐 땐 여전히 비운다.
                ResetTeamVotes();
            }
        }

        OnHazardWindowChanged?.Invoke(active);
    }

    /// <summary>파괴된 MonoBehaviour가 인터페이스 참조로 남아 있으면 null로 취급.</summary>
    static bool IsAlive(ITeamCheerRevert revert)
    {
        if (revert == null) return false;
        if (revert is UnityEngine.Object unityObject) return unityObject != null;
        return true;
    }

    // ── 서버 RPC ──────────────────────────────────────────────────

    /// <summary>[2026-09-14] 개인 버프 트리거는 항상 Space 키 입력 — 음성 경로 삭제(isVoice 파라미터 없음).</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSelfBuffServerRpc(RpcParams rpcParams = default)
    {
        ulong cheererId = rpcParams.Receive.SenderClientId;
        double now = NetworkManager.ServerTime.Time;

        if (!PlayerSpawnCoordinator.TryGetColor(cheererId, out var myColor)) return;
        int myIdx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, myColor);
        if (myIdx < 0) return;
        if (!ValidateSelfCheer(myIdx, now)) return;

        ApplyBuff(myIdx, now);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitTeamCheerServerRpc(bool isVoice, RpcParams rpcParams = default)
    {
        ulong cheererId = rpcParams.Receive.SenderClientId;
        double now = NetworkManager.ServerTime.Time;

        if (!ValidateTeamCheer(cheererId, isVoice, now)) return;

        bool added = _teamVotes.Add(cheererId);
        if (!added) return;

        int current = _teamVotes.Count;
        int required = GetRequiredTeamVotes();
        BroadcastTeamVoteChangedClientRpc(current, required, GetTeamVoterColorIndices());

        if (current >= required)
            ApplyTeamBuff();
    }

    // ── 개인 버프 적용 (Host 전용) ─────────────────────────────────

    void ApplyBuff(int targetColorIndex, double now)
    {
        float appliedDuration = 5f;

        var players = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (!PlayerSpawnCoordinator.TryGetColor(p.OwnerClientId, out var color)) continue;
            int idx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
            if (idx != targetColorIndex) continue;

            appliedDuration = p.ApplyCheerBuff(p.SelectedBuffType);
            break;
        }

        _buffEnd[targetColorIndex] = now + appliedDuration;
        BroadcastBuffActivatedClientRpc(targetColorIndex);
    }

    // ── 팀 응원 적용 (Host 전용 → 기존 ClientRpc로 전 머신 Revert) ─

    void ApplyTeamBuff()
    {
        if (!IsSpawned) return;

        // 되돌림 명령(세대 + 다음 창 재개 ServerTime)은 Host가 정해서 전 머신에 그대로 실어 보낸다.
        // 각 머신이 로컬로 계산하면, 명령을 놓친 머신만 예약이 어긋난 채 남는다.
        int generation = 0;
        double resumeAt = 0d;
        if (IsAlive(_revert))
            _revert.BuildRevertOrder(out generation, out resumeAt);

        _teamWindowConsumed = true;
        // 되돌림(=창 닫기)을 표 리셋보다 먼저 보낸다. 반대 순서면 클라이언트가 창이 열린 채로
        // "표 0개"를 먼저 받아 통과자까지 느낌표가 잠깐 다시 켜진다.
        BroadcastTeamBuffActivatedClientRpc(generation, resumeAt);
        ResetTeamVotes();
    }

    /// <summary>창이 닫힐 때(성공 또는 강제 종료) 표를 비운다. 실패로 인한 자동 리셋은 없다(2026-09-14) —
    /// 창이 열려 있는 동안엔 1회 통과가 계속 유지된다.</summary>
    void ResetTeamVotes()
    {
        if (!IsServer) return;
        if (_teamVotes.Count == 0) return;

        _teamVotes.Clear();
        if (IsSpawned)
            BroadcastTeamVoteChangedClientRpc(0, GetRequiredTeamVotes(), System.Array.Empty<int>());
    }

    // ── 주기 체크 ─────────────────────────────────────────────────

    void CheckBuffEnd(double now)
    {
        var ended = new List<int>();
        foreach (var kv in _buffEnd)
            if (now >= kv.Value) ended.Add(kv.Key);

        foreach (int t in ended)
        {
            _buffEnd.Remove(t);
            _cooldownEnd[t] = now + cheerCooldownSeconds;
            BroadcastCooldownStartClientRpc(t, cheerCooldownSeconds);
        }
    }

    // ── 유효성 검사 ───────────────────────────────────────────────

    // 연타 제한(_chatRateEnd)을 쓰지 않는다 — 팀 응원 T키와 버킷을 공유해 "T 직후 Space가 조용히 거절"되던
    // 문제가 있었고, 버프 중/쿨 중 체크만으로 재발동이 이미 막힌다(중복 RPC 수신도 _buffEnd로 거절).
    bool ValidateSelfCheer(int colorIndex, double now)
    {
        if (_buffEnd.ContainsKey(colorIndex)) return false;
        return !(_cooldownEnd.TryGetValue(colorIndex, out double cd) && now < cd);
    }

    bool ValidateTeamCheer(ulong cheererId, bool isVoice, double now)
    {
        if (!PlayerSpawnCoordinator.TryGetColor(cheererId, out _)) return false;
        if (_teamWindowConsumed) return false;
        if (!IsAlive(_revert) || !_revert.IsAvailable) return false;
        if (_teamVotes.Contains(cheererId)) return false;
        return PassRateLimit(cheererId, isVoice, now);
    }

    bool PassRateLimit(ulong cheererId, bool isVoice, double now)
    {
        if (isVoice) return true;
        if (_chatRateEnd.TryGetValue(cheererId, out double rateEnd) && now < rateEnd) return false;
        _chatRateEnd[cheererId] = now + chatRateLimitSeconds;
        return true;
    }

    int GetRequiredTeamVotes()
    {
        int n = GameSession.Instance != null ? GameSession.Instance.ActivePlayerCount : 0;
        if (n <= 0)
        {
            var nm = NetworkManager;
            n = nm != null ? nm.ConnectedClientsIds.Count : 1;
        }
        return Mathf.Max(1, n);
    }

    int[] GetTeamVoterColorIndices()
    {
        var result = new List<int>(_teamVotes.Count);
        foreach (ulong id in _teamVotes)
        {
            if (!PlayerSpawnCoordinator.TryGetColor(id, out var color)) continue;
            int idx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
            if (idx >= 0) result.Add(idx);
        }
        return result.ToArray();
    }

    // ── ClientRpc (UI 동기화) ──────────────────────────────────────

    [ClientRpc]
    void BroadcastBuffActivatedClientRpc(int targetColorIndex)
        => OnBuffActivated?.Invoke(targetColorIndex);

    [ClientRpc]
    void BroadcastCooldownStartClientRpc(int targetColorIndex, float seconds)
        => OnCooldownStart?.Invoke(targetColorIndex, seconds);

    [ClientRpc]
    void BroadcastTeamBuffActivatedClientRpc(int generation, double resumeAtServerTime)
    {
        if (IsAlive(_revert) && generation > 0)
            _revert.Revert(generation, resumeAtServerTime);
        OnTeamBuffActivated?.Invoke();
    }

    [ClientRpc]
    void BroadcastTeamVoteChangedClientRpc(int current, int required, int[] voterColorIndices)
        => OnTeamVoteChanged?.Invoke(current, required, voterColorIndices ?? System.Array.Empty<int>());

    // ── 공개 유틸 (이름 ↔ colorIndex) ─────────────────────────────

    /// <summary>
    /// 이름 → colorIndex. 우선순위 역전(CheerAndTutorialDesign.md §3.4.2, 2026-09):
    /// ①실시간 커스텀 NV → ②GameSession 확정 세션 이름 → ③색 기본값. 미매칭 시 -1.
    ///
    /// [왜 역전했나] Interlude에서 세션 스냅샷이 항상 이겨버리면, 방금 입력한 새 이름을
    /// "말해보기"로 테스트해도 그래머·판정이 옛 세션값을 계속 본다. 실시간 값을 우선하면
    /// Tutorial/Interlude 둘 다 즉시 테스트 가능해지고, 나머지 M/T 스테이지는
    /// PlayerCheerNameSync.OnNetworkSpawn의 세션값 NV 씨딩(§3.4 코드 변경 #2) 덕분에
    /// NV == 세션값이라 결과가 완전히 동일하다(동작 변화 없음).
    ///
    /// [GetAllEffectiveNames를 쓰지 않는 이유] 그쪽은 NV가 비면 색 기본값을 채워주기 때문에,
    /// 씨딩이 실패한 플레이어의 빈 NV가 "기본값"이라는 유효한 답으로 위장해 세션 확정값을
    /// 가려버린다 — 커스텀 값이 실제로 있는 NV만 1순위로 본다(2026-09-06 리뷰).
    /// </summary>
    public static int GetColorIndex(string cheerName)
    {
        string lower = cheerName.Trim().ToLower();

        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllCustomCheerNames())
        {
            if (name != lower) continue;
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color))
                return PlayerColorUtil.ColorTypeToIndex(color);
        }

        if (GameSession.Instance != null)
        {
            int idx = GameSession.Instance.GetSessionColorIndex(lower);
            if (idx >= 0) return idx;
        }

        return System.Array.IndexOf(PlayerColorUtil.DefaultCheerNames, lower);
    }

    /// <summary>
    /// colorIndex → CheerName. 우선순위 역전(§3.4.2): ①실시간 커스텀 NV
    /// → ②세션 확정값 → ③색 기본값. 커스텀 NV만 1순위로 보는 이유는 GetColorIndex 주석 참고.
    /// </summary>
    public static string GetCheerName(int colorIndex)
    {
        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllCustomCheerNames())
        {
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color) &&
                PlayerColorUtil.ColorTypeToIndex(color) == colorIndex)
                return name;
        }

        if (GameSession.Instance != null && GameSession.Instance.HasSessionCheerNames)
            return GameSession.Instance.GetSessionCheerName(colorIndex);

        var defaults = PlayerColorUtil.DefaultCheerNames;
        if (colorIndex < 0 || colorIndex >= defaults.Length) return string.Empty;
        return defaults[colorIndex];
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 자기 버프 강제 발동 color0 (Host 전용)")]
    void Debug_ForceSelfBuff()
    {
        if (!IsServer) { Debug.LogWarning("Host 전용"); return; }
        ApplyBuff(0, NetworkManager.ServerTime.Time);
    }

    [ContextMenu("테스트: 팀 버프 강제 발동 (Host 전용)")]
    void Debug_ForceTeamBuff()
    {
        if (!IsServer) { Debug.LogWarning("Host 전용"); return; }
        ApplyTeamBuff();
    }
#endif
}
