using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tutorial 씬(구 로비 사전 게이트 구간) 네트워크 매니저. NetworkBehaviour.
/// Tutorial 씬 안 NetworkObject GameObject에 부착.
///
/// [역할 — NetworkDesign.md §6B.2/§6B.3/§6B.4, §6B.7 P1+P2+P4+P5]
/// - Host/Client가 Tutorial에 접속하는 즉시 캐릭터 스폰 (대기 없음, 색 자동배정)
/// - PlayerSpawnCoordinator(구 로비가 게임 시작 시 배치로 채우던 것)를 접속자 1명씩 증분 갱신
/// - 이탈 정책: Client = 캐릭터(슬롯)만 제거·방 유지 / Host = 방 전체 종료 (예외 없음)
/// - TutorialGatherZone 헤드카운트 게이트 판정(Host 전용) → 통과 시 구 LobbyNetworkManager.
///   StartGameServerRpc가 하던 세션 확정(색·시드·세션시각) + M.Stage1 로드까지 수행
///
/// [배치]
/// Tutorial 씬 빈 GameObject → NetworkObject + TutorialNetworkManager 추가.
/// coordinatorPrefab: LobbyNetworkManager와 동일한 PlayerSpawnCoordinator 프리팹 연결.
/// 씬에 TutorialGatherZone을 1개 배치(색 무관 단일 존) — Instance로 자동 참조, 별도 연결 불필요.
///
/// [ESC 메뉴 나가기 버튼]
/// OnClickLeaveRoom() — DisconnectManager.OnClickLeaveRoom()과 동일 시그니처.
/// Tutorial 씬 ESC 메뉴의 Quit 버튼 OnClick을 이 컴포넌트의 OnClickLeaveRoom()으로 연결하면
/// 게이트 통과 전 전용 이탈 정책(§6B.4)이 적용된다 — 인게임(DisconnectManager)과는 다른 문.
/// </summary>
public class TutorialNetworkManager : NetworkBehaviour
{
    public static TutorialNetworkManager Instance { get; private set; }

    [Header("DontDestroyOnLoad 시스템 Prefab")]
    [Tooltip("PlayerSpawnCoordinator prefab (NetworkObject 포함). LobbyNetworkManager와 동일 프리팹.\n" +
             "NGO의 Network Prefab List에 반드시 등록되어 있어야 함.")]
    [SerializeField] private NetworkObject coordinatorPrefab;

    [Header("게이트 카운트다운 (§6B.3)")]
    [Tooltip("전원 존 점유 유지 후 게이트 통과까지 걸리는 시간(초). 중간에 이탈/신규 합류 시 리셋.")]
    [SerializeField] private float gateCountdownDuration = 3f;

    [Header("게이트 이벤트 (UI 연결용, §6B.7 P7)")]
    [Tooltip("매 프레임 카운트다운 남은 시간(0~gateCountdownDuration) 전달. 카운트다운 중 아니면 duration 값.")]
    public UnityEvent<float> OnGateCountdownTick;
    [Tooltip("카운트다운 리셋 시 호출 (이탈/인원 변경 등).")]
    public UnityEvent OnGateCountdownReset;
    [Tooltip("카운트다운 완료 직후, M.Stage1 로드 직전에 발동.")]
    public UnityEvent OnGateCountdownComplete;

    // clientId → 스폰된 Player NetworkObject. 이탈 시 Despawn 대상 조회용 (Host 전용).
    readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new();

    bool _isQuitting;

    // ── 게이트 상태 (Host 전용) ──────────────────────────────────
    bool  _gateCompleted;
    bool  _isCounting;
    float _countdown;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // ── DEV 전용: 스테이지 바로가기 (구 LobbyMenuController 스테이지 드롭다운 대체) ──
    // Build Settings의 "Development Build" 체크 여부로 자동 on/off — 정식 출시(체크 해제) 빌드에는
    // 이 필드/메서드 자체가 컴파일되지 않아 코드가 남지 않는다. TutorialDevStageJumpUI가 호출.
    int _devTargetStageIndex = -1;

