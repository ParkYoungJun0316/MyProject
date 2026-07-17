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
/// 3. (선택) screenFader: 이 GameObject의 자식 Canvas에 ScreenFader를 붙이고 연결
///    → 없으면 암전 없이 즉시 전환
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

    [Header("페이드 연출 (선택)")]
    [Tooltip("자식 Canvas에 ScreenFader를 배치하고 연결. 없으면 즉시 전환.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("씬 전환 전 페이드아웃 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0f;

    [Tooltip("씬 로드 후 페이드인 시간(초)")]
    [SerializeField] private float fadeInDuration = 0f;

    [Header("런타임 상태 (읽기 전용)")]
    [SerializeField] private int _currentSceneIndex = -1;

    private StageProgressState[] _stageStates;
    private bool _isTransitioning;

    // ── 프로퍼티 ─────────────────────────────────────────────────

    public int  CurrentSceneIndex => _currentSceneIndex;
    public bool IsTransitioning   => _isTransitioning;

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

        if (screenFader != null)
            screenFader.FadeIn(fadeInDuration);
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

        if (screenFader != null && fadeOutDuration > 0f)
        {
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration);
        }

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
