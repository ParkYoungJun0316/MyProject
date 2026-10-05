using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 T.Stage5 (12초) — 1층 러너(파랑) + 2층 길잡이 3명(보라·초록·노랑) + 체이서.
/// 1차(TrailerShotTStage5)의 문 탐색을 그대로 쓰되 1차에 없던 둘을 넣는다:
///   길잡이: 문 색이 바뀌는 순간 = 그 색 길잡이가 2층(바닥 윗면 50.25, 바닥은 보이지 않음) 패드 트리거에 들어가는 순간.
///     패드 = Pads2F/Zone_k(구간마다 6색 링). 고유색 패드는 그 색만 → 전환 색을 보라·초록·노랑으로 한정(흑·백은 Q 변신이 필요해 안 씀).
///     러너 색(파랑) 패드는 게임처럼 숨긴다. 길잡이는 러너에 가장 가까운 구간으로 따라다닌다(10m/s).
///   체이서: 게임 Chaser.prefab 모델, 4m/s, 0.5초마다 러너 위치로 다시 겨눔(retargetInterval), 문은 관통·바위벽(PinkWall)은 못 지남.
///     컷 동안 러너에 닿지 않는다(3.5m 이상).
/// 길잡이 셋은 러너가 다니는 곳에 가장 가까운 구간 하나에 모여 각자 자기 색 패드를 누른다(어느 구간 패드든 같은 색 문을 연다).
/// 카메라: 길잡이와 러너 가운데 위(y 78)에서 거의 수직(87°)으로 천천히 돈다 — 2층 바닥은 안 보여서 패드 위 길잡이 아래로 50m 밑 미로·러너·체이서가 보인다.
///   (비스듬히 보면 문 높이 15가 칸 바닥을 가려 러너가 안 보인다. 3인칭 컷은 1차에 있음.)
/// </summary>
public static class TrailerV2TStage5
{
    const string Stage = "T.Stage5";
    const string Src   = "Assets/Scenes/T.Stage5.unity";
    const string Doors = "StageManager5/T5_Maze/Doors/";
    const string Pads  = "StageManager5/T5_Maze/Pads2F";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;
    const float  Floor2Y  = 50.25f; // Floor2F 윗면(문서 §1.4 실측)

    const float Cell = 12f, Origin = -6f, RunSpeed = 10f;

    static readonly string[] GroupNames = { "Blue", "Purple", "Green", "Yellow", "Black", "White" };
    static readonly string[] GroupMats  = { "Assets/Mat/Ground/Boulder2.mat", "Assets/Mat/Player/Guma.mat", "Assets/Mat/Player/Sook.mat",
                                            "Assets/Mat/Player/Dan.mat", "Assets/Mat/Player/Black.mat", "Assets/Mat/Player/White.mat" };
    static readonly string[] GuideKey = { null, "P", "G", "Y" };
    // 패드 링(구간 중심 기준): 보라 (2, 3.5) · 초록 (−2, 3.5) · 노랑 (−4, 0) — 각자 자기 패드 쪽에 선다(남의 동선을 안 가로지르게)
    static readonly Vector3[] StandOff = { Vector3.zero, new Vector3(1.6f, 0f, 1.3f), new Vector3(-1.6f, 0f, 1.3f), new Vector3(-1.6f, 0f, -2.0f) };
    const float FidgetR = 0.5f; // 서로 3.2m 이상 떨어진 자리에서 반경 0.5 잔걸음 → 2m 유지

    const float StepTime    = Cell / RunSpeed;
    const float NeedBefore  = 0.3f;
    const float NeedAfter   = 0.6f;
    const float SwitchLead  = 0.45f;
    const float MinSwitchGap = 1.2f;
    const float WaitStep = 0.3f, MaxWait = 3f;

    const float ChaserSpeed = 4f, Retarget = 0.5f, ChaserR = 0.8f, ChaserMinGap = 3.5f;

    static List<Bounds> _rock;

