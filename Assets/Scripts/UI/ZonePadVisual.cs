using UnityEngine;

/// <summary>
/// 모임·도착 지점 발판 — 존 콜라이더 크기에 맞춰 크림색 테두리 + 반투명 안쪽 + 빛 벽을 바닥에 그린다.
/// 모이는 곳(Tutorial·Interlude)과 도착 지점(ReachZone·T5 Goal)이 전부 같은 모양이다(ArrivalPad 프리팹).
///
///  [모양]   SphereCollider = 원(바닥 높이에서 자른 단면), BoxCollider = 둥근 사각형.
///           텍스처를 늘리지 않고 메시를 만들어서 존 크기가 달라도 테두리 두께가 일정하다.
///  [빛 벽]  테두리를 따라 위로 갈수록 투명해지는 벽 — 25~50m 거리에서 "저기가 도착 지점"으로 읽히게.
///  [높이]   이 오브젝트의 y(바닥에 맞춰 둔다) + heightOffset.
///  [파티클] edgeEmitters의 발생 범위를 발판 테두리에 맞추고, 방출량을 테두리 길이에 비례시킨다.
///
/// [상태 연출] SetOverride/ClearOverride — 색 덮어쓰기 + 빛 벽·파티클 끄기(T.Boss P3 안전 칸: SafeZoneWarnSign이 호출).
///
/// 판정은 하지 않는다 — 존(TutorialGatherZone / ReachZoneObjective / T5 Goal)의 표시만 담당.
/// 메시는 저장하지 않는 숨김 자식으로 매번 만든다(씬 파일에 메시가 들어가지 않게).
/// </summary>
[ExecuteAlways]
public class ZonePadVisual : MonoBehaviour
{
    [System.Serializable]
    public class EdgeEmitter
    {
        public ParticleSystem system;
        [Tooltip("테두리 1m당 초당 방출 수.")]
        public float ratePerMeter = 0.5f;
    }

    [Tooltip("크기를 읽을 존 콜라이더(Sphere=원, Box=둥근 사각형). 비우면 부모에서 찾는다.")]
    [SerializeField] Collider source;
    [Tooltip("투명 Unlit 머티리얼. 색은 아래 두 값으로 칠한다.")]
    [SerializeField] Material material;
    [SerializeField] Color borderColor = new Color(1f, 0.957f, 0.902f, 1f);
    [SerializeField] Color fillColor = new Color(1f, 0.957f, 0.902f, 0.22f);
    [SerializeField] float borderWidth = 0.6f;
    [Tooltip("Box 모서리 둥글기(m). Sphere는 무시.")]
    [SerializeField] float cornerRadius = 1.5f;
    [Tooltip("바닥과 겹쳐 깜빡이지 않게 띄우는 높이.")]
    [SerializeField] float heightOffset = 0.03f;
    [SerializeField] int segmentsPerCorner = 24;

    [Header("빛 벽 (도착 지점용)")]
    [Tooltip("테두리를 따라 세우는 벽 높이(m). 0이면 벽 없음.")]
    [SerializeField] float wallHeight = 3f;
    [Tooltip("위로 갈수록 투명해지는 세로 그라데이션 텍스처를 쓴 투명 Unlit 머티리얼(양면).")]
    [SerializeField] Material wallMaterial;
    [SerializeField] Color wallColor = new Color(1f, 0.957f, 0.902f, 0.55f);

    [Header("파티클")]
    [SerializeField] EdgeEmitter[] edgeEmitters;

    const string MeshChildName = "__ZonePadMesh";
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    GameObject _child;
    MeshFilter _filter;
    MeshRenderer _renderer;
    Mesh _mesh;
    MaterialPropertyBlock _mpb;
    string _builtKey;

    // 런타임 상태 연출(SafeZoneWarnSign 등) — 인스펙터 색 대신 쓰고, 빛 벽·파티클을 끌 수 있다.
    bool _hasOverride;
    Color _overrideBorder, _overrideFill;
    bool _decorOn = true;

