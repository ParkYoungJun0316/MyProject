#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Map02 씬을 절차적으로 빌드하는 에디터 유틸리티.
/// Tools > Map02 > 빌드 Map02 씬 으로 실행.
///
/// [맵 구성 - 250 × 250, 중심 (0,0,0)]
///
///  중앙 (0,0,0)
///    ├─ PushableBox × 4  (±3, 0.5, ±3) — 시작 시 배치된 Common 박스
///    ├─ BoxSpawner  × 4  (±10, 0, ±10) — 트리거 충족 시 박스 생성
///
///  BoxColorTrigger × 4  (0,±40) / (±40,0) — Common 박스를 올려두면 활성화
///  BoxSpawnGate: 4 트리거 모두 활성 → 4 스포너 동시 발동
///
///  BoxCountZone × 4  각 모퉁이 (±95, 0.1, ±95) — 박스를 여기로 운반
///
///  경계 벽 × 4
/// </summary>
public static class Map02Builder
{
    const string k_ScenePath  = "Assets/Scenes/Map02.unity";
    const string k_PrefabRoot = "Assets/Prefab/";

    const float k_Half       = 125f;   // 250 / 2
    const float k_WallThick  = 1f;
    const float k_WallHeight = 4f;
    const float k_CamHeight  = 45f;

    // BoxColorTrigger 위치: 십자 방향, 중심에서 40m
    const float k_TriggerRadius  = 40f;
    // BoxSpawner 위치: 중앙 근처, 4방향 10m
    const float k_SpawnerOffset  = 10f;
    // BoxCountZone 위치: 벽에서 30m 안쪽 모퉁이
    const float k_ZoneInset      = 30f;
    // 초기 박스 클러스터 반경
    const float k_BoxCluster     = 3f;

    // =============================================================

