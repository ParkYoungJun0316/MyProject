using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 M.Stage3 (10초) — ColorTile 점수제. 규칙·값은 1차(TrailerShotMStage3)와 같다:
/// 자기 색 타일(흑·백은 Q로 변신한 사람)에 2초(씬 occupySeconds) 서면 물결(PadRipple) → 점수 → 타일이 다른 스폰으로 순간이동.
/// 흑·백 젤리 벽은 통과하지 않는다(TrailerV2.PlanPath — 벽+0.75 게임 몸 반지름은 절대 금지) + 저장 전 Timeline 실제 위치로 벽 검사.
/// 주인공 파랑: 흑으로 변신 → 십자 통로를 서쪽으로 달려 검정 타일 → 점수 → 고유색 복귀 → 북쪽 자기 타일.
/// 카메라: 파랑 뒤 위에서 가파르게 내려다보며 따라가다, 첫 점수 뒤 위로 빠지며 미로 전체와 다른 셋의 점수를 보여 준다.
/// </summary>
public static class TrailerV2MStage3
{
    const string Stage = "M.Stage3";
    const string Src   = "Assets/Scenes/M.Stage3.unity";
    const float  Total = 10f;
    const float  GroundY = 0.5f;
    const float  StandTime = 2f;
    const float  Speed = 8.5f;
    // 벽(젤리 렌더러 경계)에서 몸 반지름 0.75(게임 Kkultteok CapsuleCollider)는 절대 금지, 0.85는 되도록 피함.
    // 통로 폭 2.3m라 지날 수 있는 띠는 가운데 ±0.4m뿐 — 출발점도 그 안에 둔다(1차 도구는 띠 밖 출발점에서 벽 속까지 통과시켰다: 노랑).
    const float  BodyRadius = 0.75f;
    const float  PathSoft   = 0.85f;
    const string SpawnRoot = "Stage3.1/StageManager3.1/ColorTileChallenge/Spawn/";
    const string WallRoot  = "Stage3.1/StageManager3.1/Wall";

    static Scene _scene;
    static List<Bounds> _walls;
    static Vector3 S(int i) => TrailerShotKit.Find(_scene, SpawnRoot + "Spawn" + i).transform.position;
    static Vector3 G(int i) { Vector3 p = S(i); return new Vector3(p.x, GroundY, p.z); }

    sealed class Tile
    {
        public string key; public int spawn;
        public readonly List<(float score, int next)> moves = new();
        public readonly List<(float, float)> ripple = new();
    }

