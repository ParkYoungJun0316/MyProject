#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Map01 씬을 절차적으로 빌드하는 에디터 유틸리티.
/// Tools > Map01 > 빌드 Map01 씬 으로 실행.
///
/// [맵 구성 - 100 × 300]
///  Z=  0  시작 벽
///  Z= 10  GGul 플레이어 스폰
///  Z= 20  SavePoint 1 (Start)
///  Z= 80  MemoryPath 섹션 1
///  Z=150  SavePoint 2 (Mid)
///  Z=200  MemoryPath 섹션 2
///  Z=270  SavePoint 3 (End)
///  Z=278  ClearZone (진입 시 맵 클리어)
///  Z=292  Door (맵 클리어 시 열림)
///  Z=300  끝 벽
/// </summary>
public static class Map01Builder
{
    // ── 경로 ──────────────────────────────────────────────────────
    const string k_ScenePath  = "Assets/Scenes/Map01.unity";
    const string k_PrefabRoot = "Assets/Prefab/";

    // ── 맵 크기 ───────────────────────────────────────────────────
    const float k_Width      = 100f;
    const float k_Length     = 300f;
    const float k_HalfWidth  = k_Width  * 0.5f;
    const float k_HalfLength = k_Length * 0.5f;

    // ── 벽 두께 / 높이 ─────────────────────────────────────────────
    const float k_WallThick  = 1f;
    const float k_WallHeight = 4f;

    // ── 카메라 높이 ────────────────────────────────────────────────
    const float k_CamHeight  = 20f;

    // =============================================================

