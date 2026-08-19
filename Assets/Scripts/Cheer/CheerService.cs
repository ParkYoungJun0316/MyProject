using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 응원 시스템 핵심 로직 (Host 전용).
/// 씬 내 NetworkObject GameObject에 부착.
/// M.Stage1 / T.Stage1 씬에 각각 배치.
///
/// [역할]
/// - SubmitCheerServerRpc 수신 → 응원 표 집계
/// - 타임아웃(N초 내 미달) → 표 초기화
/// - 수혜자 개인 쿨타임 관리
/// - 조건 충족 시 버프 발동 → NetworkPlayerSetup.ApplyCheerBuff()
/// - UI 동기화 → ClientRpc (OnVoteChanged, OnBuffActivated 등)
///
/// [배치]
/// 빈 GameObject → NetworkObject + CheerService 추가.
/// 씬 시작 시 자동으로 동작. 사망 씬 리로드 시 자동 초기화됨.
///
/// [CheerName ↔ colorIndex]
/// 0=berry(Blue), 1=guma(Purple), 2=sook(Green), 3=hobak(Yellow)
/// </summary>
public class CheerService : NetworkBehaviour
{
    public static CheerService Instance { get; private set; }

    public enum CheerSource { Chat, Voice }

    // ── Inspector ─────────────────────────────────────────────────

    [Header("스테이지 버프 (M.Stage1 = Shield, T.Stage1 = SpeedUp)")]
    [SerializeField] PlayerBuffSystem.BuffType stageBuffType = PlayerBuffSystem.BuffType.Shield;

    [Header("버프 파라미터")]
    [Tooltip("버프 지속 시간(초). PlayerBuffSystem.buffSettings 값과 일치시킬 것.")]
    [SerializeField] float buffDuration = 5f;

    [Tooltip("버프 종료 후 수혜자 쿨타임(초)")]
    [SerializeField] float cheerCooldownSeconds = 15f;

    [Tooltip("첫 표 이후 전원 응원 타임아웃(초). 미달 시 표 전부 초기화.")]
    [SerializeField] float cheerTimeoutSeconds = 10f;

    [Tooltip("채팅 /cheer 연속 입력 최소 간격(초)")]
    [SerializeField] float chatRateLimitSeconds = 0.5f;

    // ── CheerName 매핑 ────────────────────────────────────────────

    // PlayerColorUtil.DefaultCheerNames 및 CheerLexiconBuilder 와 순서 동일하게 유지.
    // 0=berry(Blue) 1=guma(Purple) 2=sook(Green) 3=hobak(Yellow)
    static readonly string[] CheerNames = { "berry", "guma", "sook", "hobak" };

    // ── Host 내부 상태 ─────────────────────────────────────────────

    // targetColorIndex → 해당 수혜자를 응원 중인 clientId 집합
    readonly Dictionary<int, HashSet<ulong>> _votes = new();

    // targetColorIndex → 첫 표 수신 serverTime (타임아웃 계산용)
    readonly Dictionary<int, double> _timeoutStart = new();

    // clientId → 현재 응원 중인 targetColorIndex (-1 = 없음)
    readonly Dictionary<ulong, int> _cheererTarget = new();

    // targetColorIndex → 쿨타임 종료 serverTime
    readonly Dictionary<int, double> _cooldownEnd = new();

    // targetColorIndex → 버프 종료 serverTime
    readonly Dictionary<int, double> _buffEnd = new();

    // clientId → 채팅 rate limit 종료 serverTime
    readonly Dictionary<ulong, double> _chatRateEnd = new();

    // ── 이벤트 (로컬 — UI 구독용) ─────────────────────────────────

    /// <summary>(targetColorIndex, 현재표수, 필요표수)</summary>
    public event System.Action<int, int, int> OnVoteChanged;

    /// <summary>버프 발동. (targetColorIndex)</summary>
    public event System.Action<int> OnBuffActivated;

    /// <summary>표 초기화. (targetColorIndex)</summary>
    public event System.Action<int> OnVoteReset;

    /// <summary>쿨타임 시작. (targetColorIndex, 쿨타임초)</summary>
    public event System.Action<int, float> OnCooldownStart;

