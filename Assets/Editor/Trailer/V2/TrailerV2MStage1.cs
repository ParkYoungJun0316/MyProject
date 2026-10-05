using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 M.Stage1 (12초). 입 4개가 차례로 사과를 뱉고, 그 색 꿀떡이 자기 발판(트리거)을 밟아 그 방향 베리어 입을 올려 막는다.
/// 카메라: 경기장 가장자리 낮은 높이(반지름 10·높이 4.5)에서 지금 사과가 오는 쪽을 비스듬히 보며 경기장을 한 바퀴 가까이 돈다.
/// 베리어·바닥 충돌체는 되살린다 — 없으면 사과 파편이 베리어를 뚫고 경기장 안으로 쏟아져 게임과 달라진다.
/// 꿀떡: 제각각인 자리에서 잔걸음으로 기다리다 자기 차례에 발판으로 → 문이 올라가면 바로 내려와 다시 서성인다
///       (게임은 다음 발판이 눌릴 때까지 문이 열려 있으므로 계속 서 있을 필요가 없다).
/// 게임 값(1차와 같음): 문 -8→2 1초, 사과 12m/s 48→23.5m, 입 Open 0.13 → Hold 0.8 → Close.
/// </summary>
public static class TrailerV2MStage1
{
    const string Stage = "M.Stage1";
    const string Src   = "Assets/Scenes/M.Stage1.unity";
    const float  Total = 12f;

    const float DoorClosedY = -8f, DoorOpenY = 2f, DoorMove = 1f;
    const float AppleSpeed = 12f, AppleStartDist = 48f, AppleHitDist = 23.5f;
    static float AppleTravel => (AppleStartDist - AppleHitDist) / AppleSpeed;

    const float GroundY = 0.5f;
    const float Speed   = 9f;
    const float PadKeepOut = 2.1f; // 발판(2.7m) 반폭 + 몸 — 서성이다 남의 발판을 밟지 않게

    struct Lane { public string key; public Vector3 dir; public string trapMouth; public Vector3 pad; public float launch; public bool eastWest; }

    [MenuItem("Tools/Trailer/v2/M.Stage1")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.OpenFreshCopy(Src, TrailerV2.ScenePath(Stage));
        TrailerShotKit.UnpackAll(scene);
        TrailerShotKit.Delete(scene, "Camera", "EventSystem", "StageFlow", "SceneFlowRelay", "StageNetworkState",
            "DisconnectManager", "CheerService", "BackGround/UI");
        TrailerShotKit.HideByPrefix(scene, "ColorStartZone.");
        TrailerShotKit.HideByPrefix(scene, "WarnMarker");
        TrailerShotKit.StripLogic(scene, "EnvironmentEchoRotator");
        TrailerV2.AddSolidFromRenderer(TrailerShotKit.Find(scene, "Stage1/StageManager1/Ground/Ground"));

        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);
        Transform root = shot.Root;

        const string mouths = "Stage1/StageManager1/ArrowIncomingDirector/";
        float[] launch = { 0.7f, 3.3f, 5.9f, 8.5f };
        var lanes = new[]
        {
            new Lane { key = "B", dir = Vector3.forward, trapMouth = mouths + "Mouth1 (1)", pad = new Vector3(-5f, GroundY, 0f), launch = launch[0] },
            new Lane { key = "P", dir = Vector3.right,   trapMouth = mouths + "Mouth1 (3)", pad = new Vector3(0f, GroundY, 5f),  launch = launch[1], eastWest = true },
            new Lane { key = "G", dir = Vector3.back,    trapMouth = mouths + "Mouth1 (2)", pad = new Vector3(5f, GroundY, 0f),  launch = launch[2] },
            new Lane { key = "Y", dir = Vector3.left,    trapMouth = mouths + "Mouth1 (4)", pad = new Vector3(0f, GroundY, -5f), launch = launch[3], eastWest = true },
        };

        // ── 꿀떡 동선 먼저(발판 도착 시각이 곧 문이 올라가는 시각) ──
        var plan = BuildMoves(lanes, out float[] arrive);
        if (plan == null) return;

        Transform pads = TrailerShotKit.Group("Pads", root).transform;
        for (int i = 0; i < lanes.Length; i++)
        {
            Lane l = lanes[i];
            TrailerShotKit.Spawn($"Assets/Prefab/ColorTile_{l.key}.prefab", "Pad_" + l.key, pads, l.pad, Quaternion.identity);
            float? down = i + 1 < lanes.Length ? arrive[i + 1] : (float?)null;
            BuildDoor(shot, root, l, arrive[i], down);
            BuildApple(shot, root, l);
            BuildTrapMouth(shot, scene, l);
        }

        Transform group = TrailerShotKit.Group("Kkultteok", root).transform;
        for (int i = 0; i < plan.Count; i++)
        {
            var k = TrailerShotKit.MakeRunner(group, plan[i].key, plan[i].m.start, plan[i].yaw);
            plan[i].m.ApplyTo(k);
            if (plan[i].cheer > 0f) k.Pose(plan[i].cheer, "Yes").Pose(plan[i].cheer + 1.1f, "Idle");
            k.Build(shot);
        }

