using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 매칭 게임 시작 게이트.
///
/// [핵심 동작]
/// 1. Armed 상태에서 모든 ColoredStartZone에 각자 색 플레이어가 서면 카운트다운 시작
/// 2. 한 명이라도 이탈하면 카운트다운 즉시 리셋 — 다시 전원 모여야 함
/// 3. countdownDuration초 유지 → StageManager.StartStage() 호출 후 Disarmed
/// 4. 마지막 리스폰 이후 armDelay초 뒤 자동 재활성화 → 다시 게임 시작 대기
///
/// [StageManager 연동]
/// - stageManager 필드에 StageManager 연결 → StartStage() 자동 호출
/// - StageManager.autoStart 는 반드시 false 로 설정할 것
///
/// [사망/리셋 연동]
/// - 플레이어 사망 시 직접 OnDied를 구독하지 않음
/// - StageResetOnPlayerDeath(오케스트레이터)가 스테이지 리셋 후 OnStageReset()을 호출
/// - OnStageReset(): Disarm + 전원 존 리스폰 + armDelay 후 재암
/// - 추가로 OnStageFailed 등 외부 이벤트와 OnStageReset() 을 연결 가능
///
/// [씬 설정 순서]
/// 1. 빈 GameObject 생성 → StageStartGate 추가
/// 2. 자식으로 ColoredStartZone 4개 배치 (Blue / Red / Green / Yellow)
/// 3. stageManager 필드에 StageManager 연결
/// 4. countdownDuration = 5 / armDelay = (respawnDelay + 0.5)
/// 5. StageManager.autoStart = false 확인
/// 6. (선택) OnCountdownTick → TimerUI.SetTime 연결
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

    [Header("리셋 재활성화")]
    [Tooltip("마지막 플레이어 리스폰 후 게이트가 다시 활성화될 때까지 대기(초).\n" +
             "Player.respawnDelay 보다 0.5~1초 크게 설정할 것.\n" +
             "(예: respawnDelay=2 → armDelay=2.5)")]
    [SerializeField] float armDelay = 2.5f;

    [Header("초기 상태")]
    [Tooltip("씬 로드 즉시 게이트 활성화.\n" +
             "false 면 Arm() 을 외부에서 직접 호출해야 함.")]
    [SerializeField] bool armOnStart = true;

    [Header("이벤트")]
    [Tooltip("매 프레임 카운트다운 남은 시간(0~countdownDuration)을 전달.\n" +
             "TimerUI.SetTime 등 UI 컴포넌트에 연결 권장.\n" +
             "카운트다운 중이 아닐 때는 countdownDuration 값을 전달.")]
    public UnityEvent<float> OnCountdownTick;

    [Tooltip("카운트다운 리셋 시 호출 (이탈·사망 등). UI 초기화 등에 사용.")]
    public UnityEvent OnCountdownReset;

    [Tooltip("카운트다운 완료 직후, StartStage() 호출 직전에 발동.\n" +
             "시작 연출(화면 효과 등)에 연결 가능.")]
    public UnityEvent OnCountdownComplete;

    bool      _isArmed;
    bool      _isCounting;
    float     _countdown;
    Coroutine _armCoroutine;

    /// <summary>현재 게이트가 활성(Armed) 상태인지. StageResetOnPlayerDeath에서 활성 게이트 탐색에 사용.</summary>
    public bool IsArmed => _isArmed;

    Player[] _players;

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Awake()
    {
        if (zones == null || zones.Length == 0)
            zones = GetComponentsInChildren<ColoredStartZone>(true);

        // 짝을 이루는 StageManager에 자신을 등록 → LinkedGate로 역참조 가능
        stageManager?.RegisterGate(this);
    }

    void Start()
    {
        CachePlayers();
        SubscribePlayerEvents();
        _countdown = countdownDuration;
    }

    void OnEnable()
    {
        // SetActive(false → true) 사이클 포함, 재활성화될 때마다 armOnStart이면 자동 재암.
        // Start()는 최초 1회만 실행되므로, 이후 재활성화 시 Arm 복원은 여기서 처리.
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

    void OnDestroy()
    {
        UnsubscribePlayerEvents();
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

    /// <summary>
    /// 전원 즉시 각자 존으로 리스폰 + armDelay 후 재활성화.
    /// Inspector에서 StageManager.OnStageFailed 등에 연결 가능.
    /// (플레이어 자동 구독으로도 처리되므로 필수 연결은 아님)
    /// </summary>
    public void OnStageReset()
    {
        if (_armCoroutine != null)
        {
            StopCoroutine(_armCoroutine);
            _armCoroutine = null;
        }
        Disarm();
        ForceRespawnAllToZones();
        StartArmCoroutine(armDelay);
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
        foreach (ColoredStartZone z in zones)
        {
            if (z == null) continue;
            if (!z.IsOccupied) return false;
        }
        return true;
    }

    void SetZoneCountdownVisual(bool counting)
    {
        if (zones == null) return;
        foreach (ColoredStartZone z in zones)
            z?.SetCountdownVisual(counting);
    }

    // ── 플레이어 이벤트 자동 구독 ─────────────────────────────────

    void CachePlayers()
    {
        _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
    }

    void SubscribePlayerEvents()
    {
        if (_players == null) return;
        foreach (Player p in _players)
        {
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev == null) continue;
            ev.OnRespawned += HandlePlayerRespawned;
        }
    }

    void UnsubscribePlayerEvents()
    {
        if (_players == null) return;
        foreach (Player p in _players)
        {
            if (p == null) continue;
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev == null) continue;
            ev.OnRespawned -= HandlePlayerRespawned;
        }
    }

    /// <summary>
    /// 플레이어 리스폰 시 자동 호출.
    /// 게이트가 비활성 상태일 때만 armDelay 후 재활성화 예약.
    /// (이미 활성 상태면 카운트다운에 영향 없이 무시)
    /// </summary>
    void HandlePlayerRespawned()
    {
        if (!_isArmed)
            StartArmCoroutine(armDelay);
    }

    // ── 전원 리스폰 처리 ──────────────────────────────────────────

    void ForceRespawnAllToZones()
    {
        if (zones == null || _players == null) return;

        foreach (Player p in _players)
        {
            if (p == null) continue;
            ColoredStartZone zone = FindZoneForPlayer(p);
            if (zone == null) continue;
            // IsDead=true  → spawn 위치만 갱신, RespawnAfter 코루틴이 해당 위치로 복귀
            // IsDead=false → 즉시 해당 존으로 텔레포트 + 체력/상태 초기화
            p.ForceRespawn(zone.SpawnPosition, zone.SpawnRotation);
        }
    }

    ColoredStartZone FindZoneForPlayer(Player p)
    {
        foreach (ColoredStartZone z in zones)
            if (z != null && z.ColorType == p.playerColorType)
                return z;
        return null;
    }

    // ── Arm 딜레이 코루틴 ─────────────────────────────────────────

    void StartArmCoroutine(float delay)
    {
        if (_armCoroutine != null) StopCoroutine(_armCoroutine);
        _armCoroutine = StartCoroutine(ArmDelayCoroutine(delay));
    }

    IEnumerator ArmDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        _armCoroutine = null;
        Arm();
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

    [ContextMenu("테스트: 즉시 활성화")]
    void Debug_Arm() => Arm();

    [ContextMenu("테스트: 비활성화")]
    void Debug_Disarm() => Disarm();

    [ContextMenu("테스트: 전원 존으로 리스폰 후 재활성화")]
    void Debug_OnStageReset() => OnStageReset();
}
