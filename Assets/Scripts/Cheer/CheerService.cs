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
/// - SubmitSelfCheerServerRpc → 즉시 개인 버프/쿨 (투표 없음)
/// - SubmitTeamCheerServerRpc → 팀 공용 키워드 투표·타임아웃·전원 Heal
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
    [Tooltip("팀 버프 발동 후 팀 공용 쿨타임(초). 값은 추후 튜닝.")]
    [SerializeField] float teamCheerCooldownSeconds = 15f;

    [Tooltip("팀 첫 인식 후 전원 미달 시 표 초기화(초).")]
    [SerializeField] float teamCheerTimeoutSeconds = 10f;

    [Tooltip("팀 버프 체력회복량 (heart 단위)")]
    [SerializeField] int teamHealAmount = 2;

    [Tooltip("숫자키 응원 연속 입력 최소 간격(초)")]
    [SerializeField] float chatRateLimitSeconds = 0.5f;

    // ── CheerName 매핑 ────────────────────────────────────────────

    static readonly string[] CheerNames = { "berry", "guma", "sook", "dan" };

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
    double _teamTimeoutStart = -1d;
    double _teamCooldownEnd;

    // ── 이벤트 (로컬 — UI 구독용) ─────────────────────────────────

    /// <summary>개인 버프 발동. (colorIndex)</summary>
    public event System.Action<int> OnBuffActivated;

    /// <summary>개인 쿨타임 시작. (colorIndex, 쿨타임초)</summary>
    public event System.Action<int, float> OnCooldownStart;

    /// <summary>구 cross-target UI가 아직 구독 중. 신규 경로는 발행하지 않음 (Phase C에서 제거).</summary>
#pragma warning disable CS0067
    public event System.Action<int, int, int> OnVoteChanged;
    public event System.Action<int> OnVoteReset;
    public event System.Action<int, int[]> OnCheerersChanged;
