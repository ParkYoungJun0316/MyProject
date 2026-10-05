using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Timeline;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// 트레일러 촬영 씬(Marketing_&lt;씬&gt;) 공용 도구.
/// 원본 씬을 복제 → 게임 로직(프로젝트 스크립트·NGO·물리)을 전부 걷어냄 → Timeline 하나가
/// 카메라·꿀떡·함정을 움직이고, 같은 Timeline의 Recorder 트랙이 Play 중 MP4로 녹화한다.
/// 원본 씬·프리팹은 건드리지 않는다.
/// </summary>
public static class TrailerShotKit
{
    public const float Fps = 30f;

    const string KkultteokPrefab = "Assets/Prefab/Kkultteok.prefab";
    const string CameraPrefab    = "Assets/Prefab/LocalPlayerCamera.prefab";
    const string BodyFbx         = "Assets/NoAI/Kkultteok.fbx";
    const string RunFbx          = "Assets/NoAI/Kkultteok@Run_v3.fbx";

    // 이 어셈블리의 MonoBehaviour만 남긴다(렌더·볼륨·Timeline). 나머지 스크립트는 전부 제거.
    static readonly string[] KeepAssemblyPrefixes = { "UnityEngine", "Unity.RenderPipelines", "Unity.Timeline", "Unity.TextMeshPro" };

    // ── 씬 준비 ─────────────────────────────────────────────────

    /// <summary>
    /// 저장 확인 창은 띄우지 않는다(MCP 호출 중 창이 뜨면 에디터가 멈춤). 촬영용 Marketing_ 씬의 변경은
    /// 버리고, 그 외 씬에 저장 안 한 변경이 있으면 아무것도 하지 않고 예외로 멈춘다.
    /// </summary>
    /// <summary>
    /// 스테이지 씬 공통 준비: 복제본 열기 → 언팩 → 네트워크·흐름·UI 루트 삭제 → 시작 발판·경고선 숨김 → 로직 제거.
    /// EnvironmentEchoRotator(배경 회전)만 남긴다. keepTypes로 추가로 남길 게임 스크립트를 지정.
    /// </summary>
    public static Scene PrepStage(string srcPath, string dstPath, params string[] keepTypes)
    {
        Scene scene = OpenFreshCopy(srcPath, dstPath);
        UnpackAll(scene);
        Delete(scene, "Camera", "EventSystem", "StageFlow", "BossFlow", "SceneFlowRelay", "StageNetworkState",
            "DisconnectManager", "CheerService", "SFXEventManager", "SalivaHazard", "BackGround/UI");
        HideByPrefix(scene, "ColorStartZone.");
        HideByPrefix(scene, "WarnMarker");
        var keep = new List<string>(keepTypes) { "EnvironmentEchoRotator" };
        StripLogic(scene, keep.ToArray());
        return scene;
    }

    public static readonly string[] SquadKeys = { "B", "P", "G", "Y" };

    public static string BodyMat(string key) => key switch
    {
        "B" => "Assets/Mat/Player/Berry.mat",
        "P" => "Assets/Mat/Player/Guma.mat",
        "G" => "Assets/Mat/Player/Sook.mat",
        _   => "Assets/Mat/Player/Dan.mat",
    };

    public static Runner MakeRunner(Transform group, string key, Vector3 pos, float yaw)
    {
        var (rig, model) = SpawnKkultteok("Kk_" + key, BodyMat(key), group, pos, yaw);
        return new Runner(rig, model, pos, yaw);
    }

    public static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

