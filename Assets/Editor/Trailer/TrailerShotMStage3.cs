using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Stage3 컷 (12초, M 공통 카메라).
/// (a) ColorTile 점수제: 자기 색 타일(흑·백은 아무나)에 2초 서면 물결(PadRipple) → 점수 → 타일이 다른 스폰 자리로 순간이동.
/// (b) 같은 흐름 중 입 닫힘(MouthController, 이 씬 ScreenFader maxAlpha 0.98) → 모여서 외침 → 열림 → 다시 점수.
/// 흑·백 젤리 벽(Wall)은 통과하지 않는다 — 벽 렌더러 경계 + 몸 반지름으로 A* 경로를 잡아 돌아간다.
/// </summary>
public static class TrailerShotMStage3
{
    const string Src       = "Assets/Scenes/M.Stage3.unity";
    const string AssetDir  = "Assets/Trailer/M.Stage3";
    const float  Total     = 12f;
    const float  GroundY   = 0.5f;
    const float  StandTime = 2f;   // 게임: 2초 점유하면 점수
    const float  Speed     = 8f;   // 달리기(m/s)
    const float  BodyRadius = 0.9f;

    const string SpawnRoot = "Stage3.1/StageManager3.1/ColorTileChallenge/Spawn/";
    const string WallRoot  = "Stage3.1/StageManager3.1/Wall";