    [MenuItem("Tools/Map02/빌드 Map02 씬", false, 1)]
    static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!System.IO.File.Exists(k_ScenePath))
        {
            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, k_ScenePath);
        }

        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        foreach (var go in scene.GetRootGameObjects())
            Object.DestroyImmediate(go);

        SetupLighting();
        SetupGround();
        SetupWalls();

        var player = PlacePrefab("GGul", new Vector3(0f, 0f, -60f), "Player_GGul");
        SetupCamera(player.transform);

        var spawners  = SetupSpawners();
        var triggers  = SetupColorTriggers();
        SetupInitialBoxes();
        SetupBoxCountZones();
        SetupBoxSpawnGate(triggers, spawners);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Map02Builder] Map02 씬 빌드 완료 (250 × 250)");
        EditorUtility.DisplayDialog(
            "Map02 Builder",
            "Map02 씬 빌드 완료!\n\n" +
            "※ BulgeMesh를 각 BoxSpawner 자식으로 수동 연결하거나\n" +
            "   BoxSpawner.bulgeMesh 를 Inspector에서 연결해 주세요.\n" +
            "※ BoxSpawner.boxPrefab 에 PushableBox 프리팹을 연결해 주세요.",
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
        // Unity Plane 기본 10×10 → scale(25,1,25) = 250×250
        ground.transform.position   = Vector3.zero;
        ground.transform.localScale = new Vector3(k_Half / 5f, 1f, k_Half / 5f);
        GameObjectUtility.SetStaticEditorFlags(
            ground,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
    }

    // ── 경계 벽 ──────────────────────────────────────────────────

    static void SetupWalls()
    {
        var parent   = new GameObject("Walls");
        float yCenter = k_WallHeight * 0.5f;
        float side   = k_Half * 2f;   // 250

        CreateWall(parent.transform, "Wall_West",
            new Vector3(-(k_Half + k_WallThick * 0.5f), yCenter, 0f),
            new Vector3(k_WallThick, k_WallHeight, side + k_WallThick * 2f));

        CreateWall(parent.transform, "Wall_East",
            new Vector3( (k_Half + k_WallThick * 0.5f), yCenter, 0f),
            new Vector3(k_WallThick, k_WallHeight, side + k_WallThick * 2f));

        CreateWall(parent.transform, "Wall_South",
            new Vector3(0f, yCenter, -(k_Half + k_WallThick * 0.5f)),
            new Vector3(side + k_WallThick * 2f, k_WallHeight, k_WallThick));

        CreateWall(parent.transform, "Wall_North",
            new Vector3(0f, yCenter,  (k_Half + k_WallThick * 0.5f)),
            new Vector3(side + k_WallThick * 2f, k_WallHeight, k_WallThick));
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

        var so = new SerializedObject(tc);
        so.FindProperty("height").floatValue          = k_CamHeight;
        so.FindProperty("strictTopDown").boolValue    = true;
        so.FindProperty("positionDamping").floatValue = 0.15f;
        so.ApplyModifiedProperties();

        camGO.transform.position = new Vector3(0f, k_CamHeight, -60f);
    }

    // ── BoxSpawner × 4 (중앙 십자) ───────────────────────────────

    static BoxSpawner[] SetupSpawners()
    {
        var parent = new GameObject("BoxSpawners");

        // 중앙 근처 4방향: NW / NE / SW / SE
        (Vector3 pos, string label)[] defs =
        {
            (new Vector3(-k_SpawnerOffset, 0f,  k_SpawnerOffset), "Spawner_NW"),
            (new Vector3( k_SpawnerOffset, 0f,  k_SpawnerOffset), "Spawner_NE"),
            (new Vector3(-k_SpawnerOffset, 0f, -k_SpawnerOffset), "Spawner_SW"),
            (new Vector3( k_SpawnerOffset, 0f, -k_SpawnerOffset), "Spawner_SE"),
        };

        var result = new BoxSpawner[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var go = new GameObject(defs[i].label);
            go.transform.SetParent(parent.transform);
            go.transform.position = defs[i].pos;

            var spawner = go.AddComponent<BoxSpawner>();

            // BulgeMesh: 자식 구체 (시각 연출용, 초기 scale=0)
            var bulgeGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulgeGO.name = "BulgeMesh";
            bulgeGO.transform.SetParent(go.transform);
            bulgeGO.transform.localPosition = Vector3.zero;
            bulgeGO.transform.localScale    = Vector3.zero;

            // Collider 제거 (BulgeMesh는 시각 전용)
            Object.DestroyImmediate(bulgeGO.GetComponent<SphereCollider>());

            var so = new SerializedObject(spawner);
            so.FindProperty("bulgeMesh").objectReferenceValue = bulgeGO.transform;
            so.ApplyModifiedProperties();

            result[i] = spawner;
        }

        return result;
    }

    // ── BoxColorTrigger × 4 (십자, 반경 40) ──────────────────────

    static BoxColorTrigger[] SetupColorTriggers()
    {
        var parent = new GameObject("BoxColorTriggers");

        (Vector3 pos, string label)[] defs =
        {
            (new Vector3( 0f,            0.1f,  k_TriggerRadius), "Trigger_North"),
            (new Vector3( 0f,            0.1f, -k_TriggerRadius), "Trigger_South"),
            (new Vector3(-k_TriggerRadius, 0.1f, 0f),             "Trigger_West"),
            (new Vector3( k_TriggerRadius, 0.1f, 0f),             "Trigger_East"),
        };

        var result = new BoxColorTrigger[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var go = new GameObject(defs[i].label);
            go.transform.SetParent(parent.transform);
            go.transform.position = defs[i].pos;

            // 바닥 표시용 큐브 (시각, 콜라이더는 BoxColorTrigger용 BoxCollider로 별도)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "TriggerVisual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale    = new Vector3(8f, 0.1f, 8f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>()); // 충돌은 부모에서 처리

            // BoxColorTrigger 전용 BoxCollider 추가
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = false; // BoxColorTrigger는 OverlapBox 폴링 방식 사용
            col.size      = new Vector3(8f, 2f, 8f);
            col.center    = new Vector3(0f, 1f, 0f);

            var trigger = go.AddComponent<BoxColorTrigger>();

            // Common 박스 감지
            var so = new SerializedObject(trigger);
            so.FindProperty("requiredColor").enumValueIndex =
                (int)PushableBox.BoxOwnerColor.Common;
            so.FindProperty("inactiveColor").colorValue = new Color(0.4f, 0.4f, 0.4f);
            so.FindProperty("activeColor").colorValue   = Color.yellow;
            so.ApplyModifiedProperties();

            result[i] = trigger;
        }

        return result;
    }

    // ── 초기 PushableBox × 4 (중앙 클러스터) ──────────────────────

    static void SetupInitialBoxes()
    {
        var parent = new GameObject("InitialBoxes");

        Vector3[] positions =
        {
            new Vector3(-k_BoxCluster, 0.5f,  k_BoxCluster),
            new Vector3( k_BoxCluster, 0.5f,  k_BoxCluster),
            new Vector3(-k_BoxCluster, 0.5f, -k_BoxCluster),
            new Vector3( k_BoxCluster, 0.5f, -k_BoxCluster),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var go = PlacePrefab("PushableBox", positions[i], $"Box_Initial_{i + 1}");
            go.transform.SetParent(parent.transform);

            // Common 박스로 설정
            var box = go.GetComponent<PushableBox>();
            if (box != null)
            {
                var so = new SerializedObject(box);
                so.FindProperty("ownerColor").enumValueIndex =
                    (int)PushableBox.BoxOwnerColor.Common;
                so.ApplyModifiedProperties();
            }
        }
    }

    // ── BoxCountZone × 4 (모퉁이) ────────────────────────────────

    static void SetupBoxCountZones()
    {
        var parent  = new GameObject("BoxCountZones");
        float inset = k_Half - k_ZoneInset;   // 125 - 30 = 95

        (Vector3 pos, string label)[] defs =
        {
            (new Vector3(-inset, 0.1f, -inset), "CountZone_SW"),
            (new Vector3( inset, 0.1f, -inset), "CountZone_SE"),
            (new Vector3(-inset, 0.1f,  inset), "CountZone_NW"),
            (new Vector3( inset, 0.1f,  inset), "CountZone_NE"),
        };

        foreach (var (pos, label) in defs)
        {
            var go = PlacePrefab("BoxCountZone", pos, label);
            go.transform.SetParent(parent.transform);

            var zone = go.GetComponent<BoxCountZone>();
            if (zone != null)
            {
                var so = new SerializedObject(zone);
                so.FindProperty("requiredColor").enumValueIndex =
                    (int)PushableBox.BoxOwnerColor.Common;
                so.FindProperty("requiredCount").intValue = 1;
                so.ApplyModifiedProperties();
            }
        }
    }

    // ── BoxSpawnGate ─────────────────────────────────────────────

    static void SetupBoxSpawnGate(BoxColorTrigger[] triggers, BoxSpawner[] spawners)
    {
        var go   = new GameObject("BoxSpawnGate");
        var gate = go.AddComponent<BoxSpawnGate>();

        var so = new SerializedObject(gate);

        // requiredTriggers 배열 설정
        var triggersProp = so.FindProperty("requiredTriggers");
        triggersProp.arraySize = triggers.Length;
        for (int i = 0; i < triggers.Length; i++)
            triggersProp.GetArrayElementAtIndex(i).objectReferenceValue = triggers[i];

        // spawners 배열 설정
        var spawnersProp = so.FindProperty("spawners");
        spawnersProp.arraySize = spawners.Length;
        for (int i = 0; i < spawners.Length; i++)
            spawnersProp.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];

        so.FindProperty("cooldown").floatValue = 15f;
        so.ApplyModifiedProperties();
    }

    // ── 프리팹 배치 헬퍼 ─────────────────────────────────────────

    static GameObject PlacePrefab(string prefabName, Vector3 position, string overrideName = null)
    {
        var path   = k_PrefabRoot + prefabName + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning($"[Map02Builder] 프리팹을 찾을 수 없음: {path}");
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
