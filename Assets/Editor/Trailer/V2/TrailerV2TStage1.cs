using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 T.Stage1 (12초) — 이빨 벽(WallMover_Seq_1) 지그재그 + 뒤에서 굴러오는 볼더.
/// 게임 값 그대로: 꿀떡 10m/s, 볼더 8m/s·SpinRoller 10rad/s·지름 80·중심 y20(볼더는 Wall/Ground 레이어를 통과),
///   이빨 = 게임 WallMover.Activate(Signal) — 시퀀서 트리거(z 60~70)에 첫 꿀떡이 닿는 순간부터 누적 지연 0/1/3초, 0.5초 이동.
/// 옆으로 꺾는 만큼 앞으로 덜 가서 볼더가 실제로 따라붙는다(속도는 그대로).
/// 지나갈 수 있는 자리는 원본 충돌체(닫힌 이빨 + 벽)를 캡슐(반지름 0.75)로 0.25m마다 찍어 만든 지도로 검사한다.
/// </summary>
public static class TrailerV2TStage1
{
    const string Stage = "T.Stage1";
    const string Src   = "Assets/Scenes/T.Stage1.unity";
    const string Seq   = "Stage1/StageManager1/WallMover/WallMover_Seq_1";
    const float  Total = 12f;
    const float  GroundY = 0.5f;
    const float  RunSpeed = 10f;
    const float  BoulderSpeed = 8f, BoulderSpin = 10f * Mathf.Rad2Deg, BoulderY = 20f, BoulderR = 40f;
    const float  BoulderGap = 2.2f;     // 꿀떡 몸(0.75)과 볼더 표면 사이 최소 거리 — 컷에서 가장 가까운 순간
    const float  TriggerZ = 60f - 0.75f; // 시퀀서 BoxCollider z 60~70에 캡슐이 닿는 z
    static readonly float[] ToothDelay = { 0f, 1f, 3f }; // wallEntries 누적(1_2, 1_1, 1_3)
    static readonly string[] ToothOrder = { "ToothMover_1_2", "ToothMover_1_1", "ToothMover_1_3" };

    // 지도
    const float MapX0 = -24f, MapZ0 = 30f, MapCell = 0.25f;
    const int   MapW = 193, MapH = 721; // x −24~24, z 30~210
    static bool[,] _free;

    // 공통 경유점(x, z, 사람마다 흔들 폭) — 폭 0 = 좁은 틈(한 줄로)
    static readonly (float x, float z, float jitter)[] Line =
    {
        (16f,   84f,  2.5f),
        (22.4f, 97f,  0f),
        (22.4f, 110.5f, 0f),
        (14f,   118.5f, 1.2f),
        (-13f,  124.5f, 1.0f),
        (-17.5f, 133f, 1.8f),
        (-17.5f, 141.5f, 1.5f),
        (-4f,   150f,  3f),
        (15f,   159f,  1.5f),
        (16.5f, 172f,  1.5f),
        (12f,   192f,  3f),
    };

    // (꿀떡, 0초 위치, 경유점 흔들기 시드)
    static readonly (string key, Vector2 start, int seed)[] Squad =
    {
        ("B", new Vector2( 2.6f, 51.5f), 11),
        ("G", new Vector2(-3.4f, 46.5f), 23),
        ("P", new Vector2( 6.8f, 41.0f), 37),
        ("Y", new Vector2( 0.4f, 35.5f), 41),
    };

