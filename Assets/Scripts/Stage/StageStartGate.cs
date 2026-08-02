using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 매칭 게임 시작 게이트.
///
/// [핵심 동작]
/// 1. Armed 상태에서 모든 ColoredStartZone에 각자 색 플레이어가 서면 카운트다운 시작
/// 2. 한 명이라도 이탈하면 카운트다운 즉시 리셋 — 다시 전원 모여야 함
/// 3. countdownDuration초 유지 → StageManager.StartStage() 호출 후 Disarmed
///
/// [StageManager 연동]
/// - stageManager 필드에 StageManager 연결 → StartStage() 자동 호출
/// - StageManager.autoStart 는 반드시 false 로 설정할 것
///
/// [씬 설정 순서]
/// 1. 빈 GameObject 생성 → StageStartGate 추가
/// 2. 자식으로 ColoredStartZone 4개 배치 (Blue / Red / Green / Yellow)
/// 3. stageManager 필드에 StageManager 연결
/// 4. countdownDuration = 5
/// 5. StageManager.autoStart = false 확인
/// 6. (선택) OnCountdownTick → TimerUI.SetTime 연결
/// 7. ⚠ 씬 안에 StageStartGate가 여러 개면(T.Stage2/4/5처럼 방마다 게이트가 있는 씬)
///    gateId에 게이트마다 서로 다른 값(0, 1, 2...)을 반드시 지정할 것 — 안 하면 콘솔에
///    설정 오류 로그가 뜨고, 실제로는 뒤 게이트가 앞 게이트의 낡은 시작 신호로 오작동한다.
///    씬에 게이트가 1개뿐이면 기본값(-1) 그대로 둬도 무방.
///
/// ※ 사망 시 씬 재로드 방식 → 게이트는 씬 로드 시 armOnStart=true로 자동 초기화됨
/// </summary>
public class StageStartGate : MonoBehaviour
{
    [Header("구성 존")]
    [Tooltip("색상 매칭 존 4개. 비우면 자식 GameObject에서 자동 수집")]
    [SerializeField] ColoredStartZone[] zones;

    [Header("연결")]
    [Tooltip("시작할 StageManager. StartStage()를 자동 호출함.\n" +
             "⚠ StageManager.autoStart 는 반드시 false 로 설정할 것")]
    [SerializeField] StageManager stageManager;

    [Header("카운트다운")]
    [Tooltip("전원 점유 후 게임 시작까지 유지해야 하는 시간(초).\n" +
             "중간에 이탈하면 리셋됨.")]
    [SerializeField] float countdownDuration = 5f;

    [Header("초기 상태")]
    [Tooltip("씬 로드 즉시 게이트 활성화.\n" +
             "false 면 Arm() 을 외부에서 직접 호출해야 함.")]
    [SerializeField] bool armOnStart = true;

    [Header("게이트 식별자 (다중 게이트 씬 전용)")]
    [Tooltip("씬에 이 게이트가 유일하면 -1(기본값) 그대로 둘 것.\n" +
             "씬 안에 StageStartGate가 여러 개면(예: T.Stage2/4/5) 각 게이트마다 서로 다른 값을 줄 것" +
             "(0, 1, 2...) — StageNetworkState._stageStartSignal 슬롯을 공유하기 때문에, 이 값이 없으면" +
             "앞 게이트가 완료했다는 낡은 신호를 뒤 게이트가 자기 것으로 오인해 즉시 시작해버린다" +
             "(2026-08 버그 수정, NetworkDesign.md §11A.1 참고).")]
    [SerializeField] int gateId = -1;

    [Header("이벤트")]
    [Tooltip("매 프레임 카운트다운 남은 시간(0~countdownDuration)을 전달.\n" +
             "TimerUI.SetTime 등 UI 컴포넌트에 연결 권장.\n" +
             "카운트다운 중이 아닐 때는 countdownDuration 값을 전달.")]
    public UnityEvent<float> OnCountdownTick;

