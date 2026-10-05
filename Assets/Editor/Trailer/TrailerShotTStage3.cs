using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Stage3 컷 (12초) — Sector D(z 905~1068) 4인 버티기 발판 JellyPad_4 → JellyDoor_4가 내려간다.
/// 값은 게임 그대로: PressurePad.requiredCount 4, DoorController SlideDown 6m · duration 10초(SmoothStep) · latchOnFullyOpen
///   (다 내려가면 고정 → 표시 초록 4/4). 발판 표시 = WorldCountLabel + PressurePadCountUI 규칙(흰 n/4, 주황 남은 초),
///   물결 = 게임 PadOccupancyFeedback(발판 OnCountChanged). 구역 간판 Sign.D = SegmentTimerSign 규칙.
///   위액 수면(SegmentAcidRise)은 시간 초과 3초 전까지 꺼져 있어 숨긴다.
/// 카메라(천장 = 팔각 원통 꼭대기 y≈24.4):
///   (a) 발판 중심 원호 반지름 20 — 발판 20m 뒤 낮은 곳 → 발판 바로 위(높이 21.5)
///   (b) 사용자 지정 (0,0,950)에서 출발해 천장 아래(≤ 21.5)로 날아 들어가 발판 바로 위에서 내려다봄
/// </summary>
public static class TrailerShotTStage3
{
    const string Src      = "Assets/Scenes/T.Stage3.unity";
    const string AssetDir = "Assets/Trailer/T.Stage3";
    const string Way      = "Stage3/Stagemanager3/PushWay/PushWay (8)/";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;
    const float  EyeY     = 1.5f;
    const float  RunSpeed = 10f, BodyR = 0.75f;
    const float  SignRemaining = 24f; // 컷 시작 때 구역 D 남은 초(제한 40)

    [MenuItem("Tools/Trailer/T.Stage3 (a) 발판 원호")]
    public static void BuildOrbit() => Build(false);

    [MenuItem("Tools/Trailer/T.Stage3 (b) 950에서 날아오기")]
    public static void BuildFly() => Build(true);

    static void Build(bool fly)
    {
        string suffix   = fly ? "_b" : "_a";
        string shotName = "Trailer_T.Stage3" + suffix;
        string dst      = $"Assets/Scenes/Marketing_T.Stage3{suffix}.unity";

        Scene scene = TrailerShotKit.PrepStage(Src, dst, "PressurePad", "PadOccupancyFeedback", "DoorController", "SegmentTimerSign", "SegmentTimer");
        TrailerShotKit.HideByPrefix(scene, "AcidSurface.");
        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);

        var pad  = TrailerShotKit.Find(scene, Way + "JellyPad_4").GetComponent<PressurePad>();
        var door = TrailerShotKit.Find(scene, Way + "JellyDoor_4").GetComponent<DoorController>();
        var padCol = pad.GetComponent<Collider>();
        Bounds padB = pad.GetComponent<Renderer>().bounds;
        Vector3 padC = padB.center;
        float standY = padCol != null && !padCol.isTrigger ? padB.max.y : GroundY; // 트리거면 바닥 높이 그대로(올라서지 않음)

        // ── 구역 간판: 게임 컴포넌트 값을 읽고 촬영용으로 바꿔 단다 ──
        var signGo = TrailerShotKit.Find(scene, "SegmentDeadlines/Sign.D");
        var signSrc = new SerializedObject(signGo.GetComponent("SegmentTimerSign"));
        var sign = signGo.AddComponent<TrailerSegmentSign>();
        var fillsProp = signSrc.FindProperty("timerFills");
        var fills = new List<Renderer>();
        for (int i = 0; i < fillsProp.arraySize; i++) fills.Add((Renderer)fillsProp.GetArrayElementAtIndex(i).objectReferenceValue);
        sign.fills         = fills.ToArray();
        sign.secondsText   = (TMP_Text)signSrc.FindProperty("secondsText").objectReferenceValue;
        sign.fillColor     = signSrc.FindProperty("fillColor").colorValue;
        sign.fillWarnColor = signSrc.FindProperty("fillWarnColor").colorValue;
        sign.warnSeconds   = signSrc.FindProperty("warnSeconds").floatValue;
        var timer = (Component)signSrc.FindProperty("timer").objectReferenceValue;
        sign.duration         = new SerializedObject(timer).FindProperty("duration").floatValue;
        sign.remainingAtStart = SignRemaining;
        foreach (var mb in scene.GetRootGameObjects())
            foreach (var c in mb.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null && (c.GetType().Name == "SegmentTimerSign" || c.GetType().Name == "SegmentTimer")) Object.DestroyImmediate(c);

        // ── 발판 표시 ──
        var label = TrailerShotKit.Group("PadLabel", shot.Root).AddComponent<TrailerPadLabel>();
        label.pad = pad; label.door = door; label.required = pad.requiredCount;