    [MenuItem("Tools/Trailer/M.Stage3 (a) 플레이")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Stage3 (b) 방해공작")]
    public static void BuildHazard() => Build(true);

    sealed class Tile
    {
        public string key;
        public int spawn;
        public readonly List<(float score, int next)> moves = new();
        public readonly List<(float, float)> ripple = new();
    }

    static Scene _scene;
    static List<Bounds> _walls;
    static Vector3 S(int i) => TrailerShotKit.Find(_scene, SpawnRoot + "Spawn" + i).transform.position;
    static Vector3 G(int i) { Vector3 p = S(i); return new Vector3(p.x, GroundY, p.z); }

    // 입 닫힘 (b)
    const float Close = 4.6f, Hold = 6.6f, Open = 7.6f, Idle = 9.6f;

    static void Build(bool hazard)
    {
        string suffix   = hazard ? "_b" : "_a";
        string shotName = "Trailer_M.Stage3" + suffix;
        string dst      = $"Assets/Scenes/Marketing_M.Stage3{suffix}.unity";

        _scene = TrailerShotKit.PrepStage(Src, dst);
        _walls = new List<Bounds>();
        foreach (Renderer r in TrailerShotKit.Find(_scene, WallRoot).GetComponentsInChildren<Renderer>()) _walls.Add(r.bounds);

        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, 35f, 0f, 90f, 1f);
        if (hazard) TrailerShotKit.AddMouthClose(shot, _scene, "BackGround/MouthBG", Close, Hold, Open, Idle, 0.98f);

        var tiles = new Dictionary<string, Tile>
        {
            ["B"] = new Tile { key = "B", spawn = 12 },
            ["P"] = new Tile { key = "P", spawn = 9 },
            ["G"] = new Tile { key = "G", spawn = 15 },
            ["Y"] = new Tile { key = "Y", spawn = 7 },
            ["Black"] = new Tile { key = "Black", spawn = 19 },
            ["White"] = new Tile { key = "White", spawn = 28 },
        };

        // 꿀떡별 (밟을 타일, 점수 뒤 그 타일이 옮겨 갈 스폰) — 흑·백은 아무나 밟는다
        var plans = new (string key, Vector3 start, (string tile, int next)[] visits)[]
        {
            // 시작은 가운데 십자 통로(폭 2.3m) 안
            ("B", new Vector3(-3f, GroundY, 0f), new[] { ("B", 20), ("Black", 24), ("B", 3) }),
            ("P", new Vector3(0f, GroundY, 3f),  new[] { ("P", 5), ("White", 11), ("P", 29) }),
            ("G", new Vector3(3f, GroundY, 0f),  new[] { ("G", 2), ("G", 33), ("G", 17) }),
            ("Y", new Vector3(0f, GroundY, -3f), new[] { ("Y", 31), ("Y", 8), ("Y", 21) }),
        };
        // 모이는 자리도 십자 통로 한가운데(벽 모서리는 통로 중심에서 1.15m)
        Vector3[] gather = { new(-1.7f, GroundY, 0f), new(0f, GroundY, 1.7f), new(1.7f, GroundY, 0f), new(0f, GroundY, -1.7f) };

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var planned = new List<Track>(); // 먼저 정해진 사람들의 동선 — 뒤 사람은 이들과 겹치지 않게 출발을 늦춘다
        for (int i = 0; i < plans.Length; i++)
        {
            var plan = plans[i];
            var k = TrailerShotKit.MakeRunner(group, plan.key, plan.start, TrailerShotKit.Yaw(-plan.start));
            var me = new Track(plan.start);
            float t = 0.4f + i * 0.08f;
            bool gathered = false;
            string body = TrailerShotKit.BodyMat(plan.key); // 지금 몸 색
            foreach (var (tileKey, next) in plan.visits)
            {
                Tile tile = tiles[tileKey];
                // (b) 입이 닫히기 전에 끝낼 수 없는 방문은 미루고, 먼저 가운데로 모여 외친다
                Vector3 at = G(tile.moves.Count > 0 ? tile.moves[tile.moves.Count - 1].next : tile.spawn);
                if (hazard && !gathered)
                {
                    float arrive = t + PathLength(k.Position, at) / Speed;
                    if (arrive + StandTime > Close - 0.6f)
                    {
                        t = Mathf.Max(t, Close - 0.9f);
                        var toGather = TrailerShotKit.PlanPath(k.Position, gather[i], _walls, BodyRadius, 15f);
                        me.Add(t, toGather);
                        k.RunVia(t, Speed, toGather);
                        k.Pose(Close + 1.2f, "Yes");
                        // 길목에 선 사람이 먼저 떠난다(노랑→파랑→초록→보라) — 서 있는 사람을 뚫고 지나가지 않게
                        float[] resumeDelay = { 0.35f, 1.05f, 0.7f, 0f };
                        t = Open + 0.4f + resumeDelay[i];
                        gathered = true;
                    }
                }
                // 타일 색에 맞춰 변신해야 밟힌다: 흑·백 칸은 Q로 흑/백, 자기 칸은 Alt로 고유색 (제자리에서 ChangeColor)
                string need = tileKey == "Black" ? "Assets/Mat/Player/Black.mat"
                            : tileKey == "White" ? "Assets/Mat/Player/White.mat"
                            : TrailerShotKit.BodyMat(plan.key);
                if (need != body) { t = k.ChangeColor(t, need); body = need; }
                var path = TrailerShotKit.PlanPath(k.Position, at, _walls, BodyRadius, 15f);
                // 이미 정해진 사람과 1.6m 안으로 겹치면(엇갈려 지나가기 포함) 0.2초씩 늦게 출발
                int tries = 0;
                while (tries < 12 && Conflicts(me, t, path, planned)) { t += 0.2f; tries++; }
                if (tries == 12 || t > Total) break; // 끝까지 길이 막히면 이 이동은 하지 않고 제자리
                me.Add(t, path);
                float arrived = k.RunVia(t, Speed, path);
                float score = arrived + StandTime;
                if (score > Total - 0.3f) break;
                tile.ripple.Add((arrived, score));
                tile.moves.Add((score, next));
                t = score + 0.2f;
            }
            planned.Add(me);
            k.Build(shot);
        }

        foreach (Tile t in tiles.Values) BuildTile(shot, t);

        shot.AddRecorder(shotName);
        shot.Save(_scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 → Recordings/{shotName}.mp4");
    }

    /// <summary>한 사람의 위치 기록(구간별 선형) — 서로 겹침 검사용.</summary>
    sealed class Track
    {
        readonly List<(float t, Vector3 p)> _keys = new();
        public Track(Vector3 start) => _keys.Add((0f, start));
        public Vector3 Last => _keys[_keys.Count - 1].p;
        public void Add(float t0, List<Vector3> path)
        {
            Vector3 prev = Last;
            _keys.Add((t0, prev));
            float t = t0;
            foreach (Vector3 p in path) { t += Vector3.Distance(prev, p) / Speed; _keys.Add((t, p)); prev = p; }
        }
        public Vector3 At(float t)
        {
            if (t <= _keys[0].t) return _keys[0].p;
            for (int i = 1; i < _keys.Count; i++)
                if (t <= _keys[i].t) return Vector3.Lerp(_keys[i - 1].p, _keys[i].p, Mathf.InverseLerp(_keys[i - 1].t, _keys[i].t, t));
            return Last;
        }
    }

    static bool Conflicts(Track me, float t0, List<Vector3> path, List<Track> others)
    {
        // 이 이동 + 도착해서 서 있는 2초 동안
        var probe = new Track(me.Last);
        probe.Add(t0, path);
        float end = t0 + PathLength(me.Last, path) / Speed + StandTime;
        for (float s = t0; s <= end; s += 0.05f)
            foreach (Track o in others)
                if (Vector3.Distance(probe.At(s), o.At(s)) < 1.6f) return true;
        return false;
    }

    static float PathLength(Vector3 from, List<Vector3> path)
    {
        float len = 0f; Vector3 prev = from;
        foreach (Vector3 p in path) { len += Vector3.Distance(prev, p); prev = p; }
        return len;
    }

    static float PathLength(Vector3 from, Vector3 to)
    {
        float len = 0f; Vector3 prev = from;
        foreach (Vector3 p in TrailerShotKit.PlanPath(from, to, _walls, BodyRadius, 15f)) { len += Vector3.Distance(prev, p); prev = p; }
        return len;
    }

    static void BuildTile(TrailerShotKit.Shot shot, Tile t)
    {
        Vector3 at = S(t.spawn);
        Animator rig = TrailerShotKit.Rig("Tile_" + t.key, shot.Root, at, Quaternion.identity);
        TrailerShotKit.Spawn($"Assets/Prefab/ColorTile_{t.key}.prefab", "Tile", rig.transform, at, Quaternion.identity);

        // 순간이동(게임은 transform.position 대입) — 계단 커브
        var xs = new List<(float, float)> { (0f, at.x) }; var ys = new List<(float, float)> { (0f, at.y) }; var zs = new List<(float, float)> { (0f, at.z) };
        foreach ((float score, int next) in t.moves)
        {
            Vector3 p = S(next);
            xs.Add((score, p.x)); ys.Add((score, p.y)); zs.Add((score, p.z));
        }
        AnimationClip clip = shot.NewClip(rig.name + "_Hop");
        TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Steps(xs), TrailerShotKit.Steps(ys), TrailerShotKit.Steps(zs));
        shot.BindWhole(rig, clip);

        if (t.ripple.Count == 0) return;
        GameObject ripple = TrailerShotKit.Spawn("Assets/Art/Particle/PadRipple.prefab", "Ripple", rig.transform, at + Vector3.up * 0.3f, Quaternion.identity);
        shot.BindActive(ripple, t.ripple.ToArray());
    }
}
