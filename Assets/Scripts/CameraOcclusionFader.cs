using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라와 플레이어 사이에 있는 벽을 반투명하게 처리하는 컴포넌트.
/// Camera GameObject에 부착.
///
/// [동작]
///  매 프레임 카메라 → 플레이어 방향으로 RaycastAll.
///  Wall 레이어에 걸린 오브젝트의 머티리얼을 반투명(URP Transparent)으로 전환.
///  플레이어가 빠져나오면 원래 Opaque로 복구.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFader : MonoBehaviour
{
    [Header("타겟")]
    [Tooltip("플레이어 Transform. 카메라가 이 오브젝트와의 시야선을 체크함")]
    [SerializeField] Transform target;

    [Tooltip("플레이어 중심에서 위로 오프셋 (m). 발이 아닌 몸통 기준으로 체크")]
    [SerializeField] float targetHeightOffset = 0f;

    [Header("레이캐스트")]
    [Tooltip("차폐 감지 대상 레이어. Wall 레이어 설정")]
    [SerializeField] LayerMask occlusionLayer;

    [Header("페이드")]
    [Tooltip("가려질 때 적용할 알파값 (0 = 완전 투명, 1 = 불투명)")]
    [SerializeField, Range(0f, 1f)] float fadeAlpha = 0f;

    [Tooltip("알파 변화 속도. 0 = 즉시 전환")]
    [SerializeField] float fadeSpeed = 0f;

    // 활성 렌더러 목록 (List로 순회 → Dictionary는 alpha 조회용으로만 사용)
    readonly List<Renderer>              _activeList    = new List<Renderer>();
    readonly Dictionary<Renderer, float> _alphaMap      = new Dictionary<Renderer, float>();
    readonly HashSet<Renderer>           _currentOccluders = new HashSet<Renderer>();

    // URP Lit 셰이더 프로퍼티 ID 캐시
    static readonly int PropSurface   = Shader.PropertyToID("_Surface");
    static readonly int PropBlend     = Shader.PropertyToID("_Blend");
    static readonly int PropSrcBlend  = Shader.PropertyToID("_SrcBlend");
    static readonly int PropDstBlend  = Shader.PropertyToID("_DstBlend");
    static readonly int PropZWrite    = Shader.PropertyToID("_ZWrite");
    static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int PropColor     = Shader.PropertyToID("_Color");

    void LateUpdate()
    {
        if (target == null) return;
        DetectOccluders();
        UpdateFade();
    }

    void DetectOccluders()
    {
        _currentOccluders.Clear();

        Vector3 playerPos = target.position + Vector3.up * targetHeightOffset;
        Vector3 camPos    = transform.position;
        Vector3 dir       = playerPos - camPos;
        float   dist      = dir.magnitude;

        if (dist < 0.01f) return;

        RaycastHit[] hits = Physics.RaycastAll(
            camPos, dir.normalized, dist,
            occlusionLayer,
            QueryTriggerInteraction.Collide);

        foreach (RaycastHit hit in hits)
        {
            Renderer[] renderers = hit.collider.GetComponentsInChildren<Renderer>(false);
            foreach (Renderer r in renderers)
                _currentOccluders.Add(r);
        }
    }

    void UpdateFade()
    {
        // 새로 감지된 렌더러 등록
        foreach (Renderer r in _currentOccluders)
        {
            if (!_alphaMap.ContainsKey(r))
            {
                SetTransparentMode(r.material);
                _alphaMap[r] = 1f;
                _activeList.Add(r);
            }
        }

        // List를 역순으로 순회 → 중간 삭제가 안전
        for (int i = _activeList.Count - 1; i >= 0; i--)
        {
            Renderer r = _activeList[i];

            if (r == null)
            {
                _alphaMap.Remove(r);
                _activeList.RemoveAt(i);
                continue;
            }

            bool  isOccluding = _currentOccluders.Contains(r);
            float targetAlpha = isOccluding ? fadeAlpha : 1f;
            float current     = _alphaMap[r];

            float next = fadeSpeed > 0f
                ? Mathf.MoveTowards(current, targetAlpha, fadeSpeed * Time.deltaTime)
                : targetAlpha;

            _alphaMap[r] = next;
            SetAlpha(r.material, next);

            // 완전 복구됐으면 Opaque로 되돌리고 목록에서 제거
            if (!isOccluding && Mathf.Approximately(next, 1f))
            {
                SetOpaqueMode(r.material);
                _alphaMap.Remove(r);
                _activeList.RemoveAt(i);
            }
        }
    }

    // ── 머티리얼 모드 전환 (URP) ─────────────────────────────────

    void SetTransparentMode(Material mat)
    {
        mat.SetFloat(PropSurface,  1f);
        mat.SetFloat(PropBlend,    0f);
        mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat(PropZWrite,   0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void SetOpaqueMode(Material mat)
    {
        mat.SetFloat(PropSurface,  0f);
        mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat(PropZWrite,   1f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        SetAlpha(mat, 1f);
    }

    void SetAlpha(Material mat, float alpha)
    {
        if (mat.HasProperty(PropBaseColor))
        {
            Color c = mat.GetColor(PropBaseColor);
            c.a = alpha;
            mat.SetColor(PropBaseColor, c);
        }
        else if (mat.HasProperty(PropColor))
        {
            Color c = mat.GetColor(PropColor);
            c.a = alpha;
            mat.SetColor(PropColor, c);
        }
    }
}
