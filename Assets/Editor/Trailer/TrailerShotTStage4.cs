using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Stage4 컷 (12초, 게임 탑다운 그대로). 움직이는 복도(Ring.B/F) 사이 8m 격자에서 넷이 피해 다닌다.
/// 판정은 게임 규칙을 동선으로부터 계산해 그대로 낸다(손으로 찍지 않음):
///   ① 정원 타일(capacity 1): 2명 이상이 되는 순간 경고 1.5초(BreakTileDirector.capacityWarnSeconds) → 파괴.
///      경고 중 1명 이하가 되면 취소(파괴 0.2초 전부터는 취소 불가, capacityCommitSeconds).
///   ③ 밟기 타일: 밟는 순간 경고 1초(warnSeconds) → 파괴.
///   파괴 순간 위에 있던 사람은 떨어진다 — y −5에서 Die 동작(fallAnimY), y −10에서 멈춤(fallDeathY, 사망 시 fixedY).
///   점유 판정 = 트리거 XZ 6.24m(0.8 × 7.8) + 캡슐 반지름 0.75.
/// 벽: MovingCorridor 규칙(뒤 {1.5,2,3,3.5}·앞 {−2,2,3,4} m/s, 간격 24~30 클램프) — 속도 순서만 정해 둔다.
/// 카메라: 게임 탑다운 pitch 85(CorridorTopDownIntroController) 그대로, 거리만 20 → 26(천장 아래), pivot = 두 벽 한가운데.
/// </summary>
public static class TrailerShotTStage4
{
    const string Src      = "Assets/Scenes/T.Stage4.unity";
    const string Dst      = "Assets/Scenes/Marketing_T.Stage4.unity";
    const string AssetDir = "Assets/Trailer/T.Stage4";
    const string ShotName = "Trailer_T.Stage4";
    const string Sm       = "Stage4.1/StageManager4.1/";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    // 게임 값
    const float CapWarn = 1.5f, CapCommit = 0.2f, StepWarn = 1f;
    const float OccHalf = 3.12f + 0.75f;
    const float FallAnimY = -5f, FallDeathY = -10f, Gravity = 9.81f;
    const float CamDist = 26f, CamPitch = 85f, PivotY = 1.5f; // 천장 Top y 29.5 아래(카메라 y 27.4)
    const float WallHalf = 2.5f, MinGap = 24f, MaxGap = 30f;

    // 벽 속도 순서(시각, m/s) — 게임 이산 속도 중에서만
    static readonly (float t, float v)[] BackSpeeds  = { (0f, 2f), (2f, 3f), (4f, 2f), (6f, 3.5f), (8f, 1.5f), (10f, 3f) };
    static readonly (float t, float v)[] FrontSpeeds = { (0f, 3f), (1.5f, -2f), (3f, 4f), (5f, 2f), (6.5f, -2f), (8f, 3f), (10f, 2f) };
    const float BackStart = 36f, FrontStart = 62f;

    // 꿀떡 경유점 (시각, x, z) — 사이는 직선 이동
    static readonly (string key, (float t, float x, float z)[] pts)[] Plan =
    {
        // B: ③ 밟기 타일(c3r56)을 뛰어 지나가 등 뒤에서 깨지게
        ("B", new[] { (0f, 12f, 47f), (2f, 12f, 51f), (4.2f, 12f, 51f), (5.8f, 12f, 66f), (7.5f, 12f, 72f), (11f, 12f, 74f), (12f, 12f, 75f) }),
        // P: 가운데 부종 오른쪽으로 → c2r72에 먼저 올라섬 → G가 들어와 같이 낙하
        ("P", new[] { (0f, 6f, 48f), (2f, 6f, 49f), (3.5f, 6.5f, 57f), (5.8f, 6.5f, 58.5f), (6.8f, 5f, 62f), (7.4f, 5f, 64f), (8.6f, 5.5f, 70f), (9.3f, 6f, 72f), (10f, 5.5f, 71f) }),
        // G: 부종 왼쪽으로 돌아 Y 칸에 들어감(①경고) → Y가 비켜 취소 → ③(c1r72)을 모서리로 비켜 P 칸(c2r72)으로
        ("G", new[] { (0f, -3f, 47f), (1f, -3f, 47.5f), (2f, -5.5f, 49f), (3.2f, -10f, 54.5f), (4.5f, -10.5f, 56f), (6f, -6f, 62f), (7.6f, -5f, 64f), (8.3f, -1.5f, 66.5f), (9f, 2.5f, 70f), (9.6f, 2f, 72.5f), (10.1f, 2.5f, 71.5f) }),
        // Y(로컬·카메라): G가 들어오자 앞으로 비킴 → 일찍 c2r72를 지나 c2r80으로
        ("Y", new[] { (0f, -12f, 54f), (3.4f, -12f, 56f), (4f, -12f, 62f), (5f, -3f, 63f), (5.7f, 2.5f, 66f), (6.3f, 4f, 70f), (7.6f, 3.5f, 74.5f), (9f, 3.5f, 79f), (12f, 4f, 81f) }),
    };

