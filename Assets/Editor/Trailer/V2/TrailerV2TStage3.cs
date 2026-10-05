using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 T.Stage3 (12초) — 구간 A 가시 레인(SpikeLaneField33)을 넷이 옆으로 피하며 달린다.
/// 규칙·값은 게임 그대로: 3초마다 레인 2개(직전 두 레인 제외 — excludeLastLanes), 경고 1.5초(SpikeLane.PlayWarning — 탠저린→진홍)
///   뒤 그 레인 전체가 터짐(SpikeLane.Trigger → SpikeTrap 0.3초 판정 + SpikeTrapVFX). 레인 x: L1 10.5~21, L2 3.5~10.5, L3 −3.5~3.5, L4 −10.5~−3.5, L5 −21~−10.5.
///   판정 상자 = 레인 폭 그대로, 꿀떡 몸 반지름 0.75 → 터지는 0.3초 동안 몸 중심이 레인 경계에서 0.75 밖이어야 안전.
/// 구간: 문(PushWay, z 174)을 지난 뒤 ~ 다음 벽(PushWay (1), z 291) 전. 옆벽(ColorWall) 안쪽 면 ±18 → 몸 중심 ±17.
/// 꿀떡 10m/s(방향만 바꿈), 경고를 보고 0.2~0.6초 뒤 안전 레인 쪽으로 비스듬히, 가끔 레인 앞에서 멈칫.
/// </summary>
public static class TrailerV2TStage3
{
    const string Stage = "T.Stage3";
    const string Src   = "Assets/Scenes/T.Stage3.unity";
    const string Field = "Stage3/Stagemanager3/SpikeLaneField33";
    const float  Total = 12f;
    const float  GroundY = 0.5f;
    const float  Speed = 10f, BodyR = 0.75f;
    const float  Warn = 1.5f, Burst = 0.3f;
    // 앞: PushWay (1) 벽(z 291) 전, 그 앞 버티기 발판 JellyPad_2(x 6.7~15.3, z 269.1~277.7)는 밟지 않는다(밟으면 n/4 표시가 떠야 함)
    const float  XLimit = 17f, ZLimit = 283f; // 벽 앞 287까지 갈 수 있지만 끝 화면(카메라 z ≤ 289)에 넷이 다 들어오게
    static readonly Vector2 PadMin = new Vector2(6.7f, 269.1f), PadMax = new Vector2(15.3f, 277.7f);

    static readonly (float min, float max)[] Lanes = { (10.5f, 21f), (3.5f, 10.5f), (-3.5f, 3.5f), (-10.5f, -3.5f), (-21f, -10.5f) };

    // (경고 시각, 레인 두 개 — 0부터) — 직전 두 레인은 안 고른다
    // 마지막은 L2+L5 — 넷이 가운데(L3·L4)로 모여 오른쪽 버티기 발판을 비켜 간다
    static readonly (float t, int a, int b)[] Cycles = { (0.4f, 1, 3), (3.4f, 2, 4), (6.4f, 0, 3), (9.4f, 1, 4) };

    // 꿀떡: 시작 위치, 사이클마다 목표 x(경고를 본 뒤 그쪽으로)
    static readonly (string key, Vector2 start, float yaw, float[] targets)[] Squad =
    {
        ("B", new Vector2(  6.2f, 177.0f),  -4f, new[] {  1.6f,  7.0f,  1.6f, -1.4f }),
        ("P", new Vector2( -3.9f, 182.2f),   5f, new[] { -1.4f, -6.3f, -1.8f, -5.2f }),
        ("G", new Vector2( 13.4f, 180.6f),   9f, new[] { 13.6f, 14.4f,  4.9f,  2.0f }),
        ("Y", new Vector2(-11.6f, 179.4f),  -7f, new[] { -13.4f, -7.6f, -13.6f, -8.8f }),
    };

    [MenuItem("Tools/Trailer/v2/T.Stage3")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "SpikeLane", "SpikeLaneWarnMarker", "SpikeTrap", "SpikeTrapVFX");
        TrailerShotKit.HideByPrefix(scene, "AcidSurface."); // 위액은 시간 초과 3초 전까지 없음
        GameObject field = TrailerShotKit.Find(scene, Field);
        foreach (Transform t in field.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("WarnMarker")) t.gameObject.SetActive(true); // SpikeLaneWarnMarker가 스스로 숨긴다
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        // ── 레인: 게임 SpikeLane 호출 ──
        var lanes = new List<MonoBehaviour>();
        foreach (Transform l in field.transform) lanes.Add((MonoBehaviour)l.GetComponent("SpikeLane"));
        foreach (var c in Cycles)
            foreach (int li in new[] { c.a, c.b })
            {
                shot.Call(lanes[li], "PlayWarning", c.t, Warn);
                shot.Call(lanes[li], "Trigger", c.t + Warn);
            }

