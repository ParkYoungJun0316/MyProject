using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 발판을 밑에서 솟아오르게 하거나(Rise) 아래로 내려가게(Sink) 하는 범용 컴포넌트.
/// 보스 패턴에서 boulder가 발판을 파괴한 뒤 자동 재생성하거나,
/// 직접 Rise/Sink를 호출해 수동으로 발판 연출을 제어할 때 사용.
///
/// [계층 구조 권장]
///   PlatformSlot [PlatformMover 부착]  ← 절대 파괴되지 않는 슬롯
///   └── (런타임에 platformPrefab 인스턴스 자동 생성)
///
/// [자동 재생성 흐름]
///   1. Start() → Rise() 호출 → platformPrefab이 hiddenOffset 위치에 생성 → SlotPos로 솟아오름
///   2. Boulder 충돌 → Breakable.OnBreak 이벤트 → PlatformMover.OnPlatformBroken()
///   3. autoRespawn=true 이면 respawnDelay 초 후 Rise() 재호출 → 무한 반복
///
/// [수동 제어 흐름]
///   Rise()  - 발판을 숨김 위치에서 생성 후 SlotPos로 솟아오르게 함
///   Sink()  - 현재 발판을 숨김 위치로 내려보낸 뒤 소멸 (자동 재생성 취소)
///   Break() - 외부에서 파괴 트리거 (Breakable 없이도 사용 가능)
/// </summary>
public class PlatformMover : MonoBehaviour
{
    [Header("발판 프리팹")]
    [Tooltip("런타임에 생성할 발판 프리팹.\n" +
             "Breakable 컴포넌트를 포함하면 파괴 이벤트를 자동으로 수신함.")]
    [SerializeField] private GameObject platformPrefab = null;

    [Header("숨김 위치 오프셋 (Rise 시작 / Sink 종료)")]
    [Tooltip("이 슬롯(transform.position) 기준 오프셋.\n" +
             "예: (0,-3,0) = 3m 아래에서 솟아오름 / 3m 아래로 내려감.\n" +
             "Rise 시작 위치와 Sink 종료 위치를 동일하게 사용.")]
    [SerializeField] private Vector3 hiddenOffset = new Vector3(0f, 0f, 0f);

    [Header("Rise (솟아오르기)")]
    [Tooltip("솟아오르는 데 걸리는 시간(초). 0이면 즉시 배치")]
    [SerializeField] private float riseDuration = 0f;

    [Tooltip("솟아오르기 이징 커브.\n" +
             "EaseOut(처음 빠르고 끝 느림) 권장 → 발판이 탁 올라오는 느낌")]
    [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sink (내려가기)")]
    [Tooltip("내려가는 데 걸리는 시간(초). 0이면 즉시 소멸")]
    [SerializeField] private float sinkDuration = 0f;

    [Tooltip("내려가기 이징 커브.\n" +
             "EaseIn(처음 느리고 끝 빠름) 권장 → 발판이 서서히 가라앉는 느낌")]
    [SerializeField] private AnimationCurve sinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("자동 재생성")]
    [Tooltip("발판 파괴 후 자동으로 Rise() 재호출. false이면 외부에서 수동 호출 필요.")]
    [SerializeField] private bool autoRespawn = true;

    [Tooltip("파괴 후 다시 솟아오르기까지 대기 시간(초)")]
    [SerializeField] private float respawnDelay = 0f;

    [Tooltip("게임 Start 시 자동으로 Rise() 호출")]
    [SerializeField] private bool riseOnStart = true;

    [Header("이벤트")]
    [Tooltip("Rise 애니메이션 완료 시 호출")]
    public UnityEvent onRiseComplete;

    [Tooltip("Sink 애니메이션 완료 후 소멸 시 호출")]
    public UnityEvent onSinkComplete;

    [Tooltip("발판 파괴 시 호출 (Breakable.OnBreak 수신 또는 Break() 직접 호출)")]
    public UnityEvent onPlatformBroken;

    // ── 내부 상태 ────────────────────────────────────────────────────────

    GameObject _platform;
    Coroutine  _moveCoroutine;
    Coroutine  _respawnCoroutine;
    bool       _broken;

    Vector3 SlotPos   => transform.position;
    Vector3 HiddenPos => transform.position + hiddenOffset;

    // ── Unity 라이프사이클 ───────────────────────────────────────────────

    void Start()
    {
        if (riseOnStart)
            Rise();
    }

    // ── 외부 API ────────────────────────────────────────────────────────

