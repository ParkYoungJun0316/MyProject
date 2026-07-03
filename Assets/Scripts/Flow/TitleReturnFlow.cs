using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 복귀 허브. DontDestroyOnLoad 싱글턴.
///
/// 모든 타이틀 복귀 경로는 이 클래스의 Request() 하나만 호출한다.
/// 씬 오브젝트(CheerService, PhaseManager 등)는 LoadScene(Single)으로
/// 자동 파괴되므로 이 클래스에서 별도 처리하지 않는다.
///
/// [실행 순서]
/// ① UI·입력 복원  (Cursor, timeScale, 채팅 플래그)
/// ② 페이드아웃    (선택 — screenFader가 있을 때만)
/// ③ 네트워크 종료 (NetworkManagerSetup.Shutdown)
/// ④ 세션 초기화   (GameSession.ResetSession, TimerUI.ResetTimer)
/// ⑤ LobbyContext = Offline
/// ⑥ 진행도 리셋   (FullRunReset 시만 — SceneFlowManager.ResetRunProgress)
/// ⑦ ISessionResettable 구독자 알림
/// ⑧ LoadScene("0.Title")
///
/// [배치 방법]
/// 0.Title 씬의 빈 GameObject에 추가 (GameSession 오브젝트와 같은 계층 권장).
/// DontDestroyOnLoad로 모든 씬에서 유지됨.
///
/// [호출 방법]
/// TitleReturnFlow.Instance?.Request(new TitleReturnOptions
/// {
///     Reason = TitleReturnReason.UserQuit,
///     Scope  = TitleReturnScope.SessionOnly,
/// });
/// </summary>
public class TitleReturnFlow : MonoBehaviour
{
    public static TitleReturnFlow Instance { get; private set; }

    [Header("타이틀 씬")]
    [Tooltip("복귀할 씬 이름. Build Settings 등록 이름과 정확히 일치해야 함.")]
    [SerializeField] private string titleSceneName = "0.Title";

    [Header("페이드 (선택)")]
    [Tooltip("타이틀 복귀 전 페이드아웃 시간(초). 0이면 즉시 전환.\n" +
             "주의: screenFader가 연결돼 있어야 시각적 효과가 동작함.")]
    [SerializeField] private float fadeOutDuration = 0f;

    [Tooltip("페이드 오버레이 CanvasGroup. DDOL Canvas에 붙인 ScreenFader를 연결.\n" +
             "없으면 fadeOutDuration만큼 대기 후 씬 전환.")]
    [SerializeField] private ScreenFader screenFader;

    bool _isReturning;

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
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 타이틀 복귀 요청. 모든 복귀 경로에서 이 메서드만 호출한다.
    /// 이미 복귀 중이면 중복 요청을 무시한다.
    /// </summary>
    public void Request(TitleReturnOptions options)
    {
        if (_isReturning)
        {
            Debug.LogWarning("[TitleReturnFlow] 이미 복귀 처리 중 — 중복 요청 무시");
            return;
        }
        _isReturning = true;
        StartCoroutine(ExecuteReturn(options));
    }

    // ── ISessionResettable 등록 ───────────────────────────────────

    readonly List<ISessionResettable> _resettables = new();

    /// <summary>
    /// DDOL 시스템이 복귀 알림을 받으려면 이 메서드로 등록한다.
    /// 보통 Awake()에서 호출.
    /// </summary>
    public void Register(ISessionResettable r)
    {
        if (!_resettables.Contains(r))
            _resettables.Add(r);
    }

    /// <summary>
    /// 등록 해제. 보통 OnDestroy()에서 호출.
    /// </summary>
    public void Unregister(ISessionResettable r) => _resettables.Remove(r);

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator ExecuteReturn(TitleReturnOptions options)
    {
        Debug.Log($"[TitleReturnFlow] 복귀 시작 — reason={options.Reason}, scope={options.Scope}");

        // ① UI·입력 즉시 복원 (LoadScene 전에 먼저 처리)
        Time.timeScale    = 1f;
        Cursor.visible    = true;
        Cursor.lockState  = CursorLockMode.None;
        InGameChatUI.ResetForTitleReturn();

        // ② 페이드아웃 (screenFader 없으면 시간만 대기)
        if (fadeOutDuration > 0f)
        {
            screenFader?.FadeOut(fadeOutDuration);
            yield return new WaitForSecondsRealtime(fadeOutDuration);
        }

        // ③ 네트워크 종료 (LanDiscovery 중단, NetworkSessionData 초기화, NGO Shutdown)
        NetworkManagerSetup.Instance?.Shutdown();

        // ④ 세션 데이터 초기화
        GameSession.Instance?.ResetSession();
        TimerUI.ResetTimer();

        // ⑤ 로비 모드 초기화
        LobbyContext.Mode = LobbyMode.Offline;

        // ⑥ FullRunReset 시 스테이지 진행도 초기화
        if (options.Scope == TitleReturnScope.FullRunReset)
            SceneFlowManager.Instance?.ResetRunProgress();

        // ⑦ ISessionResettable 구독자 알림 (나중에 추가될 시스템용)
        foreach (ISessionResettable r in _resettables)
            r.OnSessionReset(options.Scope);

        // ⑧ 씬 전환 (이전 씬 오브젝트 전부 파괴)
        SceneManager.LoadScene(titleSceneName);

        _isReturning = false;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀 (SessionOnly)")]
    void Debug_ReturnSession() => Request(new TitleReturnOptions
    {
        Reason = TitleReturnReason.UserQuit,
        Scope  = TitleReturnScope.SessionOnly,
    });

    [ContextMenu("테스트: 타이틀 복귀 (FullRunReset)")]
    void Debug_ReturnFull() => Request(new TitleReturnOptions
    {
        Reason = TitleReturnReason.UserQuit,
        Scope  = TitleReturnScope.FullRunReset,
    });
#endif
}