    /// <summary>
    /// 테두리·안쪽 색을 덮어쓰고 빛 벽·파티클을 켜고 끈다(런타임 상태 연출용). 메시는 다시 만들지 않는다 —
    /// 빛 벽은 알파 0으로 숨기고, 파티클은 방출만 멈춰 이미 나온 입자는 자연스럽게 사라진다.
    /// </summary>
    public void SetOverride(Color border, Color fill, bool wallAndParticles)
    {
        _hasOverride = true;
        _overrideBorder = border;
        _overrideFill = fill;
        _decorOn = wallAndParticles;
        ApplyColors();
        ApplyEmitters();
    }

    /// <summary>인스펙터 색·빛 벽·파티클로 되돌린다.</summary>
    public void ClearOverride()
    {
        _hasOverride = false;
        _decorOn = true;
        ApplyColors();
        ApplyEmitters();
    }

    void OnEnable()
    {
        _builtKey = null;
        Refresh();
    }

    void OnDisable()
    {
        if (_child != null) DestroyImmediate(_child);
        if (_mesh != null) DestroyImmediate(_mesh);
        _child = null;
        _mesh = null;
    }

    void Update()
    {
        // 플레이 중엔 존이 움직이지 않으므로 OnEnable 1회로 충분 — 에디터에선 값을 바꿔보는 즉시 반영.
        if (!Application.isPlaying) Refresh();
    }

    void OnValidate() => _builtKey = null;

    // ── 모양 계산 ────────────────────────────────────────────────

    /// <summary>발판 중심(월드)·회전(yaw만)·반폭·반깊이·모서리 반지름.</summary>
    bool TryGetFootprint(out Vector3 center, out Quaternion yaw, out float halfW, out float halfD, out float corner)
    {
        center = transform.position; yaw = Quaternion.identity; halfW = halfD = corner = 0f;

        Collider col = source != null ? source : GetComponentInParent<Collider>();
        if (col == null) return false;

        Transform t = col.transform;
        float groundY = transform.position.y;

        if (col is SphereCollider sphere)
        {
            Vector3 s = t.lossyScale;
            float r = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            Vector3 c = t.TransformPoint(sphere.center);
            float dy = c.y - groundY;
            float footR = Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            center = new Vector3(c.x, groundY, c.z);
            halfW = halfD = corner = footR;
            return footR > 0f;
        }

        if (col is BoxCollider box)
        {
            Vector3 s = t.lossyScale;
            Vector3 c = t.TransformPoint(box.center);
            center = new Vector3(c.x, groundY, c.z);
            yaw = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            halfW = Mathf.Abs(box.size.x * s.x) * 0.5f;
            halfD = Mathf.Abs(box.size.z * s.z) * 0.5f;
            corner = Mathf.Min(cornerRadius, halfW, halfD);
            return halfW > 0f && halfD > 0f;
        }

        return false;
    }

    static float Perimeter(float halfW, float halfD, float corner)
        => 4f * (halfW + halfD) - (8f - 2f * Mathf.PI) * corner;

    // ── 갱신 ─────────────────────────────────────────────────────

    void Refresh()
    {
        if (!TryGetFootprint(out Vector3 center, out Quaternion yaw, out float halfW, out float halfD, out float corner))
        {
            if (_child != null) _child.SetActive(false);
            return;
        }

        EnsureChild();
        _child.SetActive(true);

        string key = $"{halfW:F3}|{halfD:F3}|{corner:F3}|{borderWidth:F3}|{segmentsPerCorner}|{WallOn}|{wallHeight:F3}";
        if (key != _builtKey)
        {
            BuildMesh(halfW, halfD, corner);
            _builtKey = key;
        }

        // 숨김 자식은 월드 기준으로 놓는다 — 부모 스케일(존 오브젝트가 늘어나 있을 수 있음)을 상쇄.
        Transform ct = _child.transform;
        ct.SetPositionAndRotation(center + Vector3.up * heightOffset, yaw);
        Vector3 ps = transform.lossyScale;
        ct.localScale = new Vector3(SafeInv(ps.x), SafeInv(ps.y), SafeInv(ps.z));

        if (material != null)
        {
            var mats = _renderer.sharedMaterials;
            bool matsOk = mats.Length == (WallOn ? 3 : 2) && mats[0] == material && (!WallOn || mats[2] == wallMaterial);
            if (!matsOk)
                _renderer.sharedMaterials = WallOn ? new[] { material, material, wallMaterial } : new[] { material, material };
        }
        ApplyColors();

        FitEmitters(center, yaw, halfW, halfD, corner);
        ApplyEmitters();
    }

