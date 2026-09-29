using UnityEngine;

/// <summary>
/// 젤리 문 연출. 열릴 때 젤리가 펑 터지며(파티클) 사라지고, 닫힐 때 바닥에서 솟아오른다.
///
/// [동작]
///  - 이동·충돌·닫힘 넉백은 전부 DoorController 그대로다. 이 컴포넌트는 보이는 것만 바꾼다.
///  - 열림: OnOpened 시점에 젤리 크기·색으로 파티클을 터뜨리고 렌더러를 숨긴다.
///    문은 숨은 채로 DoorController가 바닥 아래로 내린다(openMode = SlideDown).
///  - 닫힘: IsOpen이 false가 되는 순간(Close 시작) 렌더러를 다시 켠다 → 바닥에서 솟아오르는 모습.
///  - 렌더러 표시는 매 프레임 IsOpen에서 파생한다 — ResetDoorState처럼 이벤트 없이 닫히는 경로도 따라간다.
///  - 소리: 터질 때 Jelly_Pop 1회(2D, 기존 문 소리와 같은 방식). 솟아오를 때는 무음.
///    그래서 젤리 문의 OnOpened/OnClosed에는 SFXEventPlayer(Door_Open/Close)를 연결하지 않는다.
///  - 막는 콜라이더(트리거 아닌 것)도 열려 있는 동안 끈다. 안 그러면 젤리가 사라진 뒤에도
///    DoorController가 duration 동안 내리는 사이 보이지 않는 벽에 막힌다.
///
/// [네트워크]
///  Open()/Close()가 이미 모든 머신에서 호출된다(StagePressurePadSetup 문 슬롯 동기화).
///  파티클은 각 머신 로컬 연출 — RPC 없음.
///
/// [씬 설정]
///  1. DoorController와 같은 GameObject에 추가
///  2. DoorController: openMode = SlideDown, openAmount ≥ 젤리 높이(바닥 아래로 완전히 숨김)
///     젤리 높이의 아래 25%는 바닥 아래에 묻어 둔다 — Box 메시의 둥근 아랫단이 바닥 위로 나오면
///     옆면이 바닥 근처에서 몇 m씩 안으로 파여 틈이 생긴다.
///  3. popPrefab 에 Assets/Art/Jelly/JellyPop 연결
///  4. jellyRenderer 는 비우면 자손의 첫 Renderer
/// </summary>
[RequireComponent(typeof(DoorController))]
public class JellyPopDoor : MonoBehaviour
{
    [Tooltip("터질 때 스폰할 파티클. 방출 모양이 있는 층은 젤리 부피로, 모양이 없는 층은 시작 크기 × 젤리 최대 변으로,\n" +
             "색은 시작 색 × 젤리 _Color로 맞춰진다.")]
    [SerializeField] ParticleSystem popPrefab;

    [Tooltip("숨기고 보일 젤리 렌더러. 비우면 자손의 첫 Renderer.")]
    [SerializeField] Renderer jellyRenderer;

    static readonly int ColorId = Shader.PropertyToID("_Color");

    // NoAI/Box.fbx 메시의 아래쪽 둥근 띠 비율. 바닥에 선 젤리는 이만큼 바닥 아래에 묻혀 있다.
    const float BuriedFraction = 0.25f;

    DoorController _door;
    Collider[]     _blockers;

    void Awake()
    {
        _door = GetComponent<DoorController>();
        if (jellyRenderer == null) jellyRenderer = GetComponentInChildren<Renderer>();

        var solid = new System.Collections.Generic.List<Collider>();
        foreach (Collider c in GetComponents<Collider>())
            if (!c.isTrigger) solid.Add(c);
        _blockers = solid.ToArray();
    }

    void OnEnable()  => _door.OnOpened.AddListener(Pop);
    void OnDisable() => _door.OnOpened.RemoveListener(Pop);

    void LateUpdate()
    {
        bool closed = !_door.IsOpen;
        if (jellyRenderer != null) jellyRenderer.enabled = closed;
        foreach (Collider c in _blockers) c.enabled = closed;
    }

    void Pop()
    {
        if (popPrefab == null || jellyRenderer == null || !jellyRenderer.enabled) return;

        // 젤리 Box는 아래 25%가 둥글어서 그만큼 바닥에 묻어 둔다 — 파티클은 보이는 위 75%에서만.
        Bounds full = jellyRenderer.bounds;
        float buried = full.size.y * BuriedFraction;
        Bounds b = new Bounds(full.center + Vector3.up * (buried / 2f),
                              new Vector3(full.size.x, full.size.y - buried, full.size.z));
        Material mat = jellyRenderer.sharedMaterial;
        Color c = mat != null && mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;

        ParticleSystem fx = Instantiate(popPrefab, b.center, Quaternion.identity);
        float maxLife = 0f;
        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;

            // 방출 모양이 있는 층(덩어리·방울)은 젤리 부피에서 나오고,
            // 모양이 없는 층(한 장짜리 물보라)은 크기를 젤리에 대한 배율로 본다.
            var shape = ps.shape;
            if (shape.enabled) shape.scale = b.size;
            else main.startSizeMultiplier *= largest;

            Color tint = main.startColor.color;
            main.startColor = new Color(tint.r * c.r, tint.g * c.g, tint.b * c.b, tint.a);

            maxLife = Mathf.Max(maxLife, main.duration + main.startLifetime.constantMax);
        }
        fx.Play(true);
        Destroy(fx.gameObject, maxLife + 0.5f);

        // 터질 때만 소리 — 다시 솟아오를 때(닫힘)는 무음. 문 소리(Door_Open/Close)는 이 문에 연결하지 않는다.
        SFXManager.Instance?.Play(SFXId.Jelly_Pop);

        jellyRenderer.enabled = false;
    }
}