    /// <summary>
    /// XZ 평면 A*(0.5m 격자) — 장애물 AABB를 inflate만큼 키워 피하고, 보이는 점끼리 이어 경유점을 줄인다.
    /// 반환: start 다음부터 goal까지의 경유점(y는 goal.y).
    /// </summary>
    public static List<Vector3> PlanPath(Vector3 start, Vector3 goal, List<Bounds> obstacles, float inflate, float half)
    {
        const float cell = 0.5f;
        int n = Mathf.CeilToInt(half * 2f / cell) + 1;
        bool Blocked(Vector2 p)
        {
            foreach (Bounds b in obstacles)
                if (p.x > b.min.x - inflate && p.x < b.max.x + inflate && p.y > b.min.z - inflate && p.y < b.max.z + inflate) return true;
            return Mathf.Abs(p.x) > half || Mathf.Abs(p.y) > half;
        }
        Vector2 W(int i, int j) => new Vector2(-half + i * cell, -half + j * cell);
        (int, int) C(Vector3 p) => (Mathf.Clamp(Mathf.RoundToInt((p.x + half) / cell), 0, n - 1), Mathf.Clamp(Mathf.RoundToInt((p.z + half) / cell), 0, n - 1));
        bool Clear(Vector2 a, Vector2 b)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f);
            for (int s = 0; s <= steps; s++) if (Blocked(Vector2.Lerp(a, b, s / (float)Mathf.Max(1, steps)))) return false;
            return true;
        }

        var a2 = new Vector2(start.x, start.z); var g2 = new Vector2(goal.x, goal.z);
        if (Clear(a2, g2)) return new List<Vector3> { goal };

        var (si, sj) = C(start); var (gi, gj) = C(goal);
        var cost = new Dictionary<(int, int), float> { [(si, sj)] = 0f };
        var from = new Dictionary<(int, int), (int, int)>();
        var open = new List<(int, int)> { (si, sj) };
        var done = new HashSet<(int, int)>();
        while (open.Count > 0)
        {
            open.Sort((x, y) => (cost[x] + Vector2.Distance(W(x.Item1, x.Item2), g2)).CompareTo(cost[y] + Vector2.Distance(W(y.Item1, y.Item2), g2)));
            var c = open[0]; open.RemoveAt(0);
            if (c == (gi, gj)) break;
            if (!done.Add(c)) continue;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue;
                    var nb = (c.Item1 + di, c.Item2 + dj);
                    if (nb.Item1 < 0 || nb.Item2 < 0 || nb.Item1 >= n || nb.Item2 >= n) continue;
                    // 벽에 붙어 선 출발점처럼 지금 칸이 막힌 영역이면, 빠져나올 때까지는 막힘을 무시한다
                    if (nb != (gi, gj) && Blocked(W(nb.Item1, nb.Item2)) && !Blocked(W(c.Item1, c.Item2))) continue;
                    float nc = cost[c] + ((di != 0 && dj != 0) ? 1.414f : 1f) * cell;
                    if (cost.TryGetValue(nb, out float old) && old <= nc) continue;
                    cost[nb] = nc; from[nb] = c;
                    open.Add(nb);
                }
        }
        if (!from.ContainsKey((gi, gj))) { Debug.LogWarning($"[Trailer] 경로 없음 {start}→{goal}"); return new List<Vector3> { goal }; }

        var cells = new List<Vector2> { g2 };
        for (var c = from[(gi, gj)]; c != (si, sj); c = from[c]) cells.Add(W(c.Item1, c.Item2));
        cells.Reverse();

        // 줄 당기기: 지금 점에서 보이는 가장 먼 점으로 건너뛴다
        var result = new List<Vector3>();
        Vector2 at = a2;
        int k = 0;
        while (k < cells.Count)
        {
            int far = k;
            for (int m = cells.Count - 1; m > k; m--) if (Clear(at, cells[m])) { far = m; break; }
            at = cells[far];
            result.Add(new Vector3(at.x, goal.y, at.y));
            k = far + 1;
        }
        return result;
    }

    public static Scene OpenFreshCopy(string srcPath, string dstPath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty && !Path.GetFileName(open.path).StartsWith("Marketing_"))
                throw new System.InvalidOperationException($"[TrailerShotKit] 저장 안 한 변경이 있는 씬: {open.path} — 먼저 저장하거나 되돌린 뒤 다시 실행");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dstPath) != null)
            AssetDatabase.DeleteAsset(dstPath);
        AssetDatabase.CopyAsset(srcPath, dstPath);
        return EditorSceneManager.OpenScene(dstPath, OpenSceneMode.Single);
    }

    public static void UnpackAll(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                    PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    public static void Delete(Scene scene, params string[] paths)
    {
        foreach (string path in paths)
        {
            GameObject go = Find(scene, path);
            if (go != null) Object.DestroyImmediate(go);
        }
    }

    public static GameObject Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            if (parts.Length == 1) return root;
            Transform t = root.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
            if (t != null) return t.gameObject;
        }
        return null;
    }

    public static void StripLogic(Scene scene, params string[] keepTypeNames)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            StripLogic(root, keepTypeNames);
    }

    /// <summary>스크립트 → 물리/오디오 순으로 지운다(RequireComponent 의존 순서 때문에 여러 번 돈다).</summary>
    public static void StripLogic(GameObject root, params string[] keepTypeNames)
    {
        var keep = new HashSet<string>(keepTypeNames);
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        // 다른 컴포넌트가 [RequireComponent]로 붙잡고 있으면 이번 패스는 건너뛰고, 붙잡은 쪽이 지워진 뒤 지운다
        for (int pass = 0; pass < 10; pass++)
        {
            bool removed = false;
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                bool strip = c is MonoBehaviour
                    ? !IsEngineType(c) && !keep.Contains(c.GetType().Name)
                    : c is Rigidbody || c is Collider || c is AudioSource;
                if (!strip || IsRequiredByOther(c)) continue;
                Object.DestroyImmediate(c);
                removed = true;
            }
            if (!removed) break;
        }
    }

    static bool IsRequiredByOther(Component c)
    {
        System.Type type = c.GetType();
        foreach (Component other in c.GetComponents<Component>())
        {
            if (other == null || other == c) continue;
            foreach (RequireComponent rc in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if (Requires(rc.m_Type0, type) || Requires(rc.m_Type1, type) || Requires(rc.m_Type2, type))
                    return true;
        }
        return false;
    }

    static bool Requires(System.Type required, System.Type type) => required != null && required.IsAssignableFrom(type);

    public static void HideByPrefix(Scene scene, string prefix)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) t.gameObject.SetActive(false);
    }

    static bool IsEngineType(Component c)
    {
        string asm = c.GetType().Assembly.GetName().Name;
        foreach (string prefix in KeepAssemblyPrefixes)
            if (asm.StartsWith(prefix)) return true;
        return false;
    }

    /// <summary>프리팹 링크 없는 복사본 + 로직 제거(keepTypeNames의 게임 스크립트는 남김).</summary>
    public static GameObject Spawn(string prefabPath, string name, Transform parent, Vector3 worldPos, Quaternion worldRot, params string[] keepTypeNames)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject go = Object.Instantiate(prefab, worldPos, worldRot, parent);
        go.name = name;
        StripLogic(go, keepTypeNames);
        return go;
    }

    /// <summary>12초 컷 공통 카메라: pivot을 보며 반지름 radius로 fromDeg→toDeg 원호(앞뒤 정지 hold초).</summary>
    public static Camera AddOrbitCamera(Shot shot, Vector3 pivot, float radius, float fromDeg, float toDeg, float hold)
    {
        Camera cam = SpawnCamera(shot.Root);
        AnimationClip clip = shot.NewClip("Camera_Orbit");
        float move = shot.Total - hold * 2f;

        float Angle(float t) => Mathf.Lerp(fromDeg, toDeg, SmootherStep((t - hold) / move));
        SetPosition(clip, "",
            Const(pivot.x, shot.Total),
            Sampled(shot.Total, t => pivot.y + radius * Mathf.Sin(Angle(t) * Mathf.Deg2Rad)),
            Sampled(shot.Total, t => pivot.z - radius * Mathf.Cos(Angle(t) * Mathf.Deg2Rad)));
        SetEuler(clip, "", Sampled(shot.Total, Angle), Const(0f, shot.Total), Const(0f, shot.Total));
        shot.BindWhole(cam.GetComponent<Animator>(), clip);
        return cam;
    }

    /// <summary>
    /// 움직이는 pivot을 따라가는 원호. yaw = 카메라가 바라보는 방향(0이면 -Z쪽에서 +Z를 봄, 180이면 +Z쪽에서 -Z를 봄).
    /// 각도 0 = pivot과 같은 높이, 90 = 바로 위에서 내려다봄.
    /// </summary>
    public static Camera AddFollowOrbitCamera(Shot shot, System.Func<float, Vector3> pivot, float radius, float fromDeg, float toDeg, float hold, float yaw)
    {
        Camera cam = SpawnCamera(shot.Root);
        AnimationClip clip = shot.NewClip("Camera_Orbit");
        float move = shot.Total - hold * 2f;
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);

        float Angle(float t) => Mathf.Lerp(fromDeg, toDeg, SmootherStep((t - hold) / move));
        Vector3 Pos(float t)
        {
            float a = Angle(t) * Mathf.Deg2Rad;
            return pivot(t) + turn * new Vector3(0f, radius * Mathf.Sin(a), -radius * Mathf.Cos(a));
        }
        SetPosition(clip, "", Sampled(shot.Total, t => Pos(t).x), Sampled(shot.Total, t => Pos(t).y), Sampled(shot.Total, t => Pos(t).z));
        SetEuler(clip, "", Sampled(shot.Total, Angle), Const(yaw, shot.Total), Const(0f, shot.Total));
        shot.BindWhole(cam.GetComponent<Animator>(), clip);
        return cam;
    }

    /// <summary>
    /// 게임 MouthController 입 닫힘: doClose + ScreenFader 암전(maxAlpha) → doHold(외칠 때까지) → doOpen + 밝아짐 → doIdle.
    /// mouthPath = MouthBG Animator 경로. 클립은 MouthBG.fbx Armature|*.
    /// </summary>
    public static void AddMouthClose(Shot shot, Scene scene, string mouthPath, float closeAt, float holdAt, float openAt, float idleAt, float maxAlpha)
    {
        var mouth = Find(scene, mouthPath).GetComponent<Animator>();
        const string fbx = "Assets/NoAI/Mouth/MouthBG/MouthBG.fbx";
        shot.BindSequence(mouth, "MouthBG", new List<(AnimationClip, float)>
        {
            (LoadClip(fbx, "Armature|Idle"),  0f),
            (LoadClip(fbx, "Armature|Close"), closeAt),
            (LoadClip(fbx, "Armature|Hold"),  holdAt),
            (LoadClip(fbx, "Armature|Open"),  openAt),
            (LoadClip(fbx, "Armature|Idle"),  idleAt),
        }, 0.05f);

        AddScreenFade(shot, new List<(float, float)>
        {
            (0f, 0f), (closeAt, 0f), (holdAt, maxAlpha), (openAt, maxAlpha), (idleAt, 0f), (shot.Total, 0f),
        });
    }

    /// <summary>화면 전체 검정 막(암전 연출). alpha 키를 Timeline으로 움직인다.</summary>
    public static void AddScreenFade(Shot shot, List<(float t, float alpha)> keys)
    {
        GameObject go = Group("ScreenFade", shot.Root);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var img = Group("Black", go.transform).AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<CanvasGroup>().alpha = 0f;
        var anim = go.AddComponent<Animator>();

        AnimationClip clip = shot.NewClip("ScreenFade");
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CanvasGroup), "m_Alpha"), Smooth(keys));
        shot.BindWhole(anim, clip);
    }

    public static GameObject Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Timeline 위치 트랙용 빈 부모. 자식(모델)의 Animator는 그대로 두고 이 부모만 움직인다.</summary>
    public static Animator Rig(string name, Transform parent, Vector3 pos, Quaternion rot)
    {
        GameObject go = Group(name, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        var a = go.AddComponent<Animator>();
        a.applyRootMotion = false;
        a.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
        return a;
    }

    public static Camera SpawnCamera(Transform parent)
    {
        GameObject go = Spawn(CameraPrefab, "TrailerCamera", parent, Vector3.zero, Quaternion.identity);
        go.tag = "MainCamera";
        var cam  = go.GetComponent<Camera>();
        var data = go.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.antialiasing        = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
        var a = go.AddComponent<Animator>();
        a.applyRootMotion = false;
        a.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
        return cam;
    }

    /// <summary>꿀떡 1명: Rig(위치) + Model(몸 애니). 몸통·팔·다리만 색 재질로 바꾼다.</summary>
    public static (Animator rig, Animator model) SpawnKkultteok(string name, string bodyMatPath, Transform parent, Vector3 pos, float yaw)
    {
        Animator rig = Rig(name, parent, pos, Quaternion.Euler(0f, yaw, 0f));
        GameObject model = Spawn(KkultteokPrefab, "Model", rig.transform, pos, Quaternion.Euler(0f, yaw, 0f));
        foreach (string child in new[] { "PunchHitBox", "Buff" })
        {
            Transform t = model.transform.Find(child);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        var bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
        foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "A_Body_Merged" || smr.name == "Arm" || smr.name == "Leg")
                smr.sharedMaterial = bodyMat;
            smr.updateWhenOffscreen = true;
        }

        var anim = model.GetComponent<Animator>();
        anim.runtimeAnimatorController = null;
        anim.applyRootMotion = false;
        anim.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
        return (rig, anim);
    }

    public static AnimationClip KkClip(string name) => name == "Run"
        ? LoadClip(RunFbx, "Run_v3")
        : LoadClip(BodyFbx, "아마튜어|" + name);

    public static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && c.name == clipName) return c;
        Debug.LogError($"[TrailerShotKit] 클립 없음: {fbxPath} / {clipName}");
        return null;
    }

    public static float Unwrap(float from, float to)
    {
        return from + Mathf.DeltaAngle(from, to);
    }

    // ── 커브 ────────────────────────────────────────────────────

    public static void SetPosition(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y, AnimationCurve z)
    {
        Set(clip, path, "m_LocalPosition.x", x);
        Set(clip, path, "m_LocalPosition.y", y);
        Set(clip, path, "m_LocalPosition.z", z);
    }

    public static void SetEuler(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y, AnimationCurve z)
    {
        Set(clip, path, "localEulerAnglesRaw.x", x);
        Set(clip, path, "localEulerAnglesRaw.y", y);
        Set(clip, path, "localEulerAnglesRaw.z", z);
    }

    public static void Set(AnimationClip clip, string path, string prop, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), curve);
    }

    public static AnimationCurve Const(float v, float total) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(total, v));

    /// <summary>Renderer 재질 프로퍼티(MaterialPropertyBlock로 적용됨) — 게임 스크립트가 MPB로 바꾸던 값을 그대로 흉내.</summary>
    public static void SetMat(AnimationClip clip, string path, System.Type rendererType, string prop, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, rendererType, "material." + prop), curve);
    }

    public static void SetMatColor(AnimationClip clip, string path, System.Type rendererType, string prop, List<(float t, Color c)> keys, bool stepped = false)
    {
        var r = new List<(float, float)>(); var g = new List<(float, float)>(); var b = new List<(float, float)>(); var a = new List<(float, float)>();
        foreach ((float t, Color c) in keys) { r.Add((t, c.r)); g.Add((t, c.g)); b.Add((t, c.b)); a.Add((t, c.a)); }
        System.Func<List<(float, float)>, AnimationCurve> make = stepped ? Steps : Linear;
        SetMat(clip, path, rendererType, prop + ".r", make(r));
        SetMat(clip, path, rendererType, prop + ".g", make(g));
        SetMat(clip, path, rendererType, prop + ".b", make(b));
        SetMat(clip, path, rendererType, prop + ".a", make(a));
    }

    /// <summary>sharedMaterial 교체(게임 GridTile 등이 재질 자체를 바꾸던 것) — (시각, 재질) 목록.</summary>
    public static void SetMaterialSwap(AnimationClip clip, string path, System.Type rendererType, List<(float t, Material m)> keys)
    {
        var frames = new ObjectReferenceKeyframe[keys.Count];
        for (int i = 0; i < keys.Count; i++) frames[i] = new ObjectReferenceKeyframe { time = keys[i].t, value = keys[i].m };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, rendererType, "m_Materials.Array.data[0]"), frames);
    }

    /// <summary>
    /// 오브젝트를 게임 Breakable로 깨지게 준비(파편 프리팹·임펄스·수명). time에 Break, restoreAt에 껐다 켜서 원상복구.
    /// Breakable은 [RequireComponent(Collider)]라 트리거 BoxCollider를 먼저 붙인다.
    /// </summary>
    public static void BreakAt(Shot shot, GameObject target, string debrisPrefab, float lifetime, float impulseMin, float impulseMax, float time, float restoreAt)
    {
        if (target.GetComponent<Collider>() == null) target.AddComponent<BoxCollider>().isTrigger = true;
        var b = target.GetComponent<Breakable>();
        if (b == null) b = target.AddComponent<Breakable>();
        var so = new SerializedObject(b);
        so.FindProperty("debrisPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(debrisPrefab);
        so.FindProperty("debrisLifetime").floatValue   = lifetime;
        so.FindProperty("debrisImpulseMin").floatValue = impulseMin;
        so.FindProperty("debrisImpulseMax").floatValue = impulseMax;
        so.FindProperty("syncBreakOverNetwork").boolValue = false;
        // 충돌로는 절대 안 깨지게(옆 칸 파편이 트리거에 닿아 연쇄 파괴되던 문제) — 안 쓰는 31번 레이어만 반응
        so.FindProperty("breakTriggerLayers").intValue = 1 << 31;
        so.ApplyModifiedPropertiesWithoutUndo();
        shot.Call(b, nameof(Breakable.Break), time);
        if (restoreAt < shot.Total)
            shot.BindActive(target, (0f, restoreAt), (restoreAt + 0.05f, shot.Total + 1f));
    }

    /// <summary>Renderer.enabled 계단 커브 — (시각, 켜짐) 목록.</summary>
    public static void SetEnabled(AnimationClip clip, string path, System.Type rendererType, List<(float t, bool on)> keys)
    {
        var k = new List<(float, float)>();
        foreach ((float t, bool on) in keys) k.Add((t, on ? 1f : 0f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, rendererType, "m_Enabled"), Steps(k));
    }

    public static AnimationCurve Steps(List<(float t, float v)> keys)
    {
        keys.Sort((a, b) => a.t.CompareTo(b.t));
        var curve = new AnimationCurve();
        foreach ((float t, float v) in keys) curve.AddKey(new Keyframe(t, v));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    public static AnimationCurve Linear(List<(float t, float v)> keys)
    {
        keys.Sort((a, b) => a.t.CompareTo(b.t));
        var curve = new AnimationCurve();
        foreach ((float t, float v) in keys) curve.AddKey(new Keyframe(t, v));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    /// <summary>게임 공용 경고색(WarnPalette): 귤색 #FF9A3D → 진홍 #E81E2B.</summary>
    public static readonly Color WarnStart = new Color(1f, 0x9A / 255f, 0x3D / 255f);
    public static readonly Color WarnEnd   = new Color(0xE8 / 255f, 0x1E / 255f, 0x2B / 255f);

    /// <summary>키 사이를 부드럽게(ClampedAuto) — 멈춘 구간은 평평하게 유지된다.</summary>
    public static AnimationCurve Smooth(List<(float t, float v)> keys)
    {
        keys.Sort((a, b) => a.t.CompareTo(b.t));
        var curve = new AnimationCurve();
        foreach ((float t, float v) in keys)
        {
            int i = curve.AddKey(new Keyframe(t, v));
            if (i < 0) continue; // 같은 시각 중복 키
        }
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    /// <summary>프레임마다 키 — 수식으로 만든 궤적(카메라 원호)용.</summary>
    public static AnimationCurve Sampled(float total, System.Func<float, float> f)
    {
        var curve = new AnimationCurve();
        int frames = Mathf.RoundToInt(total * Fps);
        for (int i = 0; i <= frames; i++)
        {
            float t = i / Fps;
            curve.AddKey(new Keyframe(t, f(t)));
        }
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    public static float SmootherStep(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * u * (u * (u * 6f - 15f) + 10f);
    }

    // ── Timeline ────────────────────────────────────────────────

    public sealed class Shot
    {
        public readonly TimelineAsset    Timeline;
        public readonly PlayableDirector Director;
        public readonly Transform        Root;
        public readonly float            Total;

        public Shot(string assetDir, string name, float total)
        {
            Total = total;
            Directory.CreateDirectory(assetDir);
            AssetDatabase.Refresh();

            string path = $"{assetDir}/{name}.playable";
            AssetDatabase.DeleteAsset(path);
            Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            Timeline.editorSettings.frameRate = Fps;
            Timeline.durationMode  = TimelineAsset.DurationMode.FixedLength;
            Timeline.fixedDuration = total;
            AssetDatabase.CreateAsset(Timeline, path);

            Root = Group("TrailerShot").transform;
            Director = Root.gameObject.AddComponent<PlayableDirector>();
            Director.playableAsset     = Timeline;
            Director.playOnAwake       = true;
            Director.extrapolationMode = DirectorWrapMode.Hold;
            Director.timeUpdateMode    = DirectorUpdateMode.GameTime;
        }

        public AnimationClip NewClip(string name)
        {
            var clip = new AnimationClip { name = name, frameRate = Fps };
            AssetDatabase.AddObjectToAsset(clip, Timeline);
            return clip;
        }

        /// <summary>0~Total을 덮는 절대 좌표 클립 하나(Rig·카메라·문·사과).</summary>
        public void BindWhole(Animator target, AnimationClip clip)
        {
            var track = Timeline.CreateTrack<AnimationTrack>(null, target.name);
            track.trackOffset = TrackOffset.ApplyTransformOffsets;
            track.position    = Vector3.zero;
            track.rotation    = Quaternion.identity;
            TimelineClip tc = track.CreateClip(clip);
            tc.start    = 0;
            tc.duration = Total;
            ((AnimationPlayableAsset)tc.asset).removeStartOffset = false;
            Director.SetGenericBinding(track, target);
        }

        /// <summary>(클립, 시작) 목록을 이어 붙인다. 다음 클립과 blend초만큼 겹쳐 섞는다.</summary>
        public void BindSequence(Animator target, string trackName, List<(AnimationClip clip, float start)> seq, float blend = 0.12f, float firstClipIn = 0f)
        {
            seq.Sort((a, b) => a.start.CompareTo(b.start));
            var track = Timeline.CreateTrack<AnimationTrack>(null, trackName);
            TimelineClip prev = null;
            for (int i = 0; i < seq.Count; i++)
            {
                float start = seq[i].start;
                float end   = i + 1 < seq.Count ? seq[i + 1].start + blend : Total;
                TimelineClip tc = track.CreateClip(seq[i].clip);
                tc.start    = start;
                tc.duration = Mathf.Max(0.05f, end - start);
                tc.displayName = seq[i].clip.name;
                if (i == 0) tc.clipIn = firstClipIn;
                if (prev != null)
                {
                    tc.blendInDuration    = blend;
                    prev.blendOutDuration = blend;
                }
                prev = tc;
            }
            Director.SetGenericBinding(track, target);
        }

        /// <summary>
        /// time에 target의 public 무인자 메서드를 호출(Signal 트랙 → SignalReceiver → UnityEvent).
        /// 게임 컴포넌트(Breakable.Break 등)를 그대로 써서 파편·연출을 게임과 똑같이 낸다.
        /// </summary>
        public void Call(MonoBehaviour target, string method, float time) => CallInternal(target, method, time, null);

        /// <summary>float 인자 하나를 받는 메서드(예: SpikeLaneWarnMarker.PlayWarning(duration)).</summary>
        public void Call(MonoBehaviour target, string method, float time, float arg) => CallInternal(target, method, time, arg);

        void CallInternal(MonoBehaviour target, string method, float time, float? arg)
        {
            // 신호 자산은 (메서드, 인자)마다 하나 — 한 수신기가 여러 반응(경고 켜기/끄기 등)을 구분하게
            string key = arg.HasValue ? $"{method}({arg.Value:0.###})" : method;
            if (!_signals.TryGetValue(key, out SignalAsset signal))
            {
                signal = ScriptableObject.CreateInstance<SignalAsset>();
                signal.name = "Call_" + key;
                AssetDatabase.AddObjectToAsset(signal, Timeline);
                _signals[key] = signal;
            }

            var receiver = target.GetComponent<SignalReceiver>();
            if (receiver == null) receiver = target.gameObject.AddComponent<SignalReceiver>();
            if (receiver.GetReaction(signal) == null)
            {
                var evt = new UnityEngine.Events.UnityEvent();
                if (arg.HasValue)
                {
                    var action = (UnityEngine.Events.UnityAction<float>)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<float>), target, method);
                    UnityEditor.Events.UnityEventTools.AddFloatPersistentListener(evt, action, arg.Value);
                }
                else
                {
                    var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(evt, action);
                }
                receiver.AddReaction(signal, evt);
            }

            var track = Timeline.CreateTrack<SignalTrack>(null, target.name + "." + key);
            var emitter = track.CreateMarker<SignalEmitter>(time);
            emitter.asset = signal;
            emitter.retroactive = false;
            emitter.emitOnce = true;
            Director.SetGenericBinding(track, receiver);
        }

        readonly Dictionary<string, SignalAsset> _signals = new();

        /// <summary>
        /// 게임 함정과 같은 타일 파괴·되감기(TrailerTileFx). 타일이 꺼져도 살아 있도록 따로 만든 오브젝트에 붙인다.
        /// </summary>
        public TrailerTileFx TileFx(GameObject tile, string debrisPrefab, float lifetime, float impulseMin, float impulseMax,
            TileRewindSettings autoRewind = null, TileRewindSettings fullRewind = null)
        {
            var go = new GameObject("TileFx_" + tile.name);
            go.transform.SetParent(Root, false);
            var fx = go.AddComponent<TrailerTileFx>();
            fx.tile = tile;
            fx.debrisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(debrisPrefab);
            fx.debrisLifetime = lifetime;
            fx.impulseMin = impulseMin;
            fx.impulseMax = impulseMax;
            fx.seed = tile.GetInstanceID();
            if (autoRewind != null) fx.autoRewind = autoRewind;
            if (fullRewind != null) fx.fullRewind = fullRewind;
            return fx;
        }

        public void BindActive(GameObject target, float start, float end) => BindActive(target, (start, end));

        /// <summary>구간들 동안만 켜짐(그 밖에는 꺼짐).</summary>
        public void BindActive(GameObject target, params (float start, float end)[] ranges)
        {
            var track = Timeline.CreateTrack<ActivationTrack>(null, target.name + " On");
            track.postPlaybackState = ActivationTrack.PostPlaybackState.LeaveAsIs;
            foreach ((float start, float end) in ranges)
            {
                TimelineClip tc = track.CreateDefaultClip();
                tc.start    = start;
                tc.duration = end - start;
            }
            Director.SetGenericBinding(track, target);
            target.SetActive(false);
        }

        /// <summary>이미 있는 오브젝트에 Animator를 붙여 clip(그 아래 상대 경로 커브들)을 0~Total 재생.</summary>
        public void Animate(GameObject target, AnimationClip clip)
        {
            var a = target.GetComponent<Animator>(); // ??는 Unity 가짜 null을 못 거른다
            if (a == null) a = target.AddComponent<Animator>();
            a.applyRootMotion = false;
            a.cullingMode     = AnimatorCullingMode.AlwaysAnimate;
            BindWhole(a, clip);
        }

        /// <summary>Play 중 Timeline이 0초부터 돌면 Game View를 MP4로 녹화(프로젝트/Recordings).</summary>
        public void AddRecorder(string outputName, int width = 1920, int height = 1080)
        {
            var track = Timeline.CreateTrack<RecorderTrack>(null, "Recorder");
            TimelineClip tc = track.CreateClip<RecorderClip>();
            tc.start       = 0;
            tc.duration    = Total;
            tc.displayName = outputName;

            var settings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            settings.name               = outputName;
            settings.Enabled            = true;
            // 편집 후 재인코딩을 견디도록 원본은 높은 비트레이트로 뽑는다(High 프리셋은 10초 ≈ 9MB)
            settings.EncoderSettings    = new CoreEncoderSettings
            {
                Codec           = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                TargetBitRate   = 40f,
            };
            settings.CaptureAudio       = false;
            settings.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            settings.FrameRatePlayback  = FrameRatePlayback.Constant;
            settings.FrameRate          = Fps;
            settings.CapFrameRate       = true;
            settings.FileNameGenerator.Root     = OutputPath.Root.Project;
            settings.FileNameGenerator.Leaf     = "Recordings";
            settings.FileNameGenerator.FileName = outputName;

            var rc = (RecorderClip)tc.asset;
            rc.settings = settings;
            AssetDatabase.AddObjectToAsset(settings, rc);
        }

        public void Save(Scene scene)
        {
            EditorUtility.SetDirty(Timeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    // ── 꿀떡 동선 ───────────────────────────────────────────────

    /// <summary>
    /// 꿀떡 한 명의 이동 계획(구간 = 시각·목적지, 등속 직선). Runner에 넣기 전에 위치를 계산·검사할 수 있다.
    /// Fidget = 가만히 기다리는 실제 플레이어처럼 가끔 키를 톡 눌러 0.4~1.6m 움직인다(이동 방향을 보고 멈춤, 제자리 회전 없음).
    /// </summary>
    public sealed class Moves
    {
        public readonly List<(float t0, float t1, Vector3 from, Vector3 to)> segs = new();
        public readonly Vector3 start;
        public Moves(Vector3 start) { this.start = start; }

        public Vector3 End => segs.Count > 0 ? segs[segs.Count - 1].to : start;

        public Moves Add(float t0, float t1, Vector3 to) { segs.Add((t0, t1, End, to)); return this; }

        /// <summary>speed로 달려 to까지. 반환 = 도착 시각.</summary>
        public float RunTo(float t0, Vector3 to, float speed)
        {
            float t1 = t0 + Mathf.Max(0.06f, Vector3.Distance(Flat(End), Flat(to)) / speed);
            Add(t0, t1, to);
            return t1;
        }

        public Vector3 At(float t)
        {
            Vector3 p = start;
            foreach (var s in segs)
            {
                if (t < s.t0) return p;
                if (t < s.t1) return Vector3.Lerp(s.from, s.to, (t - s.t0) / (s.t1 - s.t0));
                p = s.to;
            }
            return p;
        }

        /// <summary>from~until 사이 대기 중 잔걸음. anchor 반경 radius 안, avoid(t, 위치) 가 true면 그 걸음은 버린다.</summary>
        public void Fidget(System.Random rng, float from, float until, Vector3 anchor, float radius, System.Func<float, Vector3, bool> avoid = null)
        {
            float t = from + Rand(rng, 0.15f, 0.9f);
            while (t < until - 0.25f)
            {
                Vector3 cur = End;
                float ang = Rand(rng, 0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 home = anchor - cur; home.y = 0f;
                if (home.magnitude > radius * 0.5f) dir = (dir * 0.5f + home.normalized).normalized; // 멀어지면 제자리 쪽으로
                Vector3 to = cur + dir * Rand(rng, 0.4f, 1.6f);
                to.y = cur.y;
                Vector3 off = to - anchor; off.y = 0f;
                if (off.magnitude > radius) to = anchor + off.normalized * radius + Vector3.up * (cur.y - anchor.y);
                float t1 = t + Mathf.Max(0.06f, Vector3.Distance(Flat(cur), Flat(to)) / 10f);
                bool bad = false;
                if (avoid != null) for (float s = t; s <= t1 + 0.3f; s += 0.05f) if (avoid(s, Vector3.Lerp(cur, to, Mathf.Clamp01((s - t) / (t1 - t))))) { bad = true; break; }
                if (!bad && t1 < until) Add(t, t1, to);
                t = t1 + Rand(rng, 0.5f, 2.2f);
            }
        }

        public void ApplyTo(Runner r)
        {
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                bool chainedIn  = i > 0 && Mathf.Abs(segs[i - 1].t1 - s.t0) < 0.02f;
                bool chainedOut = i + 1 < segs.Count && Mathf.Abs(segs[i + 1].t0 - s.t1) < 0.02f;
                r.RunTo(s.t0, s.t1, s.to, null, !chainedIn, !chainedOut);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    }

    /// <summary>
    /// 꿀떡 한 명의 동선. RunTo로 이동(달리기 → 도착 후 Idle), Pose로 제자리 동작.
    /// Build()가 Rig 위치 클립과 Model 몸 애니 시퀀스를 Shot에 넣는다.
    /// </summary>
    public sealed class Runner
    {
        readonly Animator _rig, _model;
        readonly List<(float t, Vector3 p)> _pos = new();
        readonly List<(float t, float v)> _yaw = new();
        readonly List<(AnimationClip, float)> _body = new();
        Vector3 _cur;
        float _curYaw;

        public Runner(Animator rig, Animator model, Vector3 start, float yaw)
        {
            _rig = rig; _model = model;
            _cur = start; _curYaw = yaw;
            Transform body = model.transform.Find("A_Body_Merged");
            _startBody = body != null ? body.GetComponent<Renderer>().sharedMaterial : null;
            _pos.Add((0f, start));
            _yaw.Add((0f, yaw));
            _body.Add((KkClip("Idle"), 0f));
        }

        public Runner RunTo(float t0, float t1, Vector3 to, float? faceYaw = null) => RunTo(t0, t1, to, faceYaw, true, true);

        /// <summary>startRun/endIdle = false면 앞뒤 구간과 이어 달린다(경유점 사이에 Idle이 끼지 않게).</summary>
        public Runner RunTo(float t0, float t1, Vector3 to, float? faceYaw, bool startRun, bool endIdle)
        {
            Vector3 d = to - _cur; d.y = 0f;
            float dirYaw = Unwrap(_curYaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);

            _pos.Add((t0, _cur));
            // 발판 위로 올라서는 단차는 도착 직전에만 반영
            if (Mathf.Abs(to.y - _cur.y) > 0.01f)
            {
                float tStep = Mathf.Max(t0 + 0.01f, t1 - 0.12f);
                Vector3 mid = Vector3.Lerp(_cur, to, (tStep - t0) / (t1 - t0));
                mid.y = _cur.y;
                _pos.Add((tStep, mid));
            }
            _pos.Add((t1, to));

            _yaw.Add((t0, _curYaw));
            _yaw.Add((t0 + Mathf.Min(0.15f, (t1 - t0) * 0.5f), dirYaw));
            _curYaw = dirYaw;
            if (faceYaw.HasValue)
            {
                float f = Unwrap(dirYaw, faceYaw.Value);
                _yaw.Add((t1, dirYaw));
                _yaw.Add((t1 + 0.25f, f));
                _curYaw = f;
            }

            if (startRun) _body.Add((KkClip("Run"), t0));
            if (endIdle)  _body.Add((KkClip("Idle"), t1));
            _cur = to;
            return this;
        }

        public Runner Pose(float t, string clip)
        {
            _body.Add((KkClip(clip), t));
            return this;
        }

        float _firstClipIn;

        /// <summary>0초에 Idle 대신 clip으로 시작(이미 달리는 중인 컷). clipIn = 동작 박자 어긋나게 할 시작 오프셋(초).</summary>
        public Runner StartWith(string clip, float clipIn = 0f)
        {
            _body[0] = (KkClip(clip), 0f);
            _firstClipIn = clipIn;
            return this;
        }

        /// <summary>몸 동작은 그대로 두고 미끄러지듯 위치만 옮긴다(침 바닥 등).</summary>
        public Runner Glide(float t0, float t1, Vector3 to)
        {
            _pos.Add((t0, _cur));
            _pos.Add((t1, to));
            _cur = to;
            return this;
        }

        public Runner Turn(float t0, float t1, float yaw)
        {
            float f = Unwrap(_curYaw, yaw);
            _yaw.Add((t0, _curYaw));
            _yaw.Add((t1, f));
            _curYaw = f;
            return this;
        }

        public Vector3 Position => _cur;
        public float Yaw => _curYaw;

        readonly List<(float t, Material m)> _bodyMat = new();

        /// <summary>
        /// 색 변신(게임: Q=흑/백 전환, Alt=고유색 복귀 + doChangeColor 애니). 몸통·팔·다리 재질을 바꾸고
        /// ChangeColor 동작을 재생한다. 반환 = 변신이 끝나 움직일 수 있는 시각.
        /// </summary>
        public float ChangeColor(float t, string bodyMatPath)
        {
            if (_bodyMat.Count == 0) _bodyMat.Add((0f, _startBody));
            _body.Add((KkClip("ChangeColor"), t));
            _bodyMat.Add((t + 0.15f, AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath)));
            _body.Add((KkClip("Idle"), t + 0.63f));
            return t + 0.65f;
        }

        Material _startBody;

        /// <summary>
        /// 경유점들을 일정 속도로 이어 달린다(벽 돌아가기 등). 몸은 처음부터 끝까지 Run 한 번.
        /// 반환값 = 도착 시각.
        /// </summary>
        public float RunVia(float t0, float speed, List<Vector3> points, float? faceYaw = null)
        {
            if (points.Count == 0) return t0;
            _pos.Add((t0, _cur));
            _yaw.Add((t0, _curYaw));
            float t = t0;
            Vector3 prev = _cur;
            foreach (Vector3 p in points)
            {
                Vector3 d = p - prev; d.y = 0f;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                float yaw = Unwrap(_curYaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                _yaw.Add((t + Mathf.Min(0.12f, len / speed * 0.5f), yaw));
                _curYaw = yaw;
                // 0.5m마다 키 — 곡선 보간이 꺾이는 점에서 모서리를 깎아 벽으로 들어가지 않게
                int pieces = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                for (int s = 1; s <= pieces; s++)
                    _pos.Add((t + len / speed * s / pieces, Vector3.Lerp(prev, p, s / (float)pieces)));
                t += len / speed;
                prev = p;
            }
            _body.Add((KkClip("Run"), t0));
            _body.Add((KkClip("Idle"), t));
            if (faceYaw.HasValue)
            {
                float f = Unwrap(_curYaw, faceYaw.Value);
                _yaw.Add((t, _curYaw)); _yaw.Add((t + 0.25f, f)); _curYaw = f;
            }
            _cur = prev;
            return t;
        }

        /// <summary>
        /// (시각, 위치, 바라볼 방향) 키를 그대로 따라 움직인다 — 대형 이동처럼 이동 방향과 시선이 다를 때.
        /// moving 구간 동안 Run, 끝나면 Idle.
        /// </summary>
        public Runner Follow(List<(float t, Vector3 p, float yaw)> keys)
        {
            if (keys.Count == 0) return this;
            foreach (var k in keys)
            {
                _pos.Add((k.t, k.p));
                float y = Unwrap(_curYaw, k.yaw);
                _yaw.Add((k.t, y));
                _curYaw = y;
            }
            _cur = keys[keys.Count - 1].p;
            return this;
        }

        public void Build(Shot shot)
        {
            AnimationClip clip = shot.NewClip(_rig.name + "_Path");
            var xs = new List<(float, float)>(); var ys = new List<(float, float)>(); var zs = new List<(float, float)>();
            foreach ((float t, Vector3 p) in _pos) { xs.Add((t, p.x)); ys.Add((t, p.y)); zs.Add((t, p.z)); }
            xs.Add((shot.Total, _cur.x)); ys.Add((shot.Total, _cur.y)); zs.Add((shot.Total, _cur.z));
            _yaw.Add((shot.Total, _curYaw));
            SetPosition(clip, "", Smooth(xs), Smooth(ys), Smooth(zs));
            SetEuler(clip, "", Const(0f, shot.Total), Smooth(new List<(float, float)>(_yaw)), Const(0f, shot.Total));
            if (_bodyMat.Count > 0)
                foreach (string part in new[] { "A_Body_Merged", "Arm", "Leg" })
                    SetMaterialSwap(clip, "Model/" + part, typeof(SkinnedMeshRenderer), _bodyMat);
            shot.BindWhole(_rig, clip);
            shot.BindSequence(_model, _rig.name + " Body", _body, 0.12f, _firstClipIn);
        }
    }
}