    [MenuItem("Tools/Map01/빌드 Map01 씬", false, 1)]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Map01 씬 열기 (없으면 생성 후 열기)
        if (!System.IO.File.Exists(k_ScenePath))
        {
            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, k_ScenePath);
        }

        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        // 기존 오브젝트 전부 제거
        foreach (var go in scene.GetRootGameObjects())
            Object.DestroyImmediate(go);

        // ── 씬 구성 ───────────────────────────────────────────────
        SetupLighting();
        SetupGround();
        SetupWalls();

        var player = PlacePrefab("GGul", new Vector3(0f, 0f, 10f), "Player_GGul");
        SetupCamera(player.transform);
        SetupSavePoints();
        SetupMemoryPaths();

        var door = PlacePrefab("Door", new Vector3(0f, 0f, 292f), "Door_Exit");
        SetupStageManager(door);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Map01Builder] Map01 씬 빌드 완료 (100 × 300)");
        EditorUtility.DisplayDialog(
            "Map01 Builder",
            "Map01 씬 빌드 완료!\n\n" +
            "※ TopDownCamera의 height 값을 Inspector에서 확인하세요.\n" +
            "   권장값: 15 ~ 25",
            "확인");
    }

    // ── 조명 ─────────────────────────────────────────────────────

    static void SetupLighting()
    {
        var go = new GameObject("Directional Light");
        var dl = go.AddComponent<Light>();
        dl.type      = LightType.Directional;
        dl.intensity = 1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // ── 바닥 ─────────────────────────────────────────────────────

    static void SetupGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        // Unity Plane 기본 크기 10×10 → scale(10,1,30) = 100×300
        ground.transform.position   = new Vector3(0f, 0f, k_HalfLength);
        ground.transform.localScale = new Vector3(k_Width / 10f, 1f, k_Length / 10f);
        GameObjectUtility.SetStaticEditorFlags(
            ground,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
    }

    // ── 경계 벽 ──────────────────────────────────────────────────

    static void SetupWalls()
    {
        var parent = new GameObject("Walls");
        float yCenter = k_WallHeight * 0.5f;

        // 좌/우 벽 (Z 방향으로 긺)
        CreateWall(parent.transform, "Wall_Left",
            new Vector3(-(k_HalfWidth + k_WallThick * 0.5f), yCenter, k_HalfLength),
            new Vector3(k_WallThick, k_WallHeight, k_Length + k_WallThick * 2f));

        CreateWall(parent.transform, "Wall_Right",
            new Vector3( (k_HalfWidth + k_WallThick * 0.5f), yCenter, k_HalfLength),
            new Vector3(k_WallThick, k_WallHeight, k_Length + k_WallThick * 2f));

        // 시작/끝 벽 (X 방향으로 긺)
        CreateWall(parent.transform, "Wall_Start",
            new Vector3(0f, yCenter, -k_WallThick * 0.5f),
            new Vector3(k_Width + k_WallThick * 2f, k_WallHeight, k_WallThick));

        CreateWall(parent.transform, "Wall_End",
            new Vector3(0f, yCenter, k_Length + k_WallThick * 0.5f),
            new Vector3(k_Width + k_WallThick * 2f, k_WallHeight, k_WallThick));
    }

    static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position   = pos;
        wall.transform.localScale = scale;
        GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.BatchingStatic);
    }

    // ── 카메라 ───────────────────────────────────────────────────

    static void SetupCamera(Transform target)
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();

        var tc = camGO.AddComponent<TopDownCamera>();
        tc.target = target;

        // private [SerializeField] 필드를 SerializedObject로 설정
        var so = new SerializedObject(tc);
        so.FindProperty("height").floatValue         = k_CamHeight;
        so.FindProperty("strictTopDown").boolValue   = true;
        so.FindProperty("positionDamping").floatValue = 0.1f;
        so.ApplyModifiedProperties();

        camGO.transform.position = new Vector3(0f, k_CamHeight, 10f);
    }

    // ── 세이브 포인트 (처음/중간/끝) ─────────────────────────────

    static void SetupSavePoints()
    {
        (float z, int order, string label)[] points =
        {
            ( 20f, 0, "SavePoint_Start"),
            (150f, 1, "SavePoint_Mid"),
            (270f, 2, "SavePoint_End"),
        };

        foreach (var (z, order, label) in points)
        {
            var go = PlacePrefab("SavePoint", new Vector3(0f, 0f, z), label);

            // saveOrder 설정 (프리팹 인스턴스 직접 수정)
            var sp = go.GetComponentInChildren<SavePoint>(true)
                  ?? go.GetComponent<SavePoint>();
            if (sp != null)
                sp.saveOrder = order;
        }
    }

    // ── 메모리 패스 (섹션 1 / 섹션 2) ─────────────────────────────

    static void SetupMemoryPaths()
    {
        PlacePrefab("Memorypath", new Vector3(0f, 0f,  80f), "MemoryPath_Section1");
        PlacePrefab("Memorypath", new Vector3(0f, 0f, 200f), "MemoryPath_Section2");
    }

    // ── 스테이지 매니저 & 클리어 존 ──────────────────────────────

    static void SetupStageManager(GameObject door)
    {
        var stageGO = new GameObject("StageManager");
        var sm = stageGO.AddComponent<StageManager>();

        // ClearZone: 플레이어가 Door 직전 구역에 진입하면 맵 클리어
        var czGO = new GameObject("ClearZone");
        czGO.transform.SetParent(stageGO.transform);
        czGO.transform.position = new Vector3(0f, 2f, 278f);

        var col = czGO.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(k_Width, 4f, 10f);   // 맵 전체 폭으로 감지

        czGO.AddComponent<ReachZoneObjective>();

        // StageManager.OnStageClear → DoorController.Open() 연결
        var doorCtrl = door.GetComponent<DoorController>();
        if (doorCtrl != null)
            UnityEventTools.AddPersistentListener(sm.OnStageClear, doorCtrl.Open);
        else
            Debug.LogWarning("[Map01Builder] Door 프리팹에 DoorController가 없습니다. 이벤트 연결 실패.");
    }

    // ── 프리팹 배치 헬퍼 ─────────────────────────────────────────

    static GameObject PlacePrefab(string prefabName, Vector3 position, string overrideName = null)
    {
        var path   = k_PrefabRoot + prefabName + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning($"[Map01Builder] 프리팹을 찾을 수 없음: {path}");
            var empty = new GameObject(overrideName ?? prefabName);
            empty.transform.position = position;
            return empty;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;

        if (overrideName != null)
            instance.name = overrideName;

        return instance;
    }
}
#endif