        // ── 꿀떡 ──
        List<System.Func<float, Vector3>> tracks = null;
        string fail = null;
        int seed;
        var reasons = new Dictionary<string, int>();
        for (seed = 0; seed < 300; seed++)
        {
            tracks = Simulate(new System.Random(0x3E3 + seed * 17));
            fail = Validate(tracks);
            if (fail == null) break;
            string kind = fail.Substring(fail.IndexOf(' ') + 1, 6);
            reasons[kind] = reasons.TryGetValue(kind, out int c0) ? c0 + 1 : 1;
        }
        if (fail != null)
        {
            var r = new System.Text.StringBuilder();
            foreach (var kv in reasons) r.Append($"{kv.Key}×{kv.Value} ");
            Debug.LogError("[Trailer v2] T3 동선 실패: " + fail + " | " + r);
            return;
        }

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        for (int i = 0; i < Squad.Length; i++)
        {
            var at = tracks[i];
            var k = TrailerShotKit.MakeRunner(group, Squad[i].key, at(0f), Squad[i].yaw);
            bool running = (at(0.06f) - at(0f)).magnitude / 0.06f > 1f;
            if (running) k.StartWith("Run", i * 0.13f);
            var keys = new List<(float, Vector3, float)>();
            float yaw = Squad[i].yaw;
            for (float t = 0f; t <= Total + 0.001f; t += 1f / 30f)
            {
                Vector3 d = at(Mathf.Min(Total + 1f, t + 0.06f)) - at(t);
                bool move = d.magnitude / 0.06f > 1f;
                if (move) yaw = Mathf.LerpAngle(yaw, TrailerShotKit.Yaw(d), 0.5f);
                if (move != running && t > 0.001f) { k.Pose(t, move ? "Run" : "Idle"); running = move; }
                keys.Add((t, at(t), yaw));
            }
            k.Follow(keys);
            k.Build(shot);
        }

