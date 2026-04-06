using UnityEngine;

public class FloorTile : MonoBehaviour
{
    public enum ColorType { Black, White, Reveal }
    public ColorType type;

    [Header("Tile Materials (Inspector에서 머티리얼 3개 연결)")]
    public Material materialBlack;
    public Material materialWhite;
    public Material materialReveal;

    Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        ApplyMaterial();
    }

    void OnValidate()
    {
        rend = GetComponent<Renderer>();
        ApplyMaterial();
    }

    public void SetType(ColorType t)
    {
        type = t;
        ApplyMaterial();
    }

    void ApplyMaterial()
    {
        if (rend == null) return;

        Material m = type switch
        {
            ColorType.Black  => materialBlack,
            ColorType.White  => materialWhite,
            ColorType.Reveal => materialReveal,
            _                => materialWhite
        };

        if (m != null)
            rend.sharedMaterial = m;
    }
}