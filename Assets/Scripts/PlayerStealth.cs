using UnityEngine;

public class PlayerStealth : MonoBehaviour
{
    public LayerMask groundMask;
    public float groundCheckDistance = 1.5f;
    public float rayOriginUp = 0.5f;

    [Header("Alpha Control")]
    [Tooltip("은신 시 몸통 알파 하한 (1에 가까우면 거의 안 흐림)")]
    public float stealthMinAlpha = 0.45f;

    [Header("피격 노출")]
    [Tooltip("피격 후 고유색으로 노출되는 지속 시간(초). 0이면 비활성")]
    public float stealthRevealDuration = 0f;

    [Header("피격 노출 - 펄스 효과")]
    [Tooltip("노출 중 고유색과 번갈아 섞이는 펄스 색. 기본: 빨강")]
    public Color revealPulseColor = Color.red;
    [Tooltip("펄스 진동 횟수(초당). 0이면 고유색 고정")]
    public float revealPulseFrequency = 0f;
    [Tooltip("펄스 강도. 0=고유색 유지, 1=revealPulseColor로 완전 전환")]
    public float revealPulseIntensity = 0f;

    public bool isStealth;
    [HideInInspector] public FloorTile.ColorType groundType;

    Player player;
    PlayerVisualController playerVisualController;
    MaterialPropertyBlock mpb;
    int layerPlayer;
    int layerPlayerStealth;

    Renderer[] tintTargets;

    bool isDead_prev = false;
    bool prevStealth = false;
    bool visualsDirty = true;

    float stealthRevealTimer = 0f;
    bool prevRevealed = false;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        player                 = GetComponent<Player>();
        playerVisualController = GetComponent<PlayerVisualController>();
        mpb                    = new MaterialPropertyBlock();

        layerPlayer        = LayerMask.NameToLayer("Player");
        layerPlayerStealth = LayerMask.NameToLayer("PlayerStealth");

        if (groundMask.value == 0)
            groundMask = LayerMask.GetMask("Ground");
    }

    void EnsureTintTargets()
    {
        if (tintTargets != null) return;
        if (playerVisualController == null)
            playerVisualController = GetComponent<PlayerVisualController>();
        if (playerVisualController != null)
            tintTargets = playerVisualController.BodyTintRenderers;
    }

    public void ForceLayer(int layer)
    {
        isStealth = false;
        stealthRevealTimer = 0f;
        EnsureTintTargets();
        UpdateVisuals(0f, false);
        SetLayerRecursively(gameObject, layer);
    }

    /// <summary>
    /// 스텔스 상태 중 피격 시 호출. stealthRevealDuration 동안 강제로 완전히 보이게 함.
    /// </summary>
    public void RevealTemporarily()
    {
        if (stealthRevealDuration <= 0f) return;
        stealthRevealTimer = stealthRevealDuration;
        visualsDirty = true;
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
                visualsDirty = true;
                SetLayerRecursively(gameObject, layerPlayer);
            }
        }

        if (player.IsDead)
        {
            isStealth = false;
            stealthRevealTimer = 0f;
            EnsureTintTargets();
            UpdateVisuals(0f, false);
            return;
        }

        if (stealthRevealTimer > 0f)
        {
            stealthRevealTimer -= Time.deltaTime;
            if (stealthRevealTimer <= 0f)
            {
                stealthRevealTimer = 0f;
                visualsDirty = true;
            }
        }

        bool isRevealed = stealthRevealTimer > 0f;
        if (isRevealed != prevRevealed)
        {
            prevRevealed = isRevealed;
            visualsDirty = true;
        }

        if (player.isUniqueColor)
        {
            isStealth = false;
            EnsureTintTargets();
            UpdateVisuals(0f, isRevealed);
            if (gameObject.layer != layerPlayer)
                SetLayerRecursively(gameObject, layerPlayer);
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

        EnsureTintTargets();
        UpdateVisuals(isStealth ? 1f : 0f, isRevealed);

        int desired = (isStealth && !isRevealed) ? layerPlayerStealth : layerPlayer;
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

    void UpdateVisuals(float t, bool forReveal)
    {
        bool isFlashing = playerVisualController != null && playerVisualController.IsFlashing;

        if (forReveal)
        {
            if (isFlashing) return;

            float pulse = Mathf.Abs(Mathf.Sin(Time.time * revealPulseFrequency * Mathf.PI));
            Color c = Color.Lerp(player.uniqueColor, revealPulseColor, pulse * revealPulseIntensity);
            c.a = 1f;
            ApplyColor(c);
            return;
        }

        bool stateChanged = isStealth != prevStealth || visualsDirty;
        if (!stateChanged) return;

        prevStealth = isStealth;
        visualsDirty = false;

        if (isFlashing)
        {
            visualsDirty = true;
            return;
        }

        float a = Mathf.Lerp(1f, stealthMinAlpha, t);
        ApplyAlpha(a);
    }

    void ApplyAlpha(float a)
    {
        if (tintTargets == null || player == null) return;
        for (int i = 0; i < tintTargets.Length; i++)
        {
            var r = tintTargets[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            Color c = player.GetCurrentBaseColor();
            c.a = a;
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    void ApplyColor(Color c)
    {
        if (tintTargets == null) return;
        for (int i = 0; i < tintTargets.Length; i++)
        {
            var r = tintTargets[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
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
