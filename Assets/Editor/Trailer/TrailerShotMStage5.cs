using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Stage5 컷 (12초, M 공통 카메라). 5×5 Grid(4.9×1×4.9 칸) 한 라운드 — 값은 씬 GridChallenge/GridTileCollapse 그대로.
/// 붕괴(Random): 박자 1초마다 4칸, 경고 0.8초(게임 SpikeLaneWarnMarker.PlayWarning) → 파괴(FloorTileShards 0.5~1.5, 1초)
///   → 2.5초 뒤 자동 복구(되감기 0.5초). 정산 1초 전부터 새 붕괴 없음. 정산 0.5초 뒤 남은 구멍 일괄 복구(되감기 1.2초).
/// 공개 4.5초: 안전 칸이 각자 색 재질 → 정산 때 기본 재질. 안전 칸·사람이 있는 칸은 깨지지 않는다(구멍 위에 뜨지 않게).
/// (a) 바람 없음. (b) 바람(Push): MouthBG PushClose→Hold→Open + WindVFX_Push, 넷이 -Z로 밀린다.
///     M5는 팀 외침 방해공작이 없는 스테이지라 (b)는 바람을 방해공작으로 쓴다.
/// </summary>
public static class TrailerShotMStage5
{
    const string Src      = "Assets/Scenes/M.Stage5.unity";
    const string AssetDir = "Assets/Trailer/M.Stage5";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;
    const string Grid     = "Stage5.1/StageManager5.1/GridColorChallenge7/";
    const string Warns    = "Stage5.1/StageManager5.1/CollapseWarnMarkers/";

    // 씬 값
    const float FirstWarn = 0.3f, Beat = 1f, WarnLead = 0.8f, BrokenLife = 2.5f, StopBeforeSettle = 1f;
    const int   PerBeat = 4;
    const float PreReveal = 4f, RoundLen = 4.5f, SettleHold = 0.5f;
    static float RevealAt => FirstWarn + PreReveal;
    static float SettleAt => RevealAt + RoundLen;
    static float RestoreAllAt => SettleAt + SettleHold;

    const float Speed = 7f;

