using System.Collections;
using UnityEngine;

/// <summary>
/// WindTrap의 바람 흡입/방출에 맞춰 입 오므림/벌림 BlendShape 애니메이션을 재생하는 컴포넌트.
/// MouthTrapAnimator와 동일한 패턴으로 WindTrap 전용으로 분리.
///
/// [동작 흐름]
/// 1. OnWindCharge  → 바람 발동 chargeTime 전에 입구를 서서히 오므림
/// 2. (바람 효과 재생, 입은 오므린 상태 유지)
/// 3. OnWindEnd     → 바람 종료 후 입구를 서서히 벌림
///
/// [Push / Pull 구분]
/// Push(내뱉기) : blowShapeIndex 사용
/// Pull(흡입)   : suckShapeIndex 사용 (−1이면 blowShapeIndex로 대체)
/// Shape Key 하나로 통일할 경우 suckShapeIndex를 −1로 두면 됨.
///
/// [설정 방법]
/// 1. WindTrap과 같은 GameObject에 부착
/// 2. mouthRenderer : 입 메시의 SkinnedMeshRenderer 연결 (비워두면 자식에서 자동 탐색)
/// 3. blowShapeIndex / suckShapeIndex : Unity Inspector > SkinnedMeshRenderer > BlendShapes 인덱스
///    (Blender Basis는 Unity에서 제외 → Blender 1번 = Unity 0번)
/// 4. chargeTime : 이 값만큼 바람 발동이 자동으로 지연되어 오므림과 동기화됨
/// </summary>
[RequireComponent(typeof(WindTrap))]
public class MouthWindAnimator : MonoBehaviour
{
    [Header("BlendShape 대상")]
    [Tooltip("입 메시의 SkinnedMeshRenderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] SkinnedMeshRenderer mouthRenderer = null;

    [Tooltip("Push(내뱉기) 모드의 BlendShape 인덱스")]
    [SerializeField] int blowShapeIndex = 0;

    [Tooltip("Pull(흡입) 모드의 BlendShape 인덱스. −1이면 blowShapeIndex 사용")]
    [SerializeField] int suckShapeIndex = -1;

    [Header("타이밍 (초)")]
    [Tooltip("입구를 오므리는 데 걸리는 시간. 이 시간만큼 WindTrap 발동이 자동 지연됨")]
    [SerializeField] float chargeTime = 0.4f;

    [Tooltip("바람 종료 후 입구가 다시 벌어지는 데 걸리는 시간")]
    [SerializeField] float openTime   = 0.3f;

    [Header("애니메이션 커브")]
    [Tooltip("오므릴 때 커브. EaseIn 권장 (천천히 시작, 빠르게 닫힘)")]
    [SerializeField] AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("벌어질 때 커브. EaseOut 권장 (빠르게 열렸다가 안정)")]
    [SerializeField] AnimationCurve openCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    WindTrap  _wind;
    Coroutine _animCoroutine;
    int       _activeShapeIndex;

    void Awake()
    {
        _wind = GetComponent<WindTrap>();

        if (mouthRenderer == null)
            mouthRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        // chargeTime만큼 WindTrap 발동을 지연시켜 입 오므림과 자동 동기화
        _wind.SetWindChargeTime(chargeTime);
    }

    void OnEnable()
    {
        if (_wind == null) return;
        _wind.OnWindCharge += HandleWindCharge;
        _wind.OnWindEnd    += HandleWindEnd;
    }

    void OnDisable()
    {
        if (_wind == null) return;
        _wind.OnWindCharge -= HandleWindCharge;
        _wind.OnWindEnd    -= HandleWindEnd;
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────────────

    void HandleWindCharge()
    {
        // Pull 모드이고 별도 suckShapeIndex가 설정된 경우 해당 인덱스 사용
        _activeShapeIndex = (_wind.CurrentWindMode == WindTrap.WindMode.Pull && suckShapeIndex >= 0)
            ? suckShapeIndex
            : blowShapeIndex;

        if (!IsValid())
        {
            Debug.LogWarning($"[MouthWindAnimator] {name}: BlendShape 설정 오류 " +
                             $"(shapeIndex={_activeShapeIndex}). Inspector를 확인하세요.", this);
            return;
        }

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(LerpShape(GetWeight(), 100f, chargeTime, closeCurve));
    }

    void HandleWindEnd()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(LerpShape(GetWeight(), 0f, openTime, openCurve));
    }

    // ── 애니메이션 루틴 ────────────────────────────────────────────────────

    IEnumerator LerpShape(float from, float to, float duration, AnimationCurve curve)
    {
        if (!IsValid() || duration <= 0f)
        {
            SetWeight(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetWeight(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetWeight(to);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    bool IsValid() =>
        mouthRenderer != null &&
        mouthRenderer.sharedMesh != null &&
        _activeShapeIndex >= 0 &&
        _activeShapeIndex < mouthRenderer.sharedMesh.blendShapeCount;

    float GetWeight() => IsValid() ? mouthRenderer.GetBlendShapeWeight(_activeShapeIndex) : 0f;

    void SetWeight(float w)
    {
        if (IsValid()) mouthRenderer.SetBlendShapeWeight(_activeShapeIndex, w);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 오므리기 (Blow)")]
    void TestClose()
    {
        _activeShapeIndex = blowShapeIndex;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(LerpShape(GetWeight(), 100f, chargeTime, closeCurve));
    }

    [ContextMenu("테스트: 오므리기 (Suck)")]
    void TestCloseSuck()
    {
        _activeShapeIndex = suckShapeIndex >= 0 ? suckShapeIndex : blowShapeIndex;
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(LerpShape(GetWeight(), 100f, chargeTime, closeCurve));
    }

    [ContextMenu("테스트: 즉시 열기")]
    void TestOpen()
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        SetWeight(0f);
    }

    [ContextMenu("테스트: BlendShape 목록 출력")]
    void PrintBlendShapes()
    {
        if (mouthRenderer == null) { Debug.LogError("[MouthWindAnimator] mouthRenderer가 null입니다."); return; }
        int count = mouthRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            Debug.Log($"  [{i}] {mouthRenderer.sharedMesh.GetBlendShapeName(i)}");
    }
}
