using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Boss 컷 (12초, M 공통 카메라). 페이즈 분위기는 게임 MaterialSwapper(BossMaterialChange) 값을 그대로 적용.
/// (a) P4 턱 내리찍기(MouthBossJawSmash): 경고 0.8 → MouthBG Close 2초 → 남는 칸 빼고 전부 파괴 → Open 2초 → 자동 복구.
///     1회차 남는 칸 2개(둘씩 모임), 2회차 1개(넷이 한 칸에).
/// (b) P3 혀(팀 외침): 낙하물·음식 화살 속에 가운데 경고 → TongueRise → 가운데 9칸 파괴 → 바깥에서 외침 → Retract → 복구.
/// </summary>
public static class TrailerShotMBoss
{
    const string Src      = "Assets/Scenes/M.Boss.unity";
    const string AssetDir = "Assets/Trailer/M.Boss";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    [MenuItem("Tools/Trailer/M.Boss (a) 플레이 P4")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Boss (b) 방해공작 P3 혀")]
    public static void BuildHazard() => Build(true);

    static Scene _scene;

    static void Build(bool hazard)
    {
        string suffix   = hazard ? "_b" : "_a";
        string shotName = "Trailer_M.Boss" + suffix;
        string dst      = $"Assets/Scenes/Marketing_M.Boss{suffix}.unity";

        _scene = TrailerShotKit.PrepStage(Src, dst);
        string phase = hazard ? "Boss 130-200" : "Boss 270-360";
        foreach (string p in new[] { "Boss 0-90", "Boss 60-130", "Boss 130-200", "Boss 270-360" })
            TrailerShotKit.Find(_scene, p).SetActive(p == phase);
        ApplyPhaseLook(hazard ? 3 : 4);

        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, 35f, 0f, 90f, 1f);

        if (hazard) BuildTongue(shot); else BuildJawSmash(shot);

        shot.AddRecorder(shotName);
        shot.Save(_scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 → Recordings/{shotName}.mp4");
    }

    /// <summary>게임 BossMaterialChange.Apply(n-1)과 같은 값: 하늘·MouthBG 위/아래 이·혀 재질 + 안개색.</summary>
    static void ApplyPhaseLook(int n)
    {
        Material M(string name)
        {
            foreach (string g in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.name == name) return m;
            }
            Debug.LogError("[Trailer] 재질 없음: " + name);
            return null;
        }
        Renderer R(string name)
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name == name) return r;
            Debug.LogError("[Trailer] 렌더러 없음: " + name);
            return null;
        }
        R("Environment").sharedMaterial = M("BossPhase" + n);
        R("CC_Base_Teeth").sharedMaterials = new[] { M($"M{n}_Upper"), M($"M{n}_Lower") };
        R("CC_Base_Tongue").sharedMaterial = M($"M{n}_Tongue");
        if (n == 3)
            TrailerShotKit.Find(_scene, "Boss 130-200/StageManager_Boss3/TongueAttack/TongueAttack").GetComponent<Renderer>().sharedMaterial = M("M3_Tongue");
        RenderSettings.fogColor = n switch
        {
            1 => new Color(0.247f, 0.106f, 0.165f),
            2 => new Color(0.118f, 0.208f, 0.208f),
            3 => new Color(0.345f, 0.082f, 0.620f),
            _ => new Color(1f, 0.078f, 0.078f),
        };
    }

    static GameObject TileAt(string groundPath, float x, float z)
    {
        foreach (Transform t in TrailerShotKit.Find(_scene, groundPath).transform)
            if (Mathf.Abs(t.position.x - x) < 0.1f && Mathf.Abs(t.position.z - z) < 0.1f) return t.gameObject;
        Debug.LogError($"[Trailer] 타일 없음 {x},{z}");
        return null;
    }

    static Vector3 P(float x, float z) => new Vector3(x, GroundY, z);

    // ── (a) P4 턱 ───────────────────────────────────────────────

    static void BuildJawSmash(TrailerShotKit.Shot shot)
    {
        const string ground = "Boss 270-360/StageManager_Boss5/Ground";
        const string warns  = "Boss 270-360/StageManager_Boss5/MouthBossJawSmash5/JawWarnMarkers";

        // 회차: 경고 시작, 남는 칸들 (경고 0.8 → Close 2 → 파괴 → 1초 → Open 2 → 복구)
        var rounds = new (float warn, Vector2[] keep)[]
        {
            (0.6f, new[] { new Vector2(-5f, 5f), new Vector2(5f, -5f) }),
            (7.6f, new[] { new Vector2(0f, 0f) }),
        };

        var mouthSeq = new List<(AnimationClip, float)> { (Clip("Armature|Idle"), 0f) };
        var warnOn = new Dictionary<string, List<(float, bool)>>();
        var warnCol = new Dictionary<string, List<(float, Color)>>();
        var warnPrg = new Dictionary<string, List<(float, float)>>();
        var breakTimes = new Dictionary<GameObject, List<(float brk, float restore)>>();

        foreach (var r in rounds)
        {
            float close = r.warn + 0.8f, brk = close + 2f, open = brk + 1f, idle = open + 2f;
            mouthSeq.Add((Clip("Armature|Close"), close));
            mouthSeq.Add((Clip("Armature|Hold"), brk));
            mouthSeq.Add((Clip("Armature|Open"), open));
            mouthSeq.Add((Clip("Armature|Idle"), idle));

            for (int x = -10; x <= 10; x += 5)
                for (int z = -10; z <= 10; z += 5)
                {
                    bool keep = false;
                    foreach (Vector2 k in r.keep) keep |= Mathf.Approximately(k.x, x) && Mathf.Approximately(k.y, z);
                    if (keep) continue;

                    GameObject tile = TileAt(ground, x, z);
                    if (!breakTimes.ContainsKey(tile)) breakTimes[tile] = new List<(float, float)>();
                    breakTimes[tile].Add((brk, idle)); // 매 회차 Open 직후 자동 복구

                    string w = MarkerName(warns, x, z);
                    if (!warnOn.ContainsKey(w)) { warnOn[w] = new() { (0f, false) }; warnCol[w] = new() { (0f, TrailerShotKit.WarnStart) }; warnPrg[w] = new() { (0f, 0f) }; }
                    warnOn[w].Add((r.warn, true)); warnOn[w].Add((close, false));
                    warnCol[w].Add((r.warn, TrailerShotKit.WarnStart)); warnCol[w].Add((close, TrailerShotKit.WarnEnd));
                    warnPrg[w].Add((r.warn, 0f)); warnPrg[w].Add((close, 1f)); warnPrg[w].Add((close + 0.01f, 0f));
                }
        }
        shot.BindSequence(TrailerShotKit.Find(_scene, "BackGround/MouthBG").GetComponent<Animator>(), "MouthBG", mouthSeq, 0.05f);

        AnimationClip wClip = shot.NewClip("JawWarn");
        foreach (Transform m in TrailerShotKit.Find(_scene, warns).transform)
        {
            string n = m.name;
            TrailerShotKit.SetEnabled(wClip, n, typeof(MeshRenderer), warnOn.TryGetValue(n, out var on) ? on : new() { (0f, false) });
            if (!warnCol.ContainsKey(n)) continue;
            TrailerShotKit.SetMatColor(wClip, n, typeof(MeshRenderer), "_BaseColor", warnCol[n]);
            TrailerShotKit.SetMat(wClip, n, typeof(MeshRenderer), "_Progress", TrailerShotKit.Linear(warnPrg[n]));
        }
        shot.Animate(TrailerShotKit.Find(_scene, warns), wClip);

        // 파괴: 회차마다 Break 신호 + Open 뒤 복구(껐다 켜면 Breakable.OnEnable이 되살림)
        foreach (var kv in breakTimes)
        {
            GameObject tile = kv.Key;
            var times = kv.Value;
            TrailerShotKit.BreakAt(shot, tile, "Assets/Prefab/FloorTileShards.prefab", 2f, 2f, 5f, times[0].brk, Total + 1f);
            for (int i = 1; i < times.Count; i++) shot.Call(tile.GetComponent<Breakable>(), nameof(Breakable.Break), times[i].brk);
            var ranges = new List<(float, float)>();
            float from = 0f;
            foreach (var t in times) { if (t.restore < Total) { ranges.Add((from, t.restore)); from = t.restore + 0.05f; } }
            ranges.Add((from, Total + 1f));
            if (ranges.Count > 1) shot.BindActive(tile, ranges.ToArray());
        }

        // 꿀떡: 경고가 뜨면 남는 칸으로 둘씩 → 복구되면 흩어짐 → 2회차엔 넷이 한 칸에
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        TrailerShotKit.Runner K(string key, Vector3 p) => TrailerShotKit.MakeRunner(group, key, p, TrailerShotKit.Yaw(-p));
        float r2 = rounds[1].warn;
        K("B", P(-8f, 2f)).RunTo(0.8f, 1.4f, P(-5.8f, 5.6f)).Pose(1.6f, "Surprise")
            .RunTo(6.8f, 7.3f, P(-6f, 0f)).RunTo(r2 + 0.2f, r2 + 0.9f, P(-0.9f, 0.9f)).Pose(r2 + 1.2f, "Surprise").Build(shot);
        K("P", P(2f, 8f)).RunTo(0.9f, 1.6f, P(-4.2f, 4.4f)).Pose(1.8f, "Surprise")
            .RunTo(6.9f, 7.4f, P(0f, 7f)).RunTo(r2 + 0.25f, r2 + 1.0f, P(0.9f, 0.9f)).Pose(r2 + 1.2f, "Surprise").Build(shot);
        K("G", P(8f, -2f)).RunTo(0.8f, 1.4f, P(5.8f, -5.6f)).Pose(1.6f, "Surprise")
            .RunTo(6.8f, 7.3f, P(7f, 0f)).RunTo(r2 + 0.2f, r2 + 0.9f, P(0.9f, -0.9f)).Pose(r2 + 1.2f, "Surprise").Build(shot);
        K("Y", P(-2f, -8f)).RunTo(0.9f, 1.6f, P(4.2f, -4.4f)).Pose(1.8f, "Surprise")
            .RunTo(6.9f, 7.4f, P(0f, -7f)).RunTo(r2 + 0.25f, r2 + 1.0f, P(-0.9f, -0.9f)).Pose(r2 + 1.2f, "Surprise").Build(shot);
    }

    static string MarkerName(string warnsPath, float x, float z)
    {
        foreach (Transform t in TrailerShotKit.Find(_scene, warnsPath).transform)
            if (Mathf.Abs(t.position.x - x) < 0.1f && Mathf.Abs(t.position.z - z) < 0.1f) return t.name;
        return "";
    }

    static AnimationClip Clip(string name) => TrailerShotKit.LoadClip("Assets/NoAI/Mouth/MouthBG/MouthBG.fbx", name);

    // ── (b) P3 혀 ───────────────────────────────────────────────

    static void BuildTongue(TrailerShotKit.Shot shot)
    {
        const string mgr = "Boss 130-200/StageManager_Boss3/";
        const float warnAt = 1.0f, riseAt = 3.0f, riseLen = 5.3f, retractAt = 9.8f, retractLen = 1.3f;
        float breakAt = riseAt + riseLen;
        float restoreAt = retractAt + retractLen;

        // 가운데 경고(15×15): 귤색→진홍, _Progress 0→1
        GameObject warn = TrailerShotKit.Find(_scene, "Boss 130-200/WarnMarker_C");
        warn.SetActive(true);
        AnimationClip wClip = shot.NewClip("WarnMarker_C");
        TrailerShotKit.SetEnabled(wClip, "", typeof(MeshRenderer), new() { (0f, false), (warnAt, true), (riseAt, false) });
        TrailerShotKit.SetMatColor(wClip, "", typeof(MeshRenderer), "_BaseColor", new() { (0f, TrailerShotKit.WarnStart), (warnAt, TrailerShotKit.WarnStart), (riseAt, TrailerShotKit.WarnEnd), (Total, TrailerShotKit.WarnEnd) });
        TrailerShotKit.SetMat(wClip, "", typeof(MeshRenderer), "_Progress", TrailerShotKit.Linear(new() { (0f, 0f), (warnAt, 0f), (riseAt, 1f), (Total, 1f) }));
        shot.Animate(warn, wClip);

        GameObject tongue = TrailerShotKit.Find(_scene, mgr + "TongueAttack");
        foreach (var smr in tongue.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        const string fbx = "Assets/NoAI/Mouth/TongueAttack/TongueAttack.fbx";
        shot.BindSequence(tongue.GetComponent<Animator>(), "Tongue", new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueIdle"), 0f),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueRise"), riseAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueHold"), breakAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueRetract"), retractAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueIdle"), restoreAt),
        }, 0.05f);

        // 가운데 3×3 파괴 → 혀가 들어간 뒤 복구
        for (int x = -5; x <= 5; x += 5)
            for (int z = -5; z <= 5; z += 5)
                TrailerShotKit.BreakAt(shot, TileAt(mgr + "Ground", x, z), "Assets/Prefab/FloorTileShards.prefab", 2f, 2f, 5f, breakAt, restoreAt);

        // 낙하물(DropTrap_3=Drop3, DropTrap_8=Drop8)과 음식 화살
        Transform drops = TrailerShotKit.Group("Drops", shot.Root).transform;
        var list = new[]
        {
            new TrailerShotMStage2.Drop { warn = 0.3f, at = new Vector3(10f, 0f, 10f),  prefab = "Drop3", markerScale = 2.25f },
            new TrailerShotMStage2.Drop { warn = 2.2f, at = new Vector3(-10f, 0f, -5f), prefab = "Drop8", markerScale = 4.75f },
            new TrailerShotMStage2.Drop { warn = 5.0f, at = new Vector3(10f, 0f, -10f), prefab = "Drop3", markerScale = 2.25f },
            new TrailerShotMStage2.Drop { warn = 7.6f, at = new Vector3(-5f, 0f, 10f),  prefab = "Drop8", markerScale = 4.75f },
            new TrailerShotMStage2.Drop { warn = 10.2f, at = new Vector3(5f, 0f, -10f), prefab = "Drop3", markerScale = 2.25f },
        };
        for (int i = 0; i < list.Length; i++) TrailerShotMStage2.BuildDrop(shot, drops, list[i], i);

        TrailerShotMStage4.BuildTrackers(shot, _scene, mgr + "StageArrowTraps", new (int, float, string, float, Vector3)[]
        {
            (1, 1.6f, "Donut",    10f, new Vector3(6f, 0f, -3f)),
            (0, 4.2f, "Burger",    7f, new Vector3(-6f, 0f, 8f)),
            (3, 6.4f, "Sandwich", 10f, new Vector3(8f, 0f, 6f)),
            (1, 9.0f, "IceCream",  7f, new Vector3(-8f, 0f, -2f)),
        });

        // 꿀떡: 가운데에서 피하다 경고가 뜨면 바깥 링으로 흩어지고, 혀가 바닥을 뚫으면 외친다
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        TrailerShotKit.Runner K(string key, Vector3 p) => TrailerShotKit.MakeRunner(group, key, p, TrailerShotKit.Yaw(-p));
        K("B", P(-3f, 2f)).RunTo(warnAt + 0.2f, warnAt + 1.0f, P(-10f, 4f)).RunTo(4.6f, 5.3f, P(-10f, 9f))
            .Turn(5.4f, 5.8f, 135f).Pose(breakAt + 0.3f, "Yes").Pose(restoreAt + 0.2f, "Idle").Build(shot);
        K("P", P(2f, 3f)).RunTo(warnAt + 0.3f, warnAt + 1.1f, P(6f, 10f))
            .Turn(warnAt + 1.2f, warnAt + 1.6f, 200f).Pose(breakAt + 0.3f, "Yes").Pose(restoreAt + 0.2f, "Idle").Build(shot);
        K("G", P(3f, -2f)).RunTo(warnAt + 0.2f, warnAt + 1.0f, P(10f, -3f)).RunTo(6.6f, 7.2f, P(10f, 3f))
            .Turn(7.3f, 7.7f, 270f).Pose(breakAt + 0.3f, "Yes").Pose(restoreAt + 0.2f, "Idle").Build(shot);
        K("Y", P(-2f, -3f)).RunTo(warnAt + 0.35f, warnAt + 1.2f, P(-4f, -10f))
            .Turn(warnAt + 1.3f, warnAt + 1.7f, 20f).Pose(breakAt + 0.3f, "Yes").Pose(restoreAt + 0.2f, "Idle").Build(shot);
    }
}