        // ── 카메라: 무리 앞(+z)에서 뒤를 보며 같이 물러난다 — 레인 줄이 뒤로 길게 뻗어 보이게.
        //    앞 벽(z 291)·구역 간판(z 295) 앞에서 멈추고(z ≤ 285) 그 뒤로는 고개만 숙인다. ──
        System.Func<float, float> zc = t => { float s = 0f; foreach (var at in tracks) s += at(Mathf.Clamp(t, 0f, Total)).z; return s / tracks.Count; };
        TrailerV2.AddFuncCamera(shot,
            t => { float u = TrailerShotKit.SmootherStep(t / Total); return new Vector3(Mathf.Lerp(9f, -4f, u), Mathf.Lerp(7.5f, 15f, u), SoftMin(zc(t) + Mathf.Lerp(21f, 17f, u), 289f)); },
            t => new Vector3(Mathf.Lerp(2f, -2f, t / Total), 0.5f, zc(t) - Mathf.Lerp(9f, 4f, TrailerShotKit.SmootherStep((t - 8f) / 4f))),
            new List<(float, float)> { (0f, 52f), (Total, 56f) });

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);
        var sb = new System.Text.StringBuilder($"[Trailer v2] T3 저장 — 시드 {seed}\n");
        foreach (var c in Cycles) sb.Append($"경고 {c.t:F1}s L{c.a + 1}+L{c.b + 1} → 터짐 {c.t + Warn:F1}s | ");
        for (int i = 0; i < Squad.Length; i++) sb.Append($"\n{Squad[i].key}: z {tracks[i](0f).z:F0}→{tracks[i](Total).z:F0}");
        Debug.Log(sb.ToString());
    }

    /// <summary>속도 10 고정, 방향만 바꾼다. 경고를 보고 반응 → 목표 x로 비스듬히. 가끔 위험 레인 앞에서 멈칫(터지는 걸 보고 건넘).</summary>
    static List<System.Func<float, Vector3>> Simulate(System.Random rng)
    {
        const float dt = 0.01f;
        int n = Mathf.CeilToInt((Total + 1f) / dt) + 1;
        var list = new List<System.Func<float, Vector3>>();
        foreach (var s in Squad)
        {
            var pts = new Vector3[n];
            float x = s.start.x, z = s.start.y, vx = 0f;
            float targetX = x;
            var react = new float[Cycles.Length];
            var pause = new (float from, float to)[Cycles.Length];
            var look = new (float from, float to)[Cycles.Length];
            for (int c = 0; c < Cycles.Length; c++)
            {
                react[c] = Cycles[c].t + TrailerV2.Rand(rng, 0.2f, 0.6f);
                pause[c] = rng.NextDouble() < 0.7 ? (Cycles[c].t + Warn - TrailerV2.Rand(rng, 0.6f, 0.9f), Cycles[c].t + Warn + TrailerV2.Rand(rng, 0.1f, 0.35f)) : (-1f, -1f);
                // 경고가 뜬 순간 멈칫하고 보는 사람
                float l0 = Cycles[c].t + TrailerV2.Rand(rng, 0.1f, 0.3f);
                look[c] = rng.NextDouble() < 0.85 ? (l0, l0 + TrailerV2.Rand(rng, 0.4f, 0.85f)) : (-1f, -1f);
            }
            float sway = TrailerV2.Rand(rng, 0f, 6.28f);
            float go = TrailerV2.Rand(rng, 0.15f, 0.8f); // 문을 막 나와 둘러보다 출발
            for (int i = 0; i < n; i++)
            {
                float t = i * dt;
                pts[i] = new Vector3(x, GroundY, z);
                if (t < go) continue;
                for (int c = 0; c < Cycles.Length; c++) if (t >= react[c]) targetX = s.targets[c];
                float wanted = Mathf.Clamp((targetX + 0.5f * Mathf.Sin(sway + t * 0.9f) - x) * 3.2f, -8.5f, 8.5f);
                vx = Mathf.MoveTowards(vx, wanted, 60f * dt);
                bool stop = false;
                for (int c = 0; c < Cycles.Length; c++)
                {
                    if (t >= pause[c].from && t < pause[c].to && Mathf.Abs(targetX - x) > 0.8f && !WouldEnterDanger(x, c) && WouldEnterDanger(x + Mathf.Sign(targetX - x) * 1.2f, c)) stop = true;
                    if (t >= look[c].from && t < look[c].to && !WouldEnterDanger(x, c)) stop = true; // 지금 자리가 안전할 때만 서서 본다
                }
                if (stop) { vx = 0f; continue; }
                x += vx * dt;
                z += Mathf.Sqrt(Mathf.Max(0f, Speed * Speed - vx * vx)) * dt;
            }
            list.Add(t => { float f = Mathf.Clamp(t / dt, 0f, n - 1); int a = Mathf.FloorToInt(f); int b = Mathf.Min(n - 1, a + 1); return Vector3.Lerp(pts[a], pts[b], f - a); });
        }
        return list;
    }

    /// <summary>cap 근처에서 부드럽게 멈추는 min(카메라가 벽 앞에서 툭 서지 않게).</summary>
    static float SoftMin(float v, float cap) { const float k = 4f; return cap - k * Mathf.Log(1f + Mathf.Exp((cap - v) / k)); }

    static bool WouldEnterDanger(float x, int cycle)
    {
        foreach (int li in new[] { Cycles[cycle].a, Cycles[cycle].b })
            if (x > Lanes[li].min - BodyR && x < Lanes[li].max + BodyR) return true;
        return false;
    }

    static string Validate(List<System.Func<float, Vector3>> tracks)
    {
        for (float t = 0f; t <= Total; t += 0.01f)
            for (int i = 0; i < tracks.Count; i++)
            {
                Vector3 p = tracks[i](t);
                if (Mathf.Abs(p.x) > XLimit) return $"{t:F2}s {Squad[i].key} 옆벽 x {p.x:F1}";
                if (p.z > ZLimit) return $"{t:F2}s {Squad[i].key} 앞 벽까지 감 z {p.z:F1}";
                if (p.z > PadMin.y - BodyR && p.z < PadMax.y + BodyR && p.x > PadMin.x - BodyR && p.x < PadMax.x + BodyR)
                    return $"{t:F2}s {Squad[i].key} 버티기 발판 밟음 {p:F1}";
                for (int c = 0; c < Cycles.Length; c++)
                {
                    float f = Cycles[c].t + Warn;
                    if (t >= f - 0.02f && t <= f + Burst + 0.02f && WouldEnterDanger(p.x, c)) return $"{t:F2}s {Squad[i].key} 터지는 레인 x {p.x:F1}";
                }
                for (int j = i + 1; j < tracks.Count; j++)
                {
                    float d = TrailerV2.Flat(p - tracks[j](t)).magnitude;
                    if (d < 2f) return $"{t:F2}s {Squad[i].key}-{Squad[j].key} {d:F2}m";
                }
            }
        return null;
    }
}
