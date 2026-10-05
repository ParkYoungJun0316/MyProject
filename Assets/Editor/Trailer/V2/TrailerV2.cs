using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 2차 촬영(v2) 공용 도구. 1차 산출물(Marketing_&lt;씬&gt;, Assets/Trailer/&lt;씬&gt;, Trailer_&lt;씬&gt;.mp4)은 그대로 두고
/// 이름에 v2를 붙여 따로 저장한다. 1차와 달리 카메라는 원호 공식 대신 (시각, 위치, 바라볼 점) 키로 자유롭게 움직인다.
/// </summary>
public static class TrailerV2
{
    public static string ScenePath(string stage) => $"Assets/Scenes/Marketing_v2_{stage}.unity";
    public static string AssetDir(string stage)  => $"Assets/Trailer/v2/{stage}";
    public static string ShotName(string stage)  => $"Trailer_v2_{stage}";

    const string PreviewDir = "Assets/Screenshots/Trailer/v2";

    // ── 카메라 ───────────────────────────────────────────────────

    /// <summary>
    /// (시각, 위치, 바라볼 점) 키 사이를 부드럽게 잇는 카메라. 매 프레임 LookRotation으로 각도를 구해 넣는다.
    /// fov 키가 없으면 게임 카메라 60° 그대로.
    /// </summary>
    public static Camera AddKeyCamera(TrailerShotKit.Shot shot, List<(float t, Vector3 pos, Vector3 look)> keys, List<(float t, float fov)> fov = null)
    {
        var px = new List<(float, float)>(); var py = new List<(float, float)>(); var pz = new List<(float, float)>();
        var lx = new List<(float, float)>(); var ly = new List<(float, float)>(); var lz = new List<(float, float)>();
        foreach (var k in keys)
        {
            px.Add((k.t, k.pos.x)); py.Add((k.t, k.pos.y)); pz.Add((k.t, k.pos.z));
            lx.Add((k.t, k.look.x)); ly.Add((k.t, k.look.y)); lz.Add((k.t, k.look.z));
        }
        AnimationCurve cpx = TrailerShotKit.Smooth(px), cpy = TrailerShotKit.Smooth(py), cpz = TrailerShotKit.Smooth(pz);
        AnimationCurve clx = TrailerShotKit.Smooth(lx), cly = TrailerShotKit.Smooth(ly), clz = TrailerShotKit.Smooth(lz);
        return AddFuncCamera(shot,
            t => new Vector3(cpx.Evaluate(t), cpy.Evaluate(t), cpz.Evaluate(t)),
            t => new Vector3(clx.Evaluate(t), cly.Evaluate(t), clz.Evaluate(t)), fov);
    }

