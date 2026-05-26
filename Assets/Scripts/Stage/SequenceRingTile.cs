using UnityEngine;

/// <summary>
/// 순서 링 미니게임 바깥 링(16칸) 타일 하나.
/// SequenceRingMinigame이 Base Color로 색을 갱신합니다.
///
/// [MaterialPropertyBlock 사용]
///  renderer.material(인스턴스 생성) 대신 PropertyBlock을 사용해
///  메모리 누수 없이 색상을 변경합니다.
///  URP(_BaseColor)·Legacy(_Color) 셰이더 모두 대응.
/// </summary>
public class SequenceRingTile : MonoBehaviour
{
    [Header("링 순서")]
    [Tooltip("0 = 1번 칸(좌상단 등 씬 배치 기준). 시계 방향으로 1~15")]
    [SerializeField] int ringIndex = 0;

    public int RingIndex => ringIndex;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    MeshRenderer[]      _renderers;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mpb       = new MaterialPropertyBlock();
    }

    public void ApplyColor(Color color)
    {
        if (_renderers == null) return;

        // PropertyBlock에 두 프로퍼티를 모두 설정 — 셰이더가 사용하는 쪽만 반영됨
        _mpb.SetColor(BaseColorId, color);
        _mpb.SetColor(ColorId,     color);

        foreach (MeshRenderer r in _renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_mpb);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.02f);
    }
}
