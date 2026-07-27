using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 입 모양 함정 발사대의 오므림/벌림 BlendShape 애니메이션 컴포넌트.
///
/// [동기화 방식 — Mouth↔Arrow 타이밍 (ArrowTrap Mouth Anim 버전과 동일 패턴, 2026-07-27)]
/// Host만 로컬 OnPreFireCharge/OnFiring을 직접 구독해 재생하고(zero latency, 아래서
/// IsServer 가드), Client는 이 로컬 이벤트를 절대 구독하지 않는다 — ArrowTrap이 Host에서만
/// StageNetworkState.SyncArrowChargeClientRpc/SyncArrowFireClientRpc로 릴레이하고, Client는
/// 그 RPC로 도착한 PlayOpenFromNetwork/PlayHoldFromNetwork를 통해서만 재생한다. 각 피어가
/// 자기 로컬 이벤트로 직접 재생하면 Client의 ServerTime 추정 오차·백그라운드 스로틀링에
/// 따라 화살 Spawn(Host 발행 신호)과 다른 시계에서 출발해 어긋난다.
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

        // Host만 로컬 TrapBase 이벤트를 직접 구독. Client는 자기 로컬 스케줄(부정확한
        // ServerTime 추정)로 이 이벤트를 받으면 Host가 보낸 RPC(아래 PlayOpenFromNetwork/
        // PlayHoldFromNetwork)와 다른 시계에서 출발해 화살 Spawn과 어긋난다.
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        _trap.OnPreFireCharge += HandlePreFireCharge;
        _trap.OnFiring        += HandleFiring;
    }

    void OnDisable()
    {
        if (_trap != null)
        {
            _trap.OnPreFireCharge -= HandlePreFireCharge;
            _trap.OnFiring        -= HandleFiring;
        }

        // 발사 충전 중(입 닫힌 상태)에 리셋이 들어와도 즉시 0으로 복원
        if (_closeCoroutine != null) { StopCoroutine(_closeCoroutine); _closeCoroutine = null; }
        SetWeight(0f);
    }

    // ── 재생 진입점 (Host: 로컬 TrapBase 이벤트 직접 구독 / Client: ArrowTrap.PlayChargeById·
    // PlayFireById가 StageNetworkState RPC 수신 시 호출) ───────────────────

    /// <summary>Host는 OnPreFireCharge 직접 구독, Client는 SyncArrowChargeClientRpc 수신으로 호출됨.</summary>
    public void PlayOpenFromNetwork() => HandlePreFireCharge();

    /// <summary>Host는 OnFiring 직접 구독, Client는 SyncArrowFireClientRpc 수신으로 호출됨.</summary>
    public void PlayHoldFromNetwork() => HandleFiring();

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
