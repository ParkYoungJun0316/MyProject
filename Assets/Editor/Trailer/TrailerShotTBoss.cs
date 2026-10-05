using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 T.Boss P4 컷 (12초, 탑다운). 네 벽(BossWall_L/F/R/B)이 계단식으로 조여 오고, 같은 색 꿀떡이 막으면 밀려난다.
/// 값은 게임 그대로(WallLineRandomizer.SqueezeRoutine · AdvancingWall · AdvancingWallTelegraph · ColorWall):
///   사이클: t 색 바뀜 → t+1 흔들림 1초(x 0.1·10Hz, y 0.04·7.3Hz) → t+2 돌진 30m/s, 거리 (100 − 2×도달)×0.9
///   새 원점(한 계단 5.6m 안쪽)은 출발 전에 확정. 같은 색이 닿으면 PauseByColorRoutine:
///   그 자리에서 새 원점으로 0.5초(선형) 되돌아가 2초(ColorWall.pauseDuration) 멈춤, 색은 바로 회색(Default).
///   색 일치: 고유색 벽 = 고유색 모드인 그 색 꿀떡, 검정/하양 벽 = Q로 그 색이 된 꿀떡.
/// 꿀떡: 제각각인 자리에서 잔걸음으로 기다리다(Moves.Fidget) 흔들림이 끝날 즈음 비스듬히 뛰어가 막는다.
///   멈출 땐 움직인 방향 그대로(제자리 회전 없음). 겹침·다른 벽의 돌진 경로는 검사해서 통과하는 시드를 고른다.
/// 시점: P4 중반(벽마다 4번 들어온 뒤, 도달 22.4m — 돌진 거리 (100 − 44.8)×0.9 = 49.7m). 사탕 구체(Cull Back)는 안쪽에서 안 보여 카메라를 위에 둘 수 있다.
/// </summary>
public static class TrailerShotTBoss
{
    const string Src      = "Assets/Scenes/T.Boss.unity";
    const string Dst      = "Assets/Scenes/Marketing_T.Boss.unity";
    const string AssetDir = "Assets/Trailer/T.Boss";
    const string ShotName = "Trailer_T.Boss";
    const string Walls    = "P4/StageManager/SurviveObjective/";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;
    const string MatDir   = "Assets/Mat/Ground/T.Boss_Color/";

    // 게임 값
    const float ColorLead = 1f, TeleDur = 1f, SurgeSpeed = 30f, FloorSize = 100f, SurgeRatio = 0.9f;
    const float PauseReturn = 0.5f, Step = 5.6f, Reached0 = Step * 4f; // 벽마다 4번 들어온 뒤(도달 22.4m, 안쪽 면 ±27.85)
    const float ShakeAmp = 0.1f, ShakeFreq = 10f;
    const float RunSpeed = 10f, BodyR = 0.75f;
    const float CamHeight = 50f; // 위아래 벽 안쪽 면(±27.85)이 화면 끝에 걸리는 높이(화면 세로 ±28.9)

    // (벽, 색 재질, 사이클 시작, 막는 꿀떡, 변신 재질(null = 고유색 그대로), 출발 위치, 처음 바라보는 방향, 막은 뒤 돌아갈 자리, 뛰는 방향 비틀기°)
    static readonly (string wall, string colorMat, float cycleAt, string key, string bodyMat, Vector2 start, float yaw, Vector2 home, float skew)[] Plan =
    {
        ("BossWall_L", "T.BossY",     0.0f, "Y", null,                          new Vector2(-4.2f, -1.3f), 212f, new Vector2(-7.4f,  0.9f),  14f),
        ("BossWall_F", "T.BossWhite", 2.5f, "P", "Assets/Mat/Player/White.mat", new Vector2(-1.1f,  3.6f),  33f, new Vector2(-2.9f,  7.6f), -11f),
        ("BossWall_R", "T.BossG",     5.0f, "G", null,                          new Vector2( 2.7f, -3.4f), 141f, new Vector2( 6.8f, -1.7f),  -8f),
        ("BossWall_B", "T.BossBlack", 7.5f, "B", "Assets/Mat/Player/Black.mat", new Vector2( 4.6f,  1.9f), 287f, new Vector2( 1.9f, -7.3f),  17f),
    };

