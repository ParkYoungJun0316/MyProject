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
        }

        _countdown -= Time.deltaTime;
        OnCountdownTick?.Invoke(Mathf.Max(0f, _countdown));

        if (_countdown <= 0f)
            CompleteCountdown();
    }

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>게이트 즉시 활성화. 전원 존 점유 시 카운트다운 가능.</summary>
    public void Arm()
    {
        _isArmed    = true;
        _isCounting = false;
        _countdown  = countdownDuration;
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
    }

    void CompleteCountdown()
    {
        SetZoneCountdownVisual(false);
        OnCountdownComplete?.Invoke();
        Disarm();
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

    // ── 에디터 테스트 ─────────────────────────────────────────────

    [ContextMenu("테스트: 즉시 활성화")]
    void Debug_Arm() => Arm();

    [ContextMenu("테스트: 비활성화")]
    void Debug_Disarm() => Disarm();
}
