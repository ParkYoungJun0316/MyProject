using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 응원 시스템 핵심 로직 (Host 전용).
/// 씬 내 NetworkObject GameObject에 부착. M.Stage1/T.Stage1뿐 아니라 Tutorial 씬에도 배치한다 —
/// Host 기준 소리 설정(TrySetTeamCheerSound)이 인스턴스 메서드라 Tutorial 게이트 통과 전
/// Host가 값을 정하려면 이 씬에도 인스턴스가 있어야 한다(Phase D).
///
/// [TeamCheerSound — 2026-10-07, CheerSystemDesign.md §14]
/// 구 TeamCheerWord(영어 단어, Vosk)는 Host가 녹음한 **기준 소리**로 바뀌었다. 소리 자체는 네트워크 밖
/// CheerSoundLocalState(static, 씬 넘어 유지)에 머신마다 들고 있고, 이 클래스는
/// - _teamCheerSoundVersion NV(0 = 없음)로 "지금 어느 버전인가"만 전원에 알리고
/// - Host가 그 버전의 압축 클립+특징을 4KB 청크 ClientRpc로 배포한다(§14.5). 늦게 들어온 클라이언트는
///   RequestTeamCheerSoundServerRpc로 당겨 받는다(pull — 접속 순서·씬 전환과 무관하게 항상 맞춰짐).
/// - 연습 창(Tutorial·Interlude 구역 3) 통과자 집합을 Host가 기록해 게이트 조건으로 쓴다(§14.2).
/// 세션 스냅샷(GameSession)은 더 이상 필요 없다 — Host도 CheerSoundLocalState에 같은 값을 갖고 있어
/// 다음 씬의 CheerService.OnNetworkSpawn이 거기서 NV 버전을 되살린다.
///
/// [역할]
/// - RequestSelfBuffServerRpc → 즉시 개인 버프/쿨 (투표 없음, Space 키 전용 — 2026-09-14)
/// - SubmitTeamCheerServerRpc → 팀 공용 키워드 1회 통과 누적 → 등록된 ITeamCheerRevert 되돌림
///   (힐·120초 쿨 폐기. 타임아웃/표 리셋도 폐기 — 2026-09-14 3차 변경, §2.2. 새 RPC 없음.
///    Idle 외침 무시. Warning~Revert만 유효. 씬당 revert 하나 —
///    입 MouthController / 침 SalivaHazard / 혀 TongueController)
/// - 기준 소리 버전 NetworkVariable (Host write, Everyone read) + 청크 배포 RPC
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

    // ── TeamCheerSound (§14.5) ───────────────────────────────────

    /// <summary>Host 기준 소리 버전. 0 = 아직 없음(게이트 막힘). Host가 다시 녹음하면 +1 → 전원 등록본 무효.</summary>
    readonly NetworkVariable<int> _teamCheerSoundVersion = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>이번 씬 연습 창(§14.2 ②)에서 통과한 인원 수 — GatherZone 간판 "(N/M)" 표시용.</summary>
    readonly NetworkVariable<int> _practicePassedCount = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>지금 열린 팀 응원 창이 연습 창인지(Tutorial·Interlude 표지판으로 연 창). 실패 횟수 셈용(§14.8).</summary>
    readonly NetworkVariable<bool> _practiceWindow = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    const int SoundChunkBytes = 4096;

    // Host만: 현재 버전의 배포 원본. 다음 씬에서는 CheerSoundLocalState(Host 자신도 클라이언트)에서 되살린다.
    byte[] _hostClipMuLaw;
    byte[] _hostFeatureBytes;

    // 클라이언트: 조립 중인 청크
    int _rxVersion = -1;
    byte[][] _rxClipChunks, _rxFeatureChunks;
    int _rxClipTotal, _rxFeatureTotal;

    readonly HashSet<ulong> _practicePassed = new();
    bool _practiceWindowPendingHost;

    // ── 준비 상태 공유 (10/7 — 간판 사람별 목록·연습 거절) ───────

    /// <summary>Host "마이크 없음 — 팀 전체 T키" 토글. 켜지면 음성 응원 없이 전원 T키, 게이트는 녹음 조건을 뺀다.</summary>
    readonly NetworkVariable<bool> _teamNoMic = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>사람별 준비 상태(색 순서). Host가 보고·연습 기록으로 만들어 전원에게 동기화 — 늦게 온 사람도 그대로 받는다.</summary>
    readonly NetworkList<CheerReadyEntry> _readyList = new();

    readonly Dictionary<ulong, byte> _reportedFlags = new(); // Host: clientId → CheerSoundLocalState.LocalStatusFlags
    float _readyRebuildAt;
    int _sceneStartVersion;
    bool _sceneStartTeamNoMic;

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

    /// <summary>기준 소리 버전 변경(NV) — 스폰 직후 1회 포함. 실제 소리 도착은 CheerSoundLocalState.HostSoundChanged.</summary>
    public event System.Action OnTeamCheerSoundVersionChanged;

    /// <summary>연습 통과 인원 변경 (passed, 연습 창 여부). GatherZone 간판 구독.</summary>
    public event System.Action<int> OnPracticePassedCountChanged;

    /// <summary>사람별 준비 상태 목록이 바뀜. 간판 구독.</summary>
    public event System.Action OnReadyListChanged;

    /// <summary>연습 요청이 거절됨(준비 안 된 사람이 있음) — 전 머신. 간판 깜빡임·표지판 안내.</summary>
    public event System.Action OnPracticeRejected;

    // ── 공개 프로퍼티 ─────────────────────────────────────────────

    public float CooldownDuration => cheerCooldownSeconds;
    public PlayerBuffSystem.BuffType StageBuffType => stageBuffType;
    public bool IsHazardWindowActive => _hazardWindow;

    public int TeamCheerSoundVersion => _teamCheerSoundVersion.Value;
    /// <summary>Host가 기준 소리를 확정했는가 — Tutorial 게이트 필수 조건(§14.2).</summary>
    public bool HasTeamCheerSound => _teamCheerSoundVersion.Value > 0;
    public bool IsPracticeWindow => _practiceWindow.Value;
    public int PracticePassedCount => _practicePassedCount.Value;
    public bool TeamNoMic => _teamNoMic.Value;

    /// <summary>사람별 준비 상태(전 머신에서 읽기).</summary>
    public int ReadyCount => _readyList.Count;
    public CheerReadyEntry GetReadyEntry(int i) => _readyList[i];

    /// <summary>
    /// 이 씬 게이트가 연습 통과를 요구하는가. Tutorial은 항상, Interlude는 Host가 이 씬에서 녹음을 바꿨거나
    /// 팀 전체 마이크 없음을 바꿨을 때만(10/7 사용자 결정 — 안 바꿨으면 그대로 진행, 개인 재녹음·연습은 자유).
    /// </summary>
    public bool PracticeRequired =>
        TutorialNetworkManager.Instance != null
        || (InterludeNetworkManager.Instance != null
            && (_teamCheerSoundVersion.Value != _sceneStartVersion || _teamNoMic.Value != _sceneStartTeamNoMic));

    /// <summary>Host 전용 — 접속자 전원이 연습을 열 수 있는 상태인가(등록 완료·마이크 없음·팀 전체 T키).</summary>
    public bool AllReadyForPractice()
    {
        foreach (var (id, _) in PlayerSpawnCoordinator.GetAllEntries())
        {
            _reportedFlags.TryGetValue(id, out byte f);
            if (!CheerSoundLocalState.IsReadyFlags(f, _teamNoMic.Value)) return false;
        }
        return true;
    }

    /// <summary>Host 전용 — 접속자 전원이 이번 씬 연습 창을 통과했는가(§14.2 게이트 조건).</summary>
    public bool AllPracticePassed(IEnumerable<ulong> clientIds)
    {
        foreach (ulong id in clientIds)
            if (!_practicePassed.Contains(id)) return false;
        return true;
    }

    // ── 라이프사이클 ───────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Host: 이전 씬에서 확정한 기준 소리를 CheerSoundLocalState에서 되살려 NV 버전을 씨딩(세션 스냅샷 대체).
        if (IsServer && CheerSoundLocalState.HasHostSound)
        {
            _hostClipMuLaw = CheerSoundLocalState.HostClipMuLaw;
            _hostFeatureBytes = CheerSoundLocalState.HostTemplate.Serialize();
            _teamCheerSoundVersion.Value = CheerSoundLocalState.HostVersion;
        }

        if (IsServer) _teamNoMic.Value = CheerSoundLocalState.TeamNoMic;
        _sceneStartVersion = _teamCheerSoundVersion.Value;
        _sceneStartTeamNoMic = _teamNoMic.Value;

        _teamCheerSoundVersion.OnValueChanged += HandleSoundVersionNv;
        _practicePassedCount.OnValueChanged += HandlePracticeCountNv;
        _teamNoMic.OnValueChanged += HandleTeamNoMicNv;
        _readyList.OnListChanged += HandleReadyListChanged;
        CheerSoundLocalState.EnrollmentChanged += ReportLocalStatus;
        CheerSoundLocalState.HostSoundChanged += ReportLocalStatus;
        CheerSoundLocalState.SetTeamNoMic(_teamNoMic.Value);
        ReportLocalStatus();

        // 씨딩 write는 OnValueChanged를 안 태우므로 UI 이벤트를 직접 쏜다.
        OnTeamCheerSoundVersionChanged?.Invoke();
        OnPracticePassedCountChanged?.Invoke(_practicePassedCount.Value);
        EnsureHostSoundLocally();
    }

    public override void OnNetworkDespawn()
    {
        _teamCheerSoundVersion.OnValueChanged -= HandleSoundVersionNv;
        _practicePassedCount.OnValueChanged -= HandlePracticeCountNv;
        _teamNoMic.OnValueChanged -= HandleTeamNoMicNv;
        _readyList.OnListChanged -= HandleReadyListChanged;
        CheerSoundLocalState.EnrollmentChanged -= ReportLocalStatus;
        CheerSoundLocalState.HostSoundChanged -= ReportLocalStatus;
        _revert = null;

        // NotifyHazardWindow(false)의 표 리셋이 despawn 중에 ClientRpc를 쏘지 않도록 먼저 비운다.
        _teamVotes.Clear();
        _teamWindowConsumed = false;

        NotifyHazardWindow(false);
        if (Instance == this) Instance = null;
    }

    void HandleSoundVersionNv(int previous, int current)
    {
        OnTeamCheerSoundVersionChanged?.Invoke();
        EnsureHostSoundLocally();
    }

    void HandlePracticeCountNv(int previous, int current) => OnPracticePassedCountChanged?.Invoke(current);

    void HandleTeamNoMicNv(bool previous, bool current) => CheerSoundLocalState.SetTeamNoMic(current);

    void HandleReadyListChanged(NetworkListEvent<CheerReadyEntry> e) => OnReadyListChanged?.Invoke();

    // ── 준비 상태 보고·집계 ─────────────────────────────────────

    /// <summary>내 상태가 바뀔 때마다 Host에 보고(전 머신, Host 자신 포함). 상태 비트 1바이트라 가볍다.</summary>
    void ReportLocalStatus()
    {
        if (!IsSpawned) return;
        ReportStatusServerRpc(CheerSoundLocalState.LocalStatusFlags);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportStatusServerRpc(byte flags, RpcParams rpcParams = default)
    {
        _reportedFlags[rpcParams.Receive.SenderClientId] = flags;
        _readyRebuildAt = 0f; // 다음 Update에서 바로 반영
    }

    /// <summary>Host 전용 — "마이크 없음 — 팀 전체 T키" 토글. 바꾸면 연습 기록을 비운다(조건이 바뀌었으므로).</summary>
    public void SetTeamNoMic(bool on)
    {
        if (!IsServer || !IsSpawned || _teamNoMic.Value == on) return;
        _teamNoMic.Value = on;
        CheerSoundLocalState.SetTeamNoMic(on);
        ResetPracticePassed();
        NetLog.Transition("CheerService", "TeamNoMic", $"on={on}");
    }

    /// <summary>Host — 연습 표지판이 준비 안 된 사람 때문에 연습을 거절할 때.</summary>
    public void BroadcastPracticeRejected()
    {
        if (!IsServer || !IsSpawned) return;
        NetLog.Transition("CheerService", "PracticeRejected", null);
        PracticeRejectedClientRpc();
    }

    [ClientRpc]
    void PracticeRejectedClientRpc() => OnPracticeRejected?.Invoke();

    /// <summary>Host — 접속자 색 순서대로 (색, 상태 비트, 연습 통과) 목록을 만들어, 바뀌었을 때만 NetworkList를 고친다.</summary>
    void RebuildReadyList()
    {
        var want = new List<CheerReadyEntry>();
        bool practiceNeeded = PracticeRequired;
        foreach (var (id, color) in PlayerSpawnCoordinator.GetAllEntries())
        {
            int idx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
            if (idx < 0) continue;
            _reportedFlags.TryGetValue(id, out byte f);
            want.Add(new CheerReadyEntry
            {
                ColorIndex = (byte)idx,
                Flags = f,
                Passed = (byte)(_practicePassed.Contains(id) || !practiceNeeded ? 1 : 0),
            });
        }
        want.Sort((a, b) => a.ColorIndex.CompareTo(b.ColorIndex));

        bool same = want.Count == _readyList.Count;
        for (int i = 0; same && i < want.Count; i++) same = want[i].Equals(_readyList[i]);
        if (same) return;

        _readyList.Clear();
        foreach (var e in want) _readyList.Add(e);
    }

    /// <summary>NV 버전과 내 로컬 기준 소리 버전이 다르면 Host에게 당겨 받는다(pull). Host 자신은 항상 같다.</summary>
    void EnsureHostSoundLocally()
    {
        int v = _teamCheerSoundVersion.Value;
        if (v <= 0 || IsServer || !IsSpawned) return;
        if (CheerSoundLocalState.HostVersion == v && CheerSoundLocalState.HasHostSound) return;
        RequestTeamCheerSoundServerRpc();
    }

    void Update()
    {
        if (!IsServer) return;
        var nm = NetworkManager;
        if (nm == null || !nm.IsListening) return;
        double now = nm.ServerTime.Time;
        CheckBuffEnd(now);

        // 준비 상태 목록 — 접속·이탈·보고·연습 통과를 한곳에서 따라간다(4명 이하, 0.25초마다 비교만)
        if (IsSpawned && Time.unscaledTime >= _readyRebuildAt)
        {
            _readyRebuildAt = Time.unscaledTime + 0.25f;
            RebuildReadyList();
        }
    }

    // ── Host-only TeamCheerSound (§14.5) ─────────────────────────

    /// <summary>
    /// Host 패널 [확정]이 IsServer 가드로 직접 호출(RPC 없음). 16kHz 녹음을 받아 특징을 뽑고 검증한 뒤
    /// 버전 +1, 전원에 배포. 실패 사유는 녹음 자체 문제(CheerClipIssue)뿐 — 금칙어·사전 검사는 없다.
    /// Host 자신의 등록본 나 1 = 이 녹음(§14.2). 재녹음이면 전원 등록본·연습 기록이 무효가 된다.
    /// </summary>
    public bool TrySetTeamCheerSound(float[] pcm16k, int count, CheerSoundTemplate template, out CheerClipIssue issue)
    {
        issue = CheerClipIssue.None;
        if (!IsServer || !IsSpawned) return false;
        if (template == null || template.Issue != CheerClipIssue.None)
        {
            issue = template?.Issue ?? CheerClipIssue.TooShort;
            return false;
        }

        // 발화 구간만 잘라 압축(앞뒤 침묵 제거는 Build가 계산해 둔 범위)
        int from = template.TrimStartSample, to = template.TrimEndSample;
        var trimmed = new float[to - from];
        System.Array.Copy(pcm16k, from, trimmed, 0, trimmed.Length);

        _hostClipMuLaw = CheerSoundCodec.Encode(trimmed, trimmed.Length);
        _hostFeatureBytes = template.Serialize();

        int version = _teamCheerSoundVersion.Value + 1;
        ResetTeamVotes();
        ResetPracticePassed();
        _teamCheerSoundVersion.Value = version;

        // Host 로컬도 클라이언트와 같은 경로로 저장(등록본 무효화 포함) → 그다음 나 1 = 이 녹음
        CheerSoundLocalState.SetHostSound(version, _hostClipMuLaw, _hostFeatureBytes);
        CheerSoundLocalState.SetTemplate1(template, trimmed);

        BroadcastSound(version, default);
        NetLog.Transition("CheerService", "TeamCheerSoundSet",
            $"version={version} clipBytes={_hostClipMuLaw.Length} featBytes={_hostFeatureBytes.Length} durMs={template.DurationMs} bursts={template.Bursts}");
        return true;
    }

    /// <summary>클라이언트 → Host: 내 로컬 버전이 NV와 다름 — 현재 기준 소리를 나에게만 다시 보내 달라.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestTeamCheerSoundServerRpc(RpcParams rpcParams = default)
    {
        if (_hostClipMuLaw == null || _hostFeatureBytes == null || _teamCheerSoundVersion.Value <= 0) return;
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
        };
        BroadcastSound(_teamCheerSoundVersion.Value, target);
    }

    void BroadcastSound(int version, ClientRpcParams target)
    {
        SendChunks(version, 0, _hostClipMuLaw, target);
        SendChunks(version, 1, _hostFeatureBytes, target);
    }

    void SendChunks(int version, byte kind, byte[] data, ClientRpcParams target)
    {
        int total = (data.Length + SoundChunkBytes - 1) / SoundChunkBytes;
        for (int i = 0; i < total; i++)
        {
            int len = System.Math.Min(SoundChunkBytes, data.Length - i * SoundChunkBytes);
            var chunk = new byte[len];
            System.Array.Copy(data, i * SoundChunkBytes, chunk, 0, len);
            ReceiveSoundChunkClientRpc(version, kind, i, total, chunk, target);
        }
    }

    /// <summary>kind 0 = 압축 클립(재생용), 1 = 특징(틀 검사 기준). 버전이 바뀌면 조립 중이던 건 버린다.</summary>
    [ClientRpc]
    void ReceiveSoundChunkClientRpc(int version, byte kind, int index, int total, byte[] chunk, ClientRpcParams rpcParams = default)
    {
        if (IsServer) return; // Host는 TrySetTeamCheerSound에서 이미 로컬 저장
        if (version <= 0 || total <= 0 || index < 0 || index >= total || chunk == null) return;

        if (_rxVersion != version)
        {
            _rxVersion = version;
            _rxClipChunks = null; _rxFeatureChunks = null;
            _rxClipTotal = _rxFeatureTotal = 0;
        }

        if (kind == 0)
        {
            if (_rxClipChunks == null || _rxClipTotal != total) { _rxClipChunks = new byte[total][]; _rxClipTotal = total; }
            _rxClipChunks[index] = chunk;
        }
        else
        {
            if (_rxFeatureChunks == null || _rxFeatureTotal != total) { _rxFeatureChunks = new byte[total][]; _rxFeatureTotal = total; }
            _rxFeatureChunks[index] = chunk;
        }

        if (!Complete(_rxClipChunks) || !Complete(_rxFeatureChunks)) return;

        byte[] clip = Join(_rxClipChunks), feat = Join(_rxFeatureChunks);
        _rxClipChunks = null; _rxFeatureChunks = null;
        CheerSoundLocalState.SetHostSound(version, clip, feat);
        NetLog.Transition("CheerService", "TeamCheerSoundReceived", $"version={version} clipBytes={clip.Length} featBytes={feat.Length}");
    }

    static bool Complete(byte[][] chunks)
    {
        if (chunks == null) return false;
        foreach (var c in chunks) if (c == null) return false;
        return true;
    }

    static byte[] Join(byte[][] chunks)
    {
        int len = 0;
        foreach (var c in chunks) len += c.Length;
        var all = new byte[len];
        int o = 0;
        foreach (var c in chunks) { System.Array.Copy(c, 0, all, o, c.Length); o += c.Length; }
        return all;
    }

    // ── 연습 창 (§14.2 ②) ────────────────────────────────────────

    /// <summary>Host 전용 — Tutorial/Interlude 연습 표지판이 창을 열기 직전에 호출. 다음 NotifyHazardWindow(true)가 연습 창이 된다.</summary>
    public void MarkNextWindowAsPractice()
    {
        if (!IsServer) return;
        _practiceWindowPendingHost = true;
    }

    void ResetPracticePassed()
    {
        if (!IsServer) return;
        _practicePassed.Clear();
        if (IsSpawned) _practicePassedCount.Value = 0;
    }

    /// <summary>Host 전용 — 접속 끊긴 클라이언트는 통과 집합에서 뺀다(게이트 전 이탈 §6B.4).</summary>
    public void ForgetClient(ulong clientId)
    {
        if (!IsServer) return;
        if (_practicePassed.Remove(clientId) && IsSpawned)
            _practicePassedCount.Value = _practicePassed.Count;
        _reportedFlags.Remove(clientId);
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
                if (IsSpawned) _practiceWindow.Value = _practiceWindowPendingHost;
                _practiceWindowPendingHost = false;
            }
            else
            {
                if (IsSpawned) _practiceWindow.Value = false;
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

        // 연습 창이면 통과 기록(§14.2 게이트 조건). 음성이든 T키든 통과는 통과(§14.8 — 관문이 막히지 않게).
        if (_practiceWindow.Value && _practicePassed.Add(cheererId))
        {
            _practicePassedCount.Value = _practicePassed.Count;
            NetLog.Transition("CheerService", "PracticePassed", $"client={cheererId} voice={isVoice} passed={_practicePassed.Count}");
        }

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
    // [2026-09-14] 개인 CheerName 커스텀화 완전 삭제 — 이름은 이제 PlayerColorUtil.DefaultCheerNames
    // (berry/guma/sook/dan) 고정값 하나뿐이라 NV·세션 스냅샷 우선순위 역전 로직이 전부 불필요해졌다.

    /// <summary>이름 → colorIndex(색 기본값 매칭). 미매칭 시 -1.</summary>
    public static int GetColorIndex(string cheerName)
    {
        string lower = cheerName.Trim().ToLower();
        return System.Array.IndexOf(PlayerColorUtil.DefaultCheerNames, lower);
    }

    /// <summary>colorIndex → CheerName(색 기본값).</summary>
    public static string GetCheerName(int colorIndex)
    {
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

/// <summary>간판 사람별 상태 한 줄 — 색, 상태 비트(CheerSoundLocalState.LocalStatusFlags), 연습 통과(또는 이 씬에선 연습 불필요).</summary>
public struct CheerReadyEntry : INetworkSerializeByMemcpy, System.IEquatable<CheerReadyEntry>
{
    public byte ColorIndex;
    public byte Flags;
    public byte Passed;

    public bool Equals(CheerReadyEntry o) => ColorIndex == o.ColorIndex && Flags == o.Flags && Passed == o.Passed;
    public override bool Equals(object obj) => obj is CheerReadyEntry o && Equals(o);
    public override int GetHashCode() => ColorIndex | (Flags << 8) | (Passed << 16);
}