    void ApplyColors()
    {
        if (material == null || _renderer == null) return;

        _mpb ??= new MaterialPropertyBlock();
        _mpb.SetColor(BaseColorId, _hasOverride ? _overrideBorder : borderColor);
        _renderer.SetPropertyBlock(_mpb, 0);
        _mpb.SetColor(BaseColorId, _hasOverride ? _overrideFill : fillColor);
        _renderer.SetPropertyBlock(_mpb, 1);
        if (WallOn)
        {
            Color wc = wallColor;
            if (!_decorOn) wc.a = 0f;
            _mpb.SetColor(BaseColorId, wc);
            _renderer.SetPropertyBlock(_mpb, 2);
        }
    }

    // 에디터(비플레이)에선 건드리지 않는다 — 인스펙터 미리보기는 늘 기본 상태.
    void ApplyEmitters()
    {
        if (!Application.isPlaying || edgeEmitters == null) return;
        foreach (var e in edgeEmitters)
        {
            if (e == null || e.system == null) continue;
            if (_decorOn) { if (!e.system.isEmitting) e.system.Play(true); }
            else if (e.system.isEmitting) e.system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    bool WallOn => wallHeight > 0f && wallMaterial != null;

    static float SafeInv(float v) => Mathf.Abs(v) > 1e-5f ? 1f / v : 1f;

    void EnsureChild()
    {
        if (_child != null) return;

        // 도메인 리로드 뒤 남아 있던 숨김 자식이 있으면 재사용.
        Transform existing = transform.Find(MeshChildName);
        _child = existing != null ? existing.gameObject : new GameObject(MeshChildName);
        _child.hideFlags = HideFlags.HideAndDontSave;
        _child.layer = gameObject.layer;
        _child.transform.SetParent(transform, false);

        if (!_child.TryGetComponent(out _filter)) _filter = _child.AddComponent<MeshFilter>();
        if (!_child.TryGetComponent(out _renderer)) _renderer = _child.AddComponent<MeshRenderer>();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "ZonePad", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
        }
        _filter.sharedMesh = _mesh;
    }

    /// <summary>
    /// 둥근 사각형 테두리(서브메시 0) + 안쪽(서브메시 1). 원은 반폭=반깊이=모서리 반지름인 경우.
    /// 바깥·안쪽 경로가 같은 각도의 점을 가져서 테두리 두께가 어디서나 borderWidth로 일정하다.
    /// </summary>
    void BuildMesh(float halfW, float halfD, float corner)
    {
        int k = Mathf.Max(2, segmentsPerCorner);
        int perCorner = k + 1;
        int n = perCorner * 4;

        float bw = Mathf.Min(borderWidth, Mathf.Min(halfW, halfD) * 0.9f);
        float innerCorner = Mathf.Max(0f, corner - bw);

        var verts = new Vector3[n * 2 + 1];
        for (int c = 0; c < 4; c++)
        {
            float sx = (c == 0 || c == 3) ? 1f : -1f;
            float sz = (c == 0 || c == 1) ? 1f : -1f;
            for (int i = 0; i <= k; i++)
            {
                float a = (c * 90f + 90f * i / k) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                int idx = c * perCorner + i;

                var oc = new Vector3(sx * (halfW - corner), 0f, sz * (halfD - corner));
                verts[idx] = oc + dir * corner;

                var ic = new Vector3(sx * (halfW - bw - innerCorner), 0f, sz * (halfD - bw - innerCorner));
                verts[n + idx] = ic + dir * innerCorner;
            }
        }
        int centerIdx = n * 2;
        verts[centerIdx] = Vector3.zero;

        var ring = new int[n * 6];
        var fill = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            int o = i * 6;
            ring[o]     = i;     ring[o + 1] = n + i; ring[o + 2] = j;
            ring[o + 3] = j;     ring[o + 4] = n + i; ring[o + 5] = n + j;

            int f = i * 3;
            fill[f] = centerIdx; fill[f + 1] = n + j; fill[f + 2] = n + i;
        }

        // 빛 벽: 바깥 경로를 따라 바닥(v=0) → 꼭대기(v=1). 투명해지는 건 텍스처 알파가 맡는다.
        int wallBase = verts.Length;
        var allVerts = new System.Collections.Generic.List<Vector3>(verts);
        var uvs = new System.Collections.Generic.List<Vector2>(new Vector2[verts.Length]);
        int[] wall = null;
        if (WallOn)
        {
            for (int i = 0; i < n; i++)
            {
                allVerts.Add(verts[i]);
                allVerts.Add(verts[i] + Vector3.up * wallHeight);
                float u = (float)i / n;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }
            wall = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int b0 = wallBase + i * 2, t0 = b0 + 1;
                int b1 = wallBase + j * 2, t1 = b1 + 1;
                int o = i * 6;
                wall[o]     = b0; wall[o + 1] = t0; wall[o + 2] = b1;
                wall[o + 3] = b1; wall[o + 4] = t0; wall[o + 5] = t1;
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(allVerts);
        _mesh.SetUVs(0, uvs);
        _mesh.subMeshCount = wall != null ? 3 : 2;
        _mesh.SetTriangles(ring, 0);
        _mesh.SetTriangles(fill, 1);
        if (wall != null) _mesh.SetTriangles(wall, 2);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    void FitEmitters(Vector3 center, Quaternion yaw, float halfW, float halfD, float corner)
    {
        if (edgeEmitters == null) return;

        bool isCircle = Mathf.Approximately(halfW, halfD) && Mathf.Approximately(corner, halfW);
        float perimeter = Perimeter(halfW, halfD, corner);

        foreach (var e in edgeEmitters)
        {
            if (e == null || e.system == null) continue;
            Transform et = e.system.transform;

            // 값이 같으면 쓰지 않는다 — 에디터에서 매 프레임 씬이 더러워지지 않게.
            if ((et.position - center).sqrMagnitude > 1e-6f || Quaternion.Angle(et.rotation, yaw) > 0.01f)
                et.SetPositionAndRotation(center, yaw);

            var shape = e.system.shape;
            if (isCircle)
            {
                if (shape.shapeType != ParticleSystemShapeType.Circle) shape.shapeType = ParticleSystemShapeType.Circle;
                if (!Mathf.Approximately(shape.radius, halfW)) shape.radius = halfW;
                if (shape.radiusThickness != 0f) shape.radiusThickness = 0f;
            }
            else
            {
                if (shape.shapeType != ParticleSystemShapeType.BoxEdge) shape.shapeType = ParticleSystemShapeType.BoxEdge;
                var scale = new Vector3(halfW * 2f, halfD * 2f, 0f);
                if (shape.scale != scale) shape.scale = scale;
            }
            // Circle·BoxEdge는 XY 평면에 놓이므로 바닥(XZ)으로 눕힌다.
            var rot = new Vector3(90f, 0f, 0f);
            if (shape.rotation != rot) shape.rotation = rot;

            var emission = e.system.emission;
            float rate = e.ratePerMeter * perimeter;
            if (!Mathf.Approximately(emission.rateOverTime.constant, rate)) emission.rateOverTime = rate;
        }
    }
}