    [Tooltip("카운트다운 리셋 시 호출 (이탈 등). UI 초기화 등에 사용.")]
    public UnityEvent OnCountdownReset;

    [Tooltip("카운트다운 완료 직후, StartStage() 호출 직전에 발동.\n" +
             "시작 연출(화면 효과 등)에 연결 가능.")]
    public UnityEvent OnCountdownComplete;

    bool  _isArmed;
    bool  _isCounting;
    float _countdown;

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Awake()
    {
        if (zones == null || zones.Length == 0)
            zones = GetComponentsInChildren<ColoredStartZone>(true);

        CheckGateIdConfigured();
    }

    /// <summary>
    /// 씬에 StageStartGate가 여러 개인데 gateId가 미설정(-1)이거나 다른 게이트와 중복이면
    /// 경고. 조용히 넘어가면 나중에 "Stage5.2가 저절로 시작됨" 같은 증상으로만 드러나
    /// 원인 추적이 오래 걸린다(2026-08 버그) — 설정 실수를 여기서 바로 드러낸다.
    /// </summary>
    void CheckGateIdConfigured()
    {
        StageStartGate[] all = FindObjectsByType<StageStartGate>(FindObjectsSortMode.None);
        if (all.Length <= 1) return;

        int sameId = 0;
        foreach (StageStartGate g in all)
            if (g != null && g.gateId == gateId) sameId++;

        if (gateId < 0 || sameId > 1)
            Debug.LogError(
                $"[StageStartGate] '{name}' gateId 설정 오류 — 이 씬에 게이트 {all.Length}개인데 " +
                $"gateId={gateId} (미설정 또는 다른 게이트와 중복). 게이트마다 서로 다른 값(0,1,2...)을 " +
                "Inspector에서 지정할 것 — 안 하면 다른 게이트의 낡은 시작 신호를 자기 것으로 오인함.",
                this);
    }

    void Start()
    {
        _countdown = countdownDuration;
    }

    void OnEnable()
    {
        // 씬 로드 시 armOnStart=true이면 자동 활성화.
        if (armOnStart) Arm();
    }

    void Update()
    {
        if (!_isArmed) return;

        var nm = NetworkManager.Singleton;

        // ── Client: ServerTime 기반 카운트다운 표시 ───────────────
        if (nm != null && !nm.IsServer)
        {
            UpdateCountdownOnClient(nm);
            return;
        }

        // ── Host: 카운트다운 로직 ─────────────────────────────────
        if (!AllZonesOccupied())
        {
            if (_isCounting) ResetCountdown();
            return;
        }

        // 전원 점유 → 카운트다운 시작
        if (!_isCounting)
        {
            _isCounting = true;
            _countdown  = countdownDuration;
            SetZoneCountdownVisual(true);
            OnCountdownTick?.Invoke(_countdown);
            StageNetworkState.Instance?.MarkCountdownStart();
        }

        _countdown -= Time.deltaTime;
        OnCountdownTick?.Invoke(Mathf.Max(0f, _countdown));

        if (_countdown <= 0f)
            CompleteCountdown();
    }