    /// <summary>
    /// 발판을 HiddenPos에서 생성해 SlotPos로 솟아오르게 함.
    /// 이미 발판이 있으면 파괴 후 새로 생성.
    /// </summary>
    public void Rise()
    {
        StopMove();
        StopRespawn();

        if (platformPrefab == null)
        {
            Debug.LogWarning($"[PlatformMover] {name}: platformPrefab이 비어 있습니다.", this);
            return;
        }

        // 기존 인스턴스 정리
        if (_platform != null)
        {
            Destroy(_platform);
            _platform = null;
        }

        _broken   = false;
        _platform = Instantiate(platformPrefab, HiddenPos, transform.rotation);

        // Breakable.OnBreak 이벤트 자동 연결 (자식 포함 탐색)
        Breakable breakable = _platform.GetComponentInChildren<Breakable>();
        if (breakable != null)
            breakable.OnBreak.AddListener(OnPlatformBroken);

        _moveCoroutine = StartCoroutine(
            LerpPosition(_platform.transform, HiddenPos, SlotPos, riseDuration, riseCurve, onRiseComplete));
    }

    /// <summary>
    /// 현재 발판을 HiddenPos로 내려보낸 뒤 소멸시킴.
    /// autoRespawn 예약이 있으면 취소함.
    /// </summary>
    public void Sink()
    {
        StopRespawn();
        if (_platform == null) return;

        StopMove();
        _moveCoroutine = StartCoroutine(SinkRoutine());
    }

    /// <summary>
    /// 외부에서 발판 파괴를 강제 트리거.
    /// Breakable 컴포넌트 없이도 동작. autoRespawn이면 respawnDelay 후 Rise 예약.
    /// </summary>
    public void Break()
    {
        if (_broken) return;
        OnPlatformBroken();
    }

    // ── 이벤트 수신 ─────────────────────────────────────────────────────

    /// <summary>Breakable.OnBreak 리스너 또는 Break() 직접 호출 시 진입</summary>
    void OnPlatformBroken()
    {
        if (_broken) return;
        _broken   = true;
        _platform = null;  // Breakable이 Destroy 처리하므로 참조만 null

        StopMove();
        onPlatformBroken?.Invoke();

        if (autoRespawn)
            _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    // ── 코루틴 ──────────────────────────────────────────────────────────

    IEnumerator RespawnRoutine()
    {
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);
        Rise();
    }

    IEnumerator SinkRoutine()
    {
        if (_platform == null) yield break;

        Transform t    = _platform.transform;
        Vector3   from = t.position;

        yield return LerpPosition(t, from, HiddenPos, sinkDuration, sinkCurve, onSinkComplete);

        if (_platform != null)
        {
            Destroy(_platform);
            _platform = null;
        }
    }

    IEnumerator LerpPosition(Transform target, Vector3 from, Vector3 to,
                              float duration, AnimationCurve curve, UnityEvent onComplete)
    {
        if (duration <= 0f)
        {
            if (target != null) target.position = to;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null) yield break;  // 외부에서 Destroy된 경우 안전 탈출

            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            target.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        if (target != null) target.position = to;
        onComplete?.Invoke();
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────

    void StopMove()
    {
        if (_moveCoroutine == null) return;
        StopCoroutine(_moveCoroutine);
        _moveCoroutine = null;
    }

    void StopRespawn()
    {
        if (_respawnCoroutine == null) return;
        StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = null;
    }

    // ── 에디터 테스트 ────────────────────────────────────────────────────

    [ContextMenu("테스트: Rise (솟아오르기)")]
    void Debug_Rise() => Rise();

    [ContextMenu("테스트: Sink (내려가기)")]
    void Debug_Sink() => Sink();

    [ContextMenu("테스트: Break (파괴 + 재생성 예약)")]
    void Debug_Break() => Break();

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 slotPos   = transform.position;
        Vector3 hiddenPos = slotPos + hiddenOffset;

        // 슬롯 위치 (최종 목표 위치) = 초록
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(slotPos, 0.3f);
        UnityEditor.Handles.Label(slotPos + Vector3.right * 0.35f, "SlotPos");

        // 숨김 위치 (Rise 시작 / Sink 종료) = 빨강
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(hiddenPos, 0.3f);
        UnityEditor.Handles.Label(hiddenPos + Vector3.right * 0.35f, "HiddenPos");

        // 이동 경로 = 노랑
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawLine(slotPos, hiddenPos);
    }
#endif
}
