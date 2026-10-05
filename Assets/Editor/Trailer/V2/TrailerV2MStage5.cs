using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 M.Stage5 (10.5초) — 5×5 Grid 한 라운드. 붕괴·공개·정산 값은 1차(TrailerShotMStage5)와 같다(씬 GridTileCollapse):
/// 박자 1초마다 4칸 경고 0.8초(SpikeLaneWarnMarker.PlayWarning) → 파괴(FloorTileShards) → 2.5초 뒤 되감기 복구,
/// 공개(4초 뒤)에 안전 칸이 각자 색 → 정산 4.5초 뒤 → 0.5초 뒤 남은 구멍 일괄 복구. 안전 칸·사람이 선 칸은 깨지지 않는다.
/// 1차와 달리 꿀떡은 칸 중심이 아닌 칸 안 아무 데나 서고, 붕괴를 보고 한 번씩 옆 칸으로 비킨다(경고 칸은 피함).
/// 카메라: 판 남서쪽 모서리 바깥 낮은 곳에서 대각선으로 판을 훑으며 올라가, 공개 순간 넷이 자기 색 칸으로 뛰는 걸 비스듬히 내려다본다.
/// </summary>
public static class TrailerV2MStage5
{
    const string Stage = "M.Stage5";
    const string Src   = "Assets/Scenes/M.Stage5.unity";
    const float  Total = 10.5f;
    const float  GroundY = 0.5f;
    const string Grid  = "Stage5.1/StageManager5.1/GridColorChallenge7/";
    const string Warns = "Stage5.1/StageManager5.1/CollapseWarnMarkers/";

    const float FirstWarn = 0.3f, Beat = 1f, WarnLead = 0.8f, BrokenLife = 2.5f, StopBeforeSettle = 1f;
    const int   PerBeat = 4;
    const float PreReveal = 4f, RoundLen = 4.5f, SettleHold = 0.5f;
    static float RevealAt => FirstWarn + PreReveal;
    static float SettleAt => RevealAt + RoundLen;
    static float RestoreAllAt => SettleAt + SettleHold;
    const float Speed = 7f;

    static Vector3 Center(int r, int c) => new Vector3(-10f + 5f * c, GroundY, 10f - 5f * r);
    static int Idx(int r, int c) => r * 5 + c;
    static int TileOf(Vector3 p)
    {
        int c = Mathf.Clamp(Mathf.RoundToInt((p.x + 10f) / 5f), 0, 4);
        int r = Mathf.Clamp(Mathf.RoundToInt((10f - p.z) / 5f), 0, 4);
        return Idx(r, c);
    }

    [MenuItem("Tools/Trailer/v2/M.Stage5")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "SpikeLaneWarnMarker");
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        // (색, 시작 칸, 비키는 칸, 안전 칸, 재질)
        var plan = new (string key, (int r, int c) start, (int r, int c) dodge, (int r, int c) safe, string mat, float yaw)[]
        {
            ("B", (2, 1), (1, 1), (0, 2), "T.Stage4B", 31f),
            ("P", (1, 3), (2, 3), (1, 4), "T.Stage4P", 248f),
            ("G", (3, 3), (3, 2), (4, 3), "T.Stage4G", 117f),
            ("Y", (3, 1), (2, 2), (3, 0), "T.Stage4Y", 199f),
        };

        List<TrailerShotKit.Moves> moves = null;
        List<(int tile, float warn, float brk, float restore, bool full)> breaks = null;
        string fail = null;
        for (int seed = 0; seed < 400 && moves == null; seed++)
        {
            var rng = new System.Random(0x5A5 + seed);
            var ms = new List<TrailerShotKit.Moves>();
            for (int i = 0; i < 4; i++)
            {
                var p = plan[i];
                Vector3 jitter() => new Vector3(TrailerV2.Rand(rng, -1.4f, 1.4f), 0f, TrailerV2.Rand(rng, -1.4f, 1.4f));
                Vector3 a = Center(p.start.r, p.start.c) + jitter();
                Vector3 b = Center(p.dodge.r, p.dodge.c) + jitter();
                Vector3 goal = Center(p.safe.r, p.safe.c) + jitter() * 0.6f;
                var m = new TrailerShotKit.Moves(a);
                float dodgeAt = TrailerV2.Rand(rng, 1.4f, 2.6f);
                m.Fidget(rng, 0f, dodgeAt - 0.1f, a, 1.0f, (t, q) => TileOf(q) != TileOf(a));
                float d1 = m.RunTo(dodgeAt, b, Speed);
                m.Fidget(rng, d1 + 0.2f, RevealAt + 0.15f, b, 1.0f, (t, q) => TileOf(q) != TileOf(b));
                float t0 = RevealAt + TrailerV2.Rand(rng, 0.2f, 0.45f);
                float arrive = m.RunTo(t0, goal, Speed);
                m.Fidget(rng, arrive + 0.3f, Total, goal, 1.0f, (t, q) => TileOf(q) != TileOf(goal));
                ms.Add(m);
            }
            var pairs = new List<(string, TrailerShotKit.Moves)>();
            for (int i = 0; i < 4; i++) pairs.Add((plan[i].key, ms[i]));
            fail = TrailerV2.CheckGaps(pairs, Total, 2f);
            if (fail != null) continue;
            breaks = PlanBreaks(ms, plan, seed);
            fail = CheckFooting(ms, breaks);
            if (fail != null) continue;
            moves = ms;
            Debug.Log($"[Trailer v2] M5 동선 seed {seed}, 붕괴 {breaks.Count}칸");
        }
        if (moves == null) { Debug.LogError("[Trailer v2] M5 동선 실패: " + fail); return; }

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        for (int i = 0; i < 4; i++)
        {
            var k = TrailerShotKit.MakeRunner(group, plan[i].key, moves[i].start, plan[i].yaw);
            moves[i].ApplyTo(k); // 정산 뒤 Yes 동작 없음(10/6 지적)
            k.Build(shot);
        }

