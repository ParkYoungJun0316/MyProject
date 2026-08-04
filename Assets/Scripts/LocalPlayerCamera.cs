using UnityEngine;

/// <summary>
/// Owner 로컬 전용 카메라 래퍼. DontDestroyOnLoad 싱글턴.
///
/// [역할]
/// - Owner 플레이어 첫 스폰 시 1회 Instantiate
/// - PlayerSpawnCoordinator.OnPlayersReady 이후 target = Owner 플레이어 Transform으로 바인드
///   → destroyWithScene:true로 씬마다 플레이어가 새로 생성되므로, 씬마다 re-bind 발생
/// - TitleReturnFlow 세션 종료 시 Destroy
///
/// [씬 카메라]
/// 각 스테이지 씬 Main Camera는 비활성 유지.
/// 이 오브젝트가 유일한 활성 카메라.
///
/// [프리팹 배치]
/// Camera + ThirdPersonCamera + AudioListener + UniversalAdditionalCameraData
/// NetworkPlayerSetup._localCameraPrefab 필드에 연결.
/// </summary>
[RequireComponent(typeof(ThirdPersonCamera))]
[RequireComponent(typeof(Camera))]
public class LocalPlayerCamera : MonoBehaviour, ISessionResettable
{
    public static LocalPlayerCamera Instance { get; private set; }

    ThirdPersonCamera _thirdPersonCam;
    Camera        _camera;

    /// <summary>씬 카메라 인트로 등 외부에서 쓰는 ThirdPersonCamera 참조.</summary>
    public ThirdPersonCamera ThirdPersonCam => _thirdPersonCam;

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

        _thirdPersonCam = GetComponent<ThirdPersonCamera>();
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
        _thirdPersonCam.target = playerTransform;
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