    [MenuItem("Tools/Trailer/M.Stage5 (a) 플레이")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Stage5 (b) 방해공작(바람)")]
    public static void BuildHazard() => Build(true);

    static Vector3 Pos(int r, int c) => new Vector3(-10f + 5f * c, GroundY, 10f - 5f * r);
    static int Idx(int r, int c) => r * 5 + c;
    static int TileOf(Vector3 p)
    {
        int c = Mathf.Clamp(Mathf.RoundToInt((p.x + 10f) / 5f), 0, 4);
        int r = Mathf.Clamp(Mathf.RoundToInt((10f - p.z) / 5f), 0, 4);
        return Idx(r, c);
    }

    /// <summary>꿀떡 이동 일정(선형 근사) — 칸 예약(사람 있는 칸은 안 깸) 계산용.</summary>
    sealed class Track
    {
        public readonly List<(float t, Vector3 p)> keys = new();
        public Vector3 At(float t)
        {
            if (t <= keys[0].t) return keys[0].p;
            for (int i = 1; i < keys.Count; i++)
                if (t <= keys[i].t) return Vector3.Lerp(keys[i - 1].p, keys[i].p, Mathf.InverseLerp(keys[i - 1].t, keys[i].t, t));
            return keys[keys.Count - 1].p;
        }
    }

    static void Build(bool wind)
    {
        string suffix   = wind ? "_b" : "_a";
        string shotName = "Trailer_M.Stage5" + suffix;
        string dst      = $"Assets/Scenes/Marketing_M.Stage5{suffix}.unity";

        Scene scene = TrailerShotKit.PrepStage(Src, dst, "SpikeLaneWarnMarker");
        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, 35f, 0f, 90f, 1f);

        // ── 꿀떡 동선: 가운데에서 붕괴를 피하다(2초에 한 칸 옮김) 공개되면 자기 색 칸으로 ──
        var plan = new (string key, (int r, int c) start, (int r, int c) dodge, (int r, int c) safe, string mat)[]
        {
            ("B", (2, 1), (1, 1), (0, 1), "T.Stage4B"),
            ("P", (1, 3), (2, 3), (1, 4), "T.Stage4P"),
            ("G", (3, 3), (3, 2), (4, 3), "T.Stage4G"),
            ("Y", (3, 1), (2, 2), (3, 0), "T.Stage4Y"),
        };
        float windPush = wind ? 1.2f : 0f;
        var tracks = new Track[4];
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        for (int i = 0; i < 4; i++)
        {
            var p = plan[i];
            Vector3 a = Pos(p.start.r, p.start.c), b = Pos(p.dodge.r, p.dodge.c), goal = Pos(p.safe.r, p.safe.c);
            var tr = tracks[i] = new Track();
            var k = TrailerShotKit.MakeRunner(group, p.key, a, TrailerShotKit.Yaw(-a));
            tr.keys.Add((0f, a));

            float d0 = 1.9f + i * 0.1f;
            float d1 = d0 + Vector3.Distance(a, b) / Speed;
            k.RunTo(d0, d1, b); tr.keys.Add((d0, a)); tr.keys.Add((d1, b));
            Vector3 cur = b;
            if (wind) { Vector3 g = cur + Vector3.back * windPush; k.Glide(2.8f, RevealAt, g); tr.keys.Add((2.8f, cur)); tr.keys.Add((RevealAt, g)); cur = g; }

            // 바람을 안고 가는(+Z) 쪽은 느리다
            float speed = wind && goal.z > cur.z ? Speed * 0.55f : Speed;
            float t0 = RevealAt + 0.25f + i * 0.05f;
            float t1 = t0 + Vector3.Distance(cur, goal) / speed;
            k.RunTo(t0, t1, goal); tr.keys.Add((t0, cur)); tr.keys.Add((t1, goal));
            k.Pose(SettleAt + 0.3f, "Yes");
            k.Build(shot);
        }

        // ── 붕괴 계획: 박자마다 4칸, 안전 칸·사람 칸·이미 깨진 칸 제외 ──
        var safeSet = new HashSet<int>();
        foreach (var p in plan) safeSet.Add(Idx(p.safe.r, p.safe.c));
        var brokenUntil = new float[25];
        var rng = new System.Random(0x47434C50);
        var breaks = new List<(int tile, float warn, float brk, float restore, bool full)>();
        for (float w = FirstWarn; w + WarnLead <= SettleAt - StopBeforeSettle + 0.001f; w += Beat)
        {
            float brk = w + WarnLead;
            float auto = brk + BrokenLife;
            bool full = auto > RestoreAllAt;
            float restore = full ? RestoreAllAt : auto;

            var blocked = new HashSet<int>(safeSet);
            for (int i = 0; i < 25; i++) if (brokenUntil[i] > w - 0.05f) blocked.Add(i);
            foreach (Track tr in tracks)
                for (float s = w - 0.3f; s <= restore + 0.3f; s += 0.05f)
                {
                    Vector3 p = tr.At(s);
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

        // ── 경고·파괴·복구 (게임 컴포넌트 호출) ──
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

        // ── 안전 칸 공개: 각자 색 재질 → 정산 때 기본 재질 ──
        Material def = Mat("M5.1");
        AnimationClip gridClip = shot.NewClip("GridReveal");
        foreach (var p in plan)
            TrailerShotKit.SetMaterialSwap(gridClip, "GridColorTile" + Idx(p.safe.r, p.safe.c), typeof(MeshRenderer),
                new List<(float, Material)> { (0f, def), (RevealAt, Mat(p.mat)), (SettleAt, def) });
        shot.Animate(TrailerShotKit.Find(scene, "Stage5.1/StageManager5.1/GridColorChallenge7"), gridClip);

        if (wind) BuildWind(shot, scene);

        shot.AddRecorder(shotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 ({breaks.Count}칸 붕괴) → Recordings/{shotName}.mp4");
    }

    /// <summary>게임 MouthWindAnimator(Push): doPushClose(충전 2초) → doPushHold → doPushOpen. 입자는 힘보다 0.5초 먼저.</summary>
    static void BuildWind(TrailerShotKit.Shot shot, Scene scene)
    {
        const float chargeAt = 0.8f, holdAt = 2.8f, openAt = 9.4f;
        var mouth = TrailerShotKit.Find(scene, "BackGround/MouthBG").GetComponent<Animator>();
        const string fbx = "Assets/NoAI/Mouth/MouthBG/MouthBG.fbx";
        shot.BindSequence(mouth, "MouthBG", new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),      0f),
            (TrailerShotKit.LoadClip(fbx, "Armature|PushClose"), chargeAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|PushHold"),  holdAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|PushOpen"),  openAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),      openAt + 2.966667f),
        }, 0.05f);

        GameObject vfx = TrailerShotKit.Find(scene, "WindVFX_Push");
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.playOnAwake = true; // 켜지는 순간 재생
        }
        shot.BindActive(vfx, holdAt - 0.5f, openAt + 0.5f);
    }

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Mat/Ground/{name}.mat");
}
