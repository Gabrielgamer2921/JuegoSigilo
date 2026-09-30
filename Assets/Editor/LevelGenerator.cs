using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Genera el nivel de prueba desde el menú: Sigilo → Generar nivel (Etapa 1).
/// Este script SOLO existe en el editor: no forma parte del juego y nunca se ejecuta al dar Play.
/// Todo lo que crea queda bajo un objeto llamado "NivelGenerado" (borrarlo borra el nivel).
///
/// Mapa (vista desde arriba, norte = +Z):
///
///   z= 20  ┌──────────────────────────────┐
///          │                        [META]│
///   z=  9  │██████████████████████████    │   ← muro con salida al este
///          │   ▒        ▒                 │
///   z=  0  │ E ←──── patrulla ────→       │   ← patio del enemigo
///          │      ▒        ▒              │
///   z= -9  │██████████████      ██████████│   ← muro con entrada al centro
///          │ [INICIO]                     │
///   z=-20  └──────────────────────────────┘
///            ▒ = cajas para cubrirse
/// </summary>
public static class LevelGenerator
{
    private const string RootName = "NivelGenerado";
    private const string MaterialFolder = "Assets/Materials/Sigilo";
    private const float WallHeight = 3f;

    // ================================================================
    //  MENÚ
    // ================================================================

    [MenuItem("Sigilo/Generar nivel (Etapa 1)")]
    public static void GenerateStage1()
    {
        // Si ya existe un nivel generado, preguntamos antes de reemplazarlo
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Nivel ya generado",
                "Ya existe un objeto 'NivelGenerado' en la escena. ¿Querés reemplazarlo?",
                "Reemplazar", "Cancelar");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        // ---- Materiales (se guardan como assets en Assets/Materials/Sigilo) ----
        Material matFloor = GetMaterial("Piso", new Color(0.22f, 0.24f, 0.27f));
        Material matWall = GetMaterial("Pared", new Color(0.45f, 0.47f, 0.50f));
        Material matCrate = GetMaterial("Caja", new Color(0.62f, 0.45f, 0.25f));
        Material matPlayer = GetMaterial("Jugador", new Color(0.20f, 0.45f, 1.00f));
        Material matEnemy = GetMaterial("Enemigo", new Color(0.85f, 0.15f, 0.15f));
        Material matGoal = GetMaterial("Meta", new Color(0.20f, 0.85f, 0.35f));
        Material matStart = GetMaterial("Inicio", new Color(0.40f, 0.85f, 1.00f));
        Material matNose = GetMaterial("Frente", new Color(0.95f, 0.95f, 0.95f));

        // ---- Estructura de carpetas en la Hierarchy ----
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Generar nivel");

        Transform geometry = CreateGroup("Geometria", root.transform);
        Transform markers = CreateGroup("Marcadores", root.transform);
        Transform enemies = CreateGroup("Enemigos", root.transform);
        Transform waypoints = CreateGroup("Waypoints_Enemigo1", root.transform);

