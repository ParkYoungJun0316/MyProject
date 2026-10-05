using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 M.Stage2 (11초) — 2.1 SideSplit 2라운드. 간판·채움·하트 규칙은 1차(TrailerShotMStage2)와 같다
/// (게임 SideSplitWorldDisplay: 3·2·1 → 모양+하트 공개 → 채움 줄어듦 → 전원 도착 시 초록).
/// 리그 회전은 게임에서 3라운드부터라 이 컷(1·2라운드)에선 돌리지 않는다.
/// 2라운드 자리: 보라는 그대로(제자리 잔걸음), 파랑·노랑은 옆 자리로, 초록은 맞은편으로 가로지른다.
/// 카메라: 가운데 낮은 곳에서 FRONT 간판 카운트다운을 올려다보다가, 공개되면 넷이 흩어지는 사이 크레인처럼
///         대각선 위로 솟아 네 자리와 간판 두 개(FRONT·RIGHT)가 한 화면에 들어오게 된다. 2라운드 동안 남쪽으로 천천히 돈다.
/// </summary>
public static class TrailerV2MStage2
{
    const string Stage = "M.Stage2";
    const string Src   = "Assets/Scenes/M.Stage2.unity";
    const float  Total = 11f;
    const float  GroundY = 0.5f;
    const float  Speed = 9f;
    const string RigPath = "Stage2.1/StageManager2.1/SideSplitChallenge/SplitZoneRig";

    static readonly Color FillColor   = new Color(0.25f, 0.72f, 0.62f);
    static readonly Color FillWarn    = new Color(0.95f, 0.2f, 0.2f);
    static readonly Color FillSuccess = new Color(0.2f, 0.9f, 0.3f);
    static readonly string[] Signs = { "LEFT", "FRONT", "RIGHT", "BACK" };
    static readonly string[] Zones = { "Left", "Front", "Right", "Back" };

    sealed class Round
    {
        public float countdown, reveal, shrinkFrom, limit, success;
        public int[] signOf; // B/P/G/Y가 서야 할 간판(Signs 인덱스)
    }

    static Scene _scene;

