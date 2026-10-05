using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Stage5 컷 (12초). 1층 러너(Blue)가 10×10 문 미로를 3인칭으로 달린다. 2층 길잡이는 안 보인다.
/// 값은 게임 그대로:
///   격자 칸 12m(복도 10 + 문 2), 문 = DoorController SlideUp 9m · 0.35초(SmoothStep) — Open/Close를 그대로 호출.
///   ColorGateController 규칙: 한 번에 한 색만 열림, 다른 색을 열면 나머지는 닫힘. 러너 색(Blue) 문은 바위(Common) — 절대 안 열림.
///   전환 = 2층 길잡이가 그 색 발판을 밟을 때 — 러너 앞 문 색을 골라 연다(전환 간격 최소 1.2초).
///   러너 speed 10. 카메라 = 게임 LocalPlayerCamera(distance 5, targetOffset y2), pitch 20(앞을 보며 달림).
/// 경로는 탐색으로 고른다: 문을 지나기 0.3초 전부터 카메라(4.7m 뒤)가 그 문을 지날 때까지 그 색이 열려 있어야 한다.
/// 색이 바뀌는 문 앞에선 칸 중앙에서 잠깐 기다린다(게임 러너가 실제로 하는 것).
/// </summary>
public static class TrailerShotTStage5
{
    const string Src      = "Assets/Scenes/T.Stage5.unity";
    const string Dst      = "Assets/Scenes/Marketing_T.Stage5.unity";
    const string AssetDir = "Assets/Trailer/T.Stage5";
    const string ShotName = "Trailer_T.Stage5";
    const string Doors    = "StageManager5/T5_Maze/Doors/";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    const float Cell = 12f, Origin = -6f, RunSpeed = 10f;
    // 게임 카메라 = LocalPlayerCamera.prefab ThirdPersonCamera(distance 5, targetOffset y2, initialPitch 55, 범위 −85~80).
    // (씬의 비활성 Main Camera 값 12/y5/40은 쓰이지 않는다.) pitch는 플레이어가 마우스로 정한다 — 55면 바닥만 보여
    // 앞 문이 안 보이므로, 달리며 앞을 보는 20°. 높이 2.5+1.7 = 4.2 → 열린 문 바닥(9) 아래로 지난다.
    const float CamDist = 5f, CamPitch = 20f;
    static readonly Vector3 CamOffset = new Vector3(0f, 2f, 0f);

    // 그룹 순서 = 씬 Doors 자식 이름. 0번(Blue) = 러너 색 → 바위
    static readonly string[] GroupNames = { "Blue", "Purple", "Green", "Yellow", "Black", "White" };
    static readonly string[] GroupMats  = { "Assets/Mat/Ground/Boulder2.mat", "Assets/Mat/Player/Guma.mat", "Assets/Mat/Player/Sook.mat",
                                            "Assets/Mat/Player/Dan.mat", "Assets/Mat/Player/Black.mat", "Assets/Mat/Player/White.mat" };

    const float StepTime    = Cell / RunSpeed;          // 칸 하나 1.2초
    const float NeedBefore  = 0.3f;                     // 문을 지나기 전 다 열려 있어야 하는 여유
    const float NeedAfter   = 0.6f;                     // 카메라(수평 4.7m 뒤)가 그 문을 지날 때까지 + 여유
    const float SwitchLead  = 0.45f;                    // 길잡이가 밟는 시각 = 러너가 문에 닿기 0.45초 전(0.35초 뒤 다 열림)
    const float MinSwitchGap = 1.2f;                    // 길잡이 발판 사이 이동
    const float WaitStep = 0.3f, MaxWait = 3f;

    [MenuItem("Tools/Trailer/T.Stage5 러너 문 미로")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, Dst, "DoorController");
        var shot = new TrailerShotKit.Shot(AssetDir, ShotName, Total);