    [MenuItem("Tools/Trailer/T.Stage4 정원 타일 낙하")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, Dst, "BreakTile", "SpikeLaneWarnMarker");
        TrailerShotKit.Find(scene, Sm + "MovingCorridor/Ring.B").SetActive(true); // 게임: OnTopDownReady에서 켬
        var shot = new TrailerShotKit.Shot(AssetDir, ShotName, Total);

        // ── 벽 궤적(게임 MovingCorridor 클램프 그대로) ──
        const float dt = 1f / 120f;
        int n = Mathf.CeilToInt(Total / dt) + 1;
        var back = new float[n]; var front = new float[n];
        back[0] = BackStart; front[0] = FrontStart;
        for (int i = 1; i < n; i++)
        {
            float t = (i - 1) * dt;
            float nb = back[i - 1] + Speed(BackSpeeds, t) * dt;
            float nf = front[i - 1] + Speed(FrontSpeeds, t) * dt;
            if (nf - nb < MinGap) nf = nb + MinGap;
            if (nf - nb > MaxGap) nf = nb + MaxGap;
            back[i] = nb; front[i] = nf;
        }
        float BackAt(float t) => back[Mathf.Clamp(Mathf.RoundToInt(t / dt), 0, n - 1)];
        float FrontAt(float t) => front[Mathf.Clamp(Mathf.RoundToInt(t / dt), 0, n - 1)];

        foreach (var (wall, at) in new (string, System.Func<float, float>)[] { ("Ring.B", BackAt), ("Ring.F", FrontAt) })
        {
            GameObject go = TrailerShotKit.Find(scene, Sm + "MovingCorridor/" + wall);
            Vector3 p = go.transform.position;
            AnimationClip c = shot.NewClip(wall + "_Move");
            TrailerShotKit.SetPosition(c, "", TrailerShotKit.Const(p.x, Total), TrailerShotKit.Const(p.y, Total), TrailerShotKit.Sampled(Total, at));
            shot.Animate(go, c);
        }

        // ── 타일 격자 ──
        Transform tileRoot = TrailerShotKit.Find(scene, Sm + "Tile").transform;
        var tiles = new Dictionary<(int c, int r), BreakTile>();
        foreach (Transform t in tileRoot)
        {
            var bt = t.GetComponent<BreakTile>();
            if (bt == null) continue;
            tiles[(Col(t.position.x), Row(t.position.z))] = bt;
            foreach (Transform m in t) if (m.name.StartsWith("WarnMarker")) m.gameObject.SetActive(true);
        }
        var lumps = new List<Bounds>();
        foreach (Transform t in tileRoot.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Lump") && t.GetComponent<Renderer>() != null) lumps.Add(t.GetComponent<Renderer>().bounds);

        // ── 동선 → 판정 시뮬레이션 ──
        var tracks = new List<Vector2[]>();
        foreach (var p in Plan) tracks.Add(SampleTrack(p.pts, n, dt));
        var fallAt = new float[Plan.Length];
        for (int i = 0; i < fallAt.Length; i++) fallAt[i] = float.MaxValue;

        var state = new Dictionary<(int, int), (int st, float breakAt)>(); // st 0 Idle, 1 Warning, 2 Gone
        var events = new List<(BreakTile tile, string kind, float t, float arg)>();
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            float t = i * dt;
            var count = new Dictionary<(int, int), List<int>>();
            for (int k = 0; k < tracks.Count; k++)
            {
                if (t >= fallAt[k]) continue;
                Vector2 p = tracks[k][i];
                foreach (var key in tiles.Keys)
                {
                    Vector2 c = Center(key);
                    if (Mathf.Abs(p.x - c.x) < OccHalf && Mathf.Abs(p.y - c.y) < OccHalf)
                    {
                        if (!count.TryGetValue(key, out var l)) count[key] = l = new List<int>();
                        l.Add(k);
                    }
                }
            }
            foreach (var kv in tiles)
            {
                var key = kv.Key;
                var s = state.TryGetValue(key, out var v) ? v : (0, 0f);
                int cnt = count.TryGetValue(key, out var occ) ? occ.Count : 0;
                bool cap = kv.Value.Mode == BreakTile.TriggerMode.Capacity;
                if (s.Item1 == 2)
                {
                    if (cnt > 0) log.AppendLine($"⚠ {t:F2}s 구멍 위: {string.Join(",", occ.ConvertAll(o => Plan[o].key))} @{key}");
                    continue;
                }
                if (s.Item1 == 0 && (cap ? cnt > kv.Value.Capacity : cnt > 0))
                {
                    float w = cap ? CapWarn : StepWarn;
                    s = (1, t + w);
                    events.Add((kv.Value, cap ? "ArmCapacity" : "ArmStep", t, w));
                    log.AppendLine($"{t:F2}s {(cap ? "①경고" : "③경고")} {key} ({string.Join(",", occ.ConvertAll(o => Plan[o].key))})");
                }
                else if (s.Item1 == 1 && cap && cnt <= kv.Value.Capacity && t < s.Item2 - CapCommit)
                {
                    s = (0, 0f);
                    events.Add((kv.Value, "CancelCapacity", t, 0f));
                    log.AppendLine($"{t:F2}s ①취소 {key}");
                }
                else if (s.Item1 == 1 && t >= s.Item2)
                {
                    s = (2, s.Item2);
                    log.AppendLine($"{t:F2}s 파괴 {key}" + (cnt > 0 ? $" → 낙하 {string.Join(",", occ.ConvertAll(o => Plan[o].key))}" : ""));
                    if (occ != null) foreach (int o in occ) fallAt[o] = t;
                }
                state[key] = s;
            }
        }

