using UnityEngine;

public class PlayerStealth : MonoBehaviour
{
    public Renderer[] localRenderers;
    public Renderer[] worldRenderers;

    public LayerMask groundMask;
    public float groundCheckDistance = 1.5f;
    public float rayOriginUp = 0.5f;

    [Header("Alpha Control")]
    public float localMinAlpha = 0.45f;
    public float worldMinAlpha = 0.00f;

    public bool isStealth;
    public FloorTile.ColorType groundType;

    Player player;
    MaterialPropertyBlock mpb;
    int layerPlayer;
    int layerPlayerStealth;
    int layerPlayerDead;

    bool isDead_prev = false;
    bool prevStealth = false;
    bool visualsDirty = true; // 초기 1회 강제 적용

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        player = GetComponent<Player>();
        mpb    = new MaterialPropertyBlock();

        layerPlayer        = LayerMask.NameToLayer("Player");
        layerPlayerStealth = LayerMask.NameToLayer("PlayerStealth");
        layerPlayerDead    = LayerMask.NameToLayer("PlayerDead");

        // 인스펙터에서 따로 지정하지 않았다면,
        // 플레이어 아래 모든 Renderer를 자동으로 수집해서 전체에 적용
        if (localRenderers == null || localRenderers.Length == 0)
            localRenderers = GetComponentsInChildren<Renderer>(true);

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    public void ForceLayer(int layer)
    {
        isStealth = false;
        UpdateVisuals(0f);
        SetLayerRecursively(gameObject, layer);
    }

    void Update()
    {
        if (player == null) return;

        if (player.IsDead != isDead_prev)
        {
            isDead_prev = player.IsDead;
            if (!player.IsDead)
            {
                isStealth = false;
                visualsDirty = true; // 리스폰 시 강제 갱신
                SetLayerRecursively(gameObject, layerPlayer);
            }
        }

        if (player.IsDead)
        {
            isStealth = false;
            UpdateVisuals(0f);
            return;
        }

        bool hasTile = SampleGroundType(out groundType);

        bool matched = false;
        if (hasTile)
        {
            if (player.isBlack  && groundType == FloorTile.ColorType.Black) matched = true;
            else if (!player.isBlack && groundType == FloorTile.ColorType.White) matched = true;
        }

        isStealth = matched;
        UpdateVisuals(isStealth ? 1f : 0f);

        int desired = isStealth ? layerPlayerStealth : layerPlayer;
        if (gameObject.layer != desired)
            SetLayerRecursively(gameObject, desired);
    }

    bool SampleGroundType(out FloorTile.ColorType type)
    {
        type = default;
        Vector3 origin = transform.position + Vector3.up * rayOriginUp;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundCheckDistance, groundMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var tile = hit.collider.GetComponent<FloorTile>()
                       ?? hit.collider.GetComponentInParent<FloorTile>()
                       ?? hit.collider.GetComponentInChildren<FloorTile>();

            if (tile == null) continue;

            type = tile.type;
            return true;
        }
        return false;
    }

    void UpdateVisuals(float t)
    {
        // 스텔스 상태 변화가 없으면 MPB 재적용 생략
        // → PlayerVisualController의 피격 플래시(MPB)를 덮어쓰지 않음
        bool stateChanged = isStealth != prevStealth || visualsDirty;
        if (!stateChanged) return;

        prevStealth = isStealth;
        visualsDirty = false;

        ApplyAlpha(localRenderers, Mathf.Lerp(1f, localMinAlpha, t));
        ApplyAlpha(worldRenderers, Mathf.Lerp(1f, worldMinAlpha, t));
    }

    void ApplyAlpha(Renderer[] renderers, float a)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            // 항상 흑/백 컬러를 덮어써서, 일부 파츠만 색이 바뀌는 문제 방지
            Color c = player.isBlack ? Color.black : Color.white;
            c.a = a;
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}