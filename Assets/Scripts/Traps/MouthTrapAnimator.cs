using System.Collections;
using UnityEngine;

/// <summary>
/// 입 모양 함정 발사대의 오므림/벌림 BlendShape 애니메이션 컴포넌트.
///
/// [동작 흐름]
/// 1. OnPreFireCharge  → 입구를 서서히 오므림 (chargeTime)
/// 2. OnFiring         → 발사 순간 즉시 스냅 오픈 (weight=0) — 다음 PreFireCharge 전까지 열린 상태 유지
///
/// [타이밍 자동화]
/// chargeTime + holdTime = preFireChargeTime 으로 ArrowTrap/DropTrap waitTime에 자동 반영됨.
/// fireAtSeconds 값이 1,5,7이든 2,3,4,5이든 별도 설정 없이 각 발사에 맞춰 동작함.
///
/// [설정 방법]
/// 1. ArrowTrap / DropTrap 과 같은 오브젝트에 부착
/// 2. mouthRenderer : 입 메시의 SkinnedMeshRenderer 연결 (비워두면 자식에서 자동 탐색)
/// 3. closeShapeIndex : Unity Inspector > SkinnedMeshRenderer > BlendShapes 인덱스
///    (Blender Basis는 Unity에서 제외 → Blender 1번 = Unity 0번)
/// 4. 플레이 중 우클릭 → 테스트 메뉴로 즉시 확인 가능
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class MouthTrapAnimator : MonoBehaviour
{
    [Header("BlendShape 대상")]
    [Tooltip("입 메시의 SkinnedMeshRenderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] SkinnedMeshRenderer mouthRenderer = null;

    [Tooltip("Unity Inspector > SkinnedMeshRenderer > BlendShapes 목록의 인덱스\n" +
             "주의: Blender Basis(0번)는 Unity에서 제외 → Blender 1번 = Unity 0번")]
    [SerializeField] int closeShapeIndex = 0;

    [Header("타이밍 (초)")]
    [Tooltip("입구를 오므리는 데 걸리는 시간")]
    [SerializeField] float chargeTime = 0.4f;

    [Tooltip("완전히 오므린 뒤 유지하는 시간")]
    [SerializeField] float holdTime   = 0.1f;

    TrapBase  _trap;
    Coroutine _closeCoroutine;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();

        if (mouthRenderer == null)
            mouthRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        // 이 값이 ArrowTrap/DropTrap의 waitTime에서 자동으로 빠져 모든 스케줄에 동기화됨
        _trap.SetPreFireChargeTime(chargeTime + holdTime);
    }

    void OnEnable()
    {
        if (_trap == null) return;
        _trap.OnPreFireCharge += HandlePreFireCharge;
        _trap.OnFiring        += HandleFiring;
    }

    void OnDisable()
    {
        if (_trap == null) return;
        _trap.OnPreFireCharge -= HandlePreFireCharge;
        _trap.OnFiring        -= HandleFiring;
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────────────

    void HandlePreFireCharge()
    {
        // 현재 weight에서 100(완전 닫힘)까지 chargeTime 동안 서서히 오므림
        if (_closeCoroutine != null) StopCoroutine(_closeCoroutine);
        _closeCoroutine = StartCoroutine(CloseRoutine());
    }

    void HandleFiring()
    {
        // 발사 순간 즉시 스냅 오픈 — 코루틴 불필요
        // 다음 PreFireCharge가 언제 오든 weight=0에서 시작하므로 스케줄 간격에 무관
        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }
        SetWeight(0f);
    }

    // ── 애니메이션 루틴 ────────────────────────────────────────────────────

    IEnumerator CloseRoutine()
    {
        yield return LerpShape(GetWeight(), 100f, chargeTime);
    }

    IEnumerator LerpShape(float from, float to, float duration)
    {
        if (!IsValid()) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetWeight(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetWeight(to);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    bool IsValid() =>
        mouthRenderer != null &&
        closeShapeIndex >= 0 &&
        closeShapeIndex < mouthRenderer.sharedMesh.blendShapeCount;

    float GetWeight() => IsValid() ? mouthRenderer.GetBlendShapeWeight(closeShapeIndex) : 0f;

    void SetWeight(float w)
    {
        if (IsValid()) mouthRenderer.SetBlendShapeWeight(closeShapeIndex, w);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 오므리기")]
    void TestClose()
    {
        if (_closeCoroutine != null) StopCoroutine(_closeCoroutine);
        _closeCoroutine = StartCoroutine(CloseRoutine());
    }

    [ContextMenu("테스트: 즉시 열기")]
    void TestOpen() => SetWeight(0f);

    [ContextMenu("테스트: BlendShape 목록 출력")]
    void PrintBlendShapes()
    {
        if (mouthRenderer == null) { Debug.LogError("mouthRenderer가 null입니다."); return; }
        int count = mouthRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            Debug.Log($"  [{i}] {mouthRenderer.sharedMesh.GetBlendShapeName(i)}");
    }
}