        BuildCamera(shot, lanes);

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);
        Debug.Log($"[Trailer v2] {TrailerV2.ScenePath(Stage)} 생성 — 발판 도착 {string.Join(", ", System.Array.ConvertAll(arrive, a => a.ToString("F2")))}s");
    }

    sealed class Plan { public string key; public TrailerShotKit.Moves m; public float yaw; public float cheer; }

    static List<Plan> BuildMoves(Lane[] lanes, out float[] arrive)
    {
        // 기다리는 자리(가운데 근처, 격자 아님)와 처음 바라보는 방향
        var home = new (string key, Vector2 at, float yaw)[]
        {
            ("B", new Vector2(-1.9f,  2.3f), 312f),
            ("P", new Vector2( 2.1f,  1.2f),  27f),
            ("G", new Vector2( 1.3f, -2.4f), 141f),
            ("Y", new Vector2(-2.6f, -1.0f), 236f),
        };
        arrive = new float[lanes.Length];
        string fail = null;
        for (int seed = 0; seed < 400; seed++)
        {
            var rng = new System.Random(0x5171 + seed);
            var list = new List<Plan>();
            fail = null;
            // 1) 차례 이동(발판 왕복)을 먼저 정한다
            for (int i = 0; i < lanes.Length; i++)
            {
                Vector3 a = new Vector3(home[i].at.x, GroundY, home[i].at.y);
                var m = new TrailerShotKit.Moves(a);
                float depart = lanes[i].launch + TrailerV2.Rand(rng, 0.05f, 0.3f); // 입이 열리는 걸 보고 출발
                list.Add(new Plan { key = home[i].key, m = m, yaw = home[i].yaw });
                list[i].cheer = -1f;
                arrive[i] = depart; // 임시
                _depart[i] = depart;
            }
            // 2) 출발 전까지 잔걸음(남의 발판 금지) → 발판 → 돌아와 서성임
            for (int i = 0; i < lanes.Length; i++)
            {
                var me = list[i].m;
                Vector3 a = me.start;
                bool NearPad(Vector3 q) { foreach (Lane l in lanes) if (TrailerV2.Flat(q - l.pad).magnitude < PadKeepOut) return true; return false; }
                me.Fidget(rng, 0f, _depart[i] - 0.15f, a, 1.3f, (t, q) => NearPad(q));
                arrive[i] = me.RunTo(_depart[i], lanes[i].pad, Speed);
                // 문이 올라오는 걸 잠깐 보고 내려온다 — 가운데 쪽 아무 데나(발판에서 2.4m+)
                float leave = arrive[i] + TrailerV2.Rand(rng, 0.9f, 1.6f);
                Vector3 off = lanes[i].pad * 0.45f + new Vector3(TrailerV2.Rand(rng, -1.2f, 1.2f), 0f, TrailerV2.Rand(rng, -1.2f, 1.2f));
                off.y = GroundY;
                float back = me.RunTo(leave, off, Speed);
                // 사과가 막힌 걸 보고 짧게 환호하는 사람도 있다
                float hit = lanes[i].launch + AppleTravel;
                if (i % 2 == 0) list[i].cheer = Mathf.Max(back + 0.1f, hit + 0.15f);
                float fidgetFrom = list[i].cheer > 0f ? list[i].cheer + 1.2f : back;
                me.Fidget(rng, fidgetFrom, Total, off, 1.4f, (t, q) => NearPad(q));
            }
            // 3) 서로 2m, 그리고 남의 차례 동안 발판 침범 금지
            var pairs = list.ConvertAll(p => (p.key, p.m));
            fail = TrailerV2.CheckGaps(pairs, Total, 2f);
            if (fail == null) { Debug.Log($"[Trailer v2] M1 동선 seed {seed}"); return list; }
        }
        Debug.LogError("[Trailer v2] M1 동선 실패: " + fail);
        return null;
    }

    static readonly float[] _depart = new float[4];

    static void BuildCamera(TrailerShotKit.Shot shot, Lane[] lanes)
    {
        // 차례마다 그 사과가 날아오는 쪽을 본다. 정면으로 마주 서면 사과 파편(크기 10배)이 렌즈로 날아와 화면을 덮으므로
        // 카메라는 궤적에서 Side°만큼 비켜 서서 비스듬히 본다.
        const float Side = 20f;
        var ang = new List<(float, float)> { (0f, Side - 16f) };
        var tx = new List<(float, float)> { (0f, lanes[0].dir.x * 15f - 2f) };
        var tz = new List<(float, float)> { (0f, lanes[0].dir.z * 15f) };
        for (int i = 0; i < lanes.Length; i++)
        {
            float c = lanes[i].launch + AppleTravel * 0.6f;
            ang.Add((c, i * 90f + Side));
            tx.Add((c, lanes[i].dir.x * 15f)); tz.Add((c, lanes[i].dir.z * 15f));
        }
        ang.Add((Total, 3 * 90f + Side + 14f));
        tx.Add((Total, lanes[3].dir.x * 15f)); tz.Add((Total, lanes[3].dir.z * 15f + 2f));
        AnimationCurve a = TrailerShotKit.Smooth(ang), lx = TrailerShotKit.Smooth(tx), lz = TrailerShotKit.Smooth(tz);

        const float radius = 10f, height = 4.5f;
        TrailerV2.AddFuncCamera(shot,
            t =>
            {
                float r = a.Evaluate(t) * Mathf.Deg2Rad;
                return new Vector3(-Mathf.Sin(r) * radius, height, -Mathf.Cos(r) * radius);
            },
            t => new Vector3(lx.Evaluate(t), 1.6f, lz.Evaluate(t)));
    }

    static void BuildDoor(TrailerShotKit.Shot shot, Transform root, Lane l, float up, float? down)
    {
        Vector3 closed = l.dir * 15f + Vector3.up * DoorClosedY;
        Animator rig = TrailerShotKit.Rig("Door_" + l.key, root, closed, Quaternion.identity);
        string path = $"Assets/Prefab/입/MouthBarrier.{l.key}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Quaternion rot = l.eastWest ? Quaternion.Euler(0f, -90f, 0f) * prefab.transform.rotation : prefab.transform.rotation;
        GameObject barrier = TrailerShotKit.Spawn(path, "Barrier", rig.transform, closed, rot, "MouthTrapAnimatorAnim");
        TrailerV2.RestoreBoxColliders(barrier, path); // 사과 파편이 게임처럼 베리어에 막혀 바깥으로 튀게

        var ys = new List<(float, float)> { (0f, DoorClosedY), (up, DoorClosedY), (up + DoorMove, DoorOpenY) };
        if (down.HasValue) { ys.Add((down.Value, DoorOpenY)); ys.Add((down.Value + DoorMove, DoorClosedY)); }
        ys.Add((Total, down.HasValue ? DoorClosedY : DoorOpenY));
        AnimationClip clip = shot.NewClip(rig.name + "_Move");
        TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Const(closed.x, Total), TrailerShotKit.Smooth(ys), TrailerShotKit.Const(closed.z, Total));
        shot.BindWhole(rig, clip);
    }

    static void BuildApple(TrailerShotKit.Shot shot, Transform root, Lane l)
    {
        Vector3 start = l.dir * AppleStartDist, hit = l.dir * AppleHitDist;
        float yaw = Mathf.Atan2(-l.dir.x, -l.dir.z) * Mathf.Rad2Deg;
        float tHit = l.launch + AppleTravel;
        Animator rig = TrailerShotKit.Rig("Apple_" + l.key, root, start, Quaternion.Euler(0f, yaw, 0f));
        GameObject apple = TrailerShotKit.Spawn("Assets/Prefab/Food/use/Apple.prefab", "Apple", rig.transform, start, rig.transform.rotation, "Breakable");
        Vector3 center = rig.transform.InverseTransformPoint(apple.GetComponentInChildren<Renderer>().bounds.center);
        Transform spin = TrailerShotKit.Group("Spin", rig.transform).transform;
        spin.localPosition = center;
        apple.transform.SetParent(spin, true);

        float rollDeg = (AppleStartDist - AppleHitDist) / 5.2f * Mathf.Rad2Deg;
        AnimationClip clip = shot.NewClip(rig.name + "_Fly");
        TrailerShotKit.SetPosition(clip, "", Lin(l.launch, tHit, start.x, hit.x), TrailerShotKit.Const(0f, Total), Lin(l.launch, tHit, start.z, hit.z));
        TrailerShotKit.SetEuler(clip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(yaw, Total), TrailerShotKit.Const(0f, Total));
        TrailerShotKit.SetEuler(clip, "Spin", Lin(l.launch, tHit, 0f, rollDeg), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(rig, clip);
        shot.BindActive(apple, l.launch, Total + 1f);
        shot.Call(apple.GetComponent<Breakable>(), nameof(Breakable.Break), tHit);
    }

    static void BuildTrapMouth(TrailerShotKit.Shot shot, Scene scene, Lane l)
    {
        GameObject go = TrailerShotKit.Find(scene, l.trapMouth);
        const string fbx = "Assets/NoAI/Mouth/Mouth1/Mouth1.fbx";
        shot.BindSequence(go.GetComponent<Animator>(), "TrapMouth_" + l.key, new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Idle"),  0f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Open"),  l.launch - 0.13f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Hold"),  l.launch),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Close"), l.launch + 0.8f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Idle"),  l.launch + 0.93f),
        }, 0.05f);
    }

    static AnimationCurve Lin(float t0, float t1, float v0, float v1)
        => TrailerShotKit.Linear(new List<(float, float)> { (0f, v0), (t0, v0), (t1, v1), (Total, v1) });
}
