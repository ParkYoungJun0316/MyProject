using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 흐름 관리자. DontDestroyOnLoad 싱글턴.
///
/// [배치 방법]
/// 1. M.Stage1 씬에 빈 GameObject 생성 → SceneFlowManager 컴포넌트 추가
/// 2. sceneSequence[] 에 순서대로 씬 이름 입력
///    M.Stage1 / M.Stage2 / M.Stage3 / M.Stage4 / M.Stage5 / M.Boss
///    T.Stage1 / T.Stage2 / T.Stage3 / T.Stage4 / T.Stage5 / T.Boss
/// 3. 전환 연출(암전/최소 유지시간)은 LoadingCurtain(DDOL, 0.Title 배치)이 전담한다 —
///    LoadingCurtain.Instance가 없으면 연출 없이 즉시 전환.
///
/// [이벤트 연결 — 확정 배선 (NetworkDesign §11.1)]
/// StageManager.OnStageClear / PhaseManager.onAllPhasesComplete
///   → SceneFlowRelay.LoadNextScene (씬 배치) → 여기 LoadNextScene
/// DDOL이라 씬 Inspector에서 직접 연결 불가 — 반드시 Relay 경유.
///
/// [클리어 → 정지 → 배너 → 전환 (2026-09-08)]
/// LoadNextScene() 진입 즉시 FreezeAllHazardsNow()로 씬의 모든 함정·팀응원 함정을 정지시키고
/// (이 머신은 로컬 직접 호출 + StageNetworkState.BroadcastStageClearFreezeToClients()로 나머지
/// 머신에 전파 → 전원 동일 프레임 근처에 정지), clearToTransitionDelay(기본 2.5초,
/// StageClearBannerUI 총 재생시간과 맞춤)만큼 대기한 뒤에야 암전을 시작한다. 중간 Phase 클리어(OnAnyStageClearedPulse)는 이 정지 대상이 아니다 —
/// 오직 "다음 씬으로 넘어가는" 이 진입점에서만 씬 전체를 멈춘다.
///
/// [사망·Reset 리로드]
/// 사망·ESC Reset 모두 StageNetworkState.NotifyPlayerDeathServerRpc 담당 (§11.1).
/// 이 클래스는 클리어 → 다음 씬 전환만 처리한다.
/// </summary>
public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    [Header("씬 순서")]
    [Tooltip("순서대로 진행할 씬 이름. Build Settings 등록 이름과 정확히 일치해야 함.")]
    [SerializeField] private string[] sceneSequence;

    [Header("클리어 → 전환 텀")]
    [Tooltip("함정 정지(FreezeAllHazardsNow) 이후 암전 시작까지 대기할 시간(초).\n" +
             "StageClearBannerUI 총 재생시간(fadeIn 0.12 + scale 0.1 + hold 2.0 + fadeOut 0.25 ≈ 2.5초) " +
             "이상으로 잡아야 배너가 암전에 잘리지 않는다 — 배너 타이밍을 바꾸면 이 값도 같이 맞출 것.\n" +
             "LoadSceneByIndex(스테이지 선택 등 직접 이동)에는 적용되지 않고 LoadNextScene에만 적용됨.")]
    [SerializeField] private float clearToTransitionDelay = 2.5f;

    [Header("런타임 상태 (읽기 전용)")]
    [SerializeField] private int _currentSceneIndex = -1;

    private StageProgressState[] _stageStates;
    private bool _isTransitioning;
    private string _lastLoadedSceneName;

    // ── 프로퍼티 ─────────────────────────────────────────────────

    public int  CurrentSceneIndex => _currentSceneIndex;
    public bool IsTransitioning   => _isTransitioning;

    /// <summary>sceneSequence 총 개수. 스테이지 선택 UI 등에서 순회용.</summary>
    public int SceneCount => sceneSequence?.Length ?? 0;

    /// <summary>sceneSequence[index]의 씬 이름. 범위 밖이면 null.</summary>
    public string GetSceneName(int index) =>
        (sceneSequence != null && index >= 0 && index < sceneSequence.Length) ? sceneSequence[index] : null;

    /// <summary>sceneSequence 범위 내 index의 클리어 여부.</summary>
    public bool IsCleared(int index)
    {
        if (_stageStates == null || index < 0 || index >= _stageStates.Length) return false;
        return _stageStates[index] == StageProgressState.Cleared;
    }

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

        InitStageStates();
        SyncCurrentIndex();
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncCurrentIndex();
        PlayStageTransitionSfx(scene.name);
    }

    /// <summary>
    /// 씬 이름 접두사로 구역 진입 SFX를 자동 재생.
    /// M.* → Mouth 구역, T.* → Esophagus 구역. title/lobby/tutorial/Finish 는 재생 안 함.
    /// [주의] Host 전용 코드(TransitionTo())가 아니라 여기(OnSceneLoaded, 전 머신 로컬 실행)에 둬야
    /// Host/Client 모두 들림 — BGMManager 존 매칭과 동일한 씬 접두사 판별 패턴.
    /// 2D 재생(SFXManager.Play(id))만 사용 — 3D 재생(PlayClipAtPoint)은 임시 오브젝트가
    /// DontDestroyOnLoad가 아니라서 다음 씬 전환 때 잘릴 수 있음.
    /// [동일 씬 리로드 스킵] 직전에 처리한 씬 이름과 같으면 재생하지 않는다 — 사망/ESC Reset
    /// (StageNetworkState.ReloadAfterDeathAnim)이 같은 씬을 다시 LoadScene할 때도 이 메서드가
    /// sceneLoaded로 호출되기 때문. 최초 진입(직전 씬 이름 없음)과 실제로 다른 씬으로 넘어가는
    /// 경우(클리어 전환, 챕터 점프 등)는 이름이 다르므로 정상 재생됨.
    /// </summary>
    void PlayStageTransitionSfx(string sceneName)
    {
        bool isSameSceneReload = sceneName == _lastLoadedSceneName;
        _lastLoadedSceneName = sceneName;
        if (isSameSceneReload) return;

        if (sceneName.StartsWith("M."))
            SFXManager.Instance?.Play(SFXId.Stage_TransitionMouth);
        else if (sceneName.StartsWith("T."))
            SFXManager.Instance?.Play(SFXId.Stage_TransitionEsophagus);
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 다음 씬으로 전환.
    /// PhaseManager.onAllPhasesComplete 또는 StageManager.OnStageClear 에 연결.
    /// </summary>
    public void LoadNextScene()
    {
        if (_isTransitioning) return;

        if (sceneSequence == null || sceneSequence.Length == 0)
        {
            Debug.LogError("[SceneFlowManager] sceneSequence 가 비어 있습니다. Inspector에서 씬 목록을 입력하세요.");
            return;
        }

        int nextIndex = _currentSceneIndex + 1;

        if (nextIndex >= sceneSequence.Length)
        {
            Debug.Log("[SceneFlowManager] 마지막 씬입니다. 더 이상 진행할 씬이 없습니다.");
            return;
        }

        MarkCurrentCleared();

        // 함정 정지는 배너/전환 대기보다 먼저 — 클리어 나오는 순간 즉시 멈춰야 한다(2026-09-08).
        // [순서 주의] 이 머신(Host) 정지는 항상 로컬에서 직접 하고, RPC는 "전파"만 맡는다.
        // 예전엔 StageNetworkState 경유 한 줄로만 처리했는데, 그쪽 !IsSpawned 가드에 걸리면
        // Host 정지까지 통째로 스킵되는 구멍이 있었다(2026-09-08 리뷰).
        FreezeAllHazardsNow();
        StageNetworkState.Instance?.BroadcastStageClearFreezeToClients();

        // _isTransitioning은 TransitionTo가 첫 yield 전에 세운다(StartCoroutine은 첫 yield까지
        // 동기 실행) — 여기서 따로 세우지 않아도 재진입 가드가 같은 프레임부터 유효하다.
        StartCoroutine(TransitionTo(sceneSequence[nextIndex], clearToTransitionDelay));
    }

    /// <summary>
    /// 씬에 남아있는 모든 위협을 즉시 정지한다 — 자체 스케줄 트랩(TrapBase), 발사 감독
    /// (ArrowIncomingDirector/TrapPlayerTracker), Update 감지형(CeilingTrap), 밀어내는 복도
    /// (MovingCorridor), 추격자(Stage5ChaserAI), 팀응원 함정(Mouth/Tongue/Saliva/Esophagus/JawSmash).
    /// 어느 머신에서 호출되든 로컬로 안전 — 전부 로컬 상태 변경이고, TrapProjectile Despawn만
    /// Host 전용으로 가드된다.
    ///
    /// [범위: 씬 전체(2026-09-08 확정)] 클리어된 방의 등록 트랩만이 아니라 씬에 남은 전부를 멈춘다 —
    /// 다음 씬으로 넘어가는 순간이므로 어차피 씬을 나가는 마당에 다른 방 함정이 계속 도는 것도
    /// 보이면 안 된다는 전제. StageManager별 등록 목록(_registeredTraps) 대신 FindObjectsByType으로
    /// 직접 찾는 이유가 이것 — 여러 StageManager(방)가 있어도 전부 커버됨.
    ///
    /// [정지 방식: 하드컷(2026-09-08 확정)] 충전/공격 애니메이션 중간이어도 그대로 끊는다.
    /// StopCycle() 쪽(Mouth/Tongue/Saliva/EsophagusSqueeze/EsophagusFog)은 이미 자체적으로
    /// Idle 트리거 + 상태 복구까지 해주므로 하드컷이어도 어중간한 모습으로 남지 않는다.
    /// ArrowTrap/DropTrap 쪽은 충전 애니메이션이 마침 중간이면 열린 채로 잠깐 남을 수 있으나,
    /// 곧 씬이 전환되므로 허용(사용자 확인 완료).
    /// </summary>
    public void FreezeAllHazardsNow()
    {
        // 자체 스케줄 트랩 — Freeze()는 Deactivate() + 이후 단발 발사(FireOnce/FireAt) 차단.
        foreach (var trap in FindObjectsByType<TrapBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            trap.Freeze();

        // 발사를 "지시"하는 감독들 — TrapBase를 상속하지 않아 위 순회에 안 잡힌다. 이걸 안 멈추면
        // 트랩을 정지시켜도 감독이 계속 FireOnce()/FireAt()을 불러 화살·낙하물이 계속 나왔다
        // (2026-09-08 리뷰에서 발견 — arrowtrap/droptrap이 안 멈추던 실제 원인).
        foreach (var director in FindObjectsByType<ArrowIncomingDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            director.StopDirecting();
        foreach (var tracker in FindObjectsByType<TrapPlayerTracker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            tracker.StopTracking();

        // Update에서 직접 감지·발동하는 함정 — 위 두 경로 어디에도 안 걸린다.
        foreach (var ceiling in FindObjectsByType<CeilingTrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            ceiling.StopTrap();

        // 밀어내는 복도 — Activate() 이후 별도 정지 호출이 씬에 전혀 없어 끝까지 계속 미는 설계
        // (T.Stage4). 클리어 후에도 Kinematic Rigidbody로 계속 밀면 배너 보는 동안 플레이어가
        // 밀려난다(2026-09-08 조사에서 발견).
        foreach (var corridor in FindObjectsByType<MovingCorridor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            corridor.Deactivate();

        // 추격자 — Host 전권 시뮬이지만 Deactivate()는 전 머신 안전(Client는 애님/사운드만 정지).
        // 클리어 후에도 계속 쫓아와 사람을 죽이면 아래 씬 전환과 사망 리로드가 경합한다.
        foreach (var chaser in FindObjectsByType<Stage5ChaserAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            chaser.Deactivate();

        // 팀 응원 함정 — StopCycle()이 Idle 트리거·깨진 타일·안개·암전 복구까지 같이 해준다.
        foreach (var m in FindObjectsByType<MouthController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            m.StopCycle();
        foreach (var t in FindObjectsByType<TongueController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            t.StopCycle();
        foreach (var s in FindObjectsByType<SalivaHazard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            s.StopCycle();
        foreach (var e in FindObjectsByType<EsophagusSqueeze>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            e.StopCycle();
        foreach (var f in FindObjectsByType<EsophagusFog>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            f.StopCycle();
        // M.Boss P4 — MouthController를 상속하지 않는 별도 클래스라 위 순회에 안 잡힌다.
        // 엔딩 연출로 이미 부순 뒤라면 StopCycle()이 바닥을 되살리지 않는다(자기 안에서 판단).
        foreach (var j in FindObjectsByType<MouthBossJawSmash>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            j.StopCycle();

        // 이미 날아가는 투사체 정리 — Host 가드는 이 메서드 내부에 있다(SSOT: TrapProjectile).
        TrapProjectile.DespawnAllOnServer();
    }

    /// <summary>
    /// sceneSequence 내 특정 인덱스의 씬으로 직접 이동.
    /// 메인 메뉴 복귀, 챕터 선택 등에 사용.
    /// </summary>
    public void LoadSceneByIndex(int index)
    {
        if (_isTransitioning) return;

        if (sceneSequence == null || index < 0 || index >= sceneSequence.Length)
        {
            Debug.LogError($"[SceneFlowManager] 잘못된 씬 인덱스: {index}");
            return;
        }

        StartCoroutine(TransitionTo(sceneSequence[index]));
    }

    /// <summary>
    /// 스테이지 진행도를 초기 상태(전부 Unlocked)로 리셋한다.
    /// TitleReturnFlow.FullRunReset 시 호출됨.
    /// </summary>
    public void ResetRunProgress()
    {
        InitStageStates();
        _currentSceneIndex = -1;
        Debug.Log("[SceneFlowManager] 스테이지 진행도 초기화 완료");
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void InitStageStates()
    {
        if (sceneSequence == null) return;
        _stageStates = new StageProgressState[sceneSequence.Length];
        for (int i = 0; i < _stageStates.Length; i++)
            _stageStates[i] = StageProgressState.Unlocked;
    }

    void SyncCurrentIndex()
    {
        if (sceneSequence == null) return;

        string activeName = SceneManager.GetActiveScene().name;
        for (int i = 0; i < sceneSequence.Length; i++)
        {
            if (sceneSequence[i] == activeName)
            {
                _currentSceneIndex = i;
                return;
            }
        }

        // sceneSequence에 없는 씬(에디터 직접 플레이 등)
        _currentSceneIndex = -1;
    }

    void MarkCurrentCleared()
    {
        if (_stageStates == null || _currentSceneIndex < 0 || _currentSceneIndex >= _stageStates.Length) return;
        _stageStates[_currentSceneIndex] = StageProgressState.Cleared;
    }

    IEnumerator TransitionTo(string sceneName, float preDelay = 0f)
    {
        _isTransitioning = true;

        // 배너/함정 정지 상태를 보여줄 시간을 벌기 위한 대기(LoadNextScene 경로만 preDelay > 0).
        // Host에서만 도는 코루틴이지만 실제 씬 로드(nm.SceneManager.LoadScene)는 이 대기 뒤에
        // 호출되므로 Client도 같이 늦춰짐 — 별도 RPC 없이 전원 동일하게 지연됨.
        // Realtime을 쓰는 이유는 LoadingCurtain과 같다 — timeScale=0(스크린샷 일시정지 F8 등)에
        // 걸리면 전환이 영구히 멈춰버린다.
        if (preDelay > 0f)
            yield return new WaitForSecondsRealtime(preDelay);

        if (LoadingCurtain.Instance != null)
            yield return StartCoroutine(LoadingCurtain.Instance.BeginCoverRoutine(waitForPlayersReady: true));

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.IsHost)
            nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        _isTransitioning = false;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 다음 씬으로")]
    void Debug_LoadNext() => LoadNextScene();

    [ContextMenu("테스트: 씬 순서 콘솔 출력")]
    void Debug_PrintSequence()
    {
        if (sceneSequence == null || sceneSequence.Length == 0)
        {
            Debug.Log("[SceneFlowManager] sceneSequence 가 비어 있습니다.");
            return;
        }
        for (int i = 0; i < sceneSequence.Length; i++)
            Debug.Log($"[SceneFlowManager] [{i}] {sceneSequence[i]}{(i == _currentSceneIndex ? " ← 현재" : "")}");
    }
#endif
}
