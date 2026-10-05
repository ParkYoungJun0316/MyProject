using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// 트레일러 v2 M.Stage4 (13.3초) — 4.2 NextOnly 링(다음 칸만 색) + 가운데 혀(팀 외침).
/// 게임 값: 혀 경고 3초(WarnMarker_C) → TongueRise 5.3초 → 가운데 15×15 칸 파괴(FloorTileShards 2~5, 2초)
///   → 외칠 때까지 Hold → Retract 1.3초 → 가운데 칸 되감기 복구. 링 색·현재 칸 마커 규칙은 1차(TrailerShotMStage4)와 같다.
/// 링은 Space로 넘기므로 칸을 밟을 필요가 없다 — 넷은 경고가 뜨면 링 위 제각각 자리로 피해 끝까지 서성인다.
/// 혀 뒤 다 같이 Yes 동작은 어색하다는 지적(10/6)으로 넣지 않는다.
/// 카메라: 링 바깥 남동쪽 위에서 가운데를 보다가, 혀가 올라오는 동안 남쪽 링에 선 두 명 뒤(높이 5)로 내려가 가운데를 본다.
/// </summary>
public static class TrailerV2MStage4
{
    const string Stage = "M.Stage4";
    const string Src   = "Assets/Scenes/M.Stage4.unity";
    const float  Total = 13.3f; // 되감기 복구(0.8초)까지 보이게
    const float  GroundY = 0.5f;
    const float  Speed = 9f;
    const string Mgr = "Stage4.2/StageManger4.2/";

    const float WarnAt = 1.2f, WarnLen = 3f, RiseLen = 5.3f, HoldLen = 0.7f, RetractLen = 1.3f;
    static float RiseAt => WarnAt + WarnLen;
    static float BreakAt => RiseAt + RiseLen;
    static float RetractAt => BreakAt + HoldLen;
    static float RestoreAt => RetractAt + RetractLen;

    static readonly Color Hidden = new Color(1f, 0f, 0.486f);
    static Color StepColor(string c) => c switch
    {
        "B" => new Color(0.227f, 0.290f, 0.561f),
        "G" => new Color(0.373f, 0.498f, 0.353f),
        "Y" => new Color(0.851f, 0.643f, 0.255f),
        "P" => new Color(0.349f, 0f, 0.737f),
        "C" => Color.white,
        _   => Color.black,
    };

    static Scene _scene;

    [MenuItem("Tools/Trailer/v2/M.Stage4")]
    public static void Build()
    {
        _scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "SpikeLaneWarnMarker");
        foreach (string s in new[] { "Stage4.1", "Stage4.2", "Stage4.3" })
            TrailerShotKit.Find(_scene, s).SetActive(s == "Stage4.2");
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        Camera cam = BuildCamera(shot);

