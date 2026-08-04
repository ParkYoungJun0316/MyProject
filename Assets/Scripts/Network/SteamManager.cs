using Steamworks;
using UnityEngine;

/// <summary>
/// Steam 클라이언트 초기화 부트스트랩 (SteamworksIntegrationDesign.md §1, §5 확정).
///
/// [설계 — 로컬 스킵]
/// 로컬 개발(①ParrelSync ②Dev Build)은 이 컴포넌트를 아예 호출하지 않는 방식으로 "스킵"을 구현한다.
/// 별도 "로컬 모드" 플래그를 두지 않음 — <see cref="NetworkManagerSetup.StartHost"/> /
/// <see cref="NetworkManagerSetup.StartClient"/>(로컬 IP 경로)는 <see cref="EnsureInitialized"/>를
/// 호출하지 않고, Steam 경로(<see cref="NetworkManagerSetup.StartHostSteam"/> /
/// <see cref="NetworkManagerSetup.StartClientSteam"/>)만 호출한다.
/// 이는 "오프라인 모드"가 아니다 — architecture.mdc 온라인 전용 락(플레이 경로)과는 별개 층위
/// (개발자 로컬 테스트 편의)이다.
///
/// [배치 방법]
/// 0.Title 씬 > NetworkManager GameObject(또는 별도 DDOL 오브젝트)에 부착.
///
/// [Inspector 설정]
/// - appId : Steam App ID (Player Settings 등록 값과 동일해야 함). 0이면 초기화 시도하지 않음.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    [Header("Steam 설정")]
    [Tooltip("Steam App ID. Player Settings의 Steam App ID와 동일해야 함. 0이면 초기화 시도하지 않음.")]
    [SerializeField] private uint appId = 0;

    public bool IsInitialized { get; private set; }

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (IsInitialized)
            SteamClient.RunCallbacks();
    }

    void OnApplicationQuit()
    {
        if (!IsInitialized) return;

        SteamClient.Shutdown();
        IsInitialized = false;
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// Steam 경로(Host/Client) 진입 직전에만 호출. 이미 초기화됐으면 즉시 true 반환(idempotent).
    /// 로컬 경로(①②)는 이 메서드를 호출하지 않는 것 자체가 "스킵" 구현이다 (§5).
    /// </summary>
    public bool EnsureInitialized()
    {
        if (IsInitialized) return true;

        if (appId == 0)
        {
            Debug.LogError("[SteamManager] appId가 설정되지 않았습니다. Inspector에서 Steam App ID를 입력하세요.");
            return false;
        }

        try
        {
            SteamClient.Init(appId, asyncCallbacks: false);
            IsInitialized = true;
            Debug.Log($"[SteamManager] Steam 초기화 완료 — SteamId {SteamClient.SteamId}, Name {SteamClient.Name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamManager] Steam 초기화 실패 — Steam 클라이언트가 실행 중인지 확인하세요. {e.Message}");
            IsInitialized = false;
        }

        return IsInitialized;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Steam 초기화")]
    void Debug_Init() => EnsureInitialized();

    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status() =>
        Debug.Log($"[SteamManager] IsInitialized={IsInitialized} appId={appId}");
#endif
}
