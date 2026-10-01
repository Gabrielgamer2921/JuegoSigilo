using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

public static class LevelGenerator
{
    private const string RootName = "NivelGenerado";
    private const string MaterialFolder = "Assets/Materials/Sigilo";
    private const float WallHeight = 3f;

    private class Materials
    {
        public Material floor, wall, crate, player, enemy, goal, start, nose, hide;
    }

    private class Level
    {
        public Transform root, geometry, markers, hiding, enemies;
        public Materials mats;
    }

    [MenuItem("Sigilo/Generar nivel (Etapa 1)")]
    public static void GenerateStage1()
    {
        Level level = BeginLevel();
        if (level == null) return;

        Materials m = level.mats;
        Transform g = level.geometry;
        BuildFloorAndBorders(level);

        Wall("Muro_Sur_Izq", g, -13f, -9f, 14f, 1f, m.wall);
        Wall("Muro_Sur_Der", g, 13f, -9f, 14f, 1f, m.wall);
        Wall("Muro_Norte", g, -5f, 9f, 30f, 1f, m.wall);

        Crate("Caja_1", g, -3f, -5f, m);
        Crate("Caja_2", g, 5f, -5f, m);
        Crate("Caja_3", g, 6f, 5f, m);
        Crate("Caja_4", g, 13f, 4f, m);

        CreateHidingSpot("Escondite_1", level, -10f, -5f);
        CreateHidingSpot("Escondite_2", level, 10f, 6.5f);

        Vector3 startPos = new Vector3(-12f, 0f, -16f);
        CreateStart(level, startPos);
        CreateGoal(level, new Vector3(15f, 1f, 16f));

        Transform[] route = CreateRoute(level, "Enemigo_1",
            new Vector3(-12f, 0f, 0f), new Vector3(12f, 0f, 0f));
        CreateEnemy(level, "Enemigo_1", new Vector3(-12f, 1f, 0f), 90f, route, 10f, 90f);

        FinishLevel(level, startPos);
    }

    [MenuItem("Sigilo/Generar nivel (Etapa 2)")]
    public static void GenerateStage2()
    {
        Level level = BeginLevel();
        if (level == null) return;

        Materials m = level.mats;
        Transform g = level.geometry;
        BuildFloorAndBorders(level);

        Wall("Muro_Sur_A", g, -8f, -9f, 8f, 1f, m.wall);
        Wall("Muro_Sur_B", g, 8f, -9f, 8f, 1f, m.wall);
        Wall("Muro_Norte_A", g, -8f, 9f, 8f, 1f, m.wall);
        Wall("Muro_Norte_B", g, 8f, 9f, 8f, 1f, m.wall);
        Wall("Divisor_Oeste", g, -8f, 0f, 1f, 19f, m.wall);
        Wall("Divisor_Este", g, 8f, 0f, 1f, 19f, m.wall);

        Wall("Muro_Meta_Sur", g, 8f, 11.5f, 1f, 5f, m.wall);
        Wall("Muro_Meta_Norte", g, 8f, 19f, 1f, 2f, m.wall);

        Crate("Caja_Inicio_1", g, -8f, -13f, m);
        Crate("Caja_Inicio_2", g, 5f, -14f, m);
        Crate("Caja_Inicio_3", g, 14f, -13f, m);

        Crate("Caja_Oeste_1", g, -13f, -4f, m);
        Crate("Caja_Oeste_2", g, -15f, 3f, m);

        Crate("Caja_Centro_1", g, -4f, -2f, m);
        Crate("Caja_Centro_2", g, 4f, 2f, m);
        Crate("Caja_Centro_3", g, -6f, 5f, m);
        Crate("Caja_Centro_4", g, 6f, -5f, m);

        Box("Isla_Este", g, new Vector3(14f, 1f, 0f), new Vector3(3f, 2f, 3f), m.crate);

        Crate("Caja_Norte_1", g, -13f, 14f, m);
        Crate("Caja_Norte_2", g, -3f, 13f, m);
        Crate("Caja_Norte_3", g, 3f, 17f, m);

        Crate("Caja_Meta_1", g, 11f, 12f, m);
        Crate("Caja_Meta_2", g, 15f, 12.5f, m);

        CreateHidingSpot("Escondite_Oeste_1", level, -17f, -1f);
        CreateHidingSpot("Escondite_Oeste_2", level, -12f, 6.5f);
        CreateHidingSpot("Escondite_Este", level, 18.3f, 0f);
        CreateHidingSpot("Escondite_Norte", level, -7f, 18f);
        CreateHidingSpot("Escondite_Meta", level, 10.2f, 18.3f);

        Vector3 startPos = new Vector3(-16f, 0f, -16f);
        CreateStart(level, startPos);
        CreateGoal(level, new Vector3(17.5f, 1f, 17.5f));

        Transform[] routeCenter = CreateRoute(level, "Central",
            new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 6f));
        CreateEnemy(level, "Enemigo_Central", new Vector3(0f, 1f, -6f), 0f, routeCenter, 10f, 90f);

