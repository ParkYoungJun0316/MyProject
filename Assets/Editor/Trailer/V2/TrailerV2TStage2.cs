using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 T.Stage2 (12초) — Stage2.3 개척 길(PioneerPath, 10×10 · 4m 칸, 구역 4개를 한 붓 그리기로).
/// 규칙은 게임 그대로: 구역 담당 색이 먼저 밟은 Path 칸만 열려(PioneerPathTile.Unlock — 게임 컴포넌트를 Signal로) 이후 누구나 통과,
///   담당이 아닌 색이 안 열린 칸·함정 칸을 밟으면 데미지 → 따라가는 사람은 열린 칸만 밟는다(출발 시각이 그 칸 개방 이후).
///   색은 매니저 값(평소 검정 · 개방 분홍) — Zone.Init/Manager.Start가 그대로 칠한다.
/// 구역 담당: 1 파랑 · 2 보라 · 3 초록 · 4 노랑(씬 기본값, 4인 배정 중 하나).
/// 1구역 끝 칸에서 파랑이 칸 북쪽으로 비켜서고 보라가 남쪽으로 지나가 2구역을 연다.
/// 꿀떡 10m/s, 칸 안에서만(가장자리 0.25m 안쪽) 서고, 서로 2m 이상 — 스케줄러가 겹치면 출발을 늦춘다.
/// </summary>
public static class TrailerV2TStage2
{
    const string Stage = "T.Stage2";
    const string Src   = "Assets/Scenes/T.Stage2.unity";
    const string Mgr   = "Stage2.3/StageManager2.3/PioneerPathManager";
    const float  Total = 12f;
    const float  GroundY = 0.5f;
    const float  Speed = 10f;
    const float  Half = 1.65f; // 칸(3.8m) 중심에서 설 수 있는 범위

    static Vector3 C(int i, int j) => new Vector3(-18f + 4f * i, GroundY, 192f + 4f * j);

    // 정답 길(씬 타일 배치에서 읽은 순서) — (i, j, 구역)
    static readonly (int i, int j, int zone)[] Trail =
    {
        (1,0,1),(1,1,1),(1,2,1),(0,2,1),(0,3,1),(0,4,1),(1,4,1),(2,4,1),(3,4,1),(3,3,1),(3,2,1),(4,2,1),
        (5,2,2),(6,2,2),(6,1,2),(7,1,2),(8,1,2),(8,2,2),(8,3,2),(9,3,2),(9,4,2),
        (9,5,3),(8,5,3),(8,6,3),(8,7,3),(7,7,3),(7,8,3),(6,8,3),(5,8,3),
        (4,8,4),(4,7,4),(4,6,4),(3,6,4),(2,6,4),(2,7,4),(1,7,4),(0,7,4),(0,8,4),(0,9,4),
    };

    static readonly string[] Pioneer = { null, "B", "P", "G", "Y" };

    static Dictionary<(int, int), GameObject> _tiles;
    static readonly Dictionary<int, float> _unlock = new(); // Trail 인덱스 → 개방 시각

    [MenuItem("Tools/Trailer/v2/T.Stage2")]
    public static void Build()
    {
        Scene scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "PioneerPathManager", "PioneerPathZone", "PioneerPathTile");
        TrailerShotKit.Find(scene, "Stage2.1").SetActive(false);
        TrailerShotKit.Find(scene, "Stage2.3").SetActive(true);
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        _tiles = new Dictionary<(int, int), GameObject>();
        foreach (Transform zone in TrailerShotKit.Find(scene, Mgr).transform)
            foreach (Transform t in zone)
            {
                Vector3 c = t.GetComponent<Renderer>().bounds.center;
                _tiles[(Mathf.FloorToInt((c.x + 20f) / 4f), Mathf.FloorToInt((c.z - 190f) / 4f))] = t.gameObject;
            }

        List<(string key, TrailerShotKit.Moves m, float yaw)> plan = null;
        string fail = null;
        for (int seed = 0; seed < 200; seed++)
        {
            plan = Schedule(new System.Random(0x2A3 + seed * 31), out fail);
            if (fail == null) { Debug.Log($"[Trailer v2] T2 시드 {seed}"); break; }
        }
        if (fail != null) { Debug.LogError("[Trailer v2] T2 동선 실패: " + fail); return; }

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        foreach (var (key, m, yaw) in plan)
        {
            var k = TrailerShotKit.MakeRunner(group, key, m.start, yaw);
            m.ApplyTo(k);
            k.Build(shot);
        }

        var log = new System.Text.StringBuilder("[Trailer v2] T2 개방: ");
        foreach (var kv in _unlock)
        {
            if (kv.Value > Total) continue;
            var (i, j, _) = Trail[kv.Key];
            shot.Call((MonoBehaviour)_tiles[(i, j)].GetComponent("PioneerPathTile"), "Unlock", kv.Value);
            log.Append($"({i},{j}) {kv.Value:F2}s  ");
        }
        Debug.Log(log.ToString());

