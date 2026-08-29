using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환·로딩·사망 리로드·타이틀 복귀 등 "시간이 걸리는 전환 구간"을 하나로 통일해서
/// 덮어주는 전역 싱글턴. DontDestroyOnLoad — 0.Title 씬에 배치해 게임 실행 내내 유지된다.
///
/// [배치 방법]
/// 0.Title 씬에 Canvas(또는 그 자식) 하나를 만들고, CanvasGroup + 전체화면 검은 Image를 추가한 뒤
/// ScreenFader + LoadingCurtain을 같이 붙인다. sortOrder를 다른 UI보다 높게 잡아 항상 최상단에
/// 그려지게 할 것.
/// 회전하는 로딩 스피너를 쓰려면 정적 스피너 이미지(PNG) 자식을 화면 중앙에 하나 만들어
/// spinnerImage에 연결 — 프레임 애니메이션 GIF가 아니라 이미지 1장을 코드로 계속 회전시켜
/// 동일한 시각효과를 낸다(Unity는 애니메이션 GIF를 직접 재생하지 못함).
/// [문구 없음 — 의도적] 텍스트("로딩 중..." 등)는 넣지 않는다. 캐릭터 이미지 + 암전만으로 로딩
/// 신호는 충분하고, 문구를 넣으면 게임이 지원하는 13개 언어 전부에 대한 로컬라이제이션 부담이
/// 계속 따라붙는다(신규 전환 문구가 늘 때마다 번역 추가) — 그 비용을 피하기 위한 선택.
///
/// [사용법 — Begin/End 쌍]
/// 1. 실제로 얼마나 걸릴지 모르는 작업(NGO 씬 로드 등) 앞에서 BeginCover() 호출 → 암전 시작.
/// 2. 작업이 실제로 끝나는 시점(주로 SceneManager.sceneLoaded)에 EndCover() 호출.
///    → BeginCover 시점부터 최소 유지시간(minHoldSeconds)이 지날 때까지 기다렸다가 페이드인.
/// 이 클래스 자신이 SceneManager.sceneLoaded를 전역 구독해 EndCover()를 자동으로도 호출하므로,
/// 호출부에서 EndCover를 깜빡해도 다음 씬 로드 완료 시 자동으로 걷힌다(안전장치).
///
/// [즉시 완료되는 지점]
/// TitleReturnFlow처럼 LoadScene이 동기적으로 바로 일어나는 곳은 BeginCoverRoutine()으로
/// 페이드아웃이 끝날 때까지 대기한 뒤 실제 전환을 실행하면 된다.
///
/// [네트워크 동기화 대기 — waitForPlayersReady]
/// BeginCover(waitForPlayersReady: true)로 덮으면, 단순히 "이 머신의 씬 로드 완료"가 아니라
/// PlayerSpawnCoordinator.OnPlayersReady(Host/Client 전원 스폰+색 동기화 확정 신호,
/// NetworkDesign.md §11.3)가 실제로 도착할 때까지 커튼을 걷지 않는다.
/// Host보다 Client가 정보를 늦게/빨리 받아 생기던 초반 프레임 동기화 어긋남을 커튼 뒤로 가려준다.
/// 신호가 영영 안 오는 버그 상황을 대비해 playersReadyTimeoutSeconds 이후 강제로 페이드인한다
/// (무한 암전 방지 안전장치) — 이때 경고 로그가 남으므로 콘솔에서 동기화 문제를 바로 알 수 있다.
/// </summary>
[RequireComponent(typeof(ScreenFader))]
public class LoadingCurtain : MonoBehaviour
{
    public static LoadingCurtain Instance { get; private set; }

    [Header("스피너 (선택 — 회전 로딩 아이콘)")]
    [Tooltip("Canva 등에서 export한 정적 스피너 이미지(PNG, GIF 아님)의 RectTransform. 등록하면 " +
             "커튼이 덮여있는 동안 계속 회전한다 — 프레임 애니메이션 없이 회전만으로 GIF 스피너와 " +
             "동일한 시각효과를 낸다.")]
    [SerializeField] RectTransform spinnerImage;

