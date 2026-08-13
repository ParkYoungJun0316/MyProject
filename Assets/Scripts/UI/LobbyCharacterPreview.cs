using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로비 슬롯 1개의 3D 캐릭터 프리뷰 받침대.
/// 오프스크린 프리뷰 리그에 슬롯당 1개씩 배치 — 전용 Camera(자식)가 RenderTexture로 렌더링하고,
/// 해당 RenderTexture는 씬에서 LobbySlotUI의 RawImage에 직접 연결한다(코드로 텍스처를 갈아 끼우지 않음).
///
/// [Inspector]
/// - modelPrefab     : 프리뷰로 스폰할 캐릭터 프리팹. Player + PlayerVisualController 포함 필수 (Player1 권장).
/// - modelSpawnRoot  : 모델을 자식으로 붙일 Transform. 비우면 이 오브젝트 자신을 사용.
/// - previewLayerName: 인스턴스에 적용할 Layer 이름. 프리뷰 카메라의 Culling Mask를 이 레이어만 체크해서
///                      다른 카메라(메인 카메라 등)에 노출되지 않게 분리하는 용도. 기본값 "Default".
///
/// 프리뷰 인스턴스는 입력(PlayerInput)·물리(Rigidbody)·충돌(Collider)·게임 로직(Player)을 전부 비활성화하고
/// 색 표시(PlayerVisualController MPB 틴트)만 사용하는 순수 비주얼 오브젝트다. 네트워크와 무관.
/// </summary>
public class LobbyCharacterPreview : MonoBehaviour
{
    [Header("모델")]
    [Tooltip("프리뷰로 스폰할 캐릭터 프리팹. Player + PlayerVisualController 포함 필수 (Player1 권장).")]
    [SerializeField] private GameObject modelPrefab;

    [Tooltip("모델을 자식으로 붙일 Transform. 비우면 이 오브젝트 자신을 사용.")]
    [SerializeField] private Transform modelSpawnRoot;

    [Header("렌더 분리")]
    [Tooltip("프리뷰 모델 인스턴스에 적용할 Layer 이름. 프리뷰 카메라 Culling Mask와 짝을 맞출 것. " +
             "커스텀 레이어를 아직 안 만들었으면 기본값 Default로 둬도 동작함.")]
    [SerializeField] private string previewLayerName = "Default";

    Player _player;
    GameObject _instance;
    int _pendingColorIndex = -1;

    void Awake()
    {
        SpawnModel();
    }

    void SpawnModel()
    {
        if (modelPrefab == null || _instance != null) return;

        Transform root = modelSpawnRoot != null ? modelSpawnRoot : transform;
        _instance = Instantiate(modelPrefab, root);
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation = Quaternion.identity;

        // 프리뷰 클론은 네트워크에 관여하지 않음. Player1에 NetworkObject가 붙어 있어서 그대로 두면
        // 로비 씬이 NGO NetworkSceneManager.LoadScene으로 로드될 때 PopulateScenePlacedObjects가
        // 같은 프리팹에서 나온 여러 클론을 동일 GlobalObjectIdHash로 등록하려다 예외를 던지고,
        // 그 여파로 LobbyNetworkManager의 씬 배치 스폰까지 깨진다(호스트 슬롯 미등록 → 연결 종료).
        // NetworkSceneManager의 스캔(씬 로드 AsyncOperation 완료 콜백)보다 반드시 먼저 제거해야 하므로
        // 지연되는 Destroy가 아니라 DestroyImmediate를 쓴다. NfgoPlayer 등 [RequireComponent(NetworkObject)]
        // 의존 컴포넌트가 있으면 NetworkObject를 먼저 못 지우므로, 그런 의존 컴포넌트부터 제거한다.
        RemoveNetworkObjectAndDependents(_instance);

        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0) SetLayerRecursively(_instance, layer);

        // 프리뷰는 순수 비주얼 — 입력·물리·충돌·게임 로직 전부 비활성화 (인스턴스 여러 개가 같은 입력 장치에
        // 동시 반응하는 것을 방지). 색 적용은 PlayerColorUtil이 GetComponent로 직접 접근하므로
        // 비활성 상태에서도 정상 동작한다.
        var playerInput = _instance.GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        var rb = _instance.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        foreach (var col in _instance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (var ps in _instance.GetComponentsInChildren<ParticleSystem>(true))
            ps.gameObject.SetActive(false);

        // PlayerStealth.Update()가 매 프레임 레이어를 Player로 되돌려서(고유색 모드 분기)
        // 위에서 적용한 LobbyPreview 레이어를 덮어써버린다 — 프리뷰 카메라 컬링마스크에서
        // 캐릭터가 사라지는 원인이었음. 순수 비주얼 클론이므로 꺼도 안전.
        var stealth = _instance.GetComponent<PlayerStealth>();
        if (stealth != null) stealth.enabled = false;

        _player = _instance.GetComponent<Player>();

        if (_pendingColorIndex >= 0) ApplyColor(_pendingColorIndex);

        if (_player != null) _player.enabled = false;
    }

    /// <summary>
    /// NetworkObject를 지우기 전에 [RequireComponent(typeof(NetworkObject))]로 의존하는 컴포넌트
    /// (예: Dissonance NfgoPlayer)부터 지운다. 안 그러면 Unity가 제거를 거부한다.
    /// </summary>
    static void RemoveNetworkObjectAndDependents(GameObject go)
    {
        if (go.GetComponent<NetworkObject>() == null) return;

        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null) continue;

            foreach (var attr in behaviour.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                var req = (RequireComponent)attr;
                if (req.m_Type0 == typeof(NetworkObject) ||
                    req.m_Type1 == typeof(NetworkObject) ||
                    req.m_Type2 == typeof(NetworkObject))
                {
                    DestroyImmediate(behaviour);
                    break;
                }
            }
        }

        var networkObject = go.GetComponent<NetworkObject>();
        if (networkObject != null) DestroyImmediate(networkObject);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    /// <summary>ColorIndex(0~3) → PlayerColorType 적용. 모델 스폰 전 호출되면 스폰 후 자동 반영.</summary>
    public void SetColorIndex(int colorIndex)
    {
        _pendingColorIndex = colorIndex;
        if (_instance != null) ApplyColor(colorIndex);
    }

    void ApplyColor(int colorIndex)
    {
        if (_player == null) return;
        if (colorIndex < 0 || colorIndex >= LobbyNetworkManager.ColorOrder.Length) return;

        _player.isUniqueColor = true; // 프리뷰는 항상 고유색 표시 (흑백 스위치 모드 무시)
        PlayerColorUtil.ApplyToPlayer(_player, LobbyNetworkManager.ColorOrder[colorIndex]);
    }

    /// <summary>빈 슬롯 시 받침대(모델+카메라) 통째로 숨김. 다시 true로 켜면 렌더 재개.</summary>
    public void SetActive(bool active)
    {
        if (gameObject.activeSelf != active) gameObject.SetActive(active);
    }
}
