using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 트레일러 v2 M.Boss (10초) — P4 턱 내리찍기 한 회차(남는 칸 1개 — 게임 3~5회차).
/// 게임 값(씬 MouthBossJawSmash): 경고 0.8(SpikeLaneWarnMarker) → MouthBG Close 2 → 남는 칸 빼고 전부 파괴(FloorTileShards 2초·2~5)
///   → 1초 → Open 2 → 바닥 전체 되감기 복구(0.8초·2~5m·stagger 0.15). 암전 없음(10/2 제거). 페이즈 분위기 = BossMaterialChange P4.
/// 1차는 쓰지 않던 게임 복구 되감기(TileRestoreRewindGroup)·경고 컴포넌트를 그대로 부른다.
/// 꿀떡: 판 여기저기 흩어져 서성이다 경고가 뜨면 남는 칸 하나로 몰려들어 비좁게 붙어 선다(2m 간격) → 파괴에 놀람 → 복구 뒤 흩어짐.
/// 카메라: 남는 칸을 향해 판 위를 미끄러져 들어가, 파괴 순간엔 몰린 넷을 어깨 너머 위(높이 6.5)에서 내려다본다(사라진 바닥과 닫히는 이빨).
/// </summary>
public static class TrailerV2MBoss
{
    const string Stage = "M.Boss";
    const string Src   = "Assets/Scenes/M.Boss.unity";
    const float  Total = 10f;
    const float  GroundY = 0.5f;
    const float  Speed = 9f;
    const string Mgr = "Boss 270-360/StageManager_Boss5/";

    const float WarnAt = 1.3f, WarnLen = 0.8f, CloseLen = 2f, BreakHold = 1f, OpenLen = 2f;
    static float CloseAt => WarnAt + WarnLen;
    static float BreakAt => CloseAt + CloseLen;
    static float OpenAt => BreakAt + BreakHold;
    static float RestoreAt => OpenAt + OpenLen;

    static readonly Vector3 Keep = new Vector3(-5f, GroundY, 0f);

    static Scene _scene;

    [MenuItem("Tools/Trailer/v2/M.Boss")]
    public static void Build()
    {
        _scene = TrailerShotKit.PrepStage(Src, TrailerV2.ScenePath(Stage), "SpikeLaneWarnMarker");
        foreach (string p in new[] { "Boss 0-90", "Boss 60-130", "Boss 130-200", "Boss 270-360" })
            TrailerShotKit.Find(_scene, p).SetActive(p == "Boss 270-360");
        ApplyPhaseLook(4);
        var shot = new TrailerShotKit.Shot(TrailerV2.AssetDir(Stage), TrailerV2.ShotName(Stage), Total);

        if (!BuildRunners(shot)) return;
        BuildJaw(shot);

        TrailerV2.AddKeyCamera(shot, new List<(float, Vector3, Vector3)>
        {
            (0f,             new Vector3(8f, 7.5f, -15f),   new Vector3(-2f, 0.5f, 0f)),
            (CloseAt,        new Vector3(2f, 5f, -9.5f),    new Vector3(-5f, 1f, 0.5f)),
            (BreakAt + 0.2f, new Vector3(0.5f, 6.5f, -8.5f), new Vector3(-5f, 0.5f, 1.5f)),
            (Total,          new Vector3(2.5f, 7.5f, -10.5f), new Vector3(-3.5f, 0.5f, 1.5f)),
        });

        shot.AddRecorder(TrailerV2.ShotName(Stage));
        shot.Save(_scene);
        Debug.Log($"[Trailer v2] M.Boss 생성 — 경고 {WarnAt} 닫힘 {CloseAt} 파괴 {BreakAt} 열림 {OpenAt} 복구 {RestoreAt}");
    }