        Transform[] routeEast = CreateRoute(level, "Este",
            new Vector3(11f, 0f, -5f), new Vector3(17f, 0f, -5f),
            new Vector3(17f, 0f, 5f), new Vector3(11f, 0f, 5f));
        CreateEnemy(level, "Enemigo_Este", new Vector3(11f, 1f, -5f), 90f, routeEast, 10f, 90f);

        Transform[] routeGuard = CreateGuardRoute(level, "Guardia",
            new Vector3(13f, 0f, 16f), 270f, 225f, 180f, 225f);
        GameObject guard = CreateEnemy(level, "Guardia_Meta", new Vector3(13f, 1f, 16f), 270f, routeGuard, 9f, 70f);
        EnemyAI guardAi = guard.GetComponent<EnemyAI>();
        SetBoolField(guardAi, "faceWaypointDirection", true);
        SetFloatField(guardAi, "waitTime", 2.5f);

        FinishLevel(level, startPos);
    }

    [MenuItem("Sigilo/Borrar nivel generado")]
    public static void DeleteGenerated()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
        {
            EditorUtility.DisplayDialog("Nada que borrar", "No hay ningún 'NivelGenerado' en la escena.", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Borrar nivel",
                "Se borra 'NivelGenerado' con todo lo que contiene.\n" +
                "El Player, la cámara y el GameManager no se tocan.",
                "Borrar", "Cancelar"))
        {
            Undo.DestroyObjectImmediate(existing);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    private static Level BeginLevel()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Nivel ya generado",
                "Ya existe un objeto 'NivelGenerado' en la escena. ¿Querés reemplazarlo?",
                "Reemplazar", "Cancelar");
            if (!replace) return null;
            Undo.DestroyObjectImmediate(existing);
        }

        Level level = new Level { mats = CreateMaterials() };

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Generar nivel");

        level.root = root.transform;
        level.geometry = CreateGroup("Geometria", level.root);
        level.markers = CreateGroup("Marcadores", level.root);
        level.hiding = CreateGroup("Escondites", level.root);
        level.enemies = CreateGroup("Enemigos", level.root);

        NavMeshSurface surface = level.geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;

        return level;
    }

    private static void BuildFloorAndBorders(Level level)
    {
        Transform g = level.geometry;
        Material wall = level.mats.wall;

        Box("Piso", g, new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), level.mats.floor);

        Wall("Borde_Norte", g, 0f, 20.5f, 42f, 1f, wall);
        Wall("Borde_Sur", g, 0f, -20.5f, 42f, 1f, wall);
        Wall("Borde_Este", g, 20.5f, 0f, 1f, 42f, wall);
        Wall("Borde_Oeste", g, -20.5f, 0f, 1f, 42f, wall);
    }

    private static void FinishLevel(Level level, Vector3 startPos)
    {
        GameObject player = EnsurePlayer(startPos + Vector3.up, level.mats.player, level.mats.nose);
        EnsureCamera(player.transform);
        EnsureGameManager();
        EnsureAudioManager();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = level.geometry.gameObject;

        Debug.Log("Nivel generado. ÚLTIMO PASO: con 'Geometria' seleccionado, " +
                  "apretá el botón Bake del componente Nav Mesh Surface.");
    }

    private static void CreateStart(Level level, Vector3 position)
    {
        GameObject pad = Primitive(PrimitiveType.Cylinder, "PuntoInicio", level.markers,
            position + new Vector3(0f, 0.05f, 0f), new Vector3(2f, 0.05f, 2f), level.mats.start);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
    }

    private static void CreateGoal(Level level, Vector3 position)
    {
        GameObject goal = Box("Meta", level.markers, position, new Vector3(3f, 2f, 3f), level.mats.goal);
        goal.GetComponent<Collider>().isTrigger = true;
        goal.AddComponent<Goal>();
    }

    private static void CreateHidingSpot(string name, Level level, float x, float z)
    {
        GameObject spot = Box(name, level.hiding, new Vector3(x, 1.1f, z),
                              new Vector3(3f, 2.2f, 3f), level.mats.hide);
        spot.GetComponent<Collider>().isTrigger = true;
        spot.AddComponent<HidingSpot>();
    }

    private static Transform[] CreateRoute(Level level, string enemyName, params Vector3[] points)
    {
        Transform group = CreateGroup("Waypoints_" + enemyName, level.root);
        Transform[] route = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
            route[i] = CreateEmpty("WP_" + (i + 1), group, points[i]);
        return route;
    }

    private static Transform[] CreateGuardRoute(Level level, string enemyName, Vector3 position, params float[] yaws)
    {
        Transform group = CreateGroup("Waypoints_" + enemyName, level.root);
        Transform[] route = new Transform[yaws.Length];
        for (int i = 0; i < yaws.Length; i++)
        {
            route[i] = CreateEmpty("Mirada_" + (i + 1), group, position);
            route[i].rotation = Quaternion.Euler(0f, yaws[i], 0f);
        }
        return route;
    }

    private static GameObject CreateEnemy(Level level, string name, Vector3 position, float yaw,
                                          Transform[] route, float viewDistance, float viewAngle)
    {
        GameObject enemy = Primitive(PrimitiveType.Capsule, name, level.enemies, position,
                                     Vector3.one, level.mats.enemy);
        enemy.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f;

        EnemyVision vision = enemy.AddComponent<EnemyVision>();
        SetFloatField(vision, "viewDistance", viewDistance);
        SetFloatField(vision, "viewAngle", viewAngle);

        EnemyAI ai = enemy.AddComponent<EnemyAI>();
        SetObjectArray(ai, "waypoints", route);

        enemy.AddComponent<EnemyHearing>();
        enemy.AddComponent<ConeVisualizer>();
        AddNose(enemy.transform, level.mats.nose);
        return enemy;
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform CreateEmpty(string name, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go.transform;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent,
                                        Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        return Primitive(PrimitiveType.Cube, name, parent, position, scale, material);
    }

    private static GameObject Crate(string name, Transform parent, float x, float z, Materials m)
    {
        return Box(name, parent, new Vector3(x, 1f, z), new Vector3(2f, 2f, 2f), m.crate);
    }

    private static GameObject Wall(string name, Transform parent, float x, float z,
                                   float sizeX, float sizeZ, Material material)
    {
        return Box(name, parent,
                   new Vector3(x, WallHeight * 0.5f, z),
                   new Vector3(sizeX, WallHeight, sizeZ),
                   material);
    }

    private static void AddNose(Transform character, Material material)
    {
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Frente";
        nose.transform.SetParent(character, false);
        nose.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
        nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.5f);
        nose.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(nose.GetComponent<Collider>());
    }

    private static GameObject EnsurePlayer(Vector3 position, Material bodyMat, Material noseMat)
    {
        GameObject player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            player.GetComponent<Renderer>().sharedMaterial = bodyMat;

            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            player.AddComponent<PlayerMovement>();
            AddNose(player.transform, noseMat);
            Undo.RegisterCreatedObjectUndo(player, "Crear Player");
        }

        if (player.GetComponent<PlayerHiding>() == null)
            player.AddComponent<PlayerHiding>();
        if (player.GetComponent<PlayerStealth>() == null)
            player.AddComponent<PlayerStealth>();
        if (player.GetComponent<PlayerWhistle>() == null)
            player.AddComponent<PlayerWhistle>();

        player.transform.SetPositionAndRotation(position, Quaternion.identity);
        return player;
    }

    private static void EnsureCamera(Transform playerTransform)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            GameObject camObject = new GameObject("Main Camera");
            camObject.tag = "MainCamera";
            cam = camObject.AddComponent<Camera>();
            camObject.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(camObject, "Crear cámara");
        }

        ThirdPersonCamera follow = cam.GetComponent<ThirdPersonCamera>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<ThirdPersonCamera>();

        SetObjectField(follow, "target", playerTransform);
    }

    private static void EnsureAudioManager()
    {
        if (Object.FindFirstObjectByType<AudioManager>() != null) return;

        GameObject audio = new GameObject("AudioManager");
        audio.AddComponent<AudioManager>();
        Undo.RegisterCreatedObjectUndo(audio, "Crear AudioManager");
    }

    private static void EnsureGameManager()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null) return;

        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        Undo.RegisterCreatedObjectUndo(gm, "Crear GameManager");
    }

    private static void SetObjectField(Object component, string fieldName, Object value)
    {
        SerializedObject so = new SerializedObject(component);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetFloatField(Object component, string fieldName, float value)
    {
        SerializedObject so = new SerializedObject(component);
        so.FindProperty(fieldName).floatValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetBoolField(Object component, string fieldName, bool value)
    {
        SerializedObject so = new SerializedObject(component);
        so.FindProperty(fieldName).boolValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetObjectArray(Object component, string fieldName, Object[] values)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty array = so.FindProperty(fieldName);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }

    private static Materials CreateMaterials()
    {
        return new Materials
        {
            floor  = GetMaterial("Piso",    new Color(0.22f, 0.24f, 0.27f)),
            wall   = GetMaterial("Pared",   new Color(0.45f, 0.47f, 0.50f)),
            crate  = GetMaterial("Caja",    new Color(0.62f, 0.45f, 0.25f)),
            player = GetMaterial("Jugador", new Color(0.20f, 0.45f, 1.00f)),
            enemy  = GetMaterial("Enemigo", new Color(0.85f, 0.15f, 0.15f)),
            goal   = GetMaterial("Meta",    new Color(0.20f, 0.85f, 0.35f)),
            start  = GetMaterial("Inicio",  new Color(0.40f, 0.85f, 1.00f)),
            nose   = GetMaterial("Frente",  new Color(0.95f, 0.95f, 0.95f)),
            hide   = GetTransparentMaterial("Escondite", new Color(0.15f, 0.9f, 0.35f, 0.35f))
        };
    }

    private static Material GetMaterial(string materialName, Color color)
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Sigilo");

        string path = $"{MaterialFolder}/{materialName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material defaultMat = temp.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(temp);

            mat = new Material(defaultMat);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material GetTransparentMaterial(string materialName, Color color)
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Sigilo");

        string path = $"{MaterialFolder}/{materialName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{folderName}"))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