        var fxs = new Dictionary<int, TrailerTileFx>();
        var autoRewind = new TileRewindSettings { duration = 0.5f, distanceMin = 0.5f, distanceMax = 1f, stagger = 0.05f, sfxId = SFXId.None };
        var fullRewind = new TileRewindSettings { duration = 1.2f, distanceMin = 2f, distanceMax = 5f, stagger = 0.3f, sfxId = SFXId.None };
        foreach (var b in breaks)
        {
            var marker = TrailerShotKit.Find(scene, $"{Warns}Warn_{b.tile:00}").GetComponent<SpikeLaneWarnMarker>();
            shot.Call(marker, nameof(SpikeLaneWarnMarker.PlayWarning), b.warn, WarnLead);
            shot.Call(marker, nameof(SpikeLaneWarnMarker.ResetWarning), b.brk);
            if (!fxs.TryGetValue(b.tile, out TrailerTileFx fx))
                fxs[b.tile] = fx = shot.TileFx(TrailerShotKit.Find(scene, Grid + "GridColorTile" + b.tile),
                    "Assets/Prefab/FloorTileShards.prefab", 1f, 0.5f, 1.5f, autoRewind, fullRewind);
            shot.Call(fx, nameof(TrailerTileFx.Break), b.brk);
            if (b.restore < Total)
                shot.Call(fx, b.full ? nameof(TrailerTileFx.RestoreFull) : nameof(TrailerTileFx.RestoreAuto), b.restore);
        }

        Material def = Mat("M5.1");
        AnimationClip gridClip = shot.NewClip("GridReveal");
        foreach (var p in plan)
            TrailerShotKit.SetMaterialSwap(gridClip, "GridColorTile" + Idx(p.safe.r, p.safe.c), typeof(MeshRenderer),
                new List<(float, Material)> { (0f, def), (RevealAt, Mat(p.mat)), (SettleAt, def) });
        shot.Animate(TrailerShotKit.Find(scene, "Stage5.1/StageManager5.1/GridColorChallenge7"), gridClip);

        TrailerV2.AddKeyCamera(shot, new List<(float, Vector3, Vector3)>
        {
            (0f,       new Vector3(-17f, 4.5f, -15.5f), new Vector3(-1f, 0f, 1f)),
            (RevealAt, new Vector3(-15f, 9f, -15.5f),   new Vector3(0f, 0f, 1f)),
            (Total,    new Vector3(-8f, 13f, -15f),     new Vector3(0.5f, 0f, 2.5f)),
        });

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);
    }

    /// <summary>박자마다 4칸(안전 칸·사람이 걸친 칸·이미 깨진 칸 제외) — 1차와 같은 규칙.</summary>
    static List<(int, float, float, float, bool)> PlanBreaks(List<TrailerShotKit.Moves> ms, (string key, (int r, int c) start, (int r, int c) dodge, (int r, int c) safe, string mat, float yaw)[] plan, int seed)
    {
        var safeSet = new HashSet<int>();
        foreach (var p in plan) safeSet.Add(Idx(p.safe.r, p.safe.c));
        var brokenUntil = new float[25];
        var rng = new System.Random(0x47434C50 + seed);
        var breaks = new List<(int, float, float, float, bool)>();
        for (float w = FirstWarn; w + WarnLead <= SettleAt - StopBeforeSettle + 0.001f; w += Beat)
        {
            float brk = w + WarnLead, auto = brk + BrokenLife;
            bool full = auto > RestoreAllAt;
            float restore = full ? RestoreAllAt : auto;
            var blocked = new HashSet<int>(safeSet);
            for (int i = 0; i < 25; i++) if (brokenUntil[i] > w - 0.05f) blocked.Add(i);
            foreach (var m in ms)
                for (float s = w - 0.3f; s <= restore + 0.3f; s += 0.05f)
                {
                    Vector3 p = m.At(s);
                    foreach (Vector2 o in new[] { Vector2.zero, new(0.9f, 0f), new(-0.9f, 0f), new(0f, 0.9f), new(0f, -0.9f) })
                        blocked.Add(TileOf(p + new Vector3(o.x, 0f, o.y)));
                }
            var cand = new List<int>();
            for (int i = 0; i < 25; i++) if (!blocked.Contains(i)) cand.Add(i);
            for (int n = 0; n < PerBeat && cand.Count > 0; n++)
            {
                int pick = cand[rng.Next(cand.Count)];
                cand.Remove(pick);
                brokenUntil[pick] = restore;
                breaks.Add((pick, w, brk, restore, full));
            }
        }
        return breaks;
    }

    /// <summary>매 순간 발밑(몸 반지름 0.5 네 점)이 깨진 칸·경고 중인 칸이 아닌지.</summary>
    static string CheckFooting(List<TrailerShotKit.Moves> ms, List<(int tile, float warn, float brk, float restore, bool full)> breaks)
    {
        for (float t = 0f; t <= Total; t += 0.03f)
            for (int i = 0; i < ms.Count; i++)
            {
                Vector3 p = ms[i].At(t);
                foreach (Vector2 o in new[] { Vector2.zero, new(0.5f, 0f), new(-0.5f, 0f), new(0f, 0.5f), new(0f, -0.5f) })
                {
                    int tile = TileOf(p + new Vector3(o.x, 0f, o.y));
                    foreach (var b in breaks)
                        if (b.tile == tile && t >= b.warn && t < b.restore + 0.5f) return $"{t:F2}s #{i} 칸{tile}";
                }
            }
        return null;
    }

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Mat/Ground/{name}.mat");
}