        // ── 문 → 칸 사이 변(edge) ──
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
                if (r != null) r.sharedMaterial = mat; // ColoredDoorVisual.Apply 결과(러너 Blue 기준)
                edgeColor[EdgeOf(d.position)] = g;
            }
            groups.Add(new TrailerDoorSwitcher.Group { doors = list.ToArray(), neverOpens = g == 0 });
        }

        Plan best = Search(edgeColor);
        if (best == null) { Debug.LogError("[Trailer] T5 경로 없음"); return; }

        var switcher = TrailerShotKit.Group("DoorSwitcher", shot.Root).AddComponent<TrailerDoorSwitcher>();
        switcher.groups = groups.ToArray();
        switcher.openAtStart = best.initialColor;
        foreach (var s in best.switches) shot.Call(switcher, nameof(TrailerDoorSwitcher.Switch), s.t, s.color);

        // ── 러너: 칸 중심을 잇는 등속 직선(게임 이동 = 즉시 10m/s), 기다릴 땐 다음 문을 보고 선다 ──
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
            // 기다리는 동안은 들어온 방향 그대로(제자리 회전 없음), 출발하면서 돈다
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
        float bodyYaw = FacingAt(0f), camYaw = bodyYaw;
        var camYaws = new List<float>();
        for (int f = 0; f <= Mathf.RoundToInt(Total * TrailerShotKit.Fps); f++)
        {
            float t = f / TrailerShotKit.Fps;
            float h = FacingAt(t);
            bodyYaw = Mathf.LerpAngle(bodyYaw, h, 1f - Mathf.Exp(-(1f / TrailerShotKit.Fps) / 0.06f));
            camYaw  = Mathf.LerpAngle(camYaw,  h, 1f - Mathf.Exp(-(1f / TrailerShotKit.Fps) / 0.35f)); // 마우스로 따라 돌리는 느낌
            keys.Add((t, RunnerAt(t), bodyYaw));
            camYaws.Add(camYaw);
        }
        runner.Follow(keys);
        runner.Build(shot);

        // ── 카메라: 게임 ThirdPersonCamera 수식 ──
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        System.Func<float, float> Yaw = t => camYaws[Mathf.Clamp(Mathf.RoundToInt(t * TrailerShotKit.Fps), 0, camYaws.Count - 1)];
        Vector3 CamPos(float t) => RunnerAt(t) + CamOffset + Quaternion.Euler(CamPitch, Yaw(t), 0f) * (Vector3.back * CamDist);
        AnimationClip camClip = shot.NewClip("Camera_Follow");
        TrailerShotKit.SetPosition(camClip, "", TrailerShotKit.Sampled(Total, t => CamPos(t).x), TrailerShotKit.Sampled(Total, t => CamPos(t).y), TrailerShotKit.Sampled(Total, t => CamPos(t).z));
        TrailerShotKit.SetEuler(camClip, "", TrailerShotKit.Const(CamPitch, Total), TrailerShotKit.Sampled(Total, Yaw), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(cam.GetComponent<Animator>(), camClip);

        shot.AddRecorder(ShotName);
        shot.Save(scene);
        var sb = new System.Text.StringBuilder($"시작 열림 {GroupNames[best.initialColor]} | ");
        foreach (var m in moves) sb.Append($"{m.from}→{m.to} 출발 {m.depart:F1}s {GroupNames[edgeColor[Key(m.from, m.to)]]} | ");
        sb.Append("\n전환: ");
        foreach (var s in best.switches) sb.Append($"{s.t:F2}s→{GroupNames[s.color]} ");
        Debug.Log($"[Trailer] {Dst} 생성 완료 — 점수 {best.score:F1}, 이동 {moves.Count}칸, 대기 {best.waited:F1}s\n{sb}");
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

        public Plan Clone()
        {
            var p = (Plan)MemberwiseClone();
            p.visited  = new HashSet<(int, int)>(visited);
            p.moves    = new List<((int, int), (int, int), float)>(moves);
            p.switches = new List<(float, int)>(switches);
            return p;
        }
    }

    /// <summary>빔 탐색: 0.3초 단위 시간축에서 "기다림(0.3초)" 또는 "옆 칸으로 1.2초 이동"을 펼친다.</summary>
    static Plan Search(Dictionary<((int, int), (int, int)), int> edges)
    {
        int slots = Mathf.CeilToInt(Total / WaitStep) + 8;
        var beams = new List<Plan>[slots];
        for (int s = 0; s < slots; s++) beams[s] = new List<Plan>();
        for (int i = 0; i < 10; i++)
            for (int j = 0; j < 10; j++)
            {
                if (i < 2 && j < 2) continue; // 시작 방
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
                    if (best == null || p.score > best.score) best = p;
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
                    if (!edges.TryGetValue(Key(p.cell, nx), out int color)) continue; // 문 없는 변 = 시작 방 안쪽뿐
                    if (color == 0) continue;                                         // 러너 색 = 바위

                    float tc = p.t + StepTime * 0.5f;
                    var q = p.Clone();
                    if (p.openColor < 0)
                    {
                        q.initialColor = q.openColor = color; // 컷 시작 전에 이미 열려 있던 색
                    }
                    else if (color != p.openColor)
                    {
                        float ts = tc - SwitchLead;
                        if (ts < p.openUntil || ts - p.lastSwitch < MinSwitchGap || ts < 0.2f) continue;
                        q.switches.Add((ts, color));
                        q.openColor = color; q.lastSwitch = ts;
                        q.score += 2f; // 러너 앞에서 문이 열림
                    }
                    else if (tc - NeedBefore < p.lastSwitch + 0.35f) continue; // 아직 열리는 중
                    q.openUntil = tc + NeedAfter;
                    q.score += 1f;
                    q.score += !justArrived || d == p.dir ? 0.5f : -0.3f; // 직진 선호
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