    /// <summary>
    /// Client 전용: StageNetworkState의 NetworkVariable을 읽어
    /// ServerTime 기반 카운트다운 남은 시간을 계산하고 UI에 전달.
    /// </summary>
    void UpdateCountdownOnClient(NetworkManager nm)
    {
        var sns = StageNetworkState.Instance;
        if (sns == null) return;

        // 스테이지가 시작됐다면 이 게이트가 직접 연결된 StageManager를 시작
        // (RPC보다 NetworkVariable이 먼저 도달할 수 있으므로 여기서 처리)
        // gateId 일치까지 확인 — 씬에 게이트가 여럿이면(T.Stage2/4/5) 다른 게이트가 찍은
        // 신호까지 걸러야 함(2026-08 버그 수정, StageNetworkState.StageStartSignal 참고).
        if (sns.StageStartServerTime > 0 && sns.StageStartGateId == gateId && _isArmed)
        {
            SetZoneCountdownVisual(false);
            SetZonesActive(false);
            OnCountdownComplete?.Invoke();
            stageManager?.StartStage();
            Disarm();
            return;
        }

        if (sns.IsCountdownActive)
        {
            float remaining = Mathf.Max(0f,
                countdownDuration - (float)(nm.ServerTime.Time - sns.CountdownStartServerTime));
            if (!_isCounting)
            {
                _isCounting = true;
                SetZoneCountdownVisual(true);
            }
            OnCountdownTick?.Invoke(remaining);
        }
        else if (_isCounting)
        {
            // Host가 카운트다운을 리셋했음 (이탈 등)
            _isCounting = false;
            SetZoneCountdownVisual(false);
            OnCountdownReset?.Invoke();
            OnCountdownTick?.Invoke(countdownDuration);
        }
    }

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>게이트 즉시 활성화. 전원 존 점유 시 카운트다운 가능.</summary>
    public void Arm()
    {
        _isArmed    = true;
        _isCounting = false;
        _countdown  = countdownDuration;
        SetZonesActive(true);
        SetZoneCountdownVisual(false);
        OnCountdownReset?.Invoke();
        OnCountdownTick?.Invoke(countdownDuration);
    }

    /// <summary>게이트 비활성화. 진행 중인 카운트다운 중단.</summary>
    public void Disarm()
    {
        _isArmed    = false;
        _isCounting = false;
        _countdown  = countdownDuration;
        SetZoneCountdownVisual(false);
        OnCountdownReset?.Invoke();
    }

    // ── 내부 카운트다운 ───────────────────────────────────────────

    void ResetCountdown()
    {
        _isCounting = false;
        _countdown  = countdownDuration;
        SetZoneCountdownVisual(false);
        OnCountdownReset?.Invoke();
        OnCountdownTick?.Invoke(countdownDuration);

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.IsServer)
            StageNetworkState.Instance?.MarkCountdownReset();
    }

    void CompleteCountdown()
    {
        SetZoneCountdownVisual(false);
        SetZonesActive(false);
        OnCountdownComplete?.Invoke();
        Disarm();

        // Host만 StartStage 호출. Client의 StartStage는 UpdateCountdownOnClient()가
        // StageStartSignal(시간+gateId) NetworkVariable을 감지해 트리거한다 (§11A.1).
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        StageNetworkState.Instance?.MarkStageStart(gateId);
        stageManager?.StartStage();
    }

    bool AllZonesOccupied()
    {
        if (zones == null || zones.Length == 0) return false;

        bool anyActive = false;
        foreach (ColoredStartZone z in zones)
        {
            if (z == null || !z.gameObject.activeInHierarchy) continue;
            anyActive = true;
            if (!z.IsOccupied) return false;
        }

        // 활성화된 존이 하나도 없으면 카운트다운 불가
        return anyActive;
    }

    void SetZoneCountdownVisual(bool counting)
    {
        if (zones == null) return;
        foreach (ColoredStartZone z in zones)
            z?.SetCountdownVisual(counting);
    }

    void SetZonesActive(bool active)
    {
        if (zones == null) return;
        foreach (ColoredStartZone z in zones)
        {
            if (z == null) continue;

            // 존을 켤 때: GameSession이 있으면 활성 색인 경우만 켬
            // 존을 끌 때: 그냥 끔 (게임 시작 후 Disarm 등)
            if (active && GameSession.Instance != null
                       && !GameSession.Instance.IsColorActive(z.ColorType))
                continue;

            z.gameObject.SetActive(active);
        }
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

    [ContextMenu("테스트: 즉시 활성화")]
    void Debug_Arm() => Arm();

    [ContextMenu("테스트: 비활성화")]
    void Debug_Disarm() => Disarm();
}