        TrailerV2.AddKeyCamera(shot, new List<(float, Vector3, Vector3)>
        {
            // 식도 관(가로 반경 24 · 세로 반경 12) 안에서만 — 위로 빠지면 바깥 허공이 보인다
            (0f,   new Vector3(-17f,  4.5f, 175f), new Vector3(-12f, 0.5f, 196f)),
            (3.5f, new Vector3(-10f,  8f,   183f), new Vector3(-13f, 0f,   201f)),
            (7f,   new Vector3( -4f,  9.5f, 184f), new Vector3( -5f, 0f,   202f)),
            (12f,  new Vector3(  0f, 10.3f, 177f), new Vector3( -1f, 0f,   202f)),
        });

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(scene);
    }

    /// <summary>편집 모드 미리보기 — Signal이 안 돌아 개방 색을 MaterialPropertyBlock으로 잠깐 칠해 찍고 지운다(Build 직후에만).</summary>
    public static string PreviewWithUnlock(string times)
    {
        var pink = new Color(1f, 0f, 0.486f);
        var black = Color.black;
        var outList = new List<string>();
        foreach (string s in times.Split(','))
        {
            float t = float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            for (int k = 0; k < Trail.Length; k++)
            {
                var r = _tiles[(Trail[k].i, Trail[k].j)].GetComponent<Renderer>();
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", _unlock.TryGetValue(k, out float u) && u <= t ? pink : black);
                r.SetPropertyBlock(mpb);
            }
            outList.Add(TrailerV2.Preview(s));
        }
        foreach (var go in _tiles.Values) go.GetComponent<Renderer>().SetPropertyBlock(null);
        return string.Join("\n", outList);
    }

    sealed class Walker
    {
        public string key; public TrailerShotKit.Moves m; public int at = -1; public float free; public float yaw;
    }

    /// <summary>
    /// 순서: 파랑(1구역 개척 + 끝 칸 북쪽으로 비킴) → 보라(따라가다 남쪽으로 지나 2구역 개척) → 파랑(보라 뒤로 합류) → 초록 → 노랑.
    /// 각 걸음은 (그 칸이 열렸거나 내가 담당) + (이미 정한 동선과 2m 이상)일 때까지 출발을 늦춘다.
    /// </summary>
    static List<(string, TrailerShotKit.Moves, float)> Schedule(System.Random rng, out string fail)
    {
        fail = null;
        _unlock.Clear();
        var done = new List<TrailerShotKit.Moves>();
        var starts = new Dictionary<string, (Vector3 p, float yaw, float t0)>
        {
            ["B"] = (new Vector3(-13.3f, GroundY, 187.4f),  8f, TrailerV2.Rand(rng, 0.35f, 0.6f)),
            ["P"] = (new Vector3( -9.4f, GroundY, 185.9f), 341f, 0f),
            ["G"] = (new Vector3(-16.6f, GroundY, 185.1f), 22f, 0f),
            ["Y"] = (new Vector3(-12.1f, GroundY, 182.8f), 352f, 0f),
        };
        var w = new Dictionary<string, Walker>();
        foreach (var kv in starts) w[kv.Key] = new Walker { key = kv.Key, m = new TrailerShotKit.Moves(kv.Value.p), free = kv.Value.t0, yaw = kv.Value.yaw };

        int handoff = System.Array.FindIndex(Trail, s => s.zone == 2) - 1; // 1구역 끝 칸

        // 파랑 A: 1구역 끝 칸 북쪽까지
        if (!Walk(w["B"], rng, handoff, done, aside: +1, out fail)) return null;
        done.Add(w["B"].m);
        // 보라: 끝 칸은 남쪽으로 지나 2구역 전체
        if (!Walk(w["P"], rng, Trail.Length - 1, done, aside: 0, out fail, southAt: handoff)) return null;
        done.Add(w["P"].m);
        // 파랑 B: 보라 뒤로 합류 — 파랑의 대기(움직임 없음)는 이미 검사됐고 여기서 이어 간다
        done.Remove(w["B"].m);
        if (!Walk(w["B"], rng, Trail.Length - 1, done, aside: 0, out fail)) return null;
        done.Add(w["B"].m);
        foreach (string k in new[] { "G", "Y" })
        {
            if (!Walk(w[k], rng, Trail.Length - 1, done, aside: 0, out fail)) return null;
            done.Add(w[k].m);
        }
        var all = new List<(string, TrailerShotKit.Moves)>();
        foreach (string k in new[] { "B", "P", "G", "Y" }) all.Add((k, w[k].m));
        fail = TrailerV2.CheckGaps(all, Total, 2f);
        if (fail != null) return null;
        // 칸 밖(함정·안 열린 칸) 검사: 바닥(z < 190) 아니면 열린 길 칸 위, 또는 그 칸의 개척자
        for (float t = 0f; t <= Total && fail == null; t += 0.02f)
            foreach (var (k, m) in all)
            {
                Vector3 p = m.At(t);
                if (p.z < 189.9f) continue;
                int idx = System.Array.FindIndex(Trail, s => Mathf.Abs(C(s.i, s.j).x - p.x) <= 2f && Mathf.Abs(C(s.i, s.j).z - p.z) <= 2f);
                if (idx < 0) { fail = $"{t:F2}s {k} 함정 칸 {p:F1}"; break; }
                if (Pioneer[Trail[idx].zone] != k && (!_unlock.TryGetValue(idx, out float u) || u > t)) { fail = $"{t:F2}s {k} 안 열린 칸 {Trail[idx]}"; break; }
            }
        if (fail != null) return null;
        var result = new List<(string, TrailerShotKit.Moves, float)>();
        foreach (string k in new[] { "B", "P", "G", "Y" }) result.Add((k, w[k].m, w[k].yaw));
        return result;
    }

    static bool Walk(Walker me, System.Random rng, int until, List<TrailerShotKit.Moves> others, int aside, out string fail, int southAt = -1)
    {
        fail = null;
        while (me.at < until && me.free < Total + 2f)
        {
            int next = me.at + 1;
            var (i, j, zone) = Trail[next];
            bool pioneer = Pioneer[zone] == me.key;
            Vector3 c = C(i, j);
            Vector3 target = c + new Vector3(TrailerV2.Rand(rng, -0.7f, 0.7f), 0f, TrailerV2.Rand(rng, -0.7f, 0.7f));
            if (next == until && aside != 0) target = new Vector3(c.x + TrailerV2.Rand(rng, -0.4f, 0.4f), GroundY, c.z + aside * Half * 0.95f);
            if (next == southAt) target = new Vector3(c.x + TrailerV2.Rand(rng, -0.3f, 0.3f), GroundY, c.z - Half * 0.95f);

            float t0 = me.free;
            if (!pioneer)
            {
                if (!_unlock.TryGetValue(next, out float u)) break; // 앞 구역 담당이 아직 안 엶 — 그 자리에서 기다림
                t0 = Mathf.Max(t0, u + TrailerV2.Rand(rng, 0.05f, 0.25f));
            }
            if (t0 > Total + 1f) break;
            // 이미 정한 동선과 2m — 출발을 0.05초씩 늦춘다
            int tries = 0;
            while (Conflicts(me.m, t0, target, others) && tries < 200) { t0 += 0.05f; tries++; }
            if (tries == 200)
            {
                if (t0 > Total) break;
                fail = $"{me.key} ({i},{j}) 자리 없음"; return false;
            }

            float t1 = me.m.RunTo(t0, target, Speed);
            if (pioneer && !_unlock.ContainsKey(next))
                _unlock[next] = EnterTime(me.m, t0, t1, c);
            me.at = next;
            // 개척자는 다음 칸을 떠올리느라 가끔 멈칫, 따라가는 사람은 거의 바로
            me.free = t1 + (pioneer ? (rng.NextDouble() < 0.45 ? TrailerV2.Rand(rng, 0.15f, 0.45f) : 0f) : TrailerV2.Rand(rng, 0f, 0.12f));
        }
        return true;
    }

    static float EnterTime(TrailerShotKit.Moves m, float t0, float t1, Vector3 c)
    {
        for (float t = t0; t <= t1; t += 0.005f)
        {
            Vector3 p = m.At(t);
            if (Mathf.Abs(p.x - c.x) <= 1.9f && Mathf.Abs(p.z - c.z) <= 1.9f) return t;
        }
        return t1;
    }

    static bool Conflicts(TrailerShotKit.Moves me, float t0, Vector3 to, List<TrailerShotKit.Moves> others)
    {
        Vector3 from = me.End;
        float t1 = t0 + Vector3.Distance(TrailerV2.Flat(from), TrailerV2.Flat(to)) / Speed;
        for (float t = Mathf.Max(0f, me.segs.Count > 0 ? me.segs[me.segs.Count - 1].t1 : 0f); t <= t1 + 0.6f; t += 0.03f)
        {
            Vector3 p = t < t0 ? from : t < t1 ? Vector3.Lerp(from, to, (t - t0) / (t1 - t0)) : to;
            foreach (var o in others)
                if (TrailerV2.Flat(p - o.At(t)).magnitude < 2.05f) return true;
        }
        // 도착한 자리에 나중에 누가 지나가도 안 된다(대기 중 겹침) — 다음 걸음 검사에서 다시 막히므로 여기선 0.6초만
        return false;
    }
}
