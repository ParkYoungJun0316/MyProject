using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// T.Stage4(MovingCorridor) 탑다운 진행 오케스트레이터.
///
/// [담당 흐름]
///  StageStartGate.OnCountdownComplete
///   → 탑다운 카메라 전환 (pivot은 진행축만 추적, CorridorTopDownPivot)
///   → 블렌드 완료 대기
///   → OnTopDownReady 발동 (MovingCorridor.Activate / 뒷벽 SetActive / CrashCollider enable 연결)
///   → StartStage()
///
///  MemoryPathIntroController(Pioneer/Memory)와 동일 패턴. 탑다운은 스테이지 끝까지 유지하며,
///  해제는 다음 씬 re-bind(LocalPlayerCamera.SetTarget → ForceGameplayViewImmediate)가 담당한다.
///
/// [씬 설정 방법]
///  1. 빈 GameObject에 이 컴포넌트 추가
///  2. StageStartGate.OnCountdownComplete → BeginTopDown() 연결
///     ⚠ StageStartGate의 stageManager 필드는 비울 것 (이 컴포넌트가 대신 호출)
///     ⚠ 게이트에 걸려 있던 게임플레이 시작 호출(Activate 등)은 OnTopDownReady로 옮길 것
///  3. pivot에 CorridorTopDownPivot 연결
///  4. 프레이밍 값은 이 오브젝트를 선택한 채 씬뷰 Gizmo 보면서 조정
/// </summary>
public class CorridorTopDownIntroController : MonoBehaviour
{
    [Header("카메라")]
    [Tooltip("비워두면 LocalPlayerCamera.Instance를 자동 사용 (C안 표준).")]
    [SerializeField] ThirdPersonCamera thirdPersonCamera;

    ThirdPersonCamera ActiveCamera => thirdPersonCamera != null ? thirdPersonCamera : LocalPlayerCamera.Instance?.ThirdPersonCam;

    [Tooltip("진행축만 추적하는 pivot (CorridorTopDownPivot).")]
    [SerializeField] CorridorTopDownPivot pivot;

    [Header("탑다운 프레이밍 (씬뷰 Gizmo 보면서 조정)")]
    [SerializeField] float previewDistance = 20f;

    [Tooltip("90°면 Player 이동 방향 계산(카메라 forward 수평 투영)이 0이 되어 앞뒤 이동이 멈춘다.")]
    [SerializeField, Range(0f, 89f)] float previewPitch = 82f;

    [Tooltip("yaw는 복도 진행축(moveDirection)에서 자동 계산된다 — 화면 위쪽 = 진행 방향.\n" +
             "이 값은 거기에 더하는 보정(도). 0이 아니면 W가 복도 정면과 어긋난다.")]
    [SerializeField] float previewYawOffset = 0f;

    [SerializeField] Vector3 previewTargetOffset = Vector3.zero;

    [Header("스테이지")]
    [Tooltip("블렌드 완료 후 StartStage()를 호출할 StageManager.\n" +
             "StageStartGate의 stageManager 필드는 비워둘 것.")]
    [SerializeField] StageManager stageManager;

    [Header("이벤트")]
    [Tooltip("탑다운 블렌드 완료 시 호출. MovingCorridor.Activate() 등 게임플레이 시작을 연결.")]
    public UnityEvent OnTopDownReady;

    bool _started;

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>StageStartGate.OnCountdownComplete에 연결. 씬당 1회만 동작.</summary>
    public void BeginTopDown()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(TopDownRoutine());
    }

    /// <summary>씬 리로드 없이 되돌릴 때 수동 호출(Inspector 이벤트/ContextMenu). 멱등.</summary>
    public void ResetTopDown()
    {
        if (!_started) return;
        StopAllCoroutines();
        _started = false;
        ActiveCamera?.ExitPreviewView();
    }

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator TopDownRoutine()
    {
        var cam = ActiveCamera;
        if (cam == null || pivot == null)
        {
            Debug.LogWarning("[CorridorTopDownIntroController] 카메라 또는 pivot 미설정 — 탑다운 없이 바로 시작.", this);
            StartGameplay();
            yield break;
        }

        pivot.SetFollowTarget(cam.GameplayTarget);
        cam.EnterPreviewView(pivot.transform, previewDistance, previewPitch, ComputeYaw(), previewTargetOffset);

        yield return new WaitForSeconds(cam.PreviewBlendTime);

        StartGameplay();
    }

    void StartGameplay()
    {
        OnTopDownReady?.Invoke();
        stageManager?.StartStage();
    }

    float ComputeYaw()
    {
        Vector3 axis = pivot != null ? pivot.Axis : Vector3.zero;
        float baseYaw = axis.x * axis.x + axis.z * axis.z > 0.0001f
            ? Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg
            : 0f;
        return baseYaw + previewYawOffset;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 탑다운 시작")]
    void Debug_Begin() => BeginTopDown();

    [ContextMenu("테스트: 리셋")]
    void Debug_Reset() => ResetTopDown();

    void OnDrawGizmosSelected()
    {
        if (pivot == null) return;

        Vector3 lookAt = pivot.transform.position + previewTargetOffset;
        Quaternion rot = Quaternion.Euler(previewPitch, ComputeYaw(), 0f);
        Vector3 camPos = lookAt + rot * (Vector3.back * previewDistance);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawSphere(camPos, 0.6f);
        Gizmos.DrawLine(camPos, lookAt);

        // 씬 Main Camera는 비활성이라 Camera.main은 null — 플레이 중엔 로컬 카메라, 아니면 프리팹 FOV(60).
        Camera live = LocalPlayerCamera.Instance != null ? LocalPlayerCamera.Instance.Cam : null;
        float fov    = live != null ? live.fieldOfView : 60f;
        float aspect = live != null ? live.aspect : 16f / 9f;

        Gizmos.matrix = Matrix4x4.TRS(camPos, rot, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, fov, previewDistance * 1.5f, 0.3f, aspect);
    }
}
