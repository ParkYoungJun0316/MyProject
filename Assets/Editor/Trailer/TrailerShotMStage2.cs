using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 M.Stage2 컷 (12초, 카메라 규칙은 M 공통: (0,0,-35) → (0,35,0)).
/// (a) 2.1 SideSplit 2라운드: 간판 3·2·1 → 모양+하트 공개 → 넷이 각자 자리 → 채움 줄다가 성공(초록).
///     2라운드는 SplitZoneRig가 90° 돌아간 뒤(게임은 3라운드부터) 서로 자리를 바꾼다.
/// (b) 2.2 Drop + 침: 경고 마커가 차오르며 음식이 떨어지고, 침이 바닥을 덮어 미끄러지다 외치면 걷힌다.
/// 값은 게임 SideSplitWorldDisplay / DropTrap·DropWarnMarker / SalivaHazard(cover 2·recover 2·alpha 0.95)를 따른다.
/// </summary>
public static class TrailerShotMStage2
{
    const string Src      = "Assets/Scenes/M.Stage2.unity";
    const string AssetDir = "Assets/Trailer/M.Stage2";
    const float  Total    = 12f;
    const float  GroundY  = 0.5f;

    [MenuItem("Tools/Trailer/M.Stage2 (a) 플레이")]
    public static void BuildPlay() => Build(false);

    [MenuItem("Tools/Trailer/M.Stage2 (b) 방해공작")]
    public static void BuildHazard() => Build(true);

    static void Build(bool hazard)
    {
        string suffix   = hazard ? "_b" : "_a";
        string shotName = "Trailer_M.Stage2" + suffix;
        string dst      = $"Assets/Scenes/Marketing_M.Stage2{suffix}.unity";

        Scene scene = TrailerShotKit.PrepStage(Src, dst);
        var shot = new TrailerShotKit.Shot(AssetDir, shotName, Total);
        TrailerShotKit.AddOrbitCamera(shot, Vector3.zero, 35f, 0f, 90f, 1f);

        if (hazard)
        {
            TrailerShotKit.Find(scene, "Stage2.1").SetActive(false);
            TrailerShotKit.Find(scene, "Stage2.2").SetActive(true);
            BuildDropAndSaliva(shot, scene);
        }
        else
        {
            BuildSideSplit(shot, scene);
        }

        shot.AddRecorder(shotName);
        shot.Save(scene);
        Debug.Log($"[Trailer] {dst} 생성 완료 → Recordings/{shotName}.mp4");
    }

    // ── (a) SideSplit ────────────────────────────────────────────

    static readonly Color FillColor    = new Color(0.25f, 0.72f, 0.62f);
    static readonly Color FillWarn     = new Color(0.95f, 0.2f, 0.2f);
    static readonly Color FillSuccess  = new Color(0.2f, 0.9f, 0.3f);
    static readonly string[] Signs     = { "LEFT", "FRONT", "RIGHT", "BACK" };
    static readonly string[] Zones     = { "Left", "Front", "Right", "Back" };

    struct Round
    {
        public float countdown;   // 3이 뜨는 시각(0.6초 간격 3·2·1)
        public float reveal;      // 모양·하트 공개
        public float shrinkFrom;  // 채움 줄기 시작(읽는 시간 뒤)
        public float limit;       // 채움 0까지 걸리는 시간
        public float success;     // 전원 도착 → 초록
        public float rigYaw;      // 이번 라운드 SplitZoneRig 회전
        public int[] signOf;      // 꿀떡 B/P/G/Y가 서야 할 간판 인덱스(Signs, 리그 기준)
    }