#pragma warning restore CS0067

    /// <summary>팀 버프 발동 (전원 Heal 직후). Phase C 배너 구독용.</summary>
    public event System.Action OnTeamBuffActivated;

    /// <summary>(현재표수, 필요표수, 이미 외친 플레이어 colorIndex 배열)</summary>
    public event System.Action<int, int, int[]> OnTeamVoteChanged;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────

    public float CooldownDuration => cheerCooldownSeconds;
    public PlayerBuffSystem.BuffType StageBuffType => stageBuffType;

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
        _teamCheerWord.OnValueChanged += OnTeamCheerWordChanged;

        if (IsServer && GameSession.Instance != null && GameSession.Instance.HasSessionTeamCheerWord)
        {
            string sessionWord = GameSession.Instance.GetSessionTeamCheerWord();
            if (!string.IsNullOrEmpty(sessionWord))
                _teamCheerWord.Value = new FixedString32Bytes(sessionWord);
        }

        PlayerCheerNameSync.RebuildOwnerLocalGrammar();
    }

    public override void OnNetworkDespawn()
    {
        _teamCheerWord.OnValueChanged -= OnTeamCheerWordChanged;
        if (Instance == this) Instance = null;
    }

    void OnTeamCheerWordChanged(FixedString32Bytes previous, FixedString32Bytes current)
        => PlayerCheerNameSync.RebuildOwnerLocalGrammar();

    void Update()
    {
        if (!IsServer) return;
        var nm = NetworkManager;
        if (nm == null || !nm.IsListening) return;
        double now = nm.ServerTime.Time;
        CheckTeamTimeout(now);
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

    // ── 서버 RPC ──────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitSelfCheerServerRpc(bool isVoice, RpcParams rpcParams = default)
    {
        ulong cheererId = rpcParams.Receive.SenderClientId;
        double now = NetworkManager.ServerTime.Time;

        if (!PlayerSpawnCoordinator.TryGetColor(cheererId, out var myColor)) return;
        int myIdx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, myColor);
        if (myIdx < 0) return;
        if (!ValidateSelfCheer(myIdx, cheererId, isVoice, now)) return;

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

        if (_teamTimeoutStart < 0d)
            _teamTimeoutStart = now;

        int current = _teamVotes.Count;
        int required = GetRequiredTeamVotes();
        BroadcastTeamVoteChangedClientRpc(current, required, GetTeamVoterColorIndices());

        if (current >= required)
            ApplyTeamBuff(now);
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

    /// <summary>
    /// targetColorIndex 수혜자가 지금 응원 버프를 받는 중인지 (버프 선택 전환 잠금용).
    /// NetworkPlayerSetup.RequestToggleBuffTypeServerRpc에서 참조.
    /// </summary>
    public bool IsBuffActive(int colorIndex) => _buffEnd.ContainsKey(colorIndex);

    // ── 팀 버프 적용 (Host 전용) ───────────────────────────────────

    void ApplyTeamBuff(double now)
    {
        var setups = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
        foreach (var setup in setups)
        {
            if (!PlayerSpawnCoordinator.TryGetColor(setup.OwnerClientId, out var color)) continue;
            if (!PlayerSpawnCoordinator.IsColorInSession(color)) continue;
            var player = setup.GetComponent<Player>();
            if (player == null) continue;
            NetworkDamageUtil.ApplyHeal(player, teamHealAmount);
        }

        _teamCooldownEnd = now + teamCheerCooldownSeconds;
        ResetTeamVotes();
        BroadcastTeamBuffActivatedClientRpc();
    }

    void ResetTeamVotes()
    {
        _teamVotes.Clear();
        _teamTimeoutStart = -1d;
        if (IsSpawned)
            BroadcastTeamVoteChangedClientRpc(0, GetRequiredTeamVotes(), System.Array.Empty<int>());
    }

    // ── 주기 체크 ─────────────────────────────────────────────────

    void CheckTeamTimeout(double now)
    {
        if (_teamVotes.Count == 0 || _teamTimeoutStart < 0d) return;
        if (now - _teamTimeoutStart < teamCheerTimeoutSeconds) return;
        if (_teamVotes.Count >= GetRequiredTeamVotes()) return;
        ResetTeamVotes();
    }

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

    bool ValidateSelfCheer(int colorIndex, ulong cheererId, bool isVoice, double now)
    {
        if (_buffEnd.ContainsKey(colorIndex)) return false;
        if (_cooldownEnd.TryGetValue(colorIndex, out double cd) && now < cd) return false;
        return PassRateLimit(cheererId, isVoice, now);
    }

    bool ValidateTeamCheer(ulong cheererId, bool isVoice, double now)
    {
        if (!PlayerSpawnCoordinator.TryGetColor(cheererId, out _)) return false;
        if (now < _teamCooldownEnd) return false;
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
    void BroadcastTeamBuffActivatedClientRpc()
        => OnTeamBuffActivated?.Invoke();

    [ClientRpc]
    void BroadcastTeamVoteChangedClientRpc(int current, int required, int[] voterColorIndices)
        => OnTeamVoteChanged?.Invoke(current, required, voterColorIndices ?? System.Array.Empty<int>());

    // ── 공개 유틸 (이름 ↔ colorIndex) ─────────────────────────────

    /// <summary>
    /// 이름 → colorIndex. 우선순위: ①GameSession 확정 세션 이름(게이트 통과 후)
    /// → ②PlayerCheerNameSync 실시간 값(게이트 전) → ③정적 기본값. 미매칭 시 -1.
    /// </summary>
    public static int GetColorIndex(string cheerName)
    {
        string lower = cheerName.Trim().ToLower();

        if (GameSession.Instance != null)
        {
            int idx = GameSession.Instance.GetSessionColorIndex(lower);
            if (idx >= 0) return idx;
        }

        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (name != lower) continue;
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color))
                return PlayerColorUtil.ColorTypeToIndex(color);
        }

        return System.Array.IndexOf(CheerNames, lower);
    }

    /// <summary>colorIndex → CheerName. ①세션 확정값 → ②실시간 PlayerCheerNameSync → ③정적 기본값.</summary>
    public static string GetCheerName(int colorIndex)
    {
        if (GameSession.Instance != null && GameSession.Instance.HasSessionCheerNames)
            return GameSession.Instance.GetSessionCheerName(colorIndex);

        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color) &&
                PlayerColorUtil.ColorTypeToIndex(color) == colorIndex)
                return name;
        }

        if (colorIndex < 0 || colorIndex >= CheerNames.Length) return string.Empty;
        return CheerNames[colorIndex];
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
        ApplyTeamBuff(NetworkManager.ServerTime.Time);
    }
#endif
}
