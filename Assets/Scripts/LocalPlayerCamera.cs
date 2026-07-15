using UnityEngine;

/// <summary>
/// Owner 로컬 전용 카메라 래퍼. DontDestroyOnLoad 싱글턴.
///
/// [역할]
/// - Owner 플레이어 첫 스폰 시 1회 Instantiate (NetworkPlayerSetup.SetupOwner)
/// - target = Owner 플레이어 Transform
///   → destroyWithScene:false 유지라 씬 전환·사망 리셋 후에도 target이 끊기지 않음
/// - TitleReturnFlow 세션 종료 시 Destroy
///
/// [씬 카메라]
/// 각 스테이지 씬 Main Camera는 비활성 유지.
/// 이 오브젝트가 유일한 활성 카메라.
///
/// [프리팹 배치]
/// Camera + TopDownCamera + AudioListener + UniversalAdditionalCameraData
/// NetworkPlayerSetup._localCameraPrefab 필드에 연결.
/// </summary>
[RequireComponent(typeof(TopDownCamera))]
[RequireComponent(typeof(Camera))]
public class LocalPlayerCamera : MonoBehaviour, ISessionResettable
{
    public static LocalPlayerCamera Instance { get; private set; }

    TopDownCamera _topDownCam;
    Camera        _camera;

    /// <summary>씬 카메라 인트로 등 외부에서 쓰는 TopDownCamera 참조.</summary>
    public TopDownCamera TopDownCam => _topDownCam;

    /// <summary>Player.followCamera 용 Camera 참조.</summary>
    public Camera Cam => _camera;

    // ── 초기화 ──────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _topDownCam = GetComponent<TopDownCamera>();
        _camera     = GetComponent<Camera>();

        TitleReturnFlow.Instance?.Register(this);
    }

    void OnDestroy()
    {
        TitleReturnFlow.Instance?.Unregister(this);
        if (Instance == this) Instance = null;
    }

    // ── 공개 API ────────────────────────────────────────────────

    /// <summary>
    /// Owner 플레이어 Transform을 follow 타겟으로 설정.
    /// Player.followCamera도 이 카메라로 연결.
    /// </summary>
    public void SetTarget(Transform playerTransform, Player player = null)
    {
        _topDownCam.target = playerTransform;
        if (player != null)
            player.followCamera = _camera;

        Debug.Log($"[LocalPlayerCamera] target 설정 완료 — {playerTransform.name}");
    }

    /// <summary>
    /// Owner 스폰 시 호출. Instance가 없으면 prefab을 Instantiate 후 SetTarget.
    /// 이미 존재하면 SetTarget만 재호출(안전 방어).
    /// </summary>
    public static void EnsureForOwner(LocalPlayerCamera prefab, Transform playerTransform, Player player)
    {
        if (Instance != null)
        {
            Instance.SetTarget(playerTransform, player);
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("[LocalPlayerCamera] prefab 미설정 — NetworkPlayerSetup._localCameraPrefab 확인");
            return;
        }

        var cam = Instantiate(prefab);
        cam.SetTarget(playerTransform, player);
    }

    // ── ISessionResettable ──────────────────────────────────────

    public void OnSessionReset(TitleReturnScope scope)
    {
        Debug.Log("[LocalPlayerCamera] 세션 리셋 — 카메라 파괴");
        Destroy(gameObject);
    }
}