    static void BuildSideSplit(TrailerShotKit.Shot shot, Scene scene)
    {
        const string rigPath = "Stage2.1/StageManager2.1/SideSplitChallenge/SplitZoneRig";
        GameObject rig = TrailerShotKit.Find(scene, rigPath);

        var rounds = new[]
        {
            new Round { countdown = 0.3f, reveal = 2.1f, shrinkFrom = 2.6f, limit = 4f, success = 3.6f, rigYaw = 0f,  signOf = new[] { 0, 1, 2, 3 } },
            // 리그가 90° 돈 뒤 각자 시계 방향 다음 자리로 — 화면상 서로 엇갈려 뛴다
            new Round { countdown = 5.4f, reveal = 7.2f, shrinkFrom = 7.6f, limit = 3f, success = 9.9f, rigYaw = 90f, signOf = new[] { 0, 1, 2, 3 } },
        };

        AnimationClip clip = shot.NewClip("SplitZoneRig");
        var yaw = new List<(float, float)> { (0f, 0f) };
        foreach (Round r in rounds) yaw.Add((r.countdown, r.rigYaw));
        yaw.Add((Total, rounds[rounds.Length - 1].rigYaw));
        TrailerShotKit.SetEuler(clip, "", TrailerShotKit.Const(0f, Total), TrailerShotKit.Steps(yaw), TrailerShotKit.Const(0f, Total));

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

            foreach (Round r in rounds)
            {
                labelOn.Add((r.countdown, false)); labelOn.Add((r.reveal, true));
                panelOn.Add((r.countdown, false)); panelOn.Add((r.reveal, true));
                fillOn.Add((r.countdown, false));  fillOn.Add((r.reveal, true));
                padCol.Add((r.countdown, Color.white)); padCol.Add((r.success, FillSuccess));

                // 채움: 공개~읽기 = 1, 이후 limit초에 걸쳐 0으로(남은 1초는 경고색), 성공 순간 가득 초록
                float atSuccess = Mathf.Clamp01(1f - (r.success - r.shrinkFrom) / r.limit);
                cutoff.Add((r.reveal, 0.002f)); cutoff.Add((r.shrinkFrom, 0.002f));
                cutoff.Add((r.success - 0.001f, 1f - atSuccess)); cutoff.Add((r.success, 0.002f));
                float warnAt = r.shrinkFrom + r.limit - 1f;
                fillCol.Add((r.reveal, FillColor));
                if (warnAt < r.success) fillCol.Add((warnAt, FillWarn));
                fillCol.Add((r.success, FillSuccess));

                // 카운트다운 숫자 3·2·1 (게임: 간판마다 큰 TMP 숫자, 모양은 숨김)
                var digitSrc = TrailerShotKit.Find(scene, $"{rigPath}/SignCount_{sign}");
                for (int d = 0; d < 3; d++)
                {
                    GameObject digit = Object.Instantiate(digitSrc, digitSrc.transform.parent);
                    digit.name = $"Count_{sign}_{3 - d}@{r.countdown}";
                    digit.GetComponent<TMPro.TMP_Text>().text = (3 - d).ToString();
                    float on = r.countdown + d * 0.6f;
                    shot.BindActive(digit, on, on + 0.6f);
                }

                // 하트: 이 간판에 서야 할 색 하나(게임은 각자 자기 패널만 보이지만, 관전 카메라라 넷 다 보여준다)
                int who = System.Array.IndexOf(r.signOf, s);
                if (who >= 0)
                {
                    Transform row = TrailerShotKit.Find(scene, $"{rigPath}/HeartRow_{sign}").transform;
                    var heart = new GameObject($"Heart_{sign}@{r.reveal}");
                    heart.transform.SetParent(row, false);
                    var sr = heart.AddComponent<SpriteRenderer>();
                    sr.sprite = heartSprites[who];
                    sr.sortingOrder = 1;
                    float scale = 1.9f / sr.sprite.bounds.size.x;
                    heart.transform.localScale = new Vector3(scale, scale, 1f);
                    float end = System.Array.IndexOf(rounds, r) + 1 < rounds.Length ? rounds[System.Array.IndexOf(rounds, r) + 1].countdown : Total + 1f;
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
        shot.Animate(rig, clip);

        // 꿀떡: 공개 직후 각자 하트가 뜬 간판 아래 자리로. 2라운드는 돌아간 리그 기준 자리로 엇갈려 뛴다.
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        Vector3 ZonePos(int sign, float rigYaw)
        {
            Vector3 local = TrailerShotKit.Find(scene, $"{rigPath}/SideSplitZone_{Zones[sign]}").transform.localPosition;
            Vector3 p = Quaternion.Euler(0f, rigYaw, 0f) * local;
            return new Vector3(p.x * 0.95f, GroundY, p.z * 0.95f);
        }
        Vector3[] start = { new(-2f, GroundY, 1f), new(1f, GroundY, 2f), new(2f, GroundY, -1f), new(-1f, GroundY, -2f) };
        float[] delay = { 0.25f, 0.35f, 0.2f, 0.4f };
        for (int i = 0; i < 4; i++)
        {
            var run = TrailerShotKit.MakeRunner(group, TrailerShotKit.SquadKeys[i], start[i], TrailerShotKit.Yaw(-start[i]));
            foreach (Round r in rounds)
            {
                Vector3 to = ZonePos(r.signOf[i], r.rigYaw);
                float t0 = r.reveal + delay[i];
                float dist = Vector3.Distance(run.Position, to);
                float t1 = Mathf.Min(t0 + dist / 9f, r.success - 0.1f);
                run.RunTo(t0, t1, to).Turn(t1, t1 + 0.3f, TrailerShotKit.Yaw(to)); // 자리에서 간판을 본다
            }
            run.Pose(r1End(rounds), "Yes").Build(shot);
        }
    }

    static float r1End(Round[] rounds) => rounds[rounds.Length - 1].success + 0.3f;

    static Sprite Sprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (o is Sprite s && s.name == name) return s;
        }
        Debug.LogError("[Trailer] 스프라이트 없음: " + name);
        return null;
    }

