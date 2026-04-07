using System.Collections;
using UnityEngine;

/// <summary>
/// 입 배경 연출 컨트롤러 — 상단 턱을 회전시켜 입을 닫았다 여는 시각 효과.
/// 데미지/충돌 없이 순수 연출 전용.
///
/// [타이밍 모드]
/// FixedInterval  : fixedInterval 초마다 자동 닫힘
/// RandomInterval : randomMin~randomMax 사이 랜덤 간격으로 자동 닫힘
/// Manual         : 외부에서 TriggerClose() 직접 호출
///
/// [설정 방법]
/// 1. 이 스크립트를 루트 GameObject에 부착
/// 2. upperJaw에 Mouth_Upper Transform 연결
/// 3. closeRotationOffset으로 닫히는 각도 설정
///    → Unity 에디터에서 Mouth_Upper를 직접 돌려보고 열림/닫힘 차이값 입력
/// </summary>
public class MouthController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("상단 턱 Transform (Mouth_Upper 오브젝트)")]
    [SerializeField] private Transform upperJaw = null;

    [Header("회전 설정")]
    [Tooltip("닫힐 때 upperJaw에 더해지는 로컬 오일러 오프셋\n" +
             "예: (70,0,0) → 로컬 X축으로 70도 아래 회전")]
    [SerializeField] private Vector3 closeRotationOffset = new Vector3(70f, 0f, 0f);

    [Tooltip("닫히는 데 걸리는 시간(초)")]
    [SerializeField] private float closeSpeed = 0.3f;

    [Tooltip("완전히 닫힌 상태 유지 시간(초)")]
    [SerializeField] private float holdDuration = 1.0f;

    [Tooltip("다시 열리는 데 걸리는 시간(초)")]
    [SerializeField] private float openSpeed = 0.5f;

    [Tooltip("닫힘/열림 움직임 감속 커브 (기본: EaseInOut)")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("타이밍 설정")]
    [Tooltip("FixedInterval: 고정 간격 반복\nRandomInterval: 랜덤 간격 반복\nManual: 외부 호출만 반응")]
    [SerializeField] private TimingMode timingMode = TimingMode.FixedInterval;

    [Tooltip("FixedInterval 모드 전용 — 닫힘 발동 간격(초)")]
    [SerializeField] private float fixedInterval = 10f;

    [Tooltip("RandomInterval 모드 전용 — 최소 간격(초)")]
    [SerializeField] private float randomIntervalMin = 5f;

    [Tooltip("RandomInterval 모드 전용 — 최대 간격(초)")]
    [SerializeField] private float randomIntervalMax = 15f;

    [Tooltip("첫 발동까지 대기 시간(초)")]
    [SerializeField] private float initialDelay = 0f;

    [Tooltip("Start 시 자동으로 사이클 시작 여부")]
    [SerializeField] private bool startOnAwake = true;

    public enum TimingMode { FixedInterval, RandomInterval, Manual }

    Quaternion _openRotation;
    Quaternion _closedRotation;
    bool _isClosing;
    Coroutine _cycleCoroutine;

    void Start()
    {
        if (upperJaw == null) return;

        _openRotation   = upperJaw.localRotation;
        _closedRotation = Quaternion.Euler(upperJaw.localEulerAngles + closeRotationOffset);

        if (startOnAwake && timingMode != TimingMode.Manual)
            StartCycle();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>자동 사이클 시작 (FixedInterval / RandomInterval 모드)</summary>
    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(AutoCycle());
    }

    /// <summary>자동 사이클 중지</summary>
    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
    }

    /// <summary>Manual 모드에서 즉시 닫기 트리거. 이미 닫히는 중이면 무시.</summary>
    public void TriggerClose()
    {
        if (_isClosing) return;
        StartCoroutine(CloseCycle());
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator AutoCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (!_isClosing)
                StartCoroutine(CloseCycle());

            float interval = timingMode == TimingMode.RandomInterval
                ? Random.Range(randomIntervalMin, randomIntervalMax)
                : fixedInterval;

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator CloseCycle()
    {
        if (upperJaw == null) yield break;

        _isClosing = true;

        yield return RotateJaw(_openRotation, _closedRotation, closeSpeed);

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        yield return RotateJaw(_closedRotation, _openRotation, openSpeed);

        _isClosing = false;
    }

    IEnumerator RotateJaw(Quaternion from, Quaternion to, float duration)
    {
        if (duration <= 0f)
        {
            upperJaw.localRotation = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float curved = easeCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            upperJaw.localRotation = Quaternion.Lerp(from, to, curved);
            yield return null;
        }

        upperJaw.localRotation = to;
    }
}