        // ---- Geometría del nivel ----
        Box("Piso", geometry, new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), matFloor);

        // Bordes del mapa
        Wall("Borde_Norte", geometry, 0f, 20.5f, 42f, 1f, matWall);
        Wall("Borde_Sur", geometry, 0f, -20.5f, 42f, 1f, matWall);
        Wall("Borde_Este", geometry, 20.5f, 0f, 1f, 42f, matWall);
        Wall("Borde_Oeste", geometry, -20.5f, 0f, 1f, 42f, matWall);

        // Muro sur: deja una entrada de 12 m en el centro (x entre -6 y 6)
        Wall("Muro_Sur_Izq", geometry, -13f, -9f, 14f, 1f, matWall);
        Wall("Muro_Sur_Der", geometry, 13f, -9f, 14f, 1f, matWall);

        // Muro norte: deja una salida al este (x entre 10 y 20)
        Wall("Muro_Norte", geometry, -5f, 9f, 30f, 1f, matWall);

        // Cajas para cubrirse dentro del patio
        Box("Caja_1", geometry, new Vector3(-3f, 1f, -5f), new Vector3(2f, 2f, 2f), matCrate);
        Box("Caja_2", geometry, new Vector3(5f, 1f, -5f), new Vector3(2f, 2f, 2f), matCrate);
        Box("Caja_3", geometry, new Vector3(6f, 1f, 5f), new Vector3(2f, 2f, 2f), matCrate);
        Box("Caja_4", geometry, new Vector3(13f, 1f, 4f), new Vector3(2f, 2f, 2f), matCrate);

        // NavMeshSurface: solo hornea lo que está dentro de "Geometria"
        // (así el Player y los enemigos NO quedan "pintados" en el mapa de navegación)
        NavMeshSurface surface = geometry.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;

        // ---- Marcadores: inicio y meta ----
        Vector3 startPos = new Vector3(-12f, 0f, -16f);

        GameObject startPad = Primitive(PrimitiveType.Cylinder, "PuntoInicio", markers,
            startPos + new Vector3(0f, 0.05f, 0f), new Vector3(2f, 0.05f, 2f), matStart);
        Object.DestroyImmediate(startPad.GetComponent<Collider>()); // Solo decorativo

        GameObject goal = Box("Meta", markers, new Vector3(15f, 1f, 16f), new Vector3(3f, 2f, 3f), matGoal);
        goal.GetComponent<Collider>().isTrigger = true;
        goal.AddComponent<Goal>();

        // ---- Waypoints del enemigo ----
        Transform wp1 = CreateEmpty("WP_1", waypoints, new Vector3(-12f, 0f, 0f));
        Transform wp2 = CreateEmpty("WP_2", waypoints, new Vector3(12f, 0f, 0f));

        // ---- Enemigo ----
        GameObject enemy = Primitive(PrimitiveType.Capsule, "Enemigo_1", enemies,
            new Vector3(-12f, 1f, 0f), Vector3.one, matEnemy);
        enemy.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Mira hacia el este

        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.baseOffset = 1f; // La cápsula tiene el pivote en el centro

        enemy.AddComponent<EnemyVision>();
        EnemyAI ai = enemy.AddComponent<EnemyAI>();
        SetObjectArray(ai, "waypoints", new Object[] { wp1, wp2 });
        enemy.AddComponent<ConeVisualizer>();
        AddNose(enemy.transform, matNose);

        // ---- Jugador, cámara y GameManager (se reutilizan si ya existen) ----
        GameObject player = EnsurePlayer(startPos + Vector3.up, matPlayer, matNose);
        EnsureCamera(player.transform);
        EnsureGameManager();

        // ---- Cierre ----
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = geometry.gameObject;

        Debug.Log("Nivel generado. ÚLTIMO PASO: con 'Geometria' seleccionado, " +
                  "apretá el botón Bake del componente Nav Mesh Surface.");
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

    // ================================================================
    //  CREACIÓN DE OBJETOS
    // ================================================================

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

    // Pared: se define por su centro (x, z) y su tamaño horizontal (sizeX, sizeZ)
    private static GameObject Wall(string name, Transform parent, float x, float z,
                                   float sizeX, float sizeZ, Material material)
    {
        return Box(name, parent,
                   new Vector3(x, WallHeight * 0.5f, z),
                   new Vector3(sizeX, WallHeight, sizeZ),
                   material);
    }

    // "Nariz" para distinguir hacia dónde mira un personaje
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

    // ================================================================
    //  JUGADOR, CÁMARA Y GAMEMANAGER
    // ================================================================

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

        // Lo llevamos al punto de inicio, mirando al norte
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

    private static void EnsureGameManager()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null) return;

        GameObject gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        Undo.RegisterCreatedObjectUndo(gm, "Crear GameManager");
    }

    // ================================================================
    //  ASIGNAR CAMPOS PRIVADOS ([SerializeField]) DESDE CÓDIGO DE EDITOR
    // ================================================================

    private static void SetObjectField(Object component, string fieldName, Object value)
    {
        SerializedObject so = new SerializedObject(component);
        so.FindProperty(fieldName).objectReferenceValue = value;
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

    // ================================================================
    //  MATERIALES
    // ================================================================

    private static Material GetMaterial(string materialName, Color color)
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Sigilo");

        string path = $"{MaterialFolder}/{materialName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            // Copiamos el material por defecto de un cubo: así usa el shader correcto
            // para tu proyecto (URP o Built-in) sin que tengamos que adivinarlo.
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material defaultMat = temp.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(temp);

            mat = new Material(defaultMat);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); // URP
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);         // Built-in
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{folderName}"))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