    // ── (b) Drop + 침 ────────────────────────────────────────────

    internal struct Drop { public float warn; public Vector3 at; public string prefab; public float markerScale; }

    const float DropWarn   = 1.2f;  // 게임 2초 → 트레일러 압축
    const float DropHeight = 25f;
    const float DropSpeed  = 15f;

    // 침(SalivaHazard): 덮임 2초(알파 0→0.95) → 외칠 때까지 유지 → 걷힘 2초
    const float CoverAt = 3.6f, HeldAt = 5.6f, RecoverAt = 7.2f, ClearAt = 9.2f;

    static void BuildDropAndSaliva(TrailerShotKit.Shot shot, Scene scene)
    {
        var drops = new[]
        {
            new Drop { warn = 0.2f, at = new Vector3(5f, 0f, 5f),    prefab = "Drop4", markerScale = 2.75f },
            new Drop { warn = 1.2f, at = new Vector3(-5f, 0f, 0f),   prefab = "Drop3", markerScale = 2.25f },
            new Drop { warn = 2.4f, at = new Vector3(0f, 0f, -5f),   prefab = "Drop5", markerScale = 3.25f },
            new Drop { warn = 4.2f, at = new Vector3(10f, 0f, -5f),  prefab = "Drop3", markerScale = 2.25f },
            new Drop { warn = 6.0f, at = new Vector3(-10f, 0f, 10f), prefab = "Drop4", markerScale = 2.75f },
            new Drop { warn = 8.2f, at = new Vector3(0f, 0f, 5f),    prefab = "Drop5", markerScale = 3.25f },
            new Drop { warn = 9.4f, at = new Vector3(5f, 0f, -10f),  prefab = "Drop3", markerScale = 2.25f },
        };
        Transform root = TrailerShotKit.Group("Drops", shot.Root).transform;
        for (int i = 0; i < drops.Length; i++) BuildDrop(shot, root, drops[i], i);

        // 침 덮개: 게임처럼 켜고 _BaseColor 알파 0→0.95, 외친 뒤 0으로 걷고 끈다
        GameObject cover = TrailerShotKit.Find(scene, "Stage2.2/SalivaCover_2_2");
        var tint = new Color(0.09f, 0.19f, 0.16f, 0f);
        Color A(float a) { var c = tint; c.a = a; return c; }
        AnimationClip salivaClip = shot.NewClip("SalivaCover");
        TrailerShotKit.SetMatColor(salivaClip, "", typeof(MeshRenderer), "_BaseColor", new List<(float, Color)>
        {
            (0f, A(0f)), (CoverAt, A(0f)), (HeldAt, A(0.95f)), (RecoverAt, A(0.95f)), (ClearAt, A(0f)), (Total, A(0f)),
        });
        cover.SetActive(true);
        shot.Animate(cover, salivaClip);
        shot.BindActive(cover, CoverAt, ClearAt);

        // 꿀떡: 낙하 경고를 피해 다니다가 침이 깔리면 멈추지 못하고 미끄러지고, 외친 뒤 다시 달린다
        Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
        TrailerShotKit.Runner K(int i, Vector3 p, float yaw) => TrailerShotKit.MakeRunner(group, TrailerShotKit.SquadKeys[i], p, yaw);
        Vector3 P(float x, float z) => new Vector3(x, GroundY, z);

        K(0, P(4f, 3f), 200f)
            .RunTo(0.4f, 1.0f, P(1f, -1f))
            .RunTo(2.6f, 3.2f, P(-3f, -3f))
            .RunTo(3.6f, 4.0f, P(-5f, -6f)).Glide(4.0f, 5.4f, P(-8f, -9f))
            .Pose(5.8f, "Yes")
            .RunTo(8.0f, 8.6f, P(-4f, -4f))
            .RunTo(9.6f, 10.2f, P(-2f, 2f))
            .Build(shot);

        K(1, P(-4f, 1f), 120f)
            .RunTo(1.3f, 1.9f, P(-2f, 4f))
            .RunTo(3.7f, 4.1f, P(2f, 6f)).Glide(4.1f, 5.4f, P(5f, 9f))
            .Pose(5.8f, "Yes")
            .RunTo(8.2f, 8.8f, P(3f, 1f))
            .Build(shot);

        K(2, P(1f, -4f), 60f)
            .RunTo(2.5f, 3.1f, P(4f, -2f))
            .RunTo(3.7f, 4.1f, P(7f, 1f)).Glide(4.1f, 5.4f, P(9.5f, 4f))
            .Pose(5.8f, "Yes")
            .RunTo(8.4f, 9.0f, P(6f, 2f))
            .RunTo(9.6f, 10.2f, P(3f, -3f))
            .Build(shot);

        K(3, P(-1f, -1f), 300f)
            .RunTo(0.6f, 1.1f, P(-3f, 2f))
            .RunTo(3.8f, 4.2f, P(-6f, 4f)).Glide(4.2f, 5.4f, P(-9f, 7f))
            .Pose(5.8f, "Yes")
            .RunTo(8.1f, 8.7f, P(-4f, 2f))
            .Build(shot);
    }

