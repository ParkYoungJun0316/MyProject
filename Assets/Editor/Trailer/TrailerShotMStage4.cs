using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Stage4 컷 (12초, M 공통 카메라). SequenceRing은 Space로 넘기므로 칸을 밟지 않는다.
/// 넷은 겹치지 않는 대형으로 함께 움직이며 현재 칸을 따라간다.
/// (a) 4.1 PlusOne(현재+다음 칸 색): 가운데 15×15 안쪽 길을 2×2 대형으로 따라 돈다.
/// (b) 4.2 NextOnly(다음 칸만, 시작 때만 0·1번): 안쪽에서 돌다가 혀 경고(WarnMarker_C)가 뜨면 바깥 링으로 함께 나가
///     링 위를 한 줄로 따라 돈다 → 혀가 올라와 가운데 칸 파괴 → 외침 → 혀가 들어가고 가운데 칸 되감기 복구.
/// </summary>
public static class TrailerShotMStage4
{
    const string Src      = "Assets/Scenes/M.Stage4.unity";
    const string AssetDir = "Assets/Trailer/M.Stage4";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    [MenuItem("Tools/Trailer/M.Stage4 (a) 플레이")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Stage4 (b) 방해공작")]
    public static void BuildHazard() => Build(true);

    static Scene _scene;
    static string _mgr;

    static readonly Color Hidden = new Color(1f, 0f, 0.486f); // defaultTileColor
    static Color StepColor(string c) => c switch
    {
        "B" => new Color(0.227f, 0.290f, 0.561f),
        "G" => new Color(0.373f, 0.498f, 0.353f),
        "Y" => new Color(0.851f, 0.643f, 0.255f),
        "P" => new Color(0.349f, 0f, 0.737f),
        "C" => Color.white,  // Common
        _   => Color.black,  // Danger: 1초 뒤 자동 통과
    };

    // (b) 혀 RiseHold: 경고(게임 3초 → 2초) → Rise 5.3 → 가운데 칸 파괴 → 외칠 때까지 Hold → Retract 1.3 → 되감기 복구
    const float WarnAt = 2.0f, WarnLen = 2.0f, RiseLen = 5.3f, HoldLen = 0.7f, RetractLen = 1.3f;
    static float RiseAt => WarnAt + WarnLen;
    static float BreakAt => RiseAt + RiseLen;
    static float RetractAt => BreakAt + HoldLen;
    static float RestoreAt => RetractAt + RetractLen;

    static void Build(bool hazard)
    {
        string suffix   = hazard ? "_b" : "_a";
        string shotName = "Trailer_M.Stage4" + suffix;
        string dst      = $"Assets/Scenes/Marketing_M.Stage4{suffix}.unity";

        _scene = TrailerShotKit.PrepStage(Src, dst, "SpikeLaneWarnMarker");
        string section = hazard ? "Stage4.2" : "Stage4.1";
        foreach (string s in new[] { "Stage4.1", "Stage4.2", "Stage4.3" })
            TrailerShotKit.Find(_scene, s).SetActive(s == section);
        _mgr = $"{section}/StageManger{section.Substring(5)}/";

        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        Camera cam = TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, 35f, 0f, 90f, 1f);

        string[] steps = hazard
            ? new[] { "B", "Y", "P", "C", "G", "D", "B", "Y", "P", "C", "G", "B", "Y" }
            : new[] { "B", "Y", "P", "C", "G", "B", "D", "Y", "P", "G", "C", "B" };
        var clear = new float[steps.Length];
        float t = 1.1f;
        for (int k = 0; k < steps.Length; k++) { clear[k] = t; t += steps[k] == "D" ? 1.0f : 0.8f; }
        const float startAt = 0.4f;
        float CurrentFrom(int k) => k == 0 ? startAt : clear[k - 1];

        BuildRingColors(shot, steps, clear, startAt, nextOnly: hazard);
        BuildMarker(shot, cam, steps.Length, clear, startAt);

        if (hazard) BuildTongue(shot);

        // 대형 이동: 각 스텝이 현재가 되는 순간 0.45초에 걸쳐 그 칸 쪽으로 옮긴다
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        Vector2[] inner = { new(-1f, 1f), new(1f, 1f), new(1f, -1f), new(-1f, -1f) }; // 2×2(2m 간격)
        float outAt = WarnAt + 0.2f, outDone = WarnAt + 1.0f;

        // 링 위 한 줄(2.4m 간격 — 모서리에서도 1.7m 이상). 나가는 순간 링 진행 방향 기준으로
        // 안쪽 대형에서 앞에 있던 사람이 앞자리로 가야 서로 길이 엇갈리지 않는다.
        var along = new float[4];
        {
            int kOut = 1;
            while (kOut < steps.Length && !(CurrentFrom(kOut) + 0.8f > outAt)) kOut++;
            Vector3 tan = Ring(RingU(kOut) + 1f) - Ring(RingU(kOut) - 1f);
            var order = new List<int> { 0, 1, 2, 3 };
            order.Sort((x, y) => Vector2.Dot(inner[x], new Vector2(tan.x, tan.z)).CompareTo(Vector2.Dot(inner[y], new Vector2(tan.x, tan.z))));
            float[] slots = { -3.6f, -1.2f, 1.2f, 3.6f };
            for (int r = 0; r < 4; r++) along[order[r]] = slots[r];
        }

        for (int i = 0; i < 4; i++)
        {
            var keys = new List<(float, Vector3, float)>();
            var moving = new List<(float, float)>();
            Vector3 Inner(int k) { Vector3 c = Ring(RingU(k)) * 0.5f; return new Vector3(c.x + inner[i].x, GroundY, c.z + inner[i].y); }
            Vector3 Outer(int k) { Vector3 p = Ring(RingU(k) + along[i]); return new Vector3(p.x, GroundY, p.z); }
            float FaceInner(int k) => TrailerShotKit.Yaw(Ring(RingU(k)) - Ring(RingU(k)) * 0.5f);
            float FaceOuter(int k) => TrailerShotKit.Yaw(Ring(RingU(k) + along[i] + 1f) - Ring(RingU(k) + along[i] - 1f));

            bool movedOut = false;
            float lastU = 0f; // 바깥 링에 있을 때 대형 중심의 둘레 좌표
            for (int k = 0; k < steps.Length; k++)
            {
                float from = CurrentFrom(k);
                bool outside = hazard && from >= outDone;
                if (k > 0 && hazard && from < outDone && from + 0.8f > outAt)
                {
                    // 경고: 안쪽 대형 → 바깥 링 한 줄로 함께 나간다(그 사이 스텝은 나간 뒤 따라잡는다)
                    if (!movedOut)
                    {
                        var last = keys[keys.Count - 1];
                        keys.Add((outAt, last.Item2, last.Item3));
                        keys.Add((outDone, Outer(k), FaceOuter(k)));
                        moving.Add((outAt, outDone));
                        movedOut = true;
                        lastU = RingU(k);
                    }
                    continue;
                }
                Vector3 p = outside ? Outer(k) : Inner(k);
                float yaw = outside ? FaceOuter(k) : FaceInner(k);
                if (k == 0) { keys.Add((0f, p, yaw)); continue; }
                // (b) 외치는 동안은 멈춰 서서 외친 뒤 따라잡는다
                if (hazard && from > BreakAt - 0.1f && from < RetractAt + 0.2f) continue;
                var prev = keys[keys.Count - 1];
                from = Mathf.Max(from, prev.Item1); // 따라잡는 중이면 도착 뒤 이어서
                if (outside)
                {
                    // 링 위: 넷이 같은 둘레 거리를 같은 속도로 — 모서리를 돌며 간격이 그대로 유지된다
                    float u0 = lastU, u1 = RingU(k);
                    if (u1 < u0 - 1f) u1 += 80f;
                    float dur = Mathf.Max(0.45f, (u1 - u0) / 8f);
                    keys.Add((from, prev.Item2, prev.Item3));
                    int n = Mathf.Max(1, Mathf.CeilToInt((u1 - u0) / 1f));
                    for (int s = 1; s <= n; s++)
                    {
                        float u = Mathf.Lerp(u0, u1, s / (float)n) + along[i];
                        Vector3 q = Ring(u);
                        float tan = TrailerShotKit.Yaw(Ring(u + 1f) - Ring(u - 1f));
                        keys.Add((from + dur * s / n, new Vector3(q.x, GroundY, q.z), tan));
                    }
                    moving.Add((from, from + dur));
                    lastU = RingU(k);
                    continue;
                }
                float arrive = from + Mathf.Max(0.45f, Vector3.Distance(prev.Item2, p) / 8f);
                keys.Add((from, prev.Item2, prev.Item3));
                keys.Add((arrive, p, yaw));
                moving.Add((from, arrive));
            }
            keys.Add((Total, keys[keys.Count - 1].Item2, keys[keys.Count - 1].Item3));

            Vector3 start = keys[0].Item2;
            var run = TrailerShotKit.MakeRunner(group, TrailerShotKit.SquadKeys[i], start, keys[0].Item3).Follow(keys);
            foreach (var (m0, m1) in moving) run.Pose(m0, "Run").Pose(m1, "Idle");
            if (hazard) run.Pose(BreakAt + 0.1f, "Yes").Pose(RetractAt + 0.2f, "Idle");
            else run.Pose(clear[steps.Length - 1] + 0.4f, "Yes");
            run.Build(shot);
        }

        shot.AddRecorder(shotName);
        shot.Save(_scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 → Recordings/{shotName}.mp4");
    }

    /// <summary>링 둘레 좌표(한 변 20m 정사각형, (-10,10)에서 시계 방향). 링 칸 k의 중심은 u = 5 + 5k.</summary>
    static float RingU(int k) => 5f + 5f * (k % 16);

    static Vector3 Ring(float u)
    {
        u = Mathf.Repeat(u, 80f);
        if (u < 20f) return new Vector3(-10f + u, 0f, 10f);
        if (u < 40f) return new Vector3(10f, 0f, 10f - (u - 20f));
        if (u < 60f) return new Vector3(10f - (u - 40f), 0f, -10f);
        return new Vector3(-10f, 0f, -10f + (u - 60f));
    }

    /// <summary>
    /// 칸 색(게임 RefreshTileColors). PlusOne = 현재+다음. NextOnly = 다음만(현재는 숨김), 시작(cur=0)엔 0·1번 표시.
    /// </summary>
    static void BuildRingColors(TrailerShotKit.Shot shot, string[] steps, float[] clear, float startAt, bool nextOnly)
    {
        AnimationClip clip = shot.NewClip("RingFloor");
        float CurFrom(int k) => k == 0 ? startAt : clear[k - 1];
        for (int tile = 0; tile < 16; tile++)
        {
            var keys = new List<(float, Color)> { (0f, Hidden) };
            for (int k = 0; k < steps.Length; k++)
            {
                if (k % 16 != tile) continue;
                // 다음 칸으로 보이는 구간: cur = k-1인 동안. PlusOne은 이어서 현재(cur = k)인 동안도 보인다.
                float nextFrom = k == 0 ? startAt : CurFrom(k - 1);
                float until = nextOnly ? (k == 0 ? clear[0] : CurFrom(k)) : clear[k];
                keys.Add((nextFrom, StepColor(steps[k])));
                keys.Add((until, Hidden));
            }
            keys.Add((Total, Hidden));
            TrailerShotKit.SetMatColor(clip, "RingTile" + tile, typeof(MeshRenderer), "_BaseColor", keys, stepped: true);
        }
        shot.Animate(TrailerShotKit.Find(_scene, _mgr + "Stage4_Floor"), clip);
    }

    /// <summary>현재 칸 위 2.5m 마커(게임 SequenceRingCurrentStepMarker) — 카메라를 바라본다.</summary>
    static void BuildMarker(TrailerShotKit.Shot shot, Camera cam, int count, float[] clear, float startAt)
    {
        GameObject src = TrailerShotKit.Find(_scene, _mgr + "SequenceRingMiniGame/SequenceRingCurrentMarker/Marker");
        Vector3 Tile(int k) => Ring(RingU(k)) + Vector3.up * (GroundY + 2.5f);
        Animator rig = TrailerShotKit.Rig("RingMarker", shot.Root, Tile(0), Quaternion.identity);
        GameObject marker = Object.Instantiate(src, rig.transform);
        marker.name = "Marker";
        marker.transform.localPosition = Vector3.zero;
        var look = marker.AddComponent<LookAtConstraint>();
        look.AddSource(new ConstraintSource { sourceTransform = cam.transform, weight = 1f });
        look.rotationOffset = new Vector3(0f, 180f, 0f);
        look.constraintActive = true;

        var mx = new List<(float, float)>(); var my = new List<(float, float)>(); var mz = new List<(float, float)>();
        for (int k = 0; k < count; k++)
        {
            float at = k == 0 ? startAt : clear[k - 1];
            Vector3 p = Tile(k);
            mx.Add((at, p.x)); my.Add((at, p.y)); mz.Add((at, p.z));
        }
        AnimationClip clip = shot.NewClip("RingMarker");
        TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Steps(mx), TrailerShotKit.Steps(my), TrailerShotKit.Steps(mz));
        shot.BindWhole(rig, clip);
        shot.BindActive(marker, startAt, clear[count - 1]);
    }

