using System.Collections.Generic;
using UnityEngine;

public class PlayerVisualController : MonoBehaviour
{
    [Header("Refs")]
    public Player player;
    public PlayerEvents events;

    [Header("Body Root (추천)")]
    public Transform bodyRoot; // 본체 루트(Visual_World 같은 것)

    [Header("Fixed Renderers (눈·코·입 등 색 변환 제외)")]
    [Tooltip("색 변환에서 제외할 렌더러. 항상 자신의 머터리얼 색을 유지함.")]
    public Renderer[] fixedRenderers;

    [Header("Damage Flash")]
    public float damageFlashTime = 0.15f;
    public Color damageColor = Color.red;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Renderer[] bodyRenderers;
    MaterialPropertyBlock mpb;
    bool flashing;
    float flashUntil;

    public bool IsFlashing => flashing;

    /// <summary>흑백·스텔스 알파에 쓰는 몸통 렌더러(고정 파트 제외). Awake 이후 유효.</summary>
    public Renderer[] BodyTintRenderers => bodyRenderers;

    void Awake()
    {
        if (player == null) player = GetComponent<Player>();
        if (events == null) events = GetComponent<PlayerEvents>();
        mpb = new MaterialPropertyBlock();

        CollectBodyRenderers();
    }

    void Start()
    {
        RefreshColor();
    }

    void OnEnable()
    {
        if (events == null) return;
        events.OnBlackWhiteChanged  += OnBlackWhiteChangedHandler;
        events.OnUniqueColorChanged += OnUniqueColorChangedHandler;
        events.OnDamaged            += FlashDamage;
        events.OnRespawned          += OnRespawned;
    }

    void OnDisable()
    {
        if (events == null) return;
        events.OnBlackWhiteChanged  -= OnBlackWhiteChangedHandler;
        events.OnUniqueColorChanged -= OnUniqueColorChangedHandler;
        events.OnDamaged            -= FlashDamage;
        events.OnRespawned          -= OnRespawned;
    }

    void OnBlackWhiteChangedHandler(bool _)  => RefreshColor();
    void OnUniqueColorChangedHandler(int _)  => RefreshColor();

    void Update()
    {
        if (!flashing) return;

        if (Time.time >= flashUntil)
        {
            flashing = false;
            RefreshColor();
        }
    }

    void CollectBodyRenderers()
    {
        var fixedSet = new HashSet<Renderer>();
        if (fixedRenderers != null)
            for (int i = 0; i < fixedRenderers.Length; i++)
                if (fixedRenderers[i] != null) fixedSet.Add(fixedRenderers[i]);

        Renderer[] all = bodyRoot != null
            ? bodyRoot.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        var list = new List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null) continue;
            if (fixedSet.Contains(r)) continue;
            list.Add(r);
        }

        bodyRenderers = list.ToArray();
    }

    public void RefreshColor()
    {
        if (player == null) return;
        SetColor(player.GetCurrentBaseColor());
    }

    void FlashDamage()
    {
        if (player != null && player.IsDead) return;

        flashing = true;
        flashUntil = Time.time + damageFlashTime;
        SetColor(damageColor);
    }

    void OnRespawned()
    {
        RefreshColor();
    }

    void SetColor(Color c)
    {
        if (bodyRenderers == null) return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            var r = bodyRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}