    internal static void BuildDrop(TrailerShotKit.Shot shot, Transform root, Drop d, int index)
    {
        float fall   = d.warn + DropWarn;
        float land   = fall + DropHeight / DropSpeed;
        Vector3 top  = d.at + Vector3.up * DropHeight;

        // 바닥 경고 마커: 경고 동안 테두리만 → 떨어지는 동안 _Fill 0→1, 색 귤색→진홍 → 착지 때 사라짐
        Vector3 markerPos = new Vector3(d.at.x, 0.6f, d.at.z);
        Animator markerRig = TrailerShotKit.Rig($"DropWarn_{index}", root, markerPos, Quaternion.identity);
        GameObject marker = TrailerShotKit.Spawn("Assets/Art/Droptrap/DropWarnMarker.prefab", "Marker", markerRig.transform, markerPos, Quaternion.identity);
        marker.transform.localScale = Vector3.one * d.markerScale;
        Renderer mr = marker.GetComponentInChildren<Renderer>();
        string mPath = AnimationUtility.CalculateTransformPath(mr.transform, markerRig.transform);
        float baseAlpha = mr.sharedMaterial.HasProperty("_Color") ? mr.sharedMaterial.GetColor("_Color").a : 1f;
        Color S = TrailerShotKit.WarnStart; S.a = baseAlpha;
        Color E = TrailerShotKit.WarnEnd;   E.a = baseAlpha;
        AnimationClip mClip = shot.NewClip($"DropWarn_{index}");
        TrailerShotKit.SetMat(mClip, mPath, mr.GetType(), "_Fill", TrailerShotKit.Linear(new List<(float, float)> { (0f, 0f), (fall, 0f), (land, 1f), (Total, 1f) }));
        TrailerShotKit.SetMatColor(mClip, mPath, mr.GetType(), "_Color", new List<(float, Color)> { (0f, S), (fall, S), (land, E), (Total, E) });
        shot.BindWhole(markerRig, mClip);
        shot.BindActive(marker, d.warn, land);

        // 낙하물: 25m 위에서 아래를 보며(LookRotation(down)) 등속 낙하, 바닥을 뚫고 지나간다(게임과 같음)
        Animator dropRig = TrailerShotKit.Rig($"Drop_{index}", root, top, Quaternion.identity);
        GameObject drop = TrailerShotKit.Spawn($"Assets/Prefab/입/{d.prefab}.prefab", d.prefab, dropRig.transform, top, Quaternion.LookRotation(Vector3.down));
        float below = -DropHeight * 0.6f;
        AnimationClip dClip = shot.NewClip($"Drop_{index}");
        float tBelow = land + (-below) / DropSpeed;
        TrailerShotKit.SetPosition(dClip, "",
            TrailerShotKit.Const(d.at.x, Total),
            TrailerShotKit.Linear(new List<(float, float)> { (0f, top.y), (fall, top.y), (tBelow, below), (Total, below) }),
            TrailerShotKit.Const(d.at.z, Total));
        shot.BindWhole(dropRig, dClip);
        shot.BindActive(drop, fall, Mathf.Min(tBelow, Total + 1f));
    }
}