    /// <summary>
    /// 테스트용: 게이트 통과 시 이동할 목표 스테이지 인덱스(SceneFlowManager.sceneSequence 기준)를
    /// 지정한다. Host 전용 — Client가 호출해도 무시. CompleteGate() 호출 전까지 몇 번이든 재지정 가능.
    /// </summary>
    public void SetDevTargetStage(int index)
    {
        if (!IsHost)
        {
            Debug.LogWarning("[TutorialNetworkManager] SetDevTargetStage — Host가 아니라 무시됨");
            return;
        }
        _devTargetStageIndex = index;
        Debug.Log($"[TutorialNetworkManager] DEV 목표 스테이지 지정 — index={index}");
    }
#endif

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            NetworkManager.OnClientConnectedCallback  += OnClientJoined;
            NetworkManager.OnClientDisconnectCallback += OnClientLeft;

            EnsureCoordinatorSpawned();
            AssignColorAndSpawn(NetworkManager.LocalClientId);
        }
        else
        {
            // Client: 내 연결이 끊기면(Host 이탈 등) 타이틀 복귀 — 인게임 DisconnectManager와 동일 패턴,
            // 단 §6B.4에 따라 Reason만 다르게 남긴다(LobbyQuit 계열 문맥은 OnClickLeaveRoom에서 처리).
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedSelf;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsHost)
        {
            NetworkManager.OnClientConnectedCallback  -= OnClientJoined;
            NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        }
        else
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedSelf;
        }

        if (Instance == this) Instance = null;
    }

    // ── 코디네이터 준비 (Host 전용) ─────────────────────────────────

    /// <summary>
    /// PlayerSpawnCoordinator가 아직 없으면(=Tutorial 세션 최초 접속) 빈 매핑으로 스폰.
    /// 이후 색 배정은 AddColorEntry/RemoveColorEntry로 접속자 1명씩 증분 갱신.
    /// </summary>
    void EnsureCoordinatorSpawned()
    {
        if (PlayerSpawnCoordinator.Instance != null) return;

        if (coordinatorPrefab == null)
        {
            Debug.LogError("[TutorialNetworkManager] coordinatorPrefab 미설정 — Inspector에서 연결 필요");
            return;
        }

        var coordGo     = Instantiate(coordinatorPrefab.gameObject);
        var coordinator = coordGo.GetComponent<PlayerSpawnCoordinator>();
        coordinator.PrepareColors(new Dictionary<ulong, PlayerColorType>());
        coordGo.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
    }

    // ── 접속·이탈 콜백 (Host 전용) ────────────────────────────────

    void OnClientJoined(ulong clientId)
    {
        // Host 자신은 OnNetworkSpawn에서 이미 처리됨
        if (clientId == NetworkManager.LocalClientId) return;

        // 이미 스폰돼 있으면(중복 콜백) 무시
        if (_spawnedPlayers.ContainsKey(clientId)) return;

        AssignColorAndSpawn(clientId);

        // Host 자신의 스폰 시점에 이미 NotifyPlayersReady()가 발행된 뒤라, 그 뒤에 합류하는
        // Client는 당시의 브로드캐스트를 놓친다 — 개별 캐치업으로 카메라 바인드 등
        // §11.3 Ready Consumer들이 정상 발화하게 한다(늦은 구독 자체는 기존 패턴 그대로).
        PlayerSpawnCoordinator.Instance?.CatchUpReadyFor(clientId);
    }

    void OnClientLeft(ulong clientId)
    {
        // §6B.4: 게이트 통과 전 Client 이탈 = 캐릭터(슬롯)만 제거, 방 유지.
        if (_spawnedPlayers.TryGetValue(clientId, out var netObj))
        {
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
            _spawnedPlayers.Remove(clientId);
        }

        PlayerSpawnCoordinator.Instance?.RemoveColorEntry(clientId);

        // 물리 OnTriggerExit이 Despawn 시 항상 발동하는 건 아니므로 헤드카운트 stale 방지용 강제 정리.
        TutorialGatherZone.Instance?.RemoveOccupant(clientId);
    }

    // ── 사전 게이트 (§6B.3, Host 전용) ────────────────────────────

    void Update()
    {
        if (!IsHost || _gateCompleted) return;
        UpdateGate();
    }

    /// <summary>
    /// 존 안 인원 == 접속 인원(헤드카운트, 색 무관)이면 카운트다운 진행.
    /// 도중 인원이 안 맞게 되면(이탈/미충족) 즉시 리셋 — StageStartGate와 동일 원칙.
    /// </summary>
    void UpdateGate()
    {
        var zone = TutorialGatherZone.Instance;
        if (zone == null) return;

        int connected = PlayerSpawnCoordinator.EntryCount;
        bool allIn = connected > 0 && zone.OccupantCount == connected;

        if (!allIn)
        {
            if (_isCounting) ResetGateCountdown();
            return;
        }

        if (!_isCounting)
        {
            _isCounting = true;
            _countdown  = gateCountdownDuration;
            OnGateCountdownTick?.Invoke(_countdown);
        }

        _countdown -= Time.deltaTime;
        OnGateCountdownTick?.Invoke(Mathf.Max(0f, _countdown));

        if (_countdown <= 0f)
            CompleteGate();
    }

    void ResetGateCountdown()
    {
        _isCounting = false;
        _countdown  = gateCountdownDuration;
        OnGateCountdownReset?.Invoke();
        OnGateCountdownTick?.Invoke(gateCountdownDuration);
    }

    /// <summary>
    /// 게이트 통과 확정. 구 LobbyNetworkManager.StartGameServerRpc의 세션 확정 로직을 그대로 옮김 —
    /// PlayerSpawnCoordinator는 Tutorial 접속 시점에 이미 스폰돼 색 데이터를 들고 있으므로
    /// 재스폰은 불필요(§6B.2). CheerName/DisplayName 세션 확정은 이 메서드에서 처리
    /// (§6B.7 P6·P3 두 번째 항목, 아래 참고). VoiceId 세션 확정은 여전히 미구현 —
    /// 확정 전까지 GameSession의 기본 폴백값(null)으로 동작한다.
    /// </summary>
    void CompleteGate()
    {
        if (_gateCompleted) return;
        _gateCompleted = true;

        OnGateCountdownComplete?.Invoke();
        Debug.Log("[TutorialNetworkManager] 게이트 통과 — M.Stage1 진입 처리 시작");

        var clientColorDict = new Dictionary<ulong, PlayerColorType>();
        var colorList = new List<PlayerColorType>();
        foreach (var (id, color) in PlayerSpawnCoordinator.GetAllEntries())
        {
            clientColorDict[id] = color;
            colorList.Add(color);
        }

        if (clientColorDict.Count == 0)
        {
            Debug.LogError("[TutorialNetworkManager] 게이트 완료 — clientColorDict 비어 있음, 중단");
            return;
        }

        if (PlayerSpawnManager.Instance == null)
        {
            Debug.LogError("[TutorialNetworkManager] 게이트 완료 — PlayerSpawnManager.Instance null, 중단");
            return;
        }
        PlayerSpawnManager.Instance.InitializeOnline(clientColorDict);

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = seed;
        BroadcastSeedClientRpc(seed);

        double sessionStart = NetworkManager.ServerTime.Time;
        NetworkSessionData.SessionStartServerTime = sessionStart;
        BroadcastSessionStartClientRpc(sessionStart);

        // Client는 OnPlayersReady 이후 PlayerSpawnCoordinator에서 확정값을 읽음.
        GameSession.Instance?.SetActiveColors(colorList.ToArray());

        // §6B.7 P6 — 세션 CheerName 확정. 별도 "확정" 단계 없이 이 시점(게이트 통과)의 각자
        // 최신값이 그대로 최종값(CheerAndTutorialDesign.md §3.4). PlayerCheerNameSync NV는 이미
        // Everyone-read라 전 Client가 로컬로도 동일 배열을 만들 수 있지만, 씬 전환 직전이라는
        // 정확히 같은 시점에 적용되도록 구 LobbyNetworkManager.StartGameInternal과 동일하게
        // Host가 계산해 명시적으로 배포한다.
        var sessionNames = BuildSessionCheerNames(clientColorDict);
        GameSession.Instance?.SetSessionCheerNames(sessionNames);
        BroadcastSessionCheerNamesClientRpc(
            new FixedString32Bytes(sessionNames[0]), new FixedString32Bytes(sessionNames[1]),
            new FixedString32Bytes(sessionNames[2]), new FixedString32Bytes(sessionNames[3]));

        // §6B.7 P3 두 번째 항목 — 세션 DisplayName 확정. CheerName과 동일 패턴(게이트 통과 시점의
        // 각자 최신 보고값이 그대로 최종값). PlayerDisplayNameSync NV도 이미 Everyone-read지만,
        // CheerName과 동일하게 정확히 같은 시점에 적용되도록 Host가 명시적으로 계산해 배포한다.
        var sessionDisplayNames = BuildSessionDisplayNames(clientColorDict);
        GameSession.Instance?.SetSessionDisplayNames(sessionDisplayNames);
        BroadcastSessionDisplayNamesClientRpc(
            new FixedString64Bytes(sessionDisplayNames[0]), new FixedString64Bytes(sessionDisplayNames[1]),
            new FixedString64Bytes(sessionDisplayNames[2]), new FixedString64Bytes(sessionDisplayNames[3]));

        if (SceneFlowManager.Instance == null)
        {
            Debug.LogError("[TutorialNetworkManager] 게이트 완료 — SceneFlowManager.Instance null, 씬 전환 중단");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_devTargetStageIndex >= 0)
        {
            Debug.Log($"[TutorialNetworkManager] DEV 목표 스테이지로 진입 — index={_devTargetStageIndex}");
            SceneFlowManager.Instance.LoadSceneByIndex(_devTargetStageIndex);
            return;
        }
#endif
        SceneFlowManager.Instance.LoadNextScene();
    }

    /// <summary>시드를 모든 클라이언트에 배포.</summary>
    [ClientRpc]
    void BroadcastSeedClientRpc(int seed)
    {
        NetworkSessionData.Seed = seed;
    }

    /// <summary>세션 시작 서버 시각을 모든 클라이언트에 배포.</summary>
    [ClientRpc]
    void BroadcastSessionStartClientRpc(double serverTime)
    {
        NetworkSessionData.SessionStartServerTime = serverTime;
    }

    /// <summary>
    /// 게이트 완료 시점의 각 플레이어 유효 CheerName을 colorIndex 순 배열로 확정(§6B.7 P6).
    /// PlayerCheerNameSync.GetAllEffectiveNames()가 (clientId, 이름) 전체를 훑어주므로,
    /// clientColorDict로 clientId→colorIndex만 매칭하면 된다 — 빈 색 인덱스는 기본값 유지.
    /// </summary>
    static string[] BuildSessionCheerNames(Dictionary<ulong, PlayerColorType> clientColorDict)
    {
        var names = new string[4];
        for (int i = 0; i < 4; i++) names[i] = PlayerColorUtil.DefaultCheerNames[i];

        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (!clientColorDict.TryGetValue(clientId, out var color)) continue;
            int ci = PlayerColorUtil.ColorTypeToIndex(color);
            if (ci >= 0) names[ci] = name;
        }
        return names;
    }

    /// <summary>세션 확정 CheerName을 모든 클라이언트의 GameSession에 배포(§6B.7 P6).</summary>
    [ClientRpc]
    void BroadcastSessionCheerNamesClientRpc(FixedString32Bytes n0, FixedString32Bytes n1,
                                              FixedString32Bytes n2, FixedString32Bytes n3)
    {
        if (IsHost) return; // Host 자신은 CompleteGate()에서 이미 로컬 적용
        GameSession.Instance?.SetSessionCheerNames(
            new[] { n0.ToString(), n1.ToString(), n2.ToString(), n3.ToString() });
    }

    /// <summary>
    /// 게이트 완료 시점의 각 플레이어 보고된 DisplayName을 colorIndex 순 배열로 확정(§6B.7 P3).
    /// PlayerDisplayNameSync.GetAllEffectiveNames()가 (clientId, 보고값) 전체를 훑어주므로,
    /// clientColorDict로 clientId→colorIndex만 매칭하면 된다 — 미보고/빈 값은 "Player" 폴백
    /// (GameSession.GetSessionDisplayName 기본 폴백과 동일 값).
    /// </summary>
    static string[] BuildSessionDisplayNames(Dictionary<ulong, PlayerColorType> clientColorDict)
    {
        var names = new string[4];
        for (int i = 0; i < 4; i++) names[i] = "Player";

        foreach (var (clientId, name) in PlayerDisplayNameSync.GetAllEffectiveNames())
        {
            if (!clientColorDict.TryGetValue(clientId, out var color)) continue;
            if (string.IsNullOrEmpty(name)) continue;
            int ci = PlayerColorUtil.ColorTypeToIndex(color);
            if (ci >= 0) names[ci] = name;
        }
        return names;
    }

    /// <summary>세션 확정 DisplayName을 모든 클라이언트의 GameSession에 배포(§6B.7 P3).</summary>
    [ClientRpc]
    void BroadcastSessionDisplayNamesClientRpc(FixedString64Bytes n0, FixedString64Bytes n1,
                                                FixedString64Bytes n2, FixedString64Bytes n3)
    {
        if (IsHost) return; // Host 자신은 CompleteGate()에서 이미 로컬 적용
        GameSession.Instance?.SetSessionDisplayNames(
            new[] { n0.ToString(), n1.ToString(), n2.ToString(), n3.ToString() });
    }

    // ── 스폰 (색 자동배정, §6B.2) ────────────────────────────────

    void AssignColorAndSpawn(ulong clientId)
    {
        PlayerColorType color = GetNextFreeColor();
        AssignColorAndSpawn(clientId, color);
    }

    void AssignColorAndSpawn(ulong clientId, PlayerColorType color)
    {
        if (PlayerSpawnManager.Instance == null)
        {
            Debug.LogError("[TutorialNetworkManager] PlayerSpawnManager.Instance null — 캐릭터 스폰 중단");
            return;
        }

        var prefab = PlayerSpawnManager.Instance.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[TutorialNetworkManager] PlayerSpawnManager.PlayerPrefab 미설정 — 캐릭터 스폰 중단");
            return;
        }

        PlayerSpawnCoordinator.Instance?.AddColorEntry(clientId, color);

        Vector3 pos = PlayerSpawnManager.Instance.GetFixedSpawnPos(color);
        var go     = Instantiate(prefab, pos, Quaternion.identity);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[TutorialNetworkManager] playerPrefab에 NetworkObject 없음");
            Destroy(go);
            return;
        }

        // §6B.2 확정: destroyWithScene:true — TutorialGatherZone 통과 후 M.Stage1 로드 시
        // 씬 언로드로 자동 Despawn (§11 ②Spawn 배치 스폰과 충돌 없음).
        netObj.SpawnWithOwnership(clientId, destroyWithScene: true);
        _spawnedPlayers[clientId] = netObj;

        var setup = go.GetComponent<NetworkPlayerSetup>();
        setup?.SetColorIndex(PlayerColorUtil.ColorTypeToIndex(color));

        // §11.3 Ready 늦은 구독 패턴 재사용: 최초 1명만 발행하면, 이후 합류자는 자기 OnNetworkSpawn에서
        // IsReady==true를 보고 즉시 카메라 바인드 — 매 접속마다 재호출할 필요 없음.
        if (!PlayerSpawnCoordinator.IsReady)
            PlayerSpawnCoordinator.Instance?.NotifyPlayersReady();

        Debug.Log($"[TutorialNetworkManager] 스폰 — clientId={clientId} color={color} pos={pos} netId={netObj.NetworkObjectId}");
    }

    /// <summary>현재 코디네이터에 등록된 색 중 비어있는 첫 색 반환. 꽉 차 있으면 Blue 폴백(발생하면 안 됨 — maxConnections=4).</summary>
    PlayerColorType GetNextFreeColor()
    {
        var used = new HashSet<PlayerColorType>();
        foreach (var entry in PlayerSpawnCoordinator.GetAllEntries())
            used.Add(entry.Color);

        foreach (var color in PlayerColorUtil.ColorOrder)
            if (!used.Contains(color)) return color;

        Debug.LogWarning("[TutorialNetworkManager] 빈 색 없음(4인 초과?) — Blue 폴백");
        return PlayerColorUtil.ColorOrder[0];
    }

    // ── 이탈 감지 (Client 전용) ───────────────────────────────────

    /// <summary>
    /// Client: 내 연결이 끊기면 타이틀 복귀. Host가 Tutorial에서 나가면(=서버 종료) 발생.
    /// NotifyHostQuitClientRpc가 먼저 도착해 TitleReturnFlow가 이미 처리 중이면
    /// Request()의 _isReturning 가드가 중복 호출을 무시한다.
    /// </summary>
    void OnClientDisconnectedSelf(ulong clientId)
    {
        bool isSelf = clientId == NetworkManager.LocalClientId || !NetworkManager.IsListening;
        if (!isSelf) return;

        Debug.Log("[TutorialNetworkManager] 연결 끊김 — 타이틀 복귀");
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.ClientDisconnected,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── 나가기 버튼 (ESC 메뉴, §6B.4) ─────────────────────────────

    /// <summary>
    /// Tutorial ESC 메뉴 Quit 버튼 OnClick 대상.
    /// Host: 남은 Client 전원에 즉시 타이틀 복귀를 알린 뒤 자신도 복귀 — 게이트 통과 전 예외 없이 방 종료.
    /// Client: 자신만 타이틀 복귀 (슬롯만 비움, 방 유지 — Host의 OnClientLeft가 캐릭터 정리).
    /// DisconnectManager.OnClickLeaveRoom()과 동일 시그니처/이름 — 인게임과 헷갈리지 않도록
    /// Reason만 LobbyQuit으로 구분한다(§6A.1).
    /// </summary>
    public void OnClickLeaveRoom()
    {
        if (_isQuitting) return;
        _isQuitting = true;

        if (IsHost)
        {
            NotifyHostQuitClientRpc();
            TitleReturnFlow.Instance?.Request(new TitleReturnOptions
            {
                Reason = TitleReturnReason.LobbyQuit,
                Scope  = TitleReturnScope.SessionOnly,
            });
        }
        else
        {
            TitleReturnFlow.Instance?.Request(new TitleReturnOptions
            {
                Reason = TitleReturnReason.LobbyQuit,
                Scope  = TitleReturnScope.SessionOnly,
            });
        }
    }

    [ClientRpc]
    void NotifyHostQuitClientRpc()
    {
        if (IsHost) return;
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.HostQuitRoom,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 스폰 현황 출력")]
    void Debug_PrintState()
    {
        Debug.Log($"[TutorialNetworkManager] 스폰된 플레이어 수: {_spawnedPlayers.Count}, " +
                  $"CoordinatorEntries: {PlayerSpawnCoordinator.EntryCount}");
        foreach (var kv in _spawnedPlayers)
            Debug.Log($"  clientId={kv.Key} netId={(kv.Value != null ? kv.Value.NetworkObjectId.ToString() : "null")}");
    }
#endif
}
