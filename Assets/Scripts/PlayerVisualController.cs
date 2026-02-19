using UnityEngine;
using System.Collections.Generic;

public class PlayerVisualController : MonoBehaviour
{
    [Header("Refs")]
    public Player player;
    public PlayerEvents events;

    [Header("Body Root (추천)")]
    public Transform bodyRoot; // 본체 루트(Visual_World 같은 것)

    [Header("Exclude Local FX Layer")]
    public string localFxLayerName = "PlayerLocalFX";

    [Header("Damage Flash")]
    public float damageFlashTime = 0.15f;
    public Color damageColor = Color.red;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Renderer[] bodyRenderers;
    MaterialPropertyBlock mpb;
    bool flashing;
    float flashUntil;

    void Awake()
    {
        if (player == null) player = GetComponent<Player>();
        if (events == null) events = GetComponent<PlayerEvents>();
        mpb = new MaterialPropertyBlock();

        CollectBodyRenderers();
    }

    void OnEnable()
    {
        if (events == null) return;
        events.OnBlackWhiteChanged += ApplyBlackWhite;
        events.OnDamaged += FlashDamage;
        events.OnRespawned += OnRespawned;
    }

    void OnDisable()
    {
        if (events == null) return;
        events.OnBlackWhiteChanged -= ApplyBlackWhite;
        events.OnDamaged -= FlashDamage;
        events.OnRespawned -= OnRespawned;
    }

    void Update()
    {
        if (!flashing) return;

        if (Time.time >= flashUntil)
        {
            flashing = false;
            ApplyBlackWhite(player != null && player.isBlack);
        }
    }

    void CollectBodyRenderers()
    {
        int fxLayer = LayerMask.NameToLayer(localFxLayerName);

        Renderer[] all = bodyRoot != null
            ? bodyRoot.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        var list = new List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null) continue;

            if (fxLayer != -1 && r.gameObject.layer == fxLayer) continue;
            list.Add(r);
        }

        bodyRenderers = list.ToArray();
    }

    public void ApplyBlackWhite(bool isBlack)
    {
        SetColor(isBlack ? Color.black : Color.white);
    }

    void FlashDamage(bool isBossAtk)
    {
        if (player != null && player.IsDead) return;

        flashing = true;
        flashUntil = Time.time + damageFlashTime;
        SetColor(damageColor);
    }

    void OnRespawned()
    {
        ApplyBlackWhite(player != null && player.isBlack);
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