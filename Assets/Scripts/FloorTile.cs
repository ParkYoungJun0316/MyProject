using UnityEngine;

public class FloorTile : MonoBehaviour
{
    public enum ColorType { Black, White, Reveal }
    public ColorType type;

    [Header("Tile Colors (Inspector)")]
    public Color blackColor = Color.black;
    public Color whiteColor = Color.white;
    public Color revealTileColor = new Color(0.12f, 0.25f, 0.18f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    MaterialPropertyBlock mpb;
    Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        ApplyColor();
    }

    public void SetType(ColorType t)
    {
        type = t;
        ApplyColor();
    }

    void ApplyColor()
    {
        if (rend == null) return;

        Color c = type switch
        {
            ColorType.Black => blackColor,
            ColorType.White => whiteColor,
            ColorType.Reveal => revealTileColor, 
            _ => whiteColor
        };

        rend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        rend.SetPropertyBlock(mpb);
    }
}