    public static Camera AddFuncCamera(TrailerShotKit.Shot shot, System.Func<float, Vector3> pos, System.Func<float, Vector3> look, List<(float t, float fov)> fov = null)
    {
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        AnimationClip clip = shot.NewClip("Camera_Path");
        int frames = Mathf.RoundToInt(shot.Total * TrailerShotKit.Fps);
        var pitch = new float[frames + 1]; var yaw = new float[frames + 1];
        for (int i = 0; i <= frames; i++)
        {
            float t = i / TrailerShotKit.Fps;
            Vector3 d = look(t) - pos(t);
            pitch[i] = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float y = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            yaw[i] = i == 0 ? y : TrailerShotKit.Unwrap(yaw[i - 1], y);
        }
        float Frame(float[] a, float t) => a[Mathf.Clamp(Mathf.RoundToInt(t * TrailerShotKit.Fps), 0, frames)];
        TrailerShotKit.SetPosition(clip, "",
            TrailerShotKit.Sampled(shot.Total, t => pos(t).x), TrailerShotKit.Sampled(shot.Total, t => pos(t).y), TrailerShotKit.Sampled(shot.Total, t => pos(t).z));
        TrailerShotKit.SetEuler(clip, "",
            TrailerShotKit.Sampled(shot.Total, t => Frame(pitch, t)), TrailerShotKit.Sampled(shot.Total, t => Frame(yaw, t)), TrailerShotKit.Const(0f, shot.Total));
        if (fov != null)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Camera), "field of view"), TrailerShotKit.Smooth(fov));
        shot.BindWhole(cam.GetComponent<Animator>(), clip);
        return cam;
    }

    // ── 충돌체 되살리기 ──────────────────────────────────────────
    // TrailerShotKit은 게임 로직과 함께 충돌체도 전부 걷어낸다. 그러면 게임 파편(Breakable·RubbleShards)이
    // 베리어·바닥을 뚫고 지나가 게임과 다르게 보이므로, 파편이 부딪혀야 하는 것만 프리팹 값 그대로 다시 붙인다.

    /// <summary>prefabPath의 BoxCollider(트리거 제외)를 같은 이름 경로의 instance 자식에 그대로 복사.</summary>
    public static void RestoreBoxColliders(GameObject instance, string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        foreach (BoxCollider src in prefab.GetComponentsInChildren<BoxCollider>(true))
        {
            if (src.isTrigger) continue;
            string rel = AnimationUtility.CalculateTransformPath(src.transform, prefab.transform);
            Transform dst = rel.Length == 0 ? instance.transform : instance.transform.Find(rel);
            if (dst == null) { Debug.LogWarning($"[Trailer v2] 충돌체 대상 없음: {prefabPath}/{rel}"); continue; }
            var c = dst.gameObject.AddComponent<BoxCollider>();
            c.center = src.center;
            c.size   = src.size;
            dst.gameObject.layer = src.gameObject.layer;
        }
    }

    /// <summary>바닥처럼 단순한 판: 렌더러 경계와 같은 BoxCollider.</summary>
    public static void AddSolidFromRenderer(GameObject go)
    {
        var col = go.AddComponent<BoxCollider>();
        Bounds b = go.GetComponent<Renderer>().localBounds;
        col.center = b.center;
        col.size   = b.size;
    }

    // ── 경로 ────────────────────────────────────────────────────

    /// <summary>
    /// XZ 평면 A*(0.5m 격자) — TrailerShotKit.PlanPath와 같지만 벽 통과를 막는다.
    /// soft = 벽 + inflate(여유), hard = 벽 + hardInflate(몸 반지름). 출발점이 soft 안이면 soft만 빠져나올 수 있고
    /// hard는 어떤 경우에도 지나지 않는다(1차 도구는 출발점이 여유 구역이면 벽 속까지 통과했다 — M3 노랑).
    /// </summary>
    public static List<Vector3> PlanPath(Vector3 start, Vector3 goal, List<Bounds> obstacles, float inflate, float hardInflate, float half)
    {
        const float cell = 0.5f;
        int n = Mathf.CeilToInt(half * 2f / cell) + 1;
        bool In(Vector2 p, float pad)
        {
            foreach (Bounds b in obstacles)
                if (p.x > b.min.x - pad && p.x < b.max.x + pad && p.y > b.min.z - pad && p.y < b.max.z + pad) return true;
            return Mathf.Abs(p.x) > half || Mathf.Abs(p.y) > half;
        }
        bool Soft(Vector2 p) => In(p, inflate);
        bool Hard(Vector2 p) => In(p, hardInflate);
        Vector2 W(int i, int j) => new Vector2(-half + i * cell, -half + j * cell);
        (int, int) C(Vector3 p) => (Mathf.Clamp(Mathf.RoundToInt((p.x + half) / cell), 0, n - 1), Mathf.Clamp(Mathf.RoundToInt((p.z + half) / cell), 0, n - 1));
        bool Clear(Vector2 a, Vector2 b)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.1f);
            for (int s = 0; s <= steps; s++) if (Soft(Vector2.Lerp(a, b, s / (float)Mathf.Max(1, steps)))) return false;
            return true;
        }

        var a2 = new Vector2(start.x, start.z); var g2 = new Vector2(goal.x, goal.z);
        if (Hard(a2)) Debug.LogError($"[Trailer v2] 출발점이 벽 안: {start}");
        if (Clear(a2, g2)) return new List<Vector3> { goal };

        var (si, sj) = C(start); var (gi, gj) = C(goal);
        var cost = new Dictionary<(int, int), float> { [(si, sj)] = 0f };
        var from = new Dictionary<(int, int), (int, int)>();
        var open = new List<(int, int)> { (si, sj) };
        var done = new HashSet<(int, int)>();
        while (open.Count > 0)
        {
            open.Sort((x, y) => (cost[x] + Vector2.Distance(W(x.Item1, x.Item2), g2)).CompareTo(cost[y] + Vector2.Distance(W(y.Item1, y.Item2), g2)));
            var c = open[0]; open.RemoveAt(0);
            if (c == (gi, gj)) break;
            if (!done.Add(c)) continue;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue;
                    var nb = (c.Item1 + di, c.Item2 + dj);
                    if (nb.Item1 < 0 || nb.Item2 < 0 || nb.Item1 >= n || nb.Item2 >= n) continue;
                    Vector2 wp = W(nb.Item1, nb.Item2);
                    if (Hard(wp)) continue;
                    if (nb != (gi, gj) && Soft(wp) && !Soft(W(c.Item1, c.Item2))) continue;
                    float nc = cost[c] + ((di != 0 && dj != 0) ? 1.414f : 1f) * cell;
                    if (cost.TryGetValue(nb, out float old) && old <= nc) continue;
                    cost[nb] = nc; from[nb] = c;
                    open.Add(nb);
                }
        }
        if (!from.ContainsKey((gi, gj))) { Debug.LogError($"[Trailer v2] 경로 없음 {start}→{goal}"); return new List<Vector3> { goal }; }

        var cells = new List<Vector2> { g2 };
        for (var c = from[(gi, gj)]; c != (si, sj); c = from[c]) cells.Add(W(c.Item1, c.Item2));
        cells.Reverse();

        var result = new List<Vector3>();
        Vector2 at = a2;
        int k = 0;
        while (k < cells.Count)
        {
            int far = k;
            for (int m = cells.Count - 1; m > k; m--) if (Clear(at, cells[m])) { far = m; break; }
            at = cells[far];
            result.Add(new Vector3(at.x, goal.y, at.y));
            k = far + 1;
        }
        return result;
    }

    /// <summary>
    /// 완성된 Timeline을 편집 모드로 0.01초씩 Evaluate해 꿀떡(Kkultteok/*)이 벽(+radius)에 닿는지 실제 위치로 검사.
    /// 곡선 보간이 모서리를 깎는 것까지 잡는다. 반환 = 첫 위반 또는 null.
    /// </summary>
    public static string CheckWallsByTimeline(TrailerShotKit.Shot shot, List<Bounds> walls, float radius)
    {
        Transform group = shot.Root.Find("Kkultteok");
        string fail = null;
        for (float t = 0f; t <= shot.Total && fail == null; t += 0.01f)
        {
            shot.Director.time = t;
            shot.Director.Evaluate();
            foreach (Transform k in group)
            {
                Vector3 p = k.position;
                foreach (Bounds b in walls)
                {
                    float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
                    if (Mathf.Sqrt(dx * dx + dz * dz) < radius) { fail = $"{t:F2}s {k.name} {p:F2} 벽 {b.center}"; break; }
                }
                if (fail != null) break;
            }
        }
        shot.Director.time = 0;
        shot.Director.Evaluate();
        return fail;
    }

    // ── 동선 검사 ────────────────────────────────────────────────

    /// <summary>모든 쌍이 매 순간 minGap 이상 떨어져 있는지. 실패 시 "시각 a-b 거리" 문자열.</summary>
    public static string CheckGaps(List<(string key, TrailerShotKit.Moves m)> all, float total, float minGap)
    {
        for (float t = 0f; t <= total; t += 0.02f)
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    float d = Flat(all[i].m.At(t) - all[j].m.At(t)).magnitude;
                    if (d < minGap) return $"{t:F2}s {all[i].key}-{all[j].key} {d:F2}m";
                }
        return null;
    }

    public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    public static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // ── 미리보기 ─────────────────────────────────────────────────

    /// <summary>
    /// 지금 열린 Marketing_v2 씬의 Timeline을 편집 모드로 times마다 Evaluate → 촬영 카메라 화면을 PNG로.
    /// (편집 모드라 꿀떡 몸 애니·Signal 연출은 안 나온다 — 구도·위치 확인용)
    /// </summary>
    public static string Preview(string times)
    {
        GameObject root = GameObject.Find("TrailerShot");
        var director = root.GetComponent<PlayableDirector>();
        Camera cam = root.transform.Find("TrailerCamera").GetComponent<Camera>();
        Directory.CreateDirectory(PreviewDir);
        var outList = new List<string>();
        foreach (string s in times.Split(','))
        {
            float t = float.Parse(s, CultureInfo.InvariantCulture);
            director.time = t;
            director.Evaluate();
            string path = $"{PreviewDir}/{SceneManager.GetActiveScene().name}_edit_{t.ToString("0.00", CultureInfo.InvariantCulture)}.png";
            Render(cam, path);
            outList.Add(path);
        }
        director.time = 0;
        director.Evaluate();
        return string.Join("\n", outList);
    }

    static void Render(Camera cam, string path)
    {
        var rt  = new RenderTexture(960, 540, 24);
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
