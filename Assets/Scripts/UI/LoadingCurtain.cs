using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
/// [준비 게이트 — 시간이 아니라 '조건'으로 걷는다 (2026-09-18, 전 씬 기본값)]
/// 커튼은 "몇 초 지났으니 걷는다"가 아니라 **등록된 준비 게이트가 전부 끝나야** 걷힌다.
/// 씬마다 기다릴 것이 다르므로(맵 활성화, NavMesh, 체이서, 텔레포트 …) 각 씬의 컴포넌트가
/// <see cref="RegisterGate"/>로 자기 조건을 등록하고 끝나면 <see cref="MarkGateReady"/>를 부른다.
///
/// [등록은 코드가, 인스펙터가 아니다 — 왜]
/// 씬 인스펙터에 "기다릴 것 목록"을 선언하는 방식도 있었지만 버렸다. 코드에 조건을 추가하고
/// 씬 선언을 빠뜨리면 **그 조건을 그냥 안 기다리고 조용히 통과**한다 — 이 장치를 만든 이유가
/// 소리 없이 무너진다. 코드 자기등록은 반대로 게이트가 안 오면 타임아웃 로그로 시끄럽게 드러난다.
///
/// 등록 레이스는 없다: 기본 게이트인 OnPlayersReady는 씬 로드 + 전원 스폰 **이후**에 오고,
/// 씬 컴포넌트의 Start()는 그보다 항상 먼저다. 등록은 Start()에서 할 것(저장소 공통 관례).
/// 비활성으로 시작하는 오브젝트는 스스로 등록할 수 없으므로 **항상 활성인 매니저급 컴포넌트가
/// 대신 등록**한다.
///
/// [전원 준비 — 한 명이라도 안 끝나면 전원 대기]
/// 로컬 게이트가 전부 끝나면 StageNetworkState가 Host에 보고하고, **전원이 모여야** 마지막
/// 게이트가 풀린다. 한 명 때문에 전원이 기다리는 대가로, 누구는 이미 보이고 누구는 암전인
/// 어긋난 순간이 사라진다.
///
/// [막혔을 때 — gateTimeoutSeconds]
/// 게이트가 안 채워진 채 이 시간이 지나면 미완 게이트 이름을 로그로 남기고
/// <see cref="OnGatesTimedOut"/>을 발행한다. 듣는 쪽(StageNetworkState)이 조용한 리로드로
/// 재시도하겠다고 하면(<see cref="KeepCoveredForRetry"/>) 커튼을 덮은 채 유지하고,
/// 아무도 안 맡으면 **그냥 걷고 진행한다** — 무한 암전보다 낫다.
/// 초대 참여처럼 씬 로드 전 대기가 긴 구간은 <see cref="UseLongTimeout"/>로 이번 커튼만 늘린다.
///
/// [입력 차단 — 2026-09-24]
/// 덮여 있는 동안은 UI 클릭을 막는다. 초대 접속을 기다리는 몇 초 사이 타이틀 버튼(Start/Join)을
/// 마구 눌러 접속이 꼬이던 문제 때문. 걷히기 시작하면 바로 푼다.
///
/// [실패 — AbortCover]
/// 덮고 기다리던 작업(방 만들기·초대 참여)이 실패하면 게이트를 버리고 바로 걷는다.
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

    [Header("준비 게이트")]
    [Tooltip("게이트가 안 채워진 채 이 시간(초)이 지나면 미완 게이트를 로그로 남기고 " +
             "OnGatesTimedOut을 발행한다. 타이머는 새 진전이 있을 때마다(씬 로드·게이트 등록) 다시 시작한다.")]
    [SerializeField] float gateTimeoutSeconds = 5f;

    /// <summary>플레이어 스폰 완료 기본 게이트 id. BeginCover(waitForPlayersReady: true)가 등록한다.</summary>
    public const string PlayersGate = "players";

    /// <summary>전원 준비 취합 게이트 id. StageNetworkState가 등록하고 전원이 모이면 푼다.</summary>
    public const string AllPlayersReadyGate = "net.allready";

    /// <summary>
    /// 게이트가 제때 안 채워졌다. 인자는 미완 게이트 이름들.
    /// 듣는 쪽이 재시도를 맡으려면 <see cref="KeepCoveredForRetry"/>를 부를 것.
    /// </summary>
    public static event Action<string[]> OnGatesTimedOut;

    ScreenFader _fader;
    float _coverStartTime = -1f;
    Coroutine _endRoutine;
    bool _waitingForPlayersReady;

    readonly HashSet<string> _gates = new HashSet<string>();
    Coroutine _gateTimeoutRoutine;
    bool      _endRequested;
    bool      _retryHandled;
    float?    _pendingMinHold;
    float?    _pendingFade;
    float?    _timeoutOverride;

    CanvasGroup _canvasGroup;
    Graphic     _blocker;

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
        SetupInputBlocker();

        // 게임 최초 부팅 시: 완전 암전 상태로 시작해 타이틀로 자연스럽게 페이드인(팝인 방지).
        _fader.SetAlpha(1f);
        _coverStartTime = Time.unscaledTime;
        SetBlocking(true);
    }

    // 커튼 Canvas에 레이캐스터가 없으면 붙인다 — 최상단 Canvas라 여기서 맞으면 아래 UI는 클릭이 안 된다.
    void SetupInputBlocker()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _blocker     = GetComponent<Graphic>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    void SetBlocking(bool block)
    {
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = block;
        if (_blocker != null)     _blocker.raycastTarget      = block;
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

    // 안전장치 — 호출부가 EndCover를 깜빡해도 씬 로드가 끝나면 "요청"은 들어간다.
    // 실제로 걷히는 것은 등록된 준비 게이트가 전부 풀린 뒤다.
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새 씬이 올라왔다 = 실제 준비 작업이 이제 시작된다. 타임아웃을 여기서 다시 시작해
        // "씬 로딩이 오래 걸렸다"는 이유로 게이트가 타임아웃되는 일을 막는다.
        RestartGateTimeout();
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
        SetBlocking(true);

        if (waitForPlayersReady) StartWaitingForPlayersReady();
    }

    /// <summary>
    /// 실제 작업 완료 시점에 호출. **요청일 뿐** — 등록된 준비 게이트가 남아 있으면 그게 전부
    /// 풀릴 때까지 실제로 걷지 않는다. BeginCover부터 최소 유지시간도 함께 지킨다.
    /// BeginCover 없이 호출되면 무시(안전).
    /// </summary>
    public void EndCover(float? minHoldSeconds = null, float? fadeDuration = null)
    {
        if (_coverStartTime < 0f) return;

        _endRequested   = true;
        _pendingMinHold = minHoldSeconds;
        _pendingFade    = fadeDuration;

        TryFinishCover();
    }

    void TryFinishCover()
    {
        if (!_endRequested || _coverStartTime < 0f) return;
        if (_gates.Count > 0) return;   // 아직 기다릴 것이 남았다

        StopGateTimeout();

        if (_endRoutine != null) StopCoroutine(_endRoutine);
        _endRoutine = StartCoroutine(EndCoverRoutine(
            _pendingMinHold ?? defaultMinHoldSeconds,
            _pendingFade ?? defaultFadeDuration));
    }

    IEnumerator EndCoverRoutine(float minHoldSeconds, float fadeDuration)
    {
        float remain = minHoldSeconds - (Time.unscaledTime - _coverStartTime);
        if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

        _endRoutine = null;

        // 최소 유지시간을 기다리는 사이 새 게이트가 등록됐다(예: 부팅 커튼이 걷히려는 순간 +connect_lobby
        // 참여가 시작됨). 요청은 살려 두고 멈춘다 — 그 게이트가 풀릴 때 TryFinishCover가 다시 부른다.
        if (_gates.Count > 0) yield break;

        _fader.FadeIn(fadeDuration);
        SetBlocking(false);
        _coverStartTime  = -1f;
        _endRequested    = false;
        _timeoutOverride = null;
    }

    /// <summary>
    /// 덮고 기다리던 작업이 실패했다(방 만들기·초대 참여). 남은 게이트를 버리고 바로 걷는다.
    /// 덮여 있지 않으면 무시.
    /// </summary>
    public void AbortCover()
    {
        if (_coverStartTime < 0f) return;

        _gates.Clear();
        StopWaitingForPlayersReady();
        StopGateTimeout();

        _endRequested   = true;
        _pendingMinHold = 0f;
        _pendingFade    = null;
        TryFinishCover();
    }

    /// <summary>
    /// 이번 커튼에 한해 게이트 타임아웃을 늘린다 — 씬 로드 전 대기가 긴 구간(초대 참여: 로비 참가 +
    /// P2P 연결 + 씬 동기화) 전용. 커튼이 걷히면 기본값으로 돌아간다.
    /// </summary>
    public void UseLongTimeout(float seconds)
    {
        _timeoutOverride = seconds;
        RestartGateTimeout();
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

    // ── 준비 게이트 ─────────────────────────────────────────────────

    /// <summary>
    /// 기다릴 조건을 등록한다. 덮여 있지 않으면 무시된다(걷힌 뒤 등록해봐야 아무 의미가 없다).
    /// 같은 id를 여러 번 등록해도 하나로 취급한다 — 재시도·중복 호출에 안전하다.
    /// </summary>
    public void RegisterGate(string id)
    {
        if (string.IsNullOrEmpty(id) || _coverStartTime < 0f) return;
        if (!_gates.Add(id)) return;

        RestartGateTimeout(); // 새 진전이 있었으니 시간을 다시 준다
    }

    /// <summary>등록한 조건이 끝났다. 전부 끝나고 EndCover도 요청돼 있으면 그때 실제로 걷힌다.</summary>
    public void MarkGateReady(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!_gates.Remove(id)) return;

        if (_gates.Count > 0) RestartGateTimeout();
        TryFinishCover();
    }

    /// <summary>id 말고 다른 게이트가 아직 남아 있는가. 전원 준비 보고 시점을 잡는 데 쓴다.</summary>
    public bool HasPendingGatesOtherThan(string id)
    {
        foreach (string g in _gates)
            if (g != id) return true;
        return false;
    }

    /// <summary>덮여 있는가. 게이트를 등록해도 되는 구간인지 확인용.</summary>
    public bool IsCovered => _coverStartTime >= 0f;

    /// <summary>
    /// <see cref="OnGatesTimedOut"/> 핸들러 안에서만 호출. "내가 리로드로 재시도할 테니
    /// 커튼을 덮은 채 두라"는 뜻이다. 아무도 부르지 않으면 커튼은 그냥 걷히고 진행한다.
    /// </summary>
    public void KeepCoveredForRetry() => _retryHandled = true;

    void RestartGateTimeout()
    {
        StopGateTimeout();
        if (_gates.Count == 0 || _coverStartTime < 0f) return;
        _gateTimeoutRoutine = StartCoroutine(GateTimeoutRoutine());
    }

    void StopGateTimeout()
    {
        if (_gateTimeoutRoutine == null) return;
        StopCoroutine(_gateTimeoutRoutine);
        _gateTimeoutRoutine = null;
    }

    IEnumerator GateTimeoutRoutine()
    {
        float timeout = _timeoutOverride ?? gateTimeoutSeconds;
        yield return new WaitForSecondsRealtime(timeout);
        _gateTimeoutRoutine = null;

        var pending = new string[_gates.Count];
        _gates.CopyTo(pending);

        Debug.LogWarning($"[LoadingCurtain] 준비 게이트가 {timeout}초 안에 채워지지 않았습니다 — " +
                         $"미완: {string.Join(", ", pending)}");

        _retryHandled = false;
        OnGatesTimedOut?.Invoke(pending);

        // 누군가 리로드로 재시도하겠다고 했으면 덮은 채 유지한다.
        // 새 씬이 로드되면 게이트가 다시 등록되고 타이머도 거기서 다시 시작한다.
        if (_retryHandled) yield break;

        _gates.Clear();
        _waitingForPlayersReady = false;
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;

        // 포기하고 진행 — 무한 암전보다 낫다. 어긋난 상태라면 대개 낙사 → §11 사망 문으로
        // 전원 리로드가 걸려 스스로 복구된다.
        _endRequested = true;
        TryFinishCover();
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
        RegisterGate(PlayersGate);

        if (_waitingForPlayersReady) return;
        _waitingForPlayersReady = true;

        PlayerSpawnCoordinator.OnPlayersReady += HandlePlayersReady;
    }

    void HandlePlayersReady()
    {
        StopWaitingForPlayersReady();
        MarkGateReady(PlayersGate);
        EndCover();
    }

    void StopWaitingForPlayersReady()
    {
        if (!_waitingForPlayersReady) return;
        _waitingForPlayersReady = false;
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;
    }
}