    [MenuItem("Tools/Trailer/v2/T.Stage5")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "DoorController");
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        // ── 문 → 칸 사이 변 ──
        var edgeColor = new Dictionary<((int, int), (int, int)), int>();
        var groups = new List<TrailerDoorSwitcher.Group>();
        for (int g = 0; g < GroupNames.Length; g++)
        {
            Transform root = TrailerShotKit.Find(scene, Doors + GroupNames[g]).transform;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroupMats[g]);
            var list = new List<DoorController>();
            foreach (Transform d in root)
            {
                var dc = d.GetComponent<DoorController>();
                if (dc == null) continue;
                list.Add(dc);
                var r = d.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
                edgeColor[EdgeOf(d.position)] = g;
            }
            groups.Add(new TrailerDoorSwitcher.Group { doors = list.ToArray(), neverOpens = g == 0 });
        }
        _rock = new List<Bounds>();
        foreach (Renderer r in TrailerShotKit.Find(scene, "PinkWall").GetComponentsInChildren<Renderer>()) _rock.Add(r.bounds);

        // ── 2층 패드 구간 ──
        var clusters = new List<(Vector3 center, Dictionary<int, Vector3> pad)>();
        foreach (Transform zone in TrailerShotKit.Find(scene, Pads).transform)
        {
            var pad = new Dictionary<int, Vector3>();
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (Transform p in zone)
            {
                int g = System.Array.IndexOf(GroupNames, p.name.Substring(p.name.LastIndexOf('_') + 1));
                if (g == 0) p.gameObject.SetActive(false); // 러너 색 패드는 숨김(열 문이 없다)
                if (g >= 0) pad[g] = new Vector3(p.position.x, Floor2Y, p.position.z);
                sum += p.position; n++;
            }
            Vector3 c = sum / n; c.y = Floor2Y;
            clusters.Add((c, pad));
        }

        Plan best = Search(edgeColor);
        if (best == null) { Debug.LogError("[Trailer v2] T5 경로 없음"); return; }

        var switcher = TrailerShotKit.Group("DoorSwitcher", shot.Root).AddComponent<TrailerDoorSwitcher>();
        switcher.groups = groups.ToArray();
        switcher.openAtStart = best.initialColor;
        foreach (var s in best.switches) shot.Call(switcher, nameof(TrailerDoorSwitcher.Switch), s.t, s.color);

        // ── 러너(1차와 같음) ──
        Vector3 CellPos((int, int) c) => new Vector3(Origin + Cell * c.Item1, GroundY, Origin + Cell * c.Item2);
        var moves = best.moves;
        Vector3 RunnerAt(float t)
        {
            Vector3 p = CellPos(best.start);
            foreach (var m in moves)
            {
                if (t < m.depart) return p;
                if (t < m.depart + StepTime) return Vector3.Lerp(CellPos(m.from), CellPos(m.to), (t - m.depart) / StepTime);
                p = CellPos(m.to);
            }
            return p;
        }
        float FacingAt(float t)
        {
            for (int k = 0; k < moves.Count; k++)
            {
                var m = moves[k];
                if (t < m.depart && k > 0) return Heading(CellPos(moves[k - 1].from), CellPos(moves[k - 1].to));
                if (t < m.depart + StepTime) return Heading(CellPos(m.from), CellPos(m.to));
            }
            var last = moves[moves.Count - 1];
            return Heading(CellPos(last.from), CellPos(last.to));
        }
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var runner = TrailerShotKit.MakeRunner(group, "B", RunnerAt(0f), FacingAt(0f));
        bool running = false;
        for (int k = 0; k < moves.Count; k++)
        {
            var m = moves[k];
            if (!running) { if (m.depart < 0.001f) runner.StartWith("Run"); else runner.Pose(m.depart, "Run"); running = true; }
            float arrive = m.depart + StepTime;
            bool next = k + 1 < moves.Count && moves[k + 1].depart <= arrive + 0.01f;
            if (!next && arrive < Total) { runner.Pose(arrive, "Idle"); running = false; }
        }
        var keys = new List<(float, Vector3, float)>();
        float bodyYaw = FacingAt(0f);
        for (int f = 0; f <= Mathf.RoundToInt(Total * TrailerShotKit.Fps); f++)
        {
            float t = f / TrailerShotKit.Fps;
            float h = FacingAt(t);
            bodyYaw = Mathf.LerpAngle(bodyYaw, h, 1f - Mathf.Exp(-(1f / TrailerShotKit.Fps) / 0.06f));
            keys.Add((t, RunnerAt(t), bodyYaw));
        }
        runner.Follow(keys);
        runner.Build(shot);

        // ── 길잡이 ──
        var log = new System.Text.StringBuilder();
        int Nearest(Vector3 p)
        {
            int best_ = 0; float d = float.MaxValue;
            for (int i = 0; i < clusters.Count; i++) { float dd = TrailerV2.Flat(clusters[i].center - p).magnitude; if (dd < d) { d = dd; best_ = i; } }
            return best_;
        }
        // 길잡이 셋은 컷 동안 러너가 다니는 곳에 가장 가까운 구간 하나에 모여 있다(어느 구간 패드든 같은 색 문을 연다).
        //   미로 가장자리 구간이면 바깥벽(높이 80)이 화면 반을 덮어서, 러너가 컷 후반에 있는 곳(3/4 지점) 기준으로 고른다.
        int cl = Nearest(RunnerAt(Total * 0.75f));
        log.Append($"길잡이 구간 Z{cl + 1} {clusters[cl].center:F0} | ");
        var guides = new List<(string key, TrailerShotKit.Moves m)>();
        string guideFail = null;
        for (int g = 1; g <= 3; g++)
        {
            var rng = new System.Random(0x55 + g * 13);
            Vector3 home = clusters[cl].center + StandOff[g];
            var m = new TrailerShotKit.Moves(home);
            float free = 0f;
            foreach (var s in best.switches)
            {
                if (s.color != g || s.t > Total) continue;
                int target = cl;
                Vector3 padC = clusters[target].pad[g];
                // 패드 트리거(반 0.9 + 몸 0.75)에 들어가는 순간이 s.t가 되게 출발
                Vector3 from = m.End;
                Vector3 dir = TrailerV2.Flat(padC - from).normalized;
                Vector3 stop = padC - dir * 0.3f;
                float enterDist = Mathf.Max(0f, TrailerV2.Flat(padC - from).magnitude - 1.65f);
                float depart = s.t - enterDist / RunSpeed;
                if (depart < free) { guideFail = $"{GuideKey[g]} {s.t:F2}s 패드까지 못 감(출발 {depart:F2} < {free:F2})"; break; }
                if (depart - free > 1.2f) m.Fidget(rng, free, depart - 0.15f, m.End, FidgetR);
                float t1 = m.RunTo(depart, stop, RunSpeed);
                // 누르고 나면 자기 자리로 물러난다(컷 안에 끝나는 이동만 — 끝을 넘기면 Runner.Build의 마지막 키가 순간이동처럼 당겨진다)
                float leave = t1 + TrailerV2.Rand(rng, 0.15f, 0.4f);
                Vector3 back = home + new Vector3(TrailerV2.Rand(rng, -0.3f, 0.3f), 0f, TrailerV2.Rand(rng, -0.3f, 0.3f));
                free = leave + TrailerV2.Flat(back - stop).magnitude / RunSpeed > Total - 0.1f ? t1 : m.RunTo(leave, back, RunSpeed) + 0.1f;
                log.Append($"{GuideKey[g]} {s.t:F2}s | ");
            }
            if (guideFail != null) break;
            if (free < Total - 1f) m.Fidget(rng, free, Total, m.End, FidgetR);
            guides.Add((GuideKey[g], m));
        }
        if (guideFail != null) { Debug.LogError("[Trailer v2] T5 길잡이 실패: " + guideFail); return; }
        string gap = TrailerV2.CheckGaps(guides, Total, 2f);
        if (gap != null) { Debug.LogError("[Trailer v2] T5 길잡이 겹침: " + gap); return; }
        foreach (var (key, m) in guides)
        {
            var k = TrailerShotKit.MakeRunner(group, key, m.start, TrailerShotKit.Yaw(TrailerV2.Flat(clusters[0].center - m.start)) + 180f);
            m.ApplyTo(k);
            k.Build(shot);
        }

        // ── 체이서 ──
        var chaserTracks = PlaceChasers(RunnerAt, 2, out string chaserLog);
        if (chaserTracks == null) { Debug.LogError("[Trailer v2] T5 체이서 자리 없음"); return; }
        Transform chaserGroup = TrailerShotKit.Group("Chasers", shot.Root).transform;
        AnimationClip chaserRun = TrailerShotKit.LoadClip("Assets/NoAI/Chaser/Chaser.fbx", "아마튜어|Run");
        for (int c = 0; c < chaserTracks.Count; c++)
        {
            var tr = chaserTracks[c];
            Animator rig = TrailerShotKit.Rig("Chaser_" + c, chaserGroup, tr[0].p, Quaternion.identity);
            GameObject model = TrailerShotKit.Spawn("Assets/Prefab/식도/Chaser.prefab", "Model", rig.transform, tr[0].p, Quaternion.identity);
            var anim = model.GetComponent<Animator>();
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            AnimationClip clip = shot.NewClip(rig.name + "_Path");
            var xs = new List<(float, float)>(); var zs = new List<(float, float)>(); var ys = new List<(float, float)>();
            float yaw = 0f;
            for (int i = 0; i < tr.Count; i++)
            {
                Vector3 d = i + 1 < tr.Count ? tr[i + 1].p - tr[i].p : tr[i].p - tr[i - 1].p;
                float y = TrailerShotKit.Yaw(TrailerV2.Flat(d));
                yaw = i == 0 ? y : TrailerShotKit.Unwrap(yaw, y);
                xs.Add((tr[i].t, tr[i].p.x)); zs.Add((tr[i].t, tr[i].p.z)); ys.Add((tr[i].t, yaw));
            }
            TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Linear(xs), TrailerShotKit.Const(GroundY, Total), TrailerShotKit.Linear(zs));
            TrailerShotKit.SetEuler(clip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Smooth(ys), TrailerShotKit.Const(0f, Total));
            shot.BindWhole(rig, clip);
            shot.BindSequence(anim, rig.name + " Body", new List<(AnimationClip, float)> { (chaserRun, 0f) }, 0.12f, c * 0.37f);
        }

        // ── 카메라: 길잡이와 러너의 가운데 위에서 거의 수직으로, 천천히 돈다.
        //    비스듬하면 문(높이 15, 열린 문 9~24)이 칸 바닥을 가려 러너가 안 보인다. ──
        Vector3 GuideC(float t)
        {
            Vector3 s = Vector3.zero; int n = 0;
            foreach (var (_, m) in guides) { s += m.At(Mathf.Clamp(t, 0f, Total)); n++; }
            return s / n;
        }
        Vector3 Mid(float t)
        {
            Vector3 s = Vector3.zero; int n = 0;
            for (float d = -1f; d <= 1f; d += 0.1f, n++) { float q = Mathf.Clamp(t + d, 0f, Total); s += Vector3.Lerp(GuideC(q), RunnerAt(q), 0.5f); }
            return s / n;
        }
        // 거의 수직(87°) — 비스듬하면 바깥벽(높이 80) 안쪽 면이 화면을 덮는다. 바깥 천장(y 80) 바로 아래 y 78.
        System.Func<float, float> Spin = t => Mathf.Lerp(-20f, 25f, TrailerShotKit.SmootherStep(t / Total));
        System.Func<float, Vector3> camPos = t => { Vector3 mid = Mid(t); return new Vector3(mid.x, 78f, mid.z); };
        System.Func<float, Vector3> camLook = t => { Vector3 mid = Mid(t); return new Vector3(mid.x, 0f, mid.z) + Quaternion.Euler(0f, Spin(t), 0f) * new Vector3(0f, 0f, 4f); };
        TrailerV2.AddFuncCamera(shot, camPos, camLook, new List<(float, float)> { (0f, 50f), (Total, 44f) });
        Debug.Log($"[Trailer v2] T5 카메라 끝 {camPos(Total):F1} 길잡이 중심 {GuideC(Total):F1} 러너 {RunnerAt(Total):F1} 바라봄 {camLook(Total):F1}");

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);
        var sb = new System.Text.StringBuilder($"[Trailer v2] T5 저장 — 시작 열림 {GroupNames[best.initialColor]} | ");
        foreach (var m in moves) sb.Append($"{m.from}→{m.to} {m.depart:F1}s {GroupNames[edgeColor[Key(m.from, m.to)]]} | ");
        sb.Append("\n전환: ");
        foreach (var s in best.switches) sb.Append($"{s.t:F2}s→{GroupNames[s.color]} ");
        sb.Append("\n" + log + "\n" + chaserLog);
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 러너 뒤쪽에서 다가오는 체이서 count마리. 0.5초마다 그때 러너 위치를 향해 직선 4m/s(문 관통), 바위벽(PinkWall)·외벽은 못 지남.
    /// 후보 자리를 돌려 보고 컷 동안 러너와 3.5m 이상, 처음엔 화면 안(러너에서 12~24m)인 것만 고른다.
    /// </summary>
    static List<List<(float t, Vector3 p)>> PlaceChasers(System.Func<float, Vector3> runnerAt, int count, out string log)
    {
        log = "";
        var rng = new System.Random(0xC4A5);
        var result = new List<List<(float, Vector3)>>();
        Vector3 r0 = runnerAt(0f), r1 = runnerAt(1.5f);
        Vector3 ahead = TrailerV2.Flat(r1 - r0).sqrMagnitude > 0.01f ? TrailerV2.Flat(r1 - r0).normalized : Vector3.forward;
        for (int attempt = 0; attempt < 4000 && result.Count < count; attempt++)
        {
            float ang = TrailerV2.Rand(rng, 100f, 260f);
            float dist = TrailerV2.Rand(rng, 16f, 34f);
            Vector3 start = r0 + Quaternion.Euler(0f, ang, 0f) * ahead * dist; start.y = GroundY;
            if (start.x < -10f || start.x > 106f || start.z < -10f || start.z > 106f) continue;
            if (Blocked(start)) continue;
            bool near = false;
            foreach (var o in result) if (TrailerV2.Flat(o[0].Item2 - start).magnitude < 7f) near = true;
            if (near) continue;
            var track = new List<(float, Vector3)> { (0f, start) };
            Vector3 p = start, aim = r0;
            bool ok = true;
            for (float t = 0f; t < Total; )
            {
                if (Mathf.Repeat(t + 1e-4f, Retarget) < 0.02f) aim = runnerAt(t);
                float dt = 0.02f;
                Vector3 d = TrailerV2.Flat(aim - p);
                Vector3 step = d.magnitude > 0.05f ? d.normalized * Mathf.Min(ChaserSpeed * dt, d.magnitude) : Vector3.zero;
                Vector3 np = p + step;
                // 바위벽에 막히면 NavMesh처럼 벽을 따라 미끄러진다(남은 축으로만, 속도는 그대로)
                if (Blocked(np)) np = p + new Vector3(Mathf.Sign(step.x) * step.magnitude, 0f, 0f);
                if (Blocked(np)) np = p + new Vector3(0f, 0f, Mathf.Sign(step.z) * step.magnitude);
                if (Blocked(np)) { ok = false; break; }
                t += dt;
                p = np;
                if (TrailerV2.Flat(p - runnerAt(t)).magnitude < ChaserMinGap) { ok = false; break; }
                foreach (var o in result) if (TrailerV2.Flat(p - At(o, t)).magnitude < ChaserR * 2f + 1f) { ok = false; break; }
                if (!ok) break;
                if (Mathf.Abs(Mathf.Repeat(t, 0.1f)) < 0.019f) track.Add((t, p));
            }
            if (!ok) continue;
            track.Add((Total, p));
            result.Add(track);
            log += $"체이서 {result.Count}: {start:F0}→{p:F0}, 끝 거리 {TrailerV2.Flat(p - runnerAt(Total)).magnitude:F1}m | ";
        }
        return result.Count == count ? result : null;
    }

    static Vector3 At(List<(float t, Vector3 p)> track, float t)
    {
        for (int i = 1; i < track.Count; i++)
            if (t <= track[i].t) return Vector3.Lerp(track[i - 1].p, track[i].p, Mathf.InverseLerp(track[i - 1].t, track[i].t, t));
        return track[track.Count - 1].p;
    }

    static bool Blocked(Vector3 p)
    {
        if (p.x < -11f + ChaserR || p.x > 107f - ChaserR || p.z < -11f + ChaserR || p.z > 107f - ChaserR) return true;
        foreach (Bounds b in _rock)
            if (p.x > b.min.x - ChaserR && p.x < b.max.x + ChaserR && p.z > b.min.z - ChaserR && p.z < b.max.z + ChaserR) return true;
        return false;
    }

    static float Heading(Vector3 a, Vector3 b) { Vector3 d = b - a; d.y = 0f; return d.sqrMagnitude < 1e-6f ? 0f : TrailerShotKit.Yaw(d); }

    static ((int, int), (int, int)) Key((int, int) a, (int, int) b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    static ((int, int), (int, int)) EdgeOf(Vector3 p)
    {
        float fx = (p.x - Origin) / Cell, fz = (p.z - Origin) / Cell;
        bool xOnCenter = Mathf.Abs(fx - Mathf.Round(fx)) < 0.2f;
        if (xOnCenter) { int i = Mathf.RoundToInt(fx); int j = Mathf.FloorToInt(fz); return Key((i, j), (i, j + 1)); }
        int jj = Mathf.RoundToInt(fz); int ii = Mathf.FloorToInt(fx); return Key((ii, jj), (ii + 1, jj));
    }

    static readonly (int, int)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    sealed class Plan
    {
        public (int, int) start, cell, dir;
        public float t, score, waited;
        public int initialColor = -1, openColor = -1;
        public float openUntil = float.NegativeInfinity, lastSwitch = float.NegativeInfinity;
        public HashSet<(int, int)> visited = new();
        public List<((int, int) from, (int, int) to, float depart)> moves = new();
        public List<(float t, int color)> switches = new();
        public int[] perColor = new int[6];

        public Plan Clone()
        {
            var p = (Plan)MemberwiseClone();
            p.visited  = new HashSet<(int, int)>(visited);
            p.moves    = new List<((int, int), (int, int), float)>(moves);
            p.switches = new List<(float, int)>(switches);
            p.perColor = (int[])perColor.Clone();
            return p;
        }
    }

    /// <summary>1차 빔 탐색과 같다. 다만 전환 색은 길잡이 고유색(1~3)만, 같은 길잡이가 연달아 누르는 간격은 1.2초 이상, 셋 다 한 번씩 누르면 가산점.</summary>
    static Plan Search(Dictionary<((int, int), (int, int)), int> edges)
    {
        int slots = Mathf.CeilToInt(Total / WaitStep) + 8;
        var beams = new List<Plan>[slots];
        for (int s = 0; s < slots; s++) beams[s] = new List<Plan>();
        for (int i = 0; i < 10; i++)
            for (int j = 0; j < 10; j++)
            {
                if (i < 2 && j < 2) continue;
                var p = new Plan { start = (i, j), cell = (i, j) };
                p.visited.Add((i, j));
                beams[0].Add(p);
            }

        Plan best = null;
        for (int s = 0; s < slots; s++)
        {
            var beam = beams[s];
            beam.Sort((a, b) => b.score.CompareTo(a.score));
            if (beam.Count > 800) beam.RemoveRange(800, beam.Count - 800);
            foreach (Plan p in beam)
            {
                bool justArrived = p.moves.Count > 0 && Mathf.Abs(p.moves[p.moves.Count - 1].depart + StepTime - p.t) < 0.01f;
                if (p.t >= Total - 0.6f && justArrived)
                {
                    float final = p.score + (p.perColor[1] > 0 && p.perColor[2] > 0 && p.perColor[3] > 0 ? 3f : 0f);
                    if (best == null || final > best.score) { best = p.Clone(); best.score = final; }
                    continue;
                }
                if (p.t >= Total) continue;
                if (p.moves.Count > 0 && p.waited + WaitStep <= MaxWait)
                {
                    var w = p.Clone(); w.t += WaitStep; w.waited += WaitStep; w.score -= 0.5f;
                    beams[Mathf.Min(slots - 1, s + 1)].Add(w);
                }
                foreach (var d in Dirs)
                {
                    if (justArrived && d.Item1 == -p.dir.Item1 && d.Item2 == -p.dir.Item2) continue;
                    var nx = (p.cell.Item1 + d.Item1, p.cell.Item2 + d.Item2);
                    if (nx.Item1 < 0 || nx.Item2 < 0 || nx.Item1 > 9 || nx.Item2 > 9) continue;
                    if (nx.Item1 < 2 && nx.Item2 < 2) continue;
                    if (p.visited.Contains(nx)) continue;
                    if (!edges.TryGetValue(Key(p.cell, nx), out int color)) continue;
                    if (color < 1 || color > 3) continue; // 러너 색(바위)·흑·백은 안 연다

                    float tc = p.t + StepTime * 0.5f;
                    var q = p.Clone();
                    if (p.openColor < 0)
                    {
                        q.initialColor = q.openColor = color;
                    }
                    else if (color != p.openColor)
                    {
                        float ts = tc - SwitchLead;
                        if (ts < p.openUntil || ts - p.lastSwitch < MinSwitchGap || ts < 0.6f) continue;
                        q.switches.Add((ts, color));
                        q.perColor[color]++;
                        q.openColor = color; q.lastSwitch = ts;
                        q.score += 2f;
                    }
                    else if (tc - NeedBefore < p.lastSwitch + 0.35f) continue;
                    q.openUntil = tc + NeedAfter;
                    q.score += 1f;
                    q.score += !justArrived || d == p.dir ? 0.5f : -0.3f;
                    q.moves.Add((p.cell, nx, p.t));
                    q.visited.Add(nx);
                    q.cell = nx; q.dir = d; q.t = p.t + StepTime;
                    beams[Mathf.Min(slots - 1, s + 4)].Add(q);
                }
            }
        }
        return best;
    }
}
