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
/// - SubmitTeamCheerServerRpc → 팀 공용 키워드 투표·타임아웃 → 등록된 ITeamCheerRevert 되돌림
///   (힐·120초 쿨 폐기. 새 RPC 없음. Idle 외침 무시. Warning~Revert만 유효.
///    씬당 revert 하나 — 입 MouthController / 침 SalivaHazard / 혀 TongueController)
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
    [Tooltip("팀 첫 인식 후 전원 미달 시 표 초기화(초).")]
    [SerializeField] float teamCheerTimeoutSeconds = 10f;

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

    /// <summary>(현재표수, 필요표수, 이미 외친 플레이어 colorIndex 배열). PlayerCheerHeartsUI / TeamStatusUI 구독.</summary>
    public event System.Action<int, int, int[]> OnTeamVoteChanged;

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
        _revert = null;

        // NotifyHazardWindow(false)의 표 리셋이 despawn 중에 ClientRpc를 쏘지 않도록 먼저 비운다.
        _teamVotes.Clear();
        _teamTimeoutStart = -1d;
        _teamWindowConsumed = false;

        NotifyHazardWindow(false);
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
                // 창이 성공 없이 닫혔을 때(혀 4.2 미외침·함정 비활성·씬 전환 등) 표가 타임아웃까지
                // 남으면 다음 창을 적은 인원으로 뚫게 된다. 창 단위로 표를 끊는다.
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

    /// <summary>
    /// targetColorIndex 수혜자가 지금 응원 버프를 받는 중인지 (버프 선택 전환 잠금용).
    /// NetworkPlayerSetup.RequestToggleBuffTypeServerRpc에서 참조.
    /// </summary>
    public bool IsBuffActive(int colorIndex) => _buffEnd.ContainsKey(colorIndex);

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
        ResetTeamVotes();
        BroadcastTeamBuffActivatedClientRpc(generation, resumeAt);
    }

    void ResetTeamVotes()
    {
        if (!IsServer) return;
        if (_teamVotes.Count == 0 && _teamTimeoutStart < 0d) return;

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
        ApplyTeamBuff();
    }
#endif
}