    sealed class WallRun
    {
        public Vector3 dir; public float face0, surgeAt, contact, faceHit, back, cycleAt;
        public int blocker;
        public float Face(float t) // 진행축 좌표(안쪽 면)
        {
            if (t < surgeAt) return face0 + Reached0;
            if (t < contact) return face0 + Reached0 + SurgeSpeed * (t - surgeAt);
            if (t < back)    return Mathf.Lerp(faceHit, face0 + Reached0 + Step, (t - contact) / PauseReturn);
            return face0 + Reached0 + Step;
        }
    }

    [MenuItem("Tools/Trailer/T.Boss P4 색 벽 밀어내기")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, Dst);
        TrailerShotKit.Find(scene, "P1").SetActive(false);
        TrailerShotKit.Find(scene, "P4").SetActive(true);
        var shot = new TrailerShotKit.Shot(AssetDir, ShotName, Total);
        Material def = Mat("T.BossDefault");

        // ── 벽 정보 ──
        var walls = new WallRun[Plan.Length];
        var wallGos = new GameObject[Plan.Length];
        for (int i = 0; i < Plan.Length; i++)
        {
            GameObject wall = wallGos[i] = TrailerShotKit.Find(scene, Walls + Plan[i].wall);
            Vector3 dir = wall.transform.rotation * Vector3.right; // AdvancingWall.moveDirection (1,0,0) 로컬
            var rends = wall.GetComponentsInChildren<Renderer>(true);
            Bounds b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            walls[i] = new WallRun
            {
                dir = dir, blocker = i, cycleAt = Plan[i].cycleAt,
                face0 = Vector3.Dot(b.center, dir) + Vector3.Dot(b.extents, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))),
                surgeAt = Plan[i].cycleAt + ColorLead + TeleDur,
            };
        }

        // ── 꿀떡 동선: 시드를 바꿔 가며 겹침·벽 경로 검사를 통과할 때까지 ──
        TrailerShotKit.Moves[] moves = null;
        float[] reactAt = new float[Plan.Length];
        string fail = null;
        int seed;
        for (seed = 0; seed < 300; seed++)
        {
            moves = new TrailerShotKit.Moves[Plan.Length];
            var rng = new System.Random(0x7B055 + seed);
            for (int i = 0; i < Plan.Length; i++)
            {
                var p = Plan[i]; var w = walls[i];
                var m = moves[i] = new TrailerShotKit.Moves(new Vector3(p.start.x, GroundY, p.start.y));
                reactAt[i] = p.cycleAt + 0.35f + (float)rng.NextDouble() * 0.25f;
                float runAt = Mathf.Max(reactAt[i] + (p.bodyMat != null ? 0.65f : 0.1f), w.surgeAt - 0.25f + (float)rng.NextDouble() * 0.15f);
                m.Fidget(rng, 0f, (p.bodyMat != null ? reactAt[i] : runAt) - 0.1f, m.start, 1.6f);

                // 벽 쪽으로 비스듬히 뛰어 맞닿는 시각을 찾는다
                Vector3 from = m.End;
                Vector3 runDir = Quaternion.Euler(0f, p.skew, 0f) * -w.dir;
                float contact = -1f;
                w.contact = w.back = float.MaxValue; // 막히기 전 돌진 궤적으로 찾는다(이전 시드 값 지우기)
                float surgeEnd = w.surgeAt + (FloorSize - 2f * Reached0) * SurgeRatio / SurgeSpeed; // 돌진은 이 거리에서 끝난다
                for (float t = w.surgeAt; t < surgeEnd; t += 0.002f)
                {
                    Vector3 q = from + runDir * RunSpeed * (t - runAt);
                    if (w.Face(t) >= Vector3.Dot(q, w.dir) - BodyR) { contact = t; break; }
                }
                if (contact < 0f) { moves = null; break; }
                w.contact = contact;
                w.faceHit = w.face0 + Reached0 + SurgeSpeed * (contact - w.surgeAt);
                w.back    = contact + PauseReturn;
                m.Add(runAt, contact, from + runDir * RunSpeed * (contact - runAt));

                // 벽이 물러난 뒤 자기 자리로 — 꺾어 가는 경유점 하나, 도착하면 간 방향 그대로
                float homeAt = w.back + 0.35f + (float)rng.NextDouble() * 0.5f;
                Vector3 home = new Vector3(p.home.x, GroundY, p.home.y);
                Vector3 mid = Vector3.Lerp(m.End, home, 0.5f) + Quaternion.Euler(0f, 90f, 0f) * (home - m.End).normalized * ((float)rng.NextDouble() * 3f - 1.5f);
                if (homeAt < Total)
                {
                    float t1 = m.RunTo(homeAt, mid, RunSpeed * 0.9f);
                    float t2 = m.RunTo(t1, home, RunSpeed * 0.9f);
                    m.Fidget(rng, t2, Total, home, 1.6f);
                }
            }
            fail = moves == null ? "벽에 닿지 못함" : Validate(moves, walls);
            if (fail == null) break;
        }
        if (fail != null) { Debug.LogError("[Trailer] T.Boss 동선 실패: " + fail); return; }

        // ── 벽: 위치(진행축 이동 + 흔들림) + 색 재질 ──
        var log = new System.Text.StringBuilder($"시드 {seed}\n");
        for (int i = 0; i < Plan.Length; i++)
        {
            var p = Plan[i]; var w = walls[i]; GameObject wall = wallGos[i];
            Vector3 root = wall.transform.position;
            float Move(float t) => w.Face(t) - w.face0;
            Vector2 Shake(float t)
            {
                float e = t - (w.cycleAt + ColorLead);
                if (e < 0f || e >= TeleDur) return Vector2.zero;
                return new Vector2(Mathf.Sin(e * ShakeFreq * Mathf.PI * 2f) * ShakeAmp,
                                   Mathf.Sin(e * ShakeFreq * Mathf.PI * 2f * 0.73f + 1.1f) * ShakeAmp * 0.4f);
            }
            AnimationClip c = shot.NewClip(p.wall + "_Squeeze");
            TrailerShotKit.SetPosition(c, "",
                TrailerShotKit.Sampled(Total, t => root.x + w.dir.x * Move(t) + Shake(t).x),
                TrailerShotKit.Sampled(Total, t => root.y + Shake(t).y),
                TrailerShotKit.Sampled(Total, t => root.z + w.dir.z * Move(t)));
            TrailerShotKit.SetEuler(c, "", TrailerShotKit.Const(wall.transform.eulerAngles.x, Total),
                TrailerShotKit.Const(wall.transform.eulerAngles.y, Total), TrailerShotKit.Const(wall.transform.eulerAngles.z, Total));
            var swap = new List<(float, Material)> { (0f, def), (w.cycleAt, Mat(p.colorMat)), (w.contact, def) };
            foreach (var r in wall.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = def;
                TrailerShotKit.SetMaterialSwap(c, AnimationUtility.CalculateTransformPath(r.transform, wall.transform), typeof(MeshRenderer), swap);
            }
            shot.Animate(wall, c);
            log.AppendLine($"{p.wall} {p.colorMat}: 색 {w.cycleAt:F1}s 돌진 {w.surgeAt:F1}s 막음 {w.contact:F2}s 원점 복귀 {w.back:F2}s");
        }

        // ── 꿀떡 ──
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        for (int i = 0; i < Plan.Length; i++)
        {
            var p = Plan[i];
            var k = TrailerShotKit.MakeRunner(group, p.key, moves[i].start, p.yaw);
            if (p.bodyMat != null) k.ChangeColor(reactAt[i], p.bodyMat);
            moves[i].ApplyTo(k);
            k.Build(shot);
        }

        // ── 카메라: 바로 위에서 내려다봄(화면 위 = +Z, F 벽) ──
        Camera cam = TrailerShotKit.SpawnCamera(shot.Root);
        AnimationClip camClip = shot.NewClip("Camera_TopDown");
        TrailerShotKit.SetPosition(camClip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(CamHeight, Total), TrailerShotKit.Const(0f, Total));
        TrailerShotKit.SetEuler(camClip, "", TrailerShotKit.Const(90f, Total), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.BindWhole(cam.GetComponent<Animator>(), camClip);

        shot.AddRecorder(ShotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {Dst} 생성 완료\n{log}");
    }

    /// <summary>꿀떡끼리 2m 이상, 막는 사람 말고는 어느 벽의 돌진에도 닿지 않아야 한다.</summary>
    static string Validate(TrailerShotKit.Moves[] moves, WallRun[] walls)
    {
        for (float t = 0f; t <= Total; t += 0.03f)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                Vector3 a = moves[i].At(t);
                for (int j = i + 1; j < moves.Length; j++)
                    if (Vector3.Distance(a, moves[j].At(t)) < 2f) return $"{t:F2}s {Plan[i].key}-{Plan[j].key} 겹침";
                foreach (var w in walls)
                    if (w.blocker != i && t >= w.surgeAt && w.Face(t) >= Vector3.Dot(a, w.dir) - BodyR - 1f) return $"{t:F2}s {Plan[i].key} {Plan[w.blocker].wall}에 휩쓸림";
            }
        }
        return null;
    }

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");
}
