#if UNITY_EDITOR
using System.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 전용 개발자 도구.
/// Title → Tutorial 정식 흐름을 거치지 않고 스테이지 씬(M.Stage1 등)을 바로 Play해서
/// ArrowTrap / DropTrap 등의 ServerTime 기반 발사 스케줄을 빠르게 반복 테스트하기 위한
/// 로컬 Host 자동 부트스트랩.
///
/// [배치 방법]
/// 테스트하려는 씬에 빈 GameObject를 하나 만들고 이 컴포넌트만 부착하면 끝.
/// Inspector에 연결할 필드 없음 — NetworkManager 프리팹을 경로로 직접 로드해서 즉석 생성한다.
///
/// [동작]
/// - NetworkManager.Singleton이 이미 있으면(Title에서 정상 StartHost/StartClient로 들어온 경우,
///   또는 이미 이 컴포넌트가 한 번 부트스트랩한 이후) 아무것도 하지 않고 자기 자신만 제거한다.
/// - 없으면(씬을 곧바로 Play한 경우) Assets/Prefab/NetworkManager.prefab을 즉석 Instantiate해서
///   로컬 Host로 시작한다(포트/승인 로직은 NetworkManagerSetup.StartHost와 완전히 동일).
///
/// [버그 수정] Awake()에서 곧바로 StartHost()를 호출하면 NGO의 ILPP(컴파일 시 생성되는 네트워크
/// 메시지 테이블)가 아직 준비되기 전에 NetworkManager.Initialize()가 실행돼
/// "Allowed types is not equal to the number of message type indices!" 예외가 난다
/// (NGO 공식 포럼에서 확인된 동일 증상 — Awake처럼 아주 이른 시점에 StartHost를 부르면 재현됨).
/// Start()에서 최소 1프레임 대기 후 시작하면 재현되지 않는다.
/// 대가로, initialDelay=0인 트랩의 첫 발동 1회만 Host가 아직 없어 ServerTime 대신 Time.time
/// 기준으로 앵커링될 수 있다(ArrowTrap/DropTrap의 기존 폴백 경로) — 테스트 도구 한정 허용 가능한
/// 수준의 오차이며 두 번째 발동부터는 정상화된다.
///
/// [안전장치]
/// - 전체가 #if UNITY_EDITOR로 감싸여 있어 빌드에는 절대 포함되지 않는다.
/// - 정식 배포 흐름(Title → StartHost → Tutorial → 스테이지)에는 영향 없음 —
///   그 경로에서는 스테이지 씬 로드 시점에 이미 NetworkManager.Singleton이 존재하므로
///   이 컴포넌트는 즉시 자기 제거만 하고 끝난다.
/// </summary>
public class DevStageHostBootstrap : MonoBehaviour
{
    const string NetworkManagerPrefabPath = "Assets/Prefab/NetworkManager.prefab";

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[DevStageHostBootstrap] NetworkManager가 이미 존재함 — 정상 경로로 들어온 것으로 판단, 부트스트랩 생략.");
            Destroy(gameObject);
            return;
        }

        StartCoroutine(BootstrapAfterOneFrame());
    }

    IEnumerator BootstrapAfterOneFrame()
    {
        // NGO ILPP 메시지 테이블 준비 대기 — 위 [버그 수정] 주석 참조.
        yield return null;

        if (NetworkManager.Singleton != null)
        {
            Destroy(gameObject);
            yield break;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[DevStageHostBootstrap] NetworkManager 프리팹을 찾을 수 없습니다: {NetworkManagerPrefabPath}");
            yield break;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "NetworkManager (Dev Bootstrap)";

        NetworkManagerSetup setup = instance.GetComponent<NetworkManagerSetup>();
        if (setup == null)
        {
            Debug.LogError("[DevStageHostBootstrap] 생성된 프리팹에 NetworkManagerSetup 컴포넌트가 없습니다.");
            yield break;
        }

        bool ok = setup.StartHost(LanDiscovery.GenerateRoomCode());
        Debug.Log(ok
            ? "[DevStageHostBootstrap] 로컬 테스트 Host 시작 완료 — 트랩 ServerTime 스케줄 정상 동작 예정."
            : "[DevStageHostBootstrap] StartHost 실패 — 포트 충돌 등 콘솔 로그 확인.");
    }
}
#endif