    [MenuItem("Tools/Trailer/v2/T.Stage1")]
    public static void Build()
    {
        // ── 원본 충돌체로 지도부터(닫힌 이빨) → 그다음 로직 제거 ──
        Scene scene = TrailerShotKit.OpenFreshCopy(Src, TrailerV2.ScenePath(Stage));
        BuildMap();
        TrailerShotKit.UnpackAll(scene);
        TrailerShotKit.Delete(scene, "Camera", "EventSystem", "StageFlow", "BossFlow", "SceneFlowRelay", "StageNetworkState",
            "DisconnectManager", "DisconnetManager", "CheerService", "SFXEventManager", "SalivaHazard", "BackGround/UI");
        TrailerShotKit.HideByPrefix(scene, "ColorStartZone.");
        TrailerShotKit.HideByPrefix(scene, "WarnMarker");
        TrailerShotKit.HideByPrefix(scene, "Barrier."); // 시작 장벽은 출발 때 터져 없음(도착 구역 투명 벽도 같이 — 렌더러 없음)
        TrailerShotKit.StripLogic(scene, "EnvironmentEchoRotator", "WallMover");

        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        // ── 꿀떡 경로: 경유점을 사람마다 조금씩 흔들고 Catmull-Rom으로 둥글게, 10m/s 등속 ──
        var tracks = new List<System.Func<float, Vector3>>();
        string fail = null;
        for (int attempt = 0; attempt < 400; attempt++)
        {
            tracks.Clear();
            foreach (var s in Squad) tracks.Add(MakeTrack(s.start, new System.Random(s.seed * 7919 + attempt)));
            fail = Validate(tracks);
            if (fail == null) { Debug.Log($"[Trailer v2] T1 경로 시도 {attempt}"); break; }
        }
        if (fail != null) { Debug.LogError("[Trailer v2] T1 경로 실패: " + fail); return; }

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        for (int i = 0; i < Squad.Length; i++)
        {
            var at = tracks[i];
            var k = TrailerShotKit.MakeRunner(group, Squad[i].key, at(0f), TrailerShotKit.Yaw(at(0.05f) - at(0f)));
            k.StartWith("Run", i * 0.19f);
            var keys = new List<(float, Vector3, float)>();
            float yaw = TrailerShotKit.Yaw(at(0.05f) - at(0f));
            for (float t = 0f; t <= Total + 0.001f; t += 1f / 30f)
            {
                Vector3 d = at(t + 0.05f) - at(t);
                float target = TrailerShotKit.Yaw(d);
                yaw = Mathf.LerpAngle(yaw, target, 0.55f); // 몸은 경로보다 살짝 늦게 돈다
                keys.Add((t, at(t), yaw));
            }
            k.Follow(keys);
            k.Build(shot);
        }

        // ── 이빨: 첫 꿀떡이 트리거에 닿는 순간 게임 시퀀스 ──
        float trig = float.MaxValue;
        foreach (var at in tracks)
            for (float t = 0f; t <= Total; t += 0.005f) if (at(t).z >= TriggerZ) { trig = Mathf.Min(trig, t); break; }
        for (int i = 0; i < ToothOrder.Length; i++)
        {
            var wm = (MonoBehaviour)TrailerShotKit.Find(scene, Seq + "/" + ToothOrder[i]).GetComponent("WallMover");
            shot.Call(wm, "Activate", trig + ToothDelay[i]);
        }

        // ── 볼더: 가장 가까운 순간이 BoulderGap이 되도록 출발 위치를 정한다 ──
        float zc0 = float.MaxValue;
        foreach (var at in tracks)
            for (float t = 0f; t <= Total; t += 0.02f)
            {
                Vector3 p = at(t) + Vector3.up * 0.75f;
                float flat2 = (BoulderR + 0.75f + BoulderGap) * (BoulderR + 0.75f + BoulderGap) - p.x * p.x - (p.y - BoulderY) * (p.y - BoulderY);
                zc0 = Mathf.Min(zc0, p.z - Mathf.Sqrt(flat2) - BoulderSpeed * t);
            }
        Animator boulderRig = TrailerShotKit.Rig("Boulder", shot.Root, new Vector3(0f, BoulderY, zc0), Quaternion.identity);
        TrailerShotKit.Spawn("Assets/Prefab/식도/Boulder.prefab", "Model", boulderRig.transform, boulderRig.transform.position, Quaternion.identity);
        AnimationClip bClip = shot.NewClip("Boulder_Roll");
        TrailerShotKit.SetPosition(bClip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(BoulderY, Total),
            TrailerShotKit.Sampled(Total, t => zc0 + BoulderSpeed * t));
        TrailerShotKit.SetEuler(bClip, "", TrailerShotKit.Sampled(Total, t => BoulderSpin * t), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(boulderRig, bClip);

        // ── 카메라 ──
        var camKeys = CameraKeys();
        TrailerV2.AddKeyCamera(shot, camKeys, CameraFov());
        Transform camT = shot.Root.Find("TrailerCamera");
        for (float t = 0f; t <= Total; t += 0.05f)
        {
            shot.Director.time = t; shot.Director.Evaluate();
            float inside = BoulderR + 1f - Vector3.Distance(camT.position, new Vector3(0f, BoulderY, zc0 + BoulderSpeed * t));
            if (inside > 0f) { Debug.LogError($"[Trailer v2] T1 {t:F2}s 카메라가 볼더에 {inside:F1}m 들어감"); break; }
        }
        shot.Director.time = 0; shot.Director.Evaluate();

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);

        var sb = new System.Text.StringBuilder($"[Trailer v2] T1 저장 — 트리거 {trig:F2}s, 볼더 중심 z {zc0:F1}→{zc0 + BoulderSpeed * Total:F1}\n");
        for (int i = 0; i < Squad.Length; i++)
        {
            float closest = float.MaxValue;
            for (float t = 0f; t <= Total; t += 0.02f)
            {
                Vector3 p = tracks[i](t) + Vector3.up * 0.75f;
                closest = Mathf.Min(closest, Vector3.Distance(p, new Vector3(0f, BoulderY, zc0 + BoulderSpeed * t)) - BoulderR - 0.75f);
            }
            sb.AppendLine($"{Squad[i].key}: z {tracks[i](0f).z:F0}→{tracks[i](Total).z:F0}, 볼더와 최소 {closest:F1}m");
        }
        Debug.Log(sb.ToString());
    }