    [MenuItem("Tools/Trailer/v2/M.Stage3")]
    public static void Build()
    {
        _scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage));
        _walls = new List<Bounds>();
        foreach (Renderer r in TrailerShotKit.Find(_scene, WallRoot).GetComponentsInChildren<Renderer>()) _walls.Add(r.bounds);
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        var tiles = new Dictionary<string, Tile>
        {
            ["Black"] = new Tile { key = "Black", spawn = 12 }, // (-10,0) 파랑이 서쪽 통로 끝에서 밟는다
            ["B"]     = new Tile { key = "B",     spawn = 1  }, // (-10,10)
            ["P"]     = new Tile { key = "P",     spawn = 9  }, // (0,10)
            ["G"]     = new Tile { key = "G",     spawn = 16 }, // (10,0)
            ["Y"]     = new Tile { key = "Y",     spawn = 18 }, // (-5,-10)
            ["White"] = new Tile { key = "White", spawn = 5  }, // (10,5)
        };

        // (꿀떡, 시작, 첫 동작 시각, [(타일, 점수 뒤 옮겨 갈 스폰)]) — 파랑이 먼저(주인공), 나머지는 파랑을 피해 출발을 늦춘다
        var plans = new (string key, Vector3 start, float yaw, float t0, (string tile, int next)[] visits)[]
        {
            ("B", new Vector3(-2.3f, GroundY, -0.2f), 262f, 0.45f, new[] { ("Black", 23), ("B", 20) }),
            ("P", new Vector3( 0.25f, GroundY,  2.7f),  14f, 0.75f, new[] { ("P", 33), ("White", 29) }),
            ("G", new Vector3( 2.9f,  GroundY,  0.3f),  97f, 0.95f, new[] { ("G", 31), ("G", 2) }),
            ("Y", new Vector3(-0.3f,  GroundY, -3.1f), 171f, 0.6f,  new[] { ("Y", 27), ("Y", 10) }),
        };

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var planned = new List<Track>();
        Track hero = null;
        for (int i = 0; i < plans.Length; i++)
        {
            var plan = plans[i];
            var k = TrailerShotKit.MakeRunner(group, plan.key, plan.start, plan.yaw);
            var me = new Track(plan.start);
            float t = plan.t0;
            string body = TrailerShotKit.BodyMat(plan.key);
            foreach (var (tileKey, next) in plan.visits)
            {
                Tile tile = tiles[tileKey];
                Vector3 at = G(tile.moves.Count > 0 ? tile.moves[tile.moves.Count - 1].next : tile.spawn);
                string need = tileKey == "Black" ? "Assets/Mat/Player/Black.mat"
                            : tileKey == "White" ? "Assets/Mat/Player/White.mat"
                            : TrailerShotKit.BodyMat(plan.key);
                if (need != body) { t = k.ChangeColor(t, need); body = need; }
                var path = TrailerV2.PlanPath(k.Position, at, _walls, PathSoft, BodyRadius, 15f);
                int tries = 0;
                while (tries < 20 && Conflicts(me, t, path, planned)) { t += 0.15f; tries++; }
                if (tries == 20 || t > Total) break;
                me.Add(t, path);
                float arrived = k.RunVia(t, Speed, path);
                float score = arrived + StandTime;
                if (score > Total - 0.4f) break;
                tile.ripple.Add((arrived, score));
                tile.moves.Add((score, next));
                Debug.Log($"[Trailer v2] M3 {plan.key} → {tileKey} 도착 {arrived:F2} 점수 {score:F2}");
                t = score + 0.25f;
            }
            planned.Add(me);
            if (i == 0) hero = me;
            k.Build(shot);
        }

        foreach (Tile t in tiles.Values) BuildTile(shot, t);
        BuildCamera(shot, hero);

        string wallHit = TrailerV2.CheckWallsByTimeline(shot, _walls, BodyRadius - 0.02f); // 곡선 보간 오차 2cm만 허용
        if (wallHit != null) { Debug.LogError("[Trailer v2] M3 벽 통과: " + wallHit); return; }
        Debug.Log("[Trailer v2] M3 벽 검사 통과");

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(_scene);
    }

    /// <summary>파랑 뒤를 따라가다(진행 방향 반대쪽 4.5m·높이 12 — 통로 폭 2.3m라 낮으면 벽이 화면을 덮어 가파르게 내려다봄) 첫 점수 뒤 미로 위로 빠진다.</summary>
    static void BuildCamera(TrailerShotKit.Shot shot, Track hero)
    {
        const float liftFrom = 4.4f, liftTo = 8.6f;
        Vector3 Smoothed(float t)
        {
            Vector3 s = Vector3.zero; int n = 0;
            for (float d = -0.35f; d <= 0.35f; d += 0.05f) { s += hero.At(Mathf.Clamp(t + d, 0f, Total)); n++; }
            return s / n;
        }
        // 진행 방향: 0.6초 앞 위치 - 지금 위치(멈춰 있으면 이전 방향 유지)
        var heading = new Vector3[Mathf.RoundToInt(Total * TrailerShotKit.Fps) + 1];
        Vector3 last = Vector3.left;
        for (int i = 0; i < heading.Length; i++)
        {
            float t = i / TrailerShotKit.Fps;
            Vector3 d = Smoothed(t + 0.6f) - Smoothed(t); d.y = 0f;
            if (d.magnitude > 0.4f) last = Vector3.Slerp(last, d.normalized, 0.12f).normalized;
            heading[i] = last;
        }
        Vector3 H(float t) => heading[Mathf.Clamp(Mathf.RoundToInt(t * TrailerShotKit.Fps), 0, heading.Length - 1)];

        Vector3 highPos = new Vector3(-6f, 22f, -11f), highLook = new Vector3(-2.5f, 0f, 2f);
        TrailerV2.AddFuncCamera(shot,
            t =>
            {
                Vector3 follow = Smoothed(t) - H(t) * 4.5f + Vector3.up * 12f;
                float u = TrailerShotKit.SmootherStep((t - liftFrom) / (liftTo - liftFrom));
                return Vector3.Lerp(follow, highPos, u);
            },
            t =>
            {
                Vector3 follow = Smoothed(t) + H(t) * 2f;
                float u = TrailerShotKit.SmootherStep((t - liftFrom) / (liftTo - liftFrom));
                return Vector3.Lerp(follow, highLook, u);
            });
    }

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
        var probe = new Track(me.Last);
        probe.Add(t0, path);
        float len = 0f; Vector3 prev = me.Last;
        foreach (Vector3 p in path) { len += Vector3.Distance(prev, p); prev = p; }
        float end = t0 + len / Speed + StandTime;
        for (float s = 0f; s <= end; s += 0.05f)
            foreach (Track o in others)
                if (Vector3.Distance(s < t0 ? me.At(s) : probe.At(s), o.At(s)) < 2f) return true;
        return false;
    }

    static void BuildTile(TrailerShotKit.Shot shot, Tile t)
    {
        Vector3 at = S(t.spawn);
        Animator rig = TrailerShotKit.Rig("Tile_" + t.key, shot.Root, at, Quaternion.identity);
        TrailerShotKit.Spawn($"Assets/Prefab/ColorTile_{t.key}.prefab", "Tile", rig.transform, at, Quaternion.identity);
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