    /// <summary>응원자 색상 목록 변경. (targetColorIndex, 응원자 colorIndex 배열)</summary>
    public event System.Action<int, int[]> OnCheerersChanged;

    // ── 공개 프로퍼티 (UI에서 타이머 계산용) ──────────────────────

    public float BuffDuration                    => buffDuration;
    public float CooldownDuration                => cheerCooldownSeconds;
    public PlayerBuffSystem.BuffType StageBuffType => stageBuffType;

    // ── 라이프사이클 ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsServer) return;
        // NetworkManager 로컬 참조 사용: Singleton은 씬 전환 중 null이 될 수 있음
        var nm = NetworkManager;
        if (nm == null || !nm.IsListening) return;
        double now = nm.ServerTime.Time;
        CheckTimeouts(now);
        CheckBuffEnd(now);
    }

    // ── 서버 RPC ──────────────────────────────────────────────────

    /// <summary>
    /// 클라이언트가 응원을 신고. InvokePermission.Everyone 으로 어느 클라이언트에서나 호출 가능.
    /// 파라미터: targetColorIndex (0~3), isVoice (음성=true, 채팅=false).
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitCheerServerRpc(int targetColorIndex, bool isVoice, RpcParams rpcParams = default)
    {
        ulong cheererId = rpcParams.Receive.SenderClientId;
        double now = NetworkManager.Singleton.ServerTime.Time;

        if (!ValidateCheer(targetColorIndex, cheererId, isVoice, now)) return;

        // 타겟 변경 처리 (이전 타겟 표 제거)
        HandleTargetSwitch(cheererId, targetColorIndex, now);

        // 표 추가
        if (!_votes.ContainsKey(targetColorIndex))
            _votes[targetColorIndex] = new HashSet<ulong>();
        _votes[targetColorIndex].Add(cheererId);

        // 첫 표: 타임아웃 타이머 시작
        if (!_timeoutStart.ContainsKey(targetColorIndex))
            _timeoutStart[targetColorIndex] = now;

        int currentVotes = _votes[targetColorIndex].Count;
        int required = GetRequiredVotes();

        BroadcastVoteChangedClientRpc(targetColorIndex, currentVotes, required);
        BroadcastCheerersClientRpc(targetColorIndex, GetCheererColorIndices(targetColorIndex));

        // 버프 발동 — 쿨타임 중이면 발동만 건너뜀 (표는 이미 쌓임)
        bool onCooldown = _cooldownEnd.TryGetValue(targetColorIndex, out double cd) && now < cd;
        if (!onCooldown && currentVotes >= required)
            ApplyBuff(targetColorIndex, now);
    }

    // ── 버프 적용 (Host 전용) ──────────────────────────────────────

    void ApplyBuff(int targetColorIndex, double now)
    {
        _buffEnd[targetColorIndex] = now + buffDuration;

        // 해당 색상 플레이어 찾아 버프 적용
        var players = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (!PlayerSpawnCoordinator.TryGetColor(p.OwnerClientId, out var color)) continue;
            int idx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
            if (idx != targetColorIndex) continue;

            p.ApplyCheerBuff(stageBuffType, buffDuration);
            break;
        }

        ResetVotes(targetColorIndex);
        BroadcastBuffActivatedClientRpc(targetColorIndex);
    }

    // ── 표 초기화 (Host 전용) ─────────────────────────────────────

    void ResetVotes(int targetColorIndex)
    {
        if (_votes.TryGetValue(targetColorIndex, out var voters))
        {
            foreach (ulong id in voters)
            {
                if (_cheererTarget.TryGetValue(id, out int t) && t == targetColorIndex)
                    _cheererTarget[id] = -1;
            }
            _votes.Remove(targetColorIndex);
        }
        _timeoutStart.Remove(targetColorIndex);
        BroadcastVoteResetClientRpc(targetColorIndex);
    }

    // ── 주기 체크 ─────────────────────────────────────────────────

    void CheckTimeouts(double now)
    {
        var timedOut = new List<int>();
        foreach (var kv in _timeoutStart)
        {
            int target = kv.Key;
            if (now - kv.Value < cheerTimeoutSeconds) continue;
            bool buffActive = _buffEnd.ContainsKey(target);
            if (buffActive) continue;
            int cnt = _votes.TryGetValue(target, out var v) ? v.Count : 0;
            if (cnt < GetRequiredVotes()) timedOut.Add(target);
        }
        foreach (int t in timedOut) ResetVotes(t);
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

    bool ValidateCheer(int targetColorIndex, ulong cheererId, bool isVoice, double now)
    {
        if (targetColorIndex < 0 || targetColorIndex >= CheerNames.Length) return false;

        // 자기 자신 응원 불가 (단, 1인 세션에서는 허용).
        // 색 조회(TryGetColor) 실패 시 자기 응원 여부를 확정할 수 없으므로 안전하게 거부한다
        // (fail-closed — 조회 실패를 "자기 응원 아님"으로 취급해 통과시키면 안 됨).
        if (!PlayerSpawnCoordinator.TryGetColor(cheererId, out var myColor)) return false;

        int myIdx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, myColor);
        if (myIdx == targetColorIndex)
        {
            // 1인 세션에서는 자기 자신 응원 허용 (partySize==1 규칙)
            bool isSolo = GameSession.Instance != null && GameSession.Instance.ActivePlayerCount == 1;
            if (!isSolo) return false;
        }

        // 수혜자 버프 중 → 표 차단
        if (_buffEnd.ContainsKey(targetColorIndex)) return false;

        // 채팅 rate limit
        if (!isVoice)
        {
            if (_chatRateEnd.TryGetValue(cheererId, out double rateEnd) && now < rateEnd) return false;
            _chatRateEnd[cheererId] = now + chatRateLimitSeconds;
        }

        return true;
    }

    // ── 타겟 변경 처리 ────────────────────────────────────────────

    void HandleTargetSwitch(ulong cheererId, int newTarget, double now)
    {
        int prevTarget = _cheererTarget.TryGetValue(cheererId, out int pt) ? pt : -1;
        if (prevTarget == newTarget) return;

        // 이전 타겟 표 제거
        if (prevTarget >= 0 && _votes.TryGetValue(prevTarget, out var prevVotes))
        {
            prevVotes.Remove(cheererId);
            int remainCnt = prevVotes.Count;
            if (remainCnt == 0)
            {
                _votes.Remove(prevTarget);
                _timeoutStart.Remove(prevTarget);
            }
            BroadcastVoteChangedClientRpc(prevTarget, remainCnt, GetRequiredVotes());
            BroadcastCheerersClientRpc(prevTarget, GetCheererColorIndices(prevTarget));
        }

        _cheererTarget[cheererId] = newTarget;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    /// <summary>
    /// 필요 응원 수 = max(1, 실제 참여 인원-1). NetworkManager 연결 수가 아니라
    /// GameSession.ActivePlayerCount(이번 판 참가 인원)를 기준으로 삼는다 — 관전/유령 연결 등으로
    /// ConnectedClientsIds.Count가 실제 참여 인원과 달라지면 필요 표수가 부풀려져 버프가
    /// 영원히 발동하지 않는 문제가 있었다(CheerAndTutorialDesign.md §2.1과 소스 일치).
    /// </summary>
    int GetRequiredVotes()
    {
        if (GameSession.Instance != null && GameSession.Instance.ActivePlayerCount > 0)
            return Mathf.Max(1, GameSession.Instance.ActivePlayerCount - 1);

        if (NetworkManager.Singleton == null) return 1;
        int active = NetworkManager.Singleton.ConnectedClientsIds.Count;
        return Mathf.Max(1, active - 1);
    }

    /// <summary>현재 해당 타겟을 응원 중인 플레이어들의 colorIndex 배열을 반환.</summary>
    int[] GetCheererColorIndices(int targetColorIndex)
    {
        if (!_votes.TryGetValue(targetColorIndex, out var voters))
            return System.Array.Empty<int>();

        var result = new List<int>(voters.Count);
        foreach (ulong id in voters)
        {
            if (!PlayerSpawnCoordinator.TryGetColor(id, out var color)) continue;
            int idx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
            if (idx >= 0) result.Add(idx);
        }
        return result.ToArray();
    }

    // ── ClientRpc (UI 동기화) ──────────────────────────────────────

    [ClientRpc]
    void BroadcastVoteChangedClientRpc(int targetColorIndex, int current, int required)
        => OnVoteChanged?.Invoke(targetColorIndex, current, required);

    [ClientRpc]
    void BroadcastCheerersClientRpc(int targetColorIndex, int[] cheererColorIndices)
        => OnCheerersChanged?.Invoke(targetColorIndex, cheererColorIndices);

    [ClientRpc]
    void BroadcastBuffActivatedClientRpc(int targetColorIndex)
        => OnBuffActivated?.Invoke(targetColorIndex);

    [ClientRpc]
    void BroadcastVoteResetClientRpc(int targetColorIndex)
        => OnVoteReset?.Invoke(targetColorIndex);

    [ClientRpc]
    void BroadcastCooldownStartClientRpc(int targetColorIndex, float seconds)
        => OnCooldownStart?.Invoke(targetColorIndex, seconds);

    // ── 공개 유틸 (CheerChatInput / CheerProgressUI 사용) ─────────

    /// <summary>
    /// 이름 → colorIndex. 우선순위: ①GameSession 확정 세션 이름(게이트 통과 후, §6B.7 P5)
    /// → ②PlayerCheerNameSync 실시간 값(게이트 통과 전 Tutorial에서도 존재, §6B.2) → ③정적 기본값.
    /// 미매칭 시 -1.
    ///
    /// [②가 필요한 이유] Tutorial 게이트 통과 전엔 GameSession._sessionCheerNames가 아직 null이라
    /// ①이 항상 실패한다. 이때 커스텀 CheerName으로 응원(zone 3 체험 등)을 외쳐도 grammar는 인식하지만
    /// (PlayerCheerNameSync가 이미 로컬 grammar를 실시간 갱신함) 이 함수가 -1을 돌려주면 "인식됐으나
    /// 불일치"로 조용히 씹힌다 — NetworkDesign.md §6B.7 다음 에이전트 시작점 3번 갭.
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

    /// <summary>colorIndex → CheerName. ①GameSession 확정 세션 이름(게이트 통과 후) → ②PlayerCheerNameSync
    /// 실시간 값(게이트 전) → ③정적 기본값. 범위 밖이면 빈 문자열. 우선순위 근거는 <see cref="GetColorIndex"/> 참고.
    ///
    /// [왜 GameSession.HasSessionCheerNames로 먼저 분기하나] GameSession.GetSessionCheerName은
    /// 세션 미확정(null) 상태에서도 자체적으로 PlayerColorUtil.DefaultCheerNames로 폴백해 항상
    /// 비어있지 않은 값을 돌려준다 — 그래서 "비어있으면 다음 우선순위로" 방식으로는 게이트 전 커스텀
    /// 이름을 절대 못 본다(항상 ①에서 기본값으로 조용히 성공해버림). 그래서 값 자체가 아니라
    /// "세션이 확정됐는지"로 먼저 분기해야 한다.</summary>
    public static string GetCheerName(int colorIndex)
    {
        if (GameSession.Instance != null && GameSession.Instance.HasSessionCheerNames)
            return GameSession.Instance.GetSessionCheerName(colorIndex);

        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color) &&
                PlayerColorUtil.ColorTypeToIndex(color) == colorIndex)
                return name; // 이미 GetAllEffectiveNames 안에서 커스텀/기본값까지 해석된 값
        }

        if (colorIndex < 0 || colorIndex >= CheerNames.Length) return string.Empty;
        return CheerNames[colorIndex];
    }

    // ── 에디터 테스트 ──────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: berry(0) 응원 강제 발동 (Host 전용)")]
    void Debug_ForceCheerBerry()
    {
        if (!IsServer) { Debug.LogWarning("Host 전용"); return; }
        ApplyBuff(0, NetworkManager.Singleton.ServerTime.Time);
    }
#endif
}
