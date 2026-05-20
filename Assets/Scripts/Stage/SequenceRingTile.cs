using UnityEngine;

/// <summary>
/// 순서 링 미니게임 바깥 링(16칸) 타일 하나.
/// SequenceRingMinigame이 Base Color로 색을 갱신합니다.
/// </summary>
public class SequenceRingTile : MonoBehaviour
{
    [Header("링 순서")]
    [Tooltip("0 = 1번 칸(좌상단 등 씬 배치 기준). 시계 방향으로 1~15")]
    [SerializeField] int ringIndex = 0;

    public int RingIndex => ringIndex;

    Material[] _mats;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        CacheMaterials();
    }

    void CacheMaterials()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;
    }

    public void ApplyColor(Color color)
    {
        if (_mats == null || _mats.Length == 0)
            CacheMaterials();

        if (_mats == null) return;

        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            if (_mats[i].HasProperty(BaseColorId)) _mats[i].SetColor(BaseColorId, color);
            else if (_mats[i].HasProperty(ColorId)) _mats[i].SetColor(ColorId, color);
        }
    }

    void OnDestroy()
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.02f);
    }
}