    // 카메라 키(시각, 위치, 바라볼 점) — 이빨 위(y ≥ 14, 이빨 꼭대기 12)·원통(반지름 30) 안에서 뒤를 본다.
    // 앞쪽은 좁은 화각으로 당겨 꿀떡과 볼더를 겹쳐 보이게, 틈을 빠져나올 때 넓히며 왼쪽 앞으로 물러난다.
    static List<(float t, Vector3 pos, Vector3 look)> CameraKeys() => new()
    {
        (0f,   new Vector3( 10f, 22f, 128f), new Vector3( 8f, 1.5f,  62f)),
        (3f,   new Vector3( 12f, 21f, 127f), new Vector3(16f, 1.5f,  82f)),
        (5.5f, new Vector3(  6f, 18f, 128f), new Vector3(20f, 1f,   100f)),
        (8.5f, new Vector3(-10f, 16f, 136f), new Vector3(10f, 1f,   112f)),
        (12f,  new Vector3(-16f, 22f, 162f), new Vector3(-4f, 1f,   126f)),
    };

    static List<(float t, float fov)> CameraFov() => new() { (0f, 36f), (3f, 38f), (5.5f, 46f), (8.5f, 52f), (12f, 58f) };

    /// <summary>편집 모드 미리보기 — Signal이 안 돌아 이빨이 안 움직이므로 시각에 맞춰 직접 옮겨 찍고 되돌린다.</summary>
    public static string PreviewWithTeeth(string times, float trig)
    {
        var teeth = new List<(Transform tr, Vector3 a, Vector3 b, float t0)>();
        for (int i = 0; i < ToothOrder.Length; i++)
        {
            Transform tr = GameObject.Find(Seq + "/" + ToothOrder[i]).transform;
            var so = new SerializedObject(tr.GetComponent("WallMover"));
            teeth.Add((tr, tr.position, tr.position + tr.TransformDirection(so.FindProperty("moveOffset").vector3Value), trig + ToothDelay[i]));
        }
        var outList = new List<string>();
        foreach (string s in times.Split(','))
        {
            float t = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var th in teeth) th.tr.position = Vector3.Lerp(th.a, th.b, Mathf.Clamp01((t - th.t0) / 0.5f));
            outList.Add(TrailerV2.Preview(s));
        }
        foreach (var th in teeth) th.tr.position = th.a;
        return string.Join("\n", outList);
    }

    // ── 지도 ──

    static void BuildMap()
    {
        GameObject seq = GameObject.Find(Seq);
        var saved = new List<(Transform, Vector3)>();
        foreach (Transform t in seq.transform)
        {
            var so = new SerializedObject(t.GetComponent("WallMover"));
            saved.Add((t, t.position));
            t.position += t.TransformDirection(so.FindProperty("moveOffset").vector3Value);
        }
        Physics.SyncTransforms();
        int wall = LayerMask.NameToLayer("Wall");
        _free = new bool[MapW, MapH];
        for (int i = 0; i < MapW; i++)
            for (int j = 0; j < MapH; j++)
            {
                float x = MapX0 + i * MapCell, z = MapZ0 + j * MapCell;
                bool hit = false;
                foreach (Collider c in Physics.OverlapCapsule(new Vector3(x, 1.25f, z), new Vector3(x, 2.5f, z), 0.75f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.gameObject.layer == wall) { hit = true; break; }
                _free[i, j] = !hit;
            }
        foreach (var (t, p) in saved) t.position = p;
        Physics.SyncTransforms();
    }

    static bool Free(Vector3 p)
    {
        int i = Mathf.RoundToInt((p.x - MapX0) / MapCell), j = Mathf.RoundToInt((p.z - MapZ0) / MapCell);
        if (i < 0 || j < 0 || i >= MapW || j >= MapH) return false;
        return _free[i, j];
    }

    // ── 경로 ──

    static System.Func<float, Vector3> MakeTrack(Vector2 start, System.Random rng)
    {
        var pts = new List<Vector3> { new Vector3(start.x, GroundY, start.y) };
        foreach (var w in Line)
            pts.Add(new Vector3(w.x + TrailerV2.Rand(rng, -w.jitter, w.jitter), GroundY, w.z + TrailerV2.Rand(rng, -w.jitter, w.jitter) * 0.5f));
        // Catmull-Rom 촘촘히 → 호 길이 표
        var dense = new List<Vector3>();
        for (int s = 0; s < pts.Count - 1; s++)
        {
            Vector3 p0 = pts[Mathf.Max(0, s - 1)], p1 = pts[s], p2 = pts[s + 1], p3 = pts[Mathf.Min(pts.Count - 1, s + 2)];
            for (int k = 0; k < 40; k++)
            {
                float u = k / 40f;
                dense.Add(0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u));
            }
        }
        dense.Add(pts[pts.Count - 1]);
        var len = new float[dense.Count];
        for (int i = 1; i < dense.Count; i++) len[i] = len[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
        return t =>
        {
            float d = Mathf.Clamp(t * RunSpeed, 0f, len[len.Length - 1]);
            int i = System.Array.BinarySearch(len, d);
            if (i >= 0) return dense[i];
            i = ~i;
            return Vector3.Lerp(dense[i - 1], dense[i], Mathf.InverseLerp(len[i - 1], len[i], d));
        };
    }

    static string Validate(List<System.Func<float, Vector3>> tracks)
    {
        for (float t = 0f; t <= Total + 1f; t += 0.01f)
            for (int i = 0; i < tracks.Count; i++)
            {
                Vector3 p = tracks[i](t);
                if (!Free(p)) return $"{t:F2}s {Squad[i].key} 벽 {p:F1}";
                for (int j = i + 1; j < tracks.Count; j++)
                {
                    float d = TrailerV2.Flat(p - tracks[j](t)).magnitude;
                    if (d < 2f) return $"{t:F2}s {Squad[i].key}-{Squad[j].key} {d:F2}m";
                }
            }
        return null;
    }
}