        // ── 꿀떡: 제각각인 자리에서 한 번 꺾어 들어와 발판 위 아무 데나 선다(도착 간격도 들쭉날쭉).
        //    올라선 뒤엔 발판 안(가장자리 1.2m 안쪽)에서만 잔걸음 — 나가면 게임에서 버티기 시간이 처음부터다. ──
        var plan = new (string key, Vector2 from, float yaw, Vector2 bend, Vector2 spot, float arrive)[]
        {
            ("B", new Vector2( 4.6f, 1003.4f), 64f, new Vector2(-1.8f,  0.9f), new Vector2(-2.1f, -1.3f), 0.85f),
            ("Y", new Vector2(18.4f, 1006.6f), 251f, new Vector2( 0.7f, -2.1f), new Vector2( 1.4f, -2.2f), 1.1f),
            ("P", new Vector2(16.8f, 1019.5f), 197f, new Vector2( 2.2f,  1.0f), new Vector2( 2.5f,  1.1f), 1.5f),
            ("G", new Vector2( 1.5f, 1013.5f), 118f, new Vector2(-1.2f, -1.6f), new Vector2(-0.7f,  2.0f), 1.75f),
        };
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        var enter = new List<float>();
        var all = new List<TrailerShotKit.Moves>();
        float half = padB.extents.x + BodyR;
        float inner = padB.extents.x - 1.2f;
        string fail = null;
        for (int seed = 0; seed < 300; seed++)
        {
            var rng = new System.Random(0x73D + seed);
            all.Clear(); enter.Clear(); fail = null;
            foreach (var p in plan)
            {
                Vector3 a = new Vector3(p.from.x, GroundY, p.from.y);
                Vector3 b = new Vector3(padC.x + p.spot.x, standY, padC.z + p.spot.y);
                Vector3 mid = Vector3.Lerp(a, b, 0.5f) + new Vector3(p.bend.x, 0f, p.bend.y);
                float len = Vector3.Distance(a, mid) + Vector3.Distance(mid, b);
                float depart = p.arrive - len / RunSpeed;
                var m = new TrailerShotKit.Moves(a);
                m.Fidget(rng, 0f, depart - 0.1f, a, 1.2f);
                float t1 = m.RunTo(depart, mid, RunSpeed);
                m.RunTo(t1, b, RunSpeed);
                for (float t = 0f; t <= Total; t += 0.005f)
                {
                    Vector3 q = m.At(t);
                    if (Mathf.Abs(q.x - padC.x) < half && Mathf.Abs(q.z - padC.z) < half) { enter.Add(t); break; }
                }
                all.Add(m);
            }
            // 다 도착한 자리를 알고 나서 발판 위 잔걸음 — 앞사람 잔걸음·뒷사람 자리를 모두 피한다
            Vector3 c = new Vector3(padC.x, standY, padC.z);
            for (int i = 0; i < all.Count; i++)
            {
                var me = all[i];
                me.Fidget(rng, plan[i].arrive, Total, c, inner,
                    (t, q) => Mathf.Abs(q.x - padC.x) > inner || Mathf.Abs(q.z - padC.z) > inner
                           || all.Exists(o => o != me && Vector3.Distance(o.At(t), q) < 2f));
            }
            for (float t = 0f; t <= Total && fail == null; t += 0.03f)
                for (int i = 0; i < all.Count && fail == null; i++)
                    for (int j = i + 1; j < all.Count; j++)
                        if (Vector3.Distance(all[i].At(t), all[j].At(t)) < 2f) { fail = $"{t:F2}s {plan[i].key}-{plan[j].key} 겹침"; break; }
            if (fail == null) break;
        }
        if (fail != null) { Debug.LogError("[Trailer] T3 동선 실패: " + fail); return; }
        for (int i = 0; i < plan.Length; i++)
        {
            var k = TrailerShotKit.MakeRunner(group, plan[i].key, all[i].start, plan[i].yaw);
            all[i].ApplyTo(k);
            k.Build(shot);
        }
        enter.Sort();
        for (int i = 0; i < enter.Count; i++) shot.Call(label, nameof(TrailerPadLabel.SetCount), enter[i], i + 1);
        float fulfilled = enter[enter.Count - 1];
        shot.Call(door, nameof(DoorController.Open), fulfilled);

        // ── 카메라 ──
        Vector3 pivot = new Vector3(padC.x, EyeY, padC.z);
        if (!fly)
            TrailerShotKit.AddFollowOrbitCamera(shot, _ => pivot, 20f, 0f, 90f, 1f, 0f);
        else
            AddFlyCamera(shot, new Vector3(0f, EyeY, 950f), pivot, 20f);

        shot.AddRecorder(shotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 — 발판 {(standY > GroundY ? "단단함(위에 섬)" : "트리거(바닥 높이)")}, " +
                  $"인원 {string.Join(", ", enter.ConvertAll(t => t.ToString("F2")))}s, 문 하강 {fulfilled:F2}s → 고정 {fulfilled + door.duration:F2}s");
    }

    /// <summary>(b) start에서 출발해 target 바로 위 height까지 날아간다. 높이는 끝에 갈수록 올라가 천장(≈24.4) 아래에 머문다.</summary>
    static void AddFlyCamera(TrailerShotKit.Shot shot, Vector3 start, Vector3 target, float height)
    {
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        AnimationClip clip = shot.NewClip("Camera_Fly");
        const float hold = 1f;
        float move = Total - hold * 2f;
        Vector3 end = target + Vector3.up * height;
        float yaw = TrailerShotKit.Yaw(new Vector3(target.x - start.x, 0f, target.z - start.z));
        float U(float t) => TrailerShotKit.SmootherStep((t - hold) / move);
        Vector3 Pos(float t)
        {
            float u = U(t);
            Vector3 p = Vector3.Lerp(start, end, u);
            p.y = start.y + (end.y - start.y) * u * u; // 앞쪽은 낮게 날다가 다가가며 올라간다
            return p;
        }
        float Pitch(float t)
        {
            Vector3 d = target - Pos(t);
            float flat = new Vector2(d.x, d.z).magnitude;
            return Mathf.Clamp(Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg, 0f, 90f);
        }
        TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Sampled(Total, t => Pos(t).x), TrailerShotKit.Sampled(Total, t => Pos(t).y), TrailerShotKit.Sampled(Total, t => Pos(t).z));
        TrailerShotKit.SetEuler(clip, "", TrailerShotKit.Sampled(Total, Pitch), TrailerShotKit.Const(yaw, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(cam.GetComponent<Animator>(), clip);
    }
}
