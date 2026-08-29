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
        StartCoroutine(TransitionTo(sceneSequence[nextIndex]));
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

    IEnumerator TransitionTo(string sceneName)
    {
        _isTransitioning = true;

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