        string[] steps = { "B", "Y", "P", "C", "G", "D", "B", "Y", "P", "C", "G", "B", "Y", "D", "P", "G" };
        var clear = new float[steps.Length];
        float t = 1.1f;
        for (int k = 0; k < steps.Length; k++) { clear[k] = t; t += steps[k] == "D" ? 1.0f : 0.8f; }
        const float startAt = 0.4f;
        BuildRingColors(shot, steps, clear, startAt);
        BuildMarker(shot, cam, steps.Length, clear, startAt);
        BuildTongue(shot);
        if (!BuildRunners(shot)) return;

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(_scene);
        Debug.Log($"[Trailer v2] M4 생성 — 혀 올라옴 {RiseAt:F1} 파괴 {BreakAt:F1} 복구 {RestoreAt:F1}");
    }

    static Camera BuildCamera(TrailerShotKit.Shot shot)
    {
        return TrailerV2.AddKeyCamera(shot, new List<(float, Vector3, Vector3)>
        {
            (0f,        new Vector3(17f, 9.5f, -15f),   new Vector3(0f, 1f, 0f)),
            (RiseAt,    new Vector3(11f, 7f, -17f),     new Vector3(0f, 1f, 0f)),
            (BreakAt - 1.2f, new Vector3(4f, 5.2f, -16.5f), new Vector3(0f, 2.2f, 0f)),
            (Total,     new Vector3(-2f, 5.5f, -16.5f), new Vector3(0f, 2.5f, 1f)),
        });
    }

    // ── 꿀떡 ─────────────────────────────────────────────────────

    /// <summary>링 띠 안(가운데 칸 바깥 7.5 ~ 바깥 끝 12.5, 몸 여유 0.8)인지.</summary>
    static bool OnRing(Vector3 q)
    {
        float m = Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.z));
        return m > 7.5f + 0.8f && m < 12.5f - 0.8f;
    }

    static bool BuildRunners(TrailerShotKit.Shot shot)
    {
        var home = new (string key, Vector3 at, float yaw, Vector3 ring)[]
        {
            ("B", new Vector3(-2.6f, GroundY,  1.1f), 213f, new Vector3(-3.4f, GroundY, -10.3f)),
            ("P", new Vector3( 0.9f, GroundY,  3.2f), 341f, new Vector3(-10.1f, GroundY,  5.6f)),
            ("G", new Vector3( 2.8f, GroundY, -0.7f),  76f, new Vector3( 10.4f, GroundY,  1.9f)),
            ("Y", new Vector3(-0.2f, GroundY, -2.4f), 152f, new Vector3(  2.3f, GroundY, -9.5f)),
        };
        string fail = null;
        for (int seed = 0; seed < 500; seed++)
        {
            var rng = new System.Random(0x4D4 + seed);
            var all = new List<(string, TrailerShotKit.Moves)>();
            var arrive = new float[4];
            for (int i = 0; i < 4; i++)
            {
                var m = new TrailerShotKit.Moves(home[i].at);
                float go = WarnAt + TrailerV2.Rand(rng, 0.25f, 0.7f);
                m.Fidget(rng, 0f, go - 0.1f, home[i].at, 1.2f);
                // 바깥으로 비스듬히 한 번 꺾어 링으로
                Vector3 from = m.End, to = home[i].ring;
                Vector3 side = Vector3.Cross(Vector3.up, (to - from).normalized);
                Vector3 mid = Vector3.Lerp(from, to, TrailerV2.Rand(rng, 0.4f, 0.6f)) + side * TrailerV2.Rand(rng, -1.8f, 1.8f);
                mid.y = GroundY;
                arrive[i] = m.RunTo(m.RunTo(go, mid, Speed), to, Speed);
                // 링 위 서성임(끝까지 — 외침은 목소리라 몸 동작 없음)
                m.Fidget(rng, arrive[i] + 0.2f, Total, to, 1.3f, (tt, q) => !OnRing(q));
                all.Add((home[i].key, m));
            }
            fail = TrailerV2.CheckGaps(all, Total, 2f);
            if (fail != null) continue;
            // 가운데 칸이 깨질 때 아무도 가운데에 없어야 한다
            foreach (var (key, m) in all)
                for (float s = BreakAt - 0.3f; s <= RestoreAt; s += 0.05f)
                {
                    Vector3 q = m.At(s);
                    if (Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.z)) < 7.5f + 0.6f) { fail = $"{s:F2}s {key} 가운데"; break; }
                }
            if (fail != null) continue;

            Debug.Log($"[Trailer v2] M4 동선 seed {seed}");
            Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
            for (int i = 0; i < 4; i++)
            {
                var k = TrailerShotKit.MakeRunner(group, home[i].key, home[i].at, home[i].yaw);
                all[i].Item2.ApplyTo(k);
                k.Build(shot);
            }
            return true;
        }
        Debug.LogError("[Trailer v2] M4 동선 실패: " + fail);
        return false;
    }

    // ── 링(1차와 같은 규칙) ──────────────────────────────────────

    static float RingU(int k) => 5f + 5f * (k % 16);

    static Vector3 Ring(float u)
    {
        u = Mathf.Repeat(u, 80f);
        if (u < 20f) return new Vector3(-10f + u, 0f, 10f);
        if (u < 40f) return new Vector3(10f, 0f, 10f - (u - 20f));
        if (u < 60f) return new Vector3(10f - (u - 40f), 0f, -10f);
        return new Vector3(-10f, 0f, -10f + (u - 60f));
    }

    /// <summary>NextOnly: 다음 칸만 색(현재는 숨김), 시작(cur=0)엔 0·1번 표시.</summary>
    static void BuildRingColors(TrailerShotKit.Shot shot, string[] steps, float[] clear, float startAt)
    {
        AnimationClip clip = shot.NewClip("RingFloor");
        float CurFrom(int k) => k == 0 ? startAt : clear[k - 1];
        for (int tile = 0; tile < 16; tile++)
        {
            var keys = new List<(float, Color)> { (0f, Hidden) };
            for (int k = 0; k < steps.Length; k++)
            {
                if (k % 16 != tile) continue;
                float nextFrom = k == 0 ? startAt : CurFrom(k - 1);
                float until = k == 0 ? clear[0] : CurFrom(k);
                keys.Add((nextFrom, StepColor(steps[k])));
                keys.Add((until, Hidden));
            }
            keys.Add((Total, Hidden));
            TrailerShotKit.SetMatColor(clip, "RingTile" + tile, typeof(MeshRenderer), "_BaseColor", keys, stepped: true);
        }
        shot.Animate(TrailerShotKit.Find(_scene, Mgr + "Stage4_Floor"), clip);
    }

    static void BuildMarker(TrailerShotKit.Shot shot, Camera cam, int count, float[] clear, float startAt)
    {
        GameObject src = TrailerShotKit.Find(_scene, Mgr + "SequenceRingMiniGame/SequenceRingCurrentMarker/Marker");
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
        shot.BindActive(marker, startAt, Total + 1f);
    }

    static void BuildTongue(TrailerShotKit.Shot shot)
    {
        GameObject warn = TrailerShotKit.Find(_scene, "WarnMarker_C");
        warn.SetActive(true);
        var marker = warn.GetComponent<SpikeLaneWarnMarker>();
        shot.Call(marker, nameof(SpikeLaneWarnMarker.PlayWarning), WarnAt, WarnLen);
        shot.Call(marker, nameof(SpikeLaneWarnMarker.ResetWarning), RiseAt);

        GameObject tongue = TrailerShotKit.Find(_scene, Mgr + "TongueAttack");
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

        GameObject middle = TrailerShotKit.Find(_scene, Mgr + "Stage4_Floor/MiddleRingTile");
        var fx = shot.TileFx(middle, "Assets/Prefab/FloorTileShards.prefab", 2f, 2f, 5f,
            fullRewind: new TileRewindSettings { duration = 0.8f, distanceMin = 2f, distanceMax = 5f, stagger = 0.15f, sfxId = SFXId.None });
        shot.Call(fx, nameof(TrailerTileFx.Break), BreakAt);
        shot.Call(fx, nameof(TrailerTileFx.RestoreFull), RestoreAt);
    }
}
