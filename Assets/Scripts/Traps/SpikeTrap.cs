using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 바닥에 배치하는 가시 함정.
/// activateInterval마다 가시가 올라왔다가 raiseDuration 후 내려감.
/// 
/// [설정 방법]
/// 1. 이 스크립트를 붙인 GameObject에 Collider(Trigger) 추가 → 데미지 판정 영역
/// 2. spikeVisual에 실제 가시 메시 오브젝트(자식) 연결 → 올라갔다 내려가는 비주얼
/// 3. spikeVisual이 없으면 히트박스만 동작 (비주얼 없이도 작동)
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpikeTrap : TrapBase
{
    [Header("Spike Trap")]
    [Tooltip("가시 비주얼 Transform (자식 오브젝트). 없으면 비주얼 이동 생략")]
    [SerializeField] private Transform spikeVisual = null;

    [Tooltip("플레이어에게 입히는 데미지")]
    [SerializeField] private int damage = 0;

    [Tooltip("가시가 완전히 올라와 있는 유지 시간(초)")]
    [SerializeField] private float raiseDuration = 0f;

    [Tooltip("가시가 올라오는 높이 (로컬 Y)")]
    [SerializeField] private float raiseHeight = 0f;

    [Tooltip("가시 이동 속도 (m/s). 0이면 즉시")]
    [SerializeField] private float raiseSpeed = 0f;

    [Tooltip("연속 데미지 간격(초). 0이면 닿을 때마다 즉시")]
    [SerializeField] private float damageInterval = 0f;

    [Header("사운드 (3D)")]
    [Tooltip("가시가 올라올 때 재생할 SFX. None이면 무음.")]
    [SerializeField] private SFXId raiseSfxId = SFXId.None;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] private float raiseMinDistance = 5f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] private float raiseMaxDistance = 25f;
    [SerializeField] private AudioRolloffMode raiseRolloffMode = AudioRolloffMode.Logarithmic;

    Collider spikeTrigger;
    BoxCollider spikeTriggerBox;
    Vector3 loweredLocalPos;
    Vector3 baseColliderCenter;
    bool isRaised;
    float nextDamageTime;

    protected override void Start()
    {
        EnsureInitialized();
        base.Start();
    }

    /// <summary>
    /// [버그 수정 2026-08] spikeTrigger 등은 원래 Start()에서만 세팅했는데, Client 씬 로드 중
    /// NetworkVariable(Phase) 콜백이 Start()보다 먼저 StageManager.StartStage() → Activate()를
    /// 동기 호출해버리는 레이스가 있었다(NGO가 AsyncOperation.completed 안에서 씬 배치
    /// NetworkObject를 역직렬화하며 OnValueChanged를 그 자리에서 호출 — 이 시점은 Unity가 아직
    /// 이 씬의 Start()들을 실행하기 전일 수 있음). RaiseCycle()이 spikeTrigger를 쓰기 직전에도
    /// 같은 초기화를 멱등하게 재시도해 Start() 타이밍과 무관하게 안전하도록 방어.
    /// </summary>
    void EnsureInitialized()
    {
        if (spikeTrigger != null) return;

        spikeTrigger    = GetComponent<Collider>();
        spikeTriggerBox = spikeTrigger as BoxCollider;
        spikeTrigger.isTrigger = true;
        spikeTrigger.enabled   = false;

        if (spikeVisual != null)
            loweredLocalPos = spikeVisual.localPosition;

        if (spikeTriggerBox != null)
            baseColliderCenter = spikeTriggerBox.center;
    }

    // ── 스케줄 기준 시각 결정 ─────────────────────────────────────────
    // [버그 수정 2026-07-24] ArrowTrap/DropTrap/WindTrap과 동일한 이유로 PhaseStartServerTime
    // (Host가 이 Phase 진입 직전에 기록한 절대 ServerTime)을 앵커로 사용 — Host/Client가 동일한
    // 절대 시각을 기준으로 삼아, Client의 Activate() 호출이 Phase NV 전파 지연만큼 늦게 와도
    // 스케줄이 밀리지 않는다. StageStartServerTime이 아니라 별도 슬롯인 PhaseStartServerTime을
    // 쓴다 — StageStartGate가 그 값을 "이 방 게이트 완료" 1회성 신호로 배타적으로 쓰므로 같이
    // 쓰면 안 된다. StageNetworkState가 없는 씬(테스트 등)에서는 로컬 Activate() 시각으로 폴백.
    protected override IEnumerator TrapLoop()
    {
        var nm = NetworkManager.Singleton;
        float scheduleStartTime;

        if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
        {
            scheduleStartTime = (float)StageNetworkState.Instance.PhaseStartServerTime + initialDelay;
            while (nm != null && (float)nm.ServerTime.Time < scheduleStartTime)
                yield return null;
        }
        else
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        }

        int cycle = 0;
        while (isRunning)
        {
            yield return StartCoroutine(FireWithCharge());

            if (activateInterval <= 0f)
            {
                isRunning = false;
                yield break;
            }

            cycle++;
            float targetTime = scheduleStartTime + cycle * activateInterval;
            float now         = nm != null ? (float)nm.ServerTime.Time : Time.time;
            yield return new WaitForSeconds(Mathf.Max(0f, targetTime - now));
        }
    }

    protected override void OnTrapTrigger()
    {
        if (!isRaised) StartCoroutine(RaiseCycle());
    }

    IEnumerator RaiseCycle()
    {
        EnsureInitialized();
        isRaised = true;
        spikeTrigger.enabled = true;

        if (raiseSfxId != SFXId.None)
            SFXManager.Instance?.PlayAtPoint(raiseSfxId, transform.position, raiseMinDistance, raiseMaxDistance, raiseRolloffMode);

        yield return MoveSpikeLocal(loweredLocalPos, loweredLocalPos + Vector3.up * raiseHeight);

        yield return new WaitForSeconds(raiseDuration);

        yield return MoveSpikeLocal(loweredLocalPos + Vector3.up * raiseHeight, loweredLocalPos);

        spikeTrigger.enabled = false;
        isRaised = false;
    }

    IEnumerator MoveSpikeLocal(Vector3 from, Vector3 to)
    {
        if (spikeVisual == null) yield break;

        float dist     = Vector3.Distance(from, to);
        float duration = (raiseSpeed > 0f) ? dist / raiseSpeed : 0.01f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            spikeVisual.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            SyncColliderCenter();
            yield return null;
        }

        spikeVisual.localPosition = to;
        SyncColliderCenter();
    }

    // BoxCollider center를 spikeVisual 위치에 맞게 갱신
    void SyncColliderCenter()
    {
        if (spikeTriggerBox == null || spikeVisual == null) return;
        float yOffset = spikeVisual.localPosition.y - loweredLocalPos.y;
        spikeTriggerBox.center = baseColliderCenter + Vector3.up * yOffset;
    }

    // 가시 위로 걸어 들어올 때
    void OnTriggerEnter(Collider other)
    {
        TryDamagePlayer(other);
    }

    // 가시가 올라올 때 이미 위에 서 있던 경우 + 연속 데미지
    void OnTriggerStay(Collider other)
    {
        TryDamagePlayer(other);
    }

    void TryDamagePlayer(Collider other)
    {
        if (!isRaised) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (Time.time < nextDamageTime) return;

        // CompareTag는 자식 콜라이더에선 실패하므로 컴포넌트 검색으로 판별
        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p == null) return;

        NetworkDamageUtil.ApplyDamage(p, damage, false);
        nextDamageTime = Time.time + Mathf.Max(damageInterval, 0.1f);
    }
}