    static bool BuildRunners(TrailerShotKit.Shot shot)
    {
        var plan = new (string key, Vector3 at, float yaw, Vector2 huddle)[]
        {
            ("B", new Vector3(-8.3f, GroundY,  6.9f), 141f, new Vector2(-1.2f,  1.1f)),
            ("P", new Vector3( 6.2f, GroundY,  4.1f), 203f, new Vector2( 1.1f,  1.3f)),
            ("G", new Vector3( 7.6f, GroundY, -6.8f), 318f, new Vector2( 1.3f, -1.0f)),
            ("Y", new Vector3(-3.1f, GroundY, -8.4f),  52f, new Vector2(-1.0f, -1.2f)),
        };
        string fail = null;
        for (int seed = 0; seed < 500; seed++)
        {
            var rng = new System.Random(0xB055 + seed);
            var all = new List<(string, TrailerShotKit.Moves)>();
            bool late = false;
            foreach (var p in plan)
            {
                var m = new TrailerShotKit.Moves(p.at);
                float go = WarnAt + TrailerV2.Rand(rng, 0.15f, 0.45f);
                m.Fidget(rng, 0f, go - 0.1f, p.at, 1.4f, (t, q) => Mathf.Abs(q.x) > 11.5f || Mathf.Abs(q.z) > 11.5f);
                Vector3 spot = Keep + new Vector3(p.huddle.x + TrailerV2.Rand(rng, -0.15f, 0.15f), 0f, p.huddle.y + TrailerV2.Rand(rng, -0.15f, 0.15f));
                Vector3 from = m.End;
                Vector3 side = Vector3.Cross(Vector3.up, (spot - from).normalized);
                Vector3 mid = Vector3.Lerp(from, spot, TrailerV2.Rand(rng, 0.35f, 0.55f)) + side * TrailerV2.Rand(rng, -1.6f, 1.6f);
                mid.y = GroundY;
                float arrive = m.RunTo(m.RunTo(go, mid, Speed), spot, Speed);
                if (arrive > BreakAt - 0.5f) late = true;
                // 복구 뒤 흩어짐
                float leave = RestoreAt + TrailerV2.Rand(rng, 0.35f, 0.9f);
                Vector3 away = spot + new Vector3(p.huddle.x, 0f, p.huddle.y).normalized * TrailerV2.Rand(rng, 2.5f, 4f);
                float back = m.RunTo(leave, away, Speed);
                m.Fidget(rng, back + 0.3f, Total, away, 1.0f);
                all.Add((p.key, m));
            }
            if (late) { fail = "도착 늦음"; continue; }
            fail = TrailerV2.CheckGaps(all, Total, 2f);
            if (fail != null) continue;
            // 파괴~복구 동안 전원 남는 칸 안(몸 0.5 여유)
            foreach (var (key, m) in all)
                for (float s = BreakAt - 0.3f; s <= RestoreAt; s += 0.05f)
                {
                    Vector3 q = m.At(s) - Keep;
                    if (Mathf.Abs(q.x) > 2.45f - 0.5f || Mathf.Abs(q.z) > 2.45f - 0.5f) { fail = $"{s:F2}s {key} 칸 밖"; break; }
                }
            if (fail != null) continue;

            Debug.Log($"[Trailer v2] M.Boss 동선 seed {seed}");
            Transform group = TrailerShotKit.Group("Kkultteok", shot.Root).transform;
            for (int i = 0; i < plan.Length; i++)
            {
                var k = TrailerShotKit.MakeRunner(group, plan[i].key, plan[i].at, plan[i].yaw);
                all[i].Item2.ApplyTo(k);
                k.Pose(BreakAt + 0.05f + i * 0.07f, "Surprise").Pose(OpenAt + 0.6f, "Idle");
                k.Build(shot);
            }
            return true;
        }
        Debug.LogError("[Trailer v2] M.Boss 동선 실패: " + fail);
        return false;
    }

    static void BuildJaw(TrailerShotKit.Shot shot)
    {
        const string fbx = "Assets/NoAI/Mouth/MouthBG/MouthBG.fbx";
        shot.BindSequence(TrailerShotKit.Find(_scene, "BackGround/MouthBG").GetComponent<Animator>(), "MouthBG", new List<(AnimationClip, float)>
        {
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),  0f),
            (TrailerShotKit.LoadClip(fbx, "Armature|Close"), CloseAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Hold"),  BreakAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Open"),  OpenAt),
            (TrailerShotKit.LoadClip(fbx, "Armature|Idle"),  RestoreAt),
        }, 0.05f);

        var rewind = new TileRewindSettings { duration = 0.8f, distanceMin = 2f, distanceMax = 5f, stagger = 0.15f, sfxId = SFXId.None };
        foreach (Transform tile in TrailerShotKit.Find(_scene, Mgr + "Ground").transform)
        {
            Vector3 p = tile.position;
            if (Mathf.Abs(p.x - Keep.x) < 0.1f && Mathf.Abs(p.z - Keep.z) < 0.1f) continue;
            var marker = WarnAt_(p);
            shot.Call(marker, nameof(SpikeLaneWarnMarker.PlayWarning), WarnAt, WarnLen);
            shot.Call(marker, nameof(SpikeLaneWarnMarker.ResetWarning), CloseAt);
            var fx = shot.TileFx(tile.gameObject, "Assets/Prefab/FloorTileShards.prefab", 2f, 2f, 5f, fullRewind: rewind);
            shot.Call(fx, nameof(TrailerTileFx.Break), BreakAt);
            shot.Call(fx, nameof(TrailerTileFx.RestoreFull), RestoreAt);
        }
    }

    static SpikeLaneWarnMarker WarnAt_(Vector3 p)
    {
        foreach (Transform t in TrailerShotKit.Find(_scene, Mgr + "MouthBossJawSmash5/JawWarnMarkers").transform)
            if (Mathf.Abs(t.position.x - p.x) < 0.1f && Mathf.Abs(t.position.z - p.z) < 0.1f) return t.GetComponent<SpikeLaneWarnMarker>();
        Debug.LogError($"[Trailer v2] 경고 마커 없음 {p}");
        return null;
    }

    /// <summary>게임 BossMaterialChange.Apply(n-1)과 같은 값(1차 TrailerShotMBoss와 동일).</summary>
    static void ApplyPhaseLook(int n)
    {
        Material M(string name)
        {
            foreach (string g in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.name == name) return m;
            }
            Debug.LogError("[Trailer v2] 재질 없음: " + name);
            return null;
        }
        Renderer R(string name)
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name == name) return r;
            return null;
        }
        R("Environment").sharedMaterial = M("BossPhase" + n);
        R("CC_Base_Teeth").sharedMaterials = new[] { M($"M{n}_Upper"), M($"M{n}_Lower") };
        R("CC_Base_Tongue").sharedMaterial = M($"M{n}_Tongue");
        RenderSettings.fogColor = new Color(1f, 0.078f, 0.078f);
    }
}