        // ── 판정 → 게임 BreakTile 호출, 나머지 타일은 바닥 콜라이더만 ──
        var used = new HashSet<BreakTile>();
        foreach (var e in events) used.Add(e.tile);
        foreach (var bt in tiles.Values)
        {
            GameObject go = bt.gameObject;
            go.AddComponent<BoxCollider>();
            if (!used.Contains(bt)) { Object.DestroyImmediate(bt); continue; }
            var trig = go.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(0.8f, 1f, 0.8f);
        }
        var fx = new Dictionary<BreakTile, TrailerBreakTileFx>();
        foreach (var e in events)
        {
            if (!fx.TryGetValue(e.tile, out var f))
            {
                f = TrailerShotKit.Group("BreakFx_" + e.tile.name, shot.Root).AddComponent<TrailerBreakTileFx>();
                f.tile = e.tile;
                fx[e.tile] = f;
            }
            if (e.kind == "CancelCapacity") shot.Call(f, e.kind, e.t);
            else shot.Call(f, e.kind, e.t, e.arg);
        }

        // ── 꿀떡 ──
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var minPair = float.MaxValue;
        for (int k = 0; k < Plan.Length; k++)
        {
            Vector2[] tr = tracks[k];
            Vector3 Pos(float t)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(t / dt), 0, n - 1);
                if (t < fallAt[k]) return new Vector3(tr[i].x, GroundY, tr[i].y);
                int f = Mathf.RoundToInt(fallAt[k] / dt);
                float y = Mathf.Max(FallDeathY, GroundY - 0.5f * Gravity * (t - fallAt[k]) * (t - fallAt[k]));
                return new Vector3(tr[f].x, y, tr[f].y);
            }
            var runner = TrailerShotKit.MakeRunner(group, Plan[k].key, Pos(0f), 0f);
            var keys = new List<(float, Vector3, float)>();
            float yaw = 0f; bool running = false;
            for (float t = 0f; t <= Total + 0.0001f; t += 1f / 30f)
            {
                Vector3 a = Pos(t), b = Pos(Mathf.Min(Total, t + 0.1f));
                Vector3 d = b - a; d.y = 0f;
                bool move = d.magnitude / 0.1f > 0.5f && t < fallAt[k];
                if (move) yaw = TrailerShotKit.Yaw(d);
                if (move != running)
                {
                    if (t < 0.001f) runner.StartWith("Run", k * 0.11f);
                    else runner.Pose(t, move ? "Run" : "Idle");
                    running = move;
                }
                keys.Add((t, a, yaw));
            }
            if (fallAt[k] < Total)
            {
                float tAnim = fallAt[k] + Mathf.Sqrt(2f * (GroundY - FallAnimY) / Gravity);
                if (tAnim < Total) runner.Pose(tAnim, "Die");
            }
            runner.Follow(keys);
            runner.Build(shot);

            float maxSpeed = 0f;
            for (int i = 1; i < n; i++) if (i * dt < fallAt[k]) maxSpeed = Mathf.Max(maxSpeed, Vector2.Distance(tr[i], tr[i - 1]) / dt);
            if (maxSpeed > 10.05f) log.AppendLine($"⚠ {Plan[k].key} 최고 속도 {maxSpeed:F1} m/s > 10");

            for (int i = 0; i < n; i += 4)
            {
                float t = i * dt;
                if (t >= fallAt[k]) break;
                Vector2 p = tr[i];
                if (p.y < BackAt(t) + WallHalf + 1.2f || p.y > FrontAt(t) - WallHalf - 1.2f) { log.AppendLine($"⚠ {t:F2}s {Plan[k].key} 벽에 닿음 z={p.y:F1} (뒤 {BackAt(t) + WallHalf:F1} / 앞 {FrontAt(t) - WallHalf:F1})"); i += 120; }
                if (Mathf.Abs(p.x) > 15.2f) log.AppendLine($"⚠ {t:F2}s {Plan[k].key} 옆벽 x={p.x:F1}");
                foreach (Bounds lb in lumps)
                    if (p.x > lb.min.x - 0.9f && p.x < lb.max.x + 0.9f && p.y > lb.min.z - 0.9f && p.y < lb.max.z + 0.9f && lb.max.y > 0.6f)
                    { log.AppendLine($"⚠ {t:F2}s {Plan[k].key} 부종과 겹침 {lb.center}"); i += 120; break; }
                for (int j = k + 1; j < Plan.Length; j++)
                    if (t < fallAt[j]) minPair = Mathf.Min(minPair, Vector2.Distance(p, tracks[j][i]));
            }
        }

        // ── 카메라: 게임 탑다운 각도(pitch 85) 그대로, pivot = 두 벽 한가운데(1초 평균으로 부드럽게).
        //    게임 거리 20이면 벽(높이 15) 근처 사람이 벽 윗면에 가려져 트레일러에선 천장 바로 아래(거리 26)까지 올린다. ──
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        Vector3 CamPos(float t)
        {
            float z = 0f; int m = 0;
            for (float s = t - 0.5f; s <= t + 0.5f; s += 0.05f, m++) z += (BackAt(Mathf.Clamp(s, 0f, Total)) + FrontAt(Mathf.Clamp(s, 0f, Total))) * 0.5f;
            return new Vector3(0f, PivotY, z / m) + Quaternion.Euler(CamPitch, 0f, 0f) * (Vector3.back * CamDist);
        }
        AnimationClip camClip = shot.NewClip("Camera_TopDown");
        TrailerShotKit.SetPosition(camClip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Sampled(Total, t => CamPos(t).y), TrailerShotKit.Sampled(Total, t => CamPos(t).z));
        TrailerShotKit.SetEuler(camClip, "", TrailerShotKit.Const(CamPitch, Total), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(cam.GetComponent<Animator>(), camClip);

        shot.AddRecorder(ShotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {Dst} 생성 완료 — 꿀떡 최소 간격 {minPair:F2}m\n{log}");
    }

    static float Speed((float t, float v)[] seq, float t)
    {
        float v = seq[0].v;
        foreach (var s in seq) if (t >= s.t) v = s.v;
        return v;
    }

    static int Col(float x) => Mathf.RoundToInt((x + 12f) / 8f);
    static int Row(float z) => Mathf.RoundToInt((z - 16f) / 8f);
    static Vector2 Center((int c, int r) k) => new Vector2(-12f + 8f * k.c, 16f + 8f * k.r);

    static Vector2[] SampleTrack((float t, float x, float z)[] pts, int n, float dt)
    {
        var r = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float t = i * dt;
            Vector2 p = new Vector2(pts[pts.Length - 1].x, pts[pts.Length - 1].z);
            if (t <= pts[0].t) p = new Vector2(pts[0].x, pts[0].z);
            else
                for (int j = 1; j < pts.Length; j++)
                    if (t <= pts[j].t)
                    {
                        float u = Mathf.InverseLerp(pts[j - 1].t, pts[j].t, t); // 게임 이동처럼 등속(가감속 없음)
                        p =Vector2.Lerp(new Vector2(pts[j - 1].x, pts[j - 1].z), new Vector2(pts[j].x, pts[j].z), u);
                        break;
                    }
            r[i] = p;
        }
        return r;
    }
}