    [MenuItem("Tools/Trailer/v2/M.Stage2")]
    public static void Build()
    {
        _scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage));
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        var rounds = new[]
        {
            // 제한 = 씬 roundTimeLimitByPlayerCount[4인] 6초
            new Round { countdown = 0.4f, reveal = 2.2f, limit = 6f, signOf = new[] { 0, 1, 2, 3 } },
            new Round { countdown = 5.4f, reveal = 7.2f, limit = 6f, signOf = new[] { 3, 1, 0, 2 } },
        };
        foreach (Round r in rounds) r.shrinkFrom = r.reveal + 0.5f;

        var moves = BuildMoves(rounds);
        if (moves == null) return;

        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        float[] startYaw = { 58f, 203f, 291f, 127f };
        for (int i = 0; i < 4; i++)
        {
            var k = TrailerShotKit.MakeRunner(group, TrailerShotKit.SquadKeys[i], moves[i].start, startYaw[i]);
            moves[i].ApplyTo(k);
            if (i == 1 || i == 3) k.Pose(rounds[1].success + 0.15f + i * 0.07f, "Yes");
            k.Build(shot);
        }

        BuildSigns(shot, rounds);
        BuildCamera(shot, rounds);

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(_scene);
        Debug.Log($"[Trailer v2] M2 생성 — 성공 {rounds[0].success:F2}s / {rounds[1].success:F2}s");
    }

    static Vector3 ZoneCenter(int sign)
    {
        Vector3 p = TrailerShotKit.Find(_scene, $"{RigPath}/SideSplitZone_{Zones[sign]}").transform.position;
        return new Vector3(p.x, GroundY, p.z);
    }

    /// <summary>자리 안 아무 데나(발판 7m 길이 방향 ±2.2, 깊이는 발판 안쪽).</summary>
    static Vector3 SpotIn(int sign, System.Random rng)
    {
        Vector3 c = ZoneCenter(sign);
        Vector3 outward = c.normalized; outward.y = 0f;
        Vector3 along = Vector3.Cross(Vector3.up, outward);
        return c + along * TrailerV2.Rand(rng, -2.2f, 2.2f) + outward * TrailerV2.Rand(rng, -0.5f, 0.3f);
    }

    static TrailerShotKit.Moves[] BuildMoves(Round[] rounds)
    {
        Vector3[] home = { new(-2.4f, GroundY, 0.9f), new(0.6f, GroundY, 2.6f), new(2.2f, GroundY, -0.7f), new(-0.4f, GroundY, -2.5f) };
        string fail = null;
        for (int seed = 0; seed < 600; seed++)
        {
            var rng = new System.Random(0x2B2 + seed);
            var m = new TrailerShotKit.Moves[4];
            for (int i = 0; i < 4; i++) m[i] = new TrailerShotKit.Moves(home[i]);
            // 1라운드: 카운트다운 동안 서성이다 공개 뒤 반응 차이를 두고 자기 자리로(한 번 꺾어서)
            var arrive = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float go = rounds[0].reveal + TrailerV2.Rand(rng, 0.2f, 0.5f);
                m[i].Fidget(rng, 0f, go - 0.1f, home[i], 1.1f);
                arrive[i] = RunBent(m[i], go, SpotIn(rounds[0].signOf[i], rng), rng);
            }
            rounds[0].success = Max(arrive) + 0.05f;
            // 2라운드
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = m[i].End;
                float go = rounds[1].reveal + TrailerV2.Rand(rng, 0.2f, 0.45f);
                if (rounds[1].signOf[i] == rounds[0].signOf[i])
                {
                    // 같은 자리: 끝까지 발판 안에서 잔걸음
                    m[i].Fidget(rng, arrive[i] + 0.3f, Total, at, 1.0f, (t, q) => !InZone(q, rounds[0].signOf[i]));
                    arrive[i] = arrive[i];
                    continue;
                }
                m[i].Fidget(rng, arrive[i] + 0.4f, go - 0.1f, at, 0.9f, (t, q) => !InZone(q, rounds[0].signOf[i]));
                arrive[i] = RunBent(m[i], go, SpotIn(rounds[1].signOf[i], rng), rng);
            }
            float last = 0f;
            for (int i = 0; i < 4; i++) if (rounds[1].signOf[i] != rounds[0].signOf[i]) last = Mathf.Max(last, arrive[i]);
            rounds[1].success = last + 0.05f;
            if (rounds[0].success > rounds[0].shrinkFrom + rounds[0].limit - 1f || rounds[1].success > rounds[1].shrinkFrom + rounds[1].limit - 0.6f)
            { fail = "시간 초과"; continue; }

            var list = new List<(string, TrailerShotKit.Moves)>();
            for (int i = 0; i < 4; i++) list.Add((TrailerShotKit.SquadKeys[i], m[i]));
            fail = TrailerV2.CheckGaps(list, Total, 2f);
            if (fail == null) { Debug.Log($"[Trailer v2] M2 동선 seed {seed}"); return m; }
        }
        Debug.LogError("[Trailer v2] M2 동선 실패: " + fail);
        return null;
    }

    static bool InZone(Vector3 q, int sign)
    {
        Vector3 c = ZoneCenter(sign);
        bool ew = Mathf.Abs(c.x) > Mathf.Abs(c.z);
        float halfAlong = 3.5f - 0.6f, halfDepth = 1.25f - 0.4f;
        return ew ? Mathf.Abs(q.x - c.x) < halfDepth && Mathf.Abs(q.z - c.z) < halfAlong
                  : Mathf.Abs(q.z - c.z) < halfDepth && Mathf.Abs(q.x - c.x) < halfAlong;
    }

    /// <summary>중간에 옆으로 한 번 꺾어 달린다. 반환 = 도착 시각.</summary>
    static float RunBent(TrailerShotKit.Moves m, float t0, Vector3 to, System.Random rng)
    {
        Vector3 from = m.End;
        Vector3 d = to - from; d.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, d.normalized);
        Vector3 mid = Vector3.Lerp(from, to, TrailerV2.Rand(rng, 0.35f, 0.6f)) + side * TrailerV2.Rand(rng, -2.2f, 2.2f);
        mid.y = GroundY;
        float t1 = m.RunTo(t0, mid, Speed);
        return m.RunTo(t1, to, Speed);
    }

    static float Max(float[] a) { float x = a[0]; foreach (float v in a) x = Mathf.Max(x, v); return x; }

    // ── 간판(1차와 같은 규칙) ────────────────────────────────────

    static void BuildSigns(TrailerShotKit.Shot shot, Round[] rounds)
    {
        GameObject rig = TrailerShotKit.Find(_scene, RigPath);
        AnimationClip clip = shot.NewClip("SplitZoneRig");
        var heartSprites = new[] { Sprite("BlueHP"), Sprite("PurpleHP"), Sprite("GreenHP"), Sprite("YellowHP") };

        for (int s = 0; s < Signs.Length; s++)
        {
            string sign = Signs[s];
            var labelOn = new List<(float, bool)> { (0f, true) };
            var panelOn = new List<(float, bool)> { (0f, false) };
            var fillOn  = new List<(float, bool)> { (0f, false) };
            var cutoff  = new List<(float, float)> { (0f, 0.002f) };
            var fillCol = new List<(float, Color)> { (0f, FillColor) };
            var padCol  = new List<(float, Color)> { (0f, Color.white) };

            for (int ri = 0; ri < rounds.Length; ri++)
            {
                Round r = rounds[ri];
                labelOn.Add((r.countdown, false)); labelOn.Add((r.reveal, true));
                panelOn.Add((r.countdown, false)); panelOn.Add((r.reveal, true));
                fillOn.Add((r.countdown, false));  fillOn.Add((r.reveal, true));
                padCol.Add((r.countdown, Color.white)); padCol.Add((r.success, FillSuccess));

                float atSuccess = Mathf.Clamp01(1f - (r.success - r.shrinkFrom) / r.limit);
                cutoff.Add((r.reveal, 0.002f)); cutoff.Add((r.shrinkFrom, 0.002f));
                cutoff.Add((r.success - 0.001f, 1f - atSuccess)); cutoff.Add((r.success, 0.002f));
                float warnAt = r.shrinkFrom + r.limit - 1f;
                fillCol.Add((r.reveal, FillColor));
                if (warnAt < r.success) fillCol.Add((warnAt, FillWarn));
                fillCol.Add((r.success, FillSuccess));

                var digitSrc = TrailerShotKit.Find(_scene, $"{RigPath}/SignCount_{sign}");
                for (int d = 0; d < 3; d++)
                {
                    GameObject digit = Object.Instantiate(digitSrc, digitSrc.transform.parent);
                    digit.name = $"Count_{sign}_{3 - d}@{r.countdown}";
                    digit.GetComponent<TMPro.TMP_Text>().text = (3 - d).ToString();
                    float on = r.countdown + d * 0.6f;
                    shot.BindActive(digit, on, on + 0.6f);
                }

                int who = System.Array.IndexOf(r.signOf, s);
                if (who >= 0)
                {
                    Transform row = TrailerShotKit.Find(_scene, $"{RigPath}/HeartRow_{sign}").transform;
                    var heart = new GameObject($"Heart_{sign}@{r.reveal}");
                    heart.transform.SetParent(row, false);
                    var sr = heart.AddComponent<SpriteRenderer>();
                    sr.sprite = heartSprites[who];
                    sr.sortingOrder = 1;
                    float scale = 1.9f / sr.sprite.bounds.size.x;
                    heart.transform.localScale = new Vector3(scale, scale, 1f);
                    float end = ri + 1 < rounds.Length ? rounds[ri + 1].countdown : Total + 1f;
                    shot.BindActive(heart, r.reveal, end);
                }
            }
            labelOn.Add((Total, true)); panelOn.Add((Total, true)); fillOn.Add((Total, true));
            cutoff.Add((Total, 0.002f)); fillCol.Add((Total, FillSuccess)); padCol.Add((Total, FillSuccess));

            TrailerShotKit.SetEnabled(clip, "SignLabel_" + sign, typeof(MeshRenderer), labelOn);
            TrailerShotKit.SetEnabled(clip, "HeartPanel_" + sign, typeof(MeshRenderer), panelOn);
            TrailerShotKit.SetEnabled(clip, "SignFill_" + sign, typeof(MeshRenderer), fillOn);
            TrailerShotKit.SetMat(clip, "SignFill_" + sign, typeof(MeshRenderer), "_Cutoff", TrailerShotKit.Linear(cutoff));
            TrailerShotKit.SetMatColor(clip, "SignFill_" + sign, typeof(MeshRenderer), "_BaseColor", fillCol, stepped: true);
            TrailerShotKit.SetMatColor(clip, $"SideSplitZone_{Zones[s]}/Pad", typeof(MeshRenderer), "_BaseColor", padCol, stepped: true);
        }
        // 회전 없음 — 위치·회전 고정 키(회전 키만 있으면 Timeline이 위치를 원점으로 덮는다)
        TrailerShotKit.SetPosition(clip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total), TrailerShotKit.Const(0f, Total));
        shot.Animate(rig, clip);
    }

    static Sprite Sprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (o is Sprite s && s.name == name) return s;
        Debug.LogError("[Trailer v2] 스프라이트 없음: " + name);
        return null;
    }

    static void BuildCamera(TrailerShotKit.Shot shot, Round[] rounds)
    {
        float r1 = rounds[0].reveal, s1 = rounds[0].success, r2 = rounds[1].reveal;
        TrailerV2.AddKeyCamera(shot, new List<(float, Vector3, Vector3)>
        {
            (0f,        new Vector3( 1.2f, 1.9f, -7.0f), new Vector3(0f, 7.0f, 19.5f)),  // 낮게, FRONT 간판 3·2·1
            (r1,        new Vector3( 0.6f, 2.2f, -6.2f), new Vector3(0f, 6.5f, 19.5f)),
            (s1 + 0.4f, new Vector3(-11.0f, 13.5f, -12.5f), new Vector3(0.5f, 2.0f, 1.5f)), // 크레인: 대각선 위
            (r2,        new Vector3(-8.5f, 13.5f, -16.0f),  new Vector3(0.5f, 1.5f, 0.0f)),
            (Total,     new Vector3(-4.5f, 13.5f, -20.0f),  new Vector3(0.0f, 1.0f, -1.5f)),
        });
    }
}
