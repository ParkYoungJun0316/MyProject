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
    public FloorTile.ColorType groundType;

    Player player;
    PlayerVisualController playerVisualController;
    MaterialPropertyBlock mpb;
    int layerPlayer;
    int layerPlayerStealth;
    int layerPlayerDead;

    bool isDead_prev = false;
    bool prevStealth = false;
    bool visualsDirty = true; // 초기 1회 강제 적용

    float stealthRevealTimer = 0f;
    bool prevRevealed = false;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        player                = GetComponent<Player>();
        playerVisualController = GetComponent<PlayerVisualController>();
        mpb                   = new MaterialPropertyBlock();

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
        stealthRevealTimer = 0f;
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
                visualsDirty = true; // 리스폰 시 강제 갱신
                SetLayerRecursively(gameObject, layerPlayer);
            }
        }

        if (player.IsDead)
        {
            isStealth = false;
            stealthRevealTimer = 0f;
            UpdateVisuals(0f, false);
            return;
        }

        // 피격 reveal 타이머 카운트다운
        // ※ 고유색 모드 여부와 관계없이 먼저 처리 (고유색 상태 피격도 펄스 효과 적용)
        if (stealthRevealTimer > 0f)
        {
            stealthRevealTimer -= Time.deltaTime;
            if (stealthRevealTimer <= 0f)
            {
                stealthRevealTimer = 0f;
                visualsDirty = true; // 타이머 종료 시 원래 색 재적용
            }
        }

        bool isRevealed = stealthRevealTimer > 0f;
        if (isRevealed != prevRevealed)
        {
            prevRevealed = isRevealed;
            visualsDirty = true;
        }

        // 고유색 모드: 은신 불가, 레이어 Player 고정
        // (reveal 타이머는 위에서 처리했으므로 고유색 피격 펄스도 정상 작동)
        if (player.isUniqueColor)
        {
            isStealth = false;
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

    /// <param name="t">스텔스 블렌드 비율 (0=완전 노출, 1=완전 은신). forReveal=true일 때는 무시됨</param>
    /// <param name="forReveal">true이면 고유색 펄스 효과 적용 (피격 노출 모드)</param>
    void UpdateVisuals(float t, bool forReveal)
    {
        bool isFlashing = playerVisualController != null && playerVisualController.IsFlashing;

        if (forReveal)
        {
            // 빨강 플래시 중에는 MPB 쓰기 생략 (플래시 우선)
            if (isFlashing) return;

            // 매 프레임 펄스 애니메이션: 고유색 ↔ revealPulseColor
            float pulse = Mathf.Abs(Mathf.Sin(Time.time * revealPulseFrequency * Mathf.PI));
            Color c = Color.Lerp(player.uniqueColor, revealPulseColor, pulse * revealPulseIntensity);
            c.a = 1f;
            ApplyColor(localRenderers, c);
            ApplyColor(worldRenderers, c);
            return;
        }

        // 스텔스 상태 변화가 없으면 MPB 재적용 생략
        // → PlayerVisualController의 피격 플래시(MPB)를 덮어쓰지 않음
        bool stateChanged = isStealth != prevStealth || visualsDirty;
        if (!stateChanged) return;

        prevStealth = isStealth;
        visualsDirty = false;

        // 피격 플래시 진행 중이면 MPB 덮어쓰기 생략
        // 플래시 종료 후 재적용되도록 visualsDirty 유지
        if (isFlashing)
        {
            visualsDirty = true;
            return;
        }

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
            Color c = player.GetCurrentBaseColor();
            c.a = a;
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }

    void ApplyColor(Renderer[] renderers, Color c)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
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