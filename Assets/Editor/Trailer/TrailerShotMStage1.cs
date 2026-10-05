using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Stage1 컷 (12초 = 정지 1 + 원호 10 + 정지 1).
/// 카메라: 원점을 보며 반지름 35m, (0,0,-35) 수평 → (0,35,0) 수직 내려다보기.
/// (a) 플레이: 입 4개가 차례로 사과를 뱉고, 해당 색 꿀떡이 자기 색 발판(트리거)을 밟아 그 방향 베리어 입을
///     올려 막는다 — 실제 규칙처럼 한 번에 한 색만 올라온다. 사과는 게임 Breakable로 깨진다.
/// (b) 방해공작: 위 흐름 중간에 큰 입(MouthBG)이 닫히며 암전 → 팀 외침 → 입이 다시 열린다.
/// </summary>
public static class TrailerShotMStage1
{
    const string Src      = "Assets/Scenes/M.Stage1.unity";
    const string AssetDir = "Assets/Trailer/M.Stage1";

    const float Total  = 12f;
    const float Hold   = 1f;
    const float Radius = 35f;

    const float DoorClosedY = -8f;
    const float DoorOpenY   = 2f;
    const float DoorMove    = 1f;

    const float AppleSpeed     = 12f;
    const float AppleStartDist = 48f;
    const float AppleHitDist   = 23.5f; // 베리어 앞면(15+3.5) + 사과 반지름
    static float AppleTravel => (AppleStartDist - AppleHitDist) / AppleSpeed;

    const float GroundY = 0.5f;

    struct Lane
    {
        public string  key;       // B/P/G/Y
        public string  bodyMat;
        public Vector3 dir;       // 원점 → 이 방향 입
        public string  trapMouth; // 씬 경로
        public Vector3 pad;       // 이 색 발판 위치(트리거라 높이 없음)
        public float   launch;    // 사과 발사 시각
        public bool    eastWest;
    }

    [MenuItem("Tools/Trailer/M.Stage1 (a) 플레이")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Stage1 (b) 방해공작")]
    public static void BuildHazard() => Build(true);

    static void Build(bool hazard)
    {
        string shotName = hazard ? "Trailer_M.Stage1_b" : "Trailer_M.Stage1_a";
        string dst      = $"Assets/Scenes/Marketing_M.Stage1{(hazard ? "_b" : "_a")}.unity";

        Scene scene = TrailerShotKit.OpenFreshCopy(Src, dst);
        TrailerShotKit.UnpackAll(scene);
        TrailerShotKit.Delete(scene,
            "Camera", "EventSystem", "StageFlow", "SceneFlowRelay", "StageNetworkState",
            "DisconnectManager", "CheerService", "BackGround/UI");
        foreach (string c in new[] { "Blue", "Purple", "Green", "Yellow" })
            TrailerShotKit.Find(scene, "Stage1/StageManager1/StageStartGate1/ColorStartZone." + c)?.SetActive(false);
        TrailerShotKit.HideByPrefix(scene, "WarnMarker"); // 게임에선 발사 직전에만 켜지는 경고선
        TrailerShotKit.StripLogic(scene, "EnvironmentEchoRotator");

        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        Transform root = shot.Root;
        TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, Radius, 0f, 90f, Hold);

        const string mouths = "Stage1/StageManager1/ArrowIncomingDirector/";
        // (b)는 가운데 입 닫힘(5.2~10.2초) 동안 발사를 쉬고, 다시 열릴 때 초록 차례가 온다
        float[] launch = hazard ? new[] { 0.6f, 2.6f, 8.9f, 99f } : new[] { 0.6f, 3.3f, 6.0f, 8.7f };
        var lanes = new[]
        {
            new Lane { key = "B", bodyMat = "Assets/Mat/Player/Berry.mat", dir = Vector3.forward, trapMouth = mouths + "Mouth1 (1)", pad = new Vector3(-5f, GroundY, 0f), launch = launch[0], eastWest = false },
            new Lane { key = "P", bodyMat = "Assets/Mat/Player/Guma.mat",  dir = Vector3.right,   trapMouth = mouths + "Mouth1 (3)", pad = new Vector3(0f, GroundY, 5f),  launch = launch[1], eastWest = true  },
            new Lane { key = "G", bodyMat = "Assets/Mat/Player/Sook.mat",  dir = Vector3.back,    trapMouth = mouths + "Mouth1 (2)", pad = new Vector3(5f, GroundY, 0f),  launch = launch[2], eastWest = false },
            new Lane { key = "Y", bodyMat = "Assets/Mat/Player/Dan.mat",   dir = Vector3.left,    trapMouth = mouths + "Mouth1 (4)", pad = new Vector3(0f, GroundY, -5f), launch = launch[3], eastWest = true  },
        };
        int active = hazard ? 3 : 4; // (b)는 노랑 차례 없음

        // 발판 도착 = 발사 + 0.8초 → 그 색 문이 1초에 걸쳐 올라오고, 다음 색이 올라올 때 같이 내려간다
        var doorUp = new float[lanes.Length];
        for (int i = 0; i < lanes.Length; i++) doorUp[i] = lanes[i].launch + 0.8f;

        Transform pads = TrailerShotKit.Group("Pads", root).transform;
        for (int i = 0; i < lanes.Length; i++)
        {
            Lane l = lanes[i];
            TrailerShotKit.Spawn($"Assets/Prefab/ColorTile_{l.key}.prefab", "Pad_" + l.key, pads, l.pad, Quaternion.identity);

            if (i < active)
            {
                float? down = i + 1 < active ? doorUp[i + 1] : (float?)null;
                BuildDoor(shot, root, l, doorUp[i], down);
                BuildApple(shot, root, l);
                BuildTrapMouth(shot, scene, l);
            }
            else
            {
                BuildDoor(shot, root, l, Total + 1f, null); // 닫힌 채
            }
        }

        if (hazard)
        {
            BuildMouthClose(shot, scene);
            BuildRunnersHazard(shot, root, lanes);
        }
        else
        {
            BuildRunners(shot, root, lanes);
        }

        shot.AddRecorder(shotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 — Play 하면 {Total}초 녹화 → Recordings/{shotName}.mp4");
    }

    static void BuildDoor(TrailerShotKit.Shot shot, Transform root, Lane l, float up, float? down)
    {
        Vector3 closed = l.dir * 15f + Vector3.up * DoorClosedY;
        Animator rig = TrailerShotKit.Rig("Door_" + l.key, root, closed, Quaternion.identity);

        string path = $"Assets/Prefab/입/MouthBarrier.{l.key}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Quaternion rot = l.eastWest ? Quaternion.Euler(0f, -90f, 0f) * prefab.transform.rotation : prefab.transform.rotation;
        // 게임과 같이 베리어 입은 계속 씹는다(MouthTrapAnimatorAnim 반복 모드, TrapBase 없이 동작)
        TrailerShotKit.Spawn(path, "Barrier", rig.transform, closed, rot, "MouthTrapAnimatorAnim");

        var ys = new List<(float, float)> { (0f, DoorClosedY) };
        if (up < Total)
        {
            ys.Add((up, DoorClosedY)); ys.Add((up + DoorMove, DoorOpenY));
            if (down.HasValue) { ys.Add((down.Value, DoorOpenY)); ys.Add((down.Value + DoorMove, DoorClosedY)); }
        }
        ys.Add((Total, up < Total && !down.HasValue ? DoorOpenY : DoorClosedY));

        AnimationClip clip = shot.NewClip(rig.name + "_Move");
        TrailerShotKit.SetPosition(clip, "",
            TrailerShotKit.Const(closed.x, Total), TrailerShotKit.Smooth(ys), TrailerShotKit.Const(closed.z, Total));
        shot.BindWhole(rig, clip);
    }

    static void BuildApple(TrailerShotKit.Shot shot, Transform root, Lane l)
    {
        Vector3 start = l.dir * AppleStartDist;
        Vector3 hit   = l.dir * AppleHitDist;
        float   yaw   = Mathf.Atan2(-l.dir.x, -l.dir.z) * Mathf.Rad2Deg; // 원점 쪽으로
        float   tHit  = l.launch + AppleTravel;

        Animator rig = TrailerShotKit.Rig("Apple_" + l.key, root, start, Quaternion.Euler(0f, yaw, 0f));
        // Breakable은 남겨서 부딪히는 순간 게임과 같은 파편(RubbleShards)을 튀긴다
        GameObject apple = TrailerShotKit.Spawn("Assets/Prefab/Food/use/Apple.prefab", "Apple", rig.transform, start, rig.transform.rotation, "Breakable");

        // 굴러가도록 사과 중심에 회전축(Spin)을 둔다
        Vector3 center = rig.transform.InverseTransformPoint(apple.GetComponentInChildren<Renderer>().bounds.center);
        Transform spin = TrailerShotKit.Group("Spin", rig.transform).transform;
        spin.localPosition = center;
        apple.transform.SetParent(spin, true);

        float rollDeg = (AppleStartDist - AppleHitDist) / 5.2f * Mathf.Rad2Deg;
        AnimationClip clip = shot.NewClip(rig.name + "_Fly");
        TrailerShotKit.SetPosition(clip, "",
            Linear(l.launch, tHit, start.x, hit.x), TrailerShotKit.Const(0f, Total), Linear(l.launch, tHit, start.z, hit.z));
        TrailerShotKit.SetEuler(clip, "",
            TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(yaw, Total), TrailerShotKit.Const(0f, Total));
        TrailerShotKit.SetEuler(clip, "Spin",
            Linear(l.launch, tHit, 0f, rollDeg), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(rig, clip);

        // 끄지 않는다 — 꺼지면 Breakable.OnDisable이 파편을 즉시 치운다. 깨진 뒤엔 Breakable이 스스로 숨김.
        shot.BindActive(apple, l.launch, Total + 1f);
        shot.Call(apple.GetComponent<Breakable>(), nameof(Breakable.Break), tHit);
    }

    /// <summary>게임 MouthTrapAnimatorAnim 일반 모드와 같은 순서: Open(0.13) → 발사 → Hold(0.8) → Close(0.13) → Idle.</summary>
    static void BuildTrapMouth(TrailerShotKit.Shot shot, Scene scene, Lane l)
    {
        GameObject go = TrailerShotKit.Find(scene, l.trapMouth);
        if (go == null) { Debug.LogError("[Trailer] 입 없음: " + l.trapMouth); return; }
        const string fbx = "Assets/NoAI/Mouth/Mouth1/Mouth1.fbx";
        var seq = new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Idle"),  0f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Open"),  l.launch - 0.13f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Hold"),  l.launch),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Close"), l.launch + 0.8f),
            (TrailerShotKit.LoadClip(fbx, "아마튜어|Idle"),  l.launch + 0.93f),
        };
        shot.BindSequence(go.GetComponent<Animator>(), "TrapMouth_" + l.key, seq, 0.05f);
    }

    static void BuildRunners(TrailerShotKit.Shot shot, Transform root, Lane[] lanes)
    {
        Transform group = TrailerShotKit.Group("Kkultteok", root).transform;
        TrailerShotKit.Runner Make(Lane l, Vector3 start, float yaw)
        {
            var (rig, model) = TrailerShotKit.SpawnKkultteok("Kk_" + l.key, l.bodyMat, group, start, yaw);
            return new TrailerShotKit.Runner(rig, model, start, yaw);
        }
        float Face(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float Hit(int i) => lanes[i].launch + AppleTravel;

        // 사과가 나오면 자기 색 발판으로 뛰어가 멈춘다(발판 위에서 돌지 않음). 깨진 뒤 발판을 비켜 준다.
        Make(lanes[0], new Vector3(-2f, GroundY, 2f), Face(lanes[0].dir))
            .RunTo(lanes[0].launch + 0.2f, lanes[0].launch + 0.8f, lanes[0].pad)
            .RunTo(Hit(0) + 0.5f, Hit(0) + 1.0f, new Vector3(-2.5f, GroundY, -1.5f))
            .RunTo(7.0f, 7.4f, new Vector3(-1f, GroundY, 0.5f))
            .Pose(Total - 1.2f, "Yes")
            .Build(shot);

        Make(lanes[1], new Vector3(2f, GroundY, 2f), Face(lanes[1].dir))
            .RunTo(lanes[1].launch + 0.2f, lanes[1].launch + 0.8f, lanes[1].pad)
            .RunTo(Hit(1) + 0.5f, Hit(1) + 1.0f, new Vector3(2.5f, GroundY, 1.5f))
            .Pose(Total - 1.2f, "Yes")
            .Build(shot);

        Make(lanes[2], new Vector3(2f, GroundY, -2f), Face(lanes[2].dir))
            .RunTo(lanes[2].launch + 0.2f, lanes[2].launch + 0.8f, lanes[2].pad)
            .RunTo(Hit(2) + 0.5f, Hit(2) + 1.0f, new Vector3(2f, GroundY, -2.5f))
            .Pose(Total - 1.2f, "Yes")
            .Build(shot);

        Make(lanes[3], new Vector3(-2f, GroundY, -2f), Face(lanes[3].dir))
            .RunTo(lanes[3].launch + 0.2f, lanes[3].launch + 0.8f, lanes[3].pad)
            .Pose(Total - 1.0f, "Yes")
            .Build(shot);
    }

    // 게임 MouthController 순서·값(M.Stage1: close 2 / open 2, ScreenFader maxAlpha 0.9):
    // doClose + 암전 → doHold(외칠 때까지) → doOpen + 밝아짐 → doIdle
    const float CloseAt = 5.2f, HoldAt = 7.2f, OpenAt = 8.2f, IdleAt = 10.2f;

    static void BuildMouthClose(TrailerShotKit.Shot shot, Scene scene)
    {
        var mouth = TrailerShotKit.Find(scene, "BackGround/MouthBG").GetComponent<Animator>();
        const string fbx = "Assets/NoAI/Mouth/MouthBG/MouthBG.fbx";
        shot.BindSequence(mouth, "MouthBG", new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),  0f),
            (TrailerShotKit.LoadClip(fbx, "Armature|Close"), CloseAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Hold"),  HoldAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Open"),  OpenAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),  IdleAt),
        }, 0.05f);

        TrailerShotKit.AddScreenFade(shot, new List<(float, float)>
        {
            (0f, 0f), (CloseAt, 0f), (HoldAt, 0.9f), (OpenAt, 0.9f), (IdleAt, 0f), (Total, 0f),
        });
    }

    /// <summary>(b) 동선: 파랑·보라가 사과를 막다가, 입이 닫히기 시작하면 넷이 가운데로 모여 외치고, 열리면 초록 차례.</summary>
    static void BuildRunnersHazard(TrailerShotKit.Shot shot, Transform root, Lane[] lanes)
    {
        Transform group = TrailerShotKit.Group("Kkultteok", root).transform;
        TrailerShotKit.Runner Make(Lane l, Vector3 start, float yaw)
        {
            var (rig, model) = TrailerShotKit.SpawnKkultteok("Kk_" + l.key, l.bodyMat, group, start, yaw);
            return new TrailerShotKit.Runner(rig, model, start, yaw);
        }
        float Face(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        const float gather = CloseAt - 0.6f;  // 경고를 보고 모이기 시작
        const float cheer  = CloseAt + 1.6f;  // 어두워진 뒤 외침(Yes 반복) → 입이 열린 뒤까지

        Make(lanes[0], new Vector3(-2f, GroundY, 2f), Face(lanes[0].dir))
            .RunTo(lanes[0].launch + 0.2f, lanes[0].launch + 0.8f, lanes[0].pad)
            .RunTo(gather, gather + 0.6f, new Vector3(-1.2f, GroundY, 1.2f))
            .Pose(cheer, "Yes").Pose(IdleAt - 0.6f, "Idle")
            .Build(shot);

        Make(lanes[1], new Vector3(2f, GroundY, 2f), Face(lanes[1].dir))
            .RunTo(lanes[1].launch + 0.2f, lanes[1].launch + 0.8f, lanes[1].pad)
            .RunTo(gather + 0.1f, gather + 0.6f, new Vector3(1.2f, GroundY, 1.2f))
            .Pose(cheer, "Yes").Pose(IdleAt - 0.6f, "Idle")
            .Build(shot);

        Make(lanes[2], new Vector3(2f, GroundY, -2f), Face(lanes[2].dir))
            .RunTo(gather + 0.15f, gather + 0.6f, new Vector3(1.2f, GroundY, -1.2f))
            .Pose(cheer, "Yes")
            .RunTo(lanes[2].launch + 0.2f, lanes[2].launch + 0.8f, lanes[2].pad)
            .Build(shot);

        Make(lanes[3], new Vector3(-2f, GroundY, -2f), Face(lanes[3].dir))
            .RunTo(gather + 0.05f, gather + 0.6f, new Vector3(-1.2f, GroundY, -1.2f))
            .Pose(cheer, "Yes").Pose(Total - 1.0f, "Yes")
            .Build(shot);
    }

    static AnimationCurve Linear(float t0, float t1, float v0, float v1)
    {
        var c = new AnimationCurve(new Keyframe(0f, v0), new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(Total, v1));
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
        }
        return c;
    }
}