    /// <summary>게임 TongueController RiseHold: 경고 → doRise → 가운데 칸 파괴(FloorTileShards 2~5, 2초) → doHold → 외침 → doRetract → 되감기 복구.</summary>
    static void BuildTongue(TrailerShotKit.Shot shot)
    {
        GameObject warn = TrailerShotKit.Find(_scene, "WarnMarker_C");
        warn.SetActive(true);
        var marker = warn.GetComponent<SpikeLaneWarnMarker>();
        shot.Call(marker, nameof(SpikeLaneWarnMarker.PlayWarning), WarnAt, WarnLen);
        shot.Call(marker, nameof(SpikeLaneWarnMarker.ResetWarning), RiseAt);

        GameObject tongue = TrailerShotKit.Find(_scene, _mgr + "TongueAttack");
        foreach (var smr in tongue.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        const string fbx = "Assets/NoAI/Mouth/TongueAttack/TongueAttack.fbx";
        shot.BindSequence(tongue.GetComponent<Animator>(), "Tongue", new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueIdle"), 0f),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueRise"), RiseAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueHold"), BreakAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueRetract"), RetractAt),
            (TrailerShotKit.LoadClip(fbx, "Tongue|TongueIdle"), RestoreAt),
        }, 0.05f);

        GameObject middle = TrailerShotKit.Find(_scene, _mgr + "Stage4_Floor/MiddleRingTile");
        var fx = shot.TileFx(middle, "Assets/Prefab/FloorTileShards.prefab", 2f, 2f, 5f,
            fullRewind: new TileRewindSettings { duration = 0.8f, distanceMin = 2f, distanceMax = 5f, stagger = 0.15f, sfxId = SFXId.None });
        shot.Call(fx, nameof(TrailerTileFx.Break), BreakAt);
        shot.Call(fx, nameof(TrailerTileFx.RestoreFull), RestoreAt);
    }

    /// <summary>입 4개: 대상 쪽으로 돌고(초당 30°) Open 0.13 → 발사 → Hold 0.2 → Close. 음식은 직선 비행.</summary>
    /// <param name="shots">(입 순서: 하이어라키 순 — +X, +Z, -Z, -X) 발사 시각, 음식, 속도, 겨눌 곳</param>
    internal static void BuildTrackers(TrailerShotKit.Shot shot, Scene scene, string trapsPath,
        (int mouth, float fire, string food, float speed, Vector3 aim)[] shots)
    {
        var trackers = new List<GameObject>();
        foreach (Transform t in TrailerShotKit.Find(scene, trapsPath).transform) trackers.Add(t.gameObject);
        const string mouthFbx = "Assets/NoAI/Mouth/Mouth3/Mouth3.fbx";
        Transform foods = TrailerShotKit.Group("Foods", shot.Root).transform;

        for (int m = 0; m < trackers.Count; m++)
        {
            GameObject tr = trackers[m];
            // 남쪽 입(z -24)은 시작 카메라(z -35) 바로 앞이라 경기장을 통째로 가린다 → 촬영에선 숨김
            if (tr.transform.position.z < -20f) { tr.SetActive(false); continue; }
            Vector3 pos = tr.transform.position;
            Animator rig = TrailerShotKit.Rig("Tracker_" + m, shot.Root, pos, tr.transform.rotation);
            tr.transform.SetParent(rig.transform, true);

            float yaw0 = tr.transform.eulerAngles.y;
            var yaws = new List<(float, float)> { (0f, yaw0) };
            var anim = new List<(AnimationClip, float)> { (TrailerShotKit.LoadClip(mouthFbx, "아마튜어|Idle"), 0f) };
            float cur = yaw0;
            foreach (var s in shots)
            {
                if (s.mouth != m) continue;
                // 입의 정면(+Z)이 처음엔 원점을 향한다(씬 회전) → 겨눌 곳 방향으로 돈다
                Vector3 dir = s.aim - new Vector3(pos.x, 0f, pos.z);
                float want = TrailerShotKit.Unwrap(cur, TrailerShotKit.Yaw(dir));
                float turn = Mathf.Min(1.2f, Mathf.Abs(want - cur) / 30f);
                yaws.Add((s.fire - 1.5f, cur)); yaws.Add((s.fire - 1.5f + turn, want));
                cur = want;
                anim.Add((TrailerShotKit.LoadClip(mouthFbx, "아마튜어|Open"), s.fire - 0.13f));
                anim.Add((TrailerShotKit.LoadClip(mouthFbx, "아마튜어|Hold"), s.fire));
                anim.Add((TrailerShotKit.LoadClip(mouthFbx, "아마튜어|Close"), s.fire + 0.2f));
                anim.Add((TrailerShotKit.LoadClip(mouthFbx, "아마튜어|Idle"), s.fire + 0.33f));
                BuildFood(shot, foods, s.food, new Vector3(pos.x, pos.y, pos.z), dir.normalized, s.speed, s.fire);
            }
            yaws.Add((Total, cur));
            AnimationClip clip = shot.NewClip(rig.name + "_Aim");
            // 회전만 넣으면 Timeline 루트 처리로 위치가 원점이 되므로 제자리 위치도 고정 키로 넣는다
            TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Const(pos.x, Total), TrailerShotKit.Const(pos.y, Total), TrailerShotKit.Const(pos.z, Total));
            TrailerShotKit.SetEuler(clip, "", TrailerShotKit.Const(rig.transform.eulerAngles.x, Total), TrailerShotKit.Smooth(yaws), TrailerShotKit.Const(0f, Total));
            shot.BindWhole(rig, clip);
            shot.BindSequence(tr.GetComponent<Animator>(), "TrackerMouth_" + m, anim, 0.03f);
        }
    }

    static void BuildFood(TrailerShotKit.Shot shot, Transform parent, string food, Vector3 from, Vector3 dir, float speed, float fire)
    {
        Vector3 to = from + dir * 60f;
        float land = fire + 60f / speed;
        Animator rig = TrailerShotKit.Rig($"{food}_{fire:0.0}", parent, from, Quaternion.LookRotation(dir));
        GameObject f = TrailerShotKit.Spawn($"Assets/Prefab/Food/use/{food}.prefab", food, rig.transform, from, Quaternion.LookRotation(dir));
        AnimationClip clip = shot.NewClip(rig.name);
        TrailerShotKit.SetPosition(clip, "",
            TrailerShotKit.Linear(new List<(float, float)> { (0f, from.x), (fire, from.x), (land, to.x) }),
            TrailerShotKit.Const(from.y, Total),
            TrailerShotKit.Linear(new List<(float, float)> { (0f, from.z), (fire, from.z), (land, to.z) }));
        shot.BindWhole(rig, clip);
        shot.BindActive(f, fire, Mathf.Min(land, Total + 1f));
    }
}