    [Tooltip("스피너 회전 속도(초당 도). 양수 = 반시계, 음수 = 시계 방향.")]
    [SerializeField] float spinnerRotationSpeed = -180f;

    [Header("기본값")]
    [Tooltip("암전 시작부터 페이드인 시작까지 최소 유지시간(초). 실제 작업이 더 빨리 끝나도 이 시간만큼은 화면을 덮고 있는다.")]
    [SerializeField] float defaultMinHoldSeconds = 0.8f;

    [Tooltip("페이드아웃/페이드인에 걸리는 시간(초).")]
    [SerializeField] float defaultFadeDuration = 0.35f;

    [Header("네트워크 동기화 대기 (선택)")]
    [Tooltip("waitForPlayersReady=true로 BeginCover된 뒤 이 시간(초) 안에 OnPlayersReady가 안 오면 " +
             "무한 암전을 막기 위해 강제로 페이드인한다(경고 로그 남김).")]
    [SerializeField] float playersReadyTimeoutSeconds = 8f;

    ScreenFader _fader;
    float _coverStartTime = -1f;
    Coroutine _endRoutine;
    bool _waitingForPlayersReady;
    Coroutine _playersReadyTimeoutRoutine;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        _fader = GetComponent<ScreenFader>();

        // 게임 최초 부팅 시: 완전 암전 상태로 시작해 타이틀로 자연스럽게 페이드인(팝인 방지).
        _fader.SetAlpha(1f);
        _coverStartTime = Time.unscaledTime;
    }

    void Start()
    {
        // 다른 부팅 초기화가 끝난 뒤 최소 유지시간을 채우고 자연스럽게 걷힘.
        EndCover();
    }

    void OnEnable()  => SceneManager.sceneLoaded += HandleSceneLoaded;

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopWaitingForPlayersReady();
    }

    // 안전장치 — 호출부가 EndCover를 깜빡해도 씬 로드가 끝나면 자동으로 걷힌다.
    // 단, OnPlayersReady를 기다리는 중이면 로컬 씬 로드만으로는 걷지 않는다(동기화 확정 전 노출 방지).
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_waitingForPlayersReady) return;
        EndCover();
    }

    // 커튼이 덮여있는 동안만 회전 — 숨겨진 상태에서 불필요한 연산 방지.
    void Update()
    {
        if (spinnerImage == null || _coverStartTime < 0f) return;
        spinnerImage.Rotate(0f, 0f, spinnerRotationSpeed * Time.unscaledDeltaTime);
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 암전 시작. 이미 덮인 상태면 무시(중복 방지) — 단 waitForPlayersReady만 새로 true로 요청되면
    /// 대기 모드로 승격은 해준다.
    /// </summary>
    /// <param name="waitForPlayersReady">
    /// true면 씬 로드 완료가 아니라 PlayerSpawnCoordinator.OnPlayersReady(전원 스폰+색 동기화 확정)가
    /// 올 때까지 EndCover를 미룬다. 플레이어가 스폰되지 않는 전환(타이틀 복귀 등)에는 쓰지 말 것 —
    /// 신호가 영영 안 와서 타임아웃까지 그대로 대기하게 된다.
    /// </param>
    public void BeginCover(float? fadeDuration = null, bool waitForPlayersReady = false)
    {
        if (_coverStartTime >= 0f)
        {
            if (waitForPlayersReady) StartWaitingForPlayersReady();
            return;
        }

        if (_endRoutine != null)
        {
            StopCoroutine(_endRoutine);
            _endRoutine = null;
        }

        _coverStartTime = Time.unscaledTime;
        _fader.FadeOut(fadeDuration ?? defaultFadeDuration);

        if (waitForPlayersReady) StartWaitingForPlayersReady();
    }

    /// <summary>
    /// 실제 작업 완료 시점에 호출. BeginCover부터 최소 유지시간이 지날 때까지 기다린 뒤 페이드인.
    /// BeginCover 없이 호출되면 무시(안전).
    /// </summary>
    public void EndCover(float? minHoldSeconds = null, float? fadeDuration = null)
    {
        if (_coverStartTime < 0f) return;

        if (_endRoutine != null) StopCoroutine(_endRoutine);
        _endRoutine = StartCoroutine(EndCoverRoutine(minHoldSeconds ?? defaultMinHoldSeconds, fadeDuration ?? defaultFadeDuration));
    }

    IEnumerator EndCoverRoutine(float minHoldSeconds, float fadeDuration)
    {
        float remain = minHoldSeconds - (Time.unscaledTime - _coverStartTime);
        if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

        _fader.FadeIn(fadeDuration);
        _coverStartTime = -1f;
        _endRoutine = null;
    }

    /// <summary>
    /// BeginCover 후 페이드아웃이 끝날 때까지 대기만 하는 코루틴.
    /// LoadScene이 동기적으로 바로 일어나는 지점(TitleReturnFlow 등)에서
    /// "암전 완료 → 실제 전환 실행" 순서를 보장하기 위해 사용.
    /// </summary>
    public IEnumerator BeginCoverRoutine(float? fadeDuration = null, bool waitForPlayersReady = false)
    {
        float fd = fadeDuration ?? defaultFadeDuration;
        BeginCover(fd, waitForPlayersReady);
        if (fd > 0f) yield return new WaitForSecondsRealtime(fd);
    }

    // ── 네트워크 동기화 대기 (waitForPlayersReady) ───────────────────

    /// <summary>
    /// [주의] 다른 §11.3 구독자들과 달리 "IsReady==true면 즉시 처리"하는 늦은 구독 catch-up을
    /// 일부러 하지 않는다. 이 대기는 항상 "다음 씬으로 넘어가기 직전"에 시작되는데, 그 시점의
    /// IsReady는 아직 리셋 전인 이전 씬의 낡은 true일 수 있다(PlayerSpawnCoordinator는
    /// destroyWithScene:false라 씬이 바뀌어도 살아남고, ResetReady()는 실제 씬 로드가 시작된
    /// 뒤에야 호출됨). 여기서 즉시 catch-up하면 새 씬 스폰이 오기도 전에 커튼이 걷혀버린다.
    /// </summary>
    void StartWaitingForPlayersReady()
    {
        if (_waitingForPlayersReady) return;
        _waitingForPlayersReady = true;

        PlayerSpawnCoordinator.OnPlayersReady += HandlePlayersReady;

        if (_playersReadyTimeoutRoutine != null) StopCoroutine(_playersReadyTimeoutRoutine);
        _playersReadyTimeoutRoutine = StartCoroutine(PlayersReadyTimeoutRoutine());
    }

    void HandlePlayersReady()
    {
        StopWaitingForPlayersReady();
        EndCover();
    }

    IEnumerator PlayersReadyTimeoutRoutine()
    {
        yield return new WaitForSecondsRealtime(playersReadyTimeoutSeconds);
        Debug.LogWarning($"[LoadingCurtain] OnPlayersReady가 {playersReadyTimeoutSeconds}초 내에 오지 않아 " +
                          "강제로 페이드인합니다 — 동기화 지연/버그 확인 필요.");
        StopWaitingForPlayersReady();
        EndCover();
    }

    void StopWaitingForPlayersReady()
    {
        if (!_waitingForPlayersReady) return;
        _waitingForPlayersReady = false;
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;

        if (_playersReadyTimeoutRoutine != null)
        {
            StopCoroutine(_playersReadyTimeoutRoutine);
            _playersReadyTimeoutRoutine = null;
        }
    }
}
