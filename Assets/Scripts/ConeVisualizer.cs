using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dibuja el cono de visión del enemigo sobre el piso.
///
///  - CONO BASE: cambia de color según el estado (patrulla / alerta / investiga / persigue).
///  - RELLENO:   crece desde el enemigo hacia afuera según su medidor de sospecha.
///               Cuando llega al borde del cono, el enemigo está seguro de haberte visto.
///
/// La forma del cono se recorta contra paredes y cajas con Raycasts, igual que la visión real,
/// así lo que ves en pantalla es exactamente lo que el enemigo puede ver.
///
/// Este script solo muestra información: no decide nada ni modifica al enemigo.
/// </summary>
[RequireComponent(typeof(EnemyVision), typeof(EnemyAI), typeof(NavMeshAgent))]
public class ConeVisualizer : MonoBehaviour
{
    [Header("Malla")]
    [SerializeField, Range(10, 120)] private int rayCount = 50;   // Más rayos = borde más suave (y más costoso)
    [SerializeField] private float floorOffset = 0.03f;           // Altura sobre el piso, para que no parpadee

    [Header("Colores del cono según el estado")]
    [SerializeField] private Color patrolColor = new Color(0.2f, 1f, 0.45f);
    [SerializeField] private Color alertColor = new Color(1f, 0.9f, 0.1f);
    [SerializeField] private Color investigateColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color chaseColor = new Color(1f, 0.15f, 0.15f);

    [Header("Transparencia y suavizado")]
    [SerializeField, Range(0f, 1f)] private float coneAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.45f;
    [SerializeField] private float colorLerpSpeed = 8f;           // Qué tan rápido cambia de color

    private EnemyVision vision;
    private EnemyAI ai;
    private NavMeshAgent agent;

    private Mesh coneMesh;
    private Mesh fillMesh;
    private Material coneMaterial;
    private Material fillMaterial;
    private MeshRenderer fillRenderer;

    private Vector3[] coneVertices;
    private Vector3[] fillVertices;
    private Vector3[] rayDirections;
    private float[] rayDistances;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];
    private Color currentColor;

    private void Awake()
    {
        vision = GetComponent<EnemyVision>();
        ai = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();

        Shader shader = Shader.Find("Sprites/Default"); // Transparente y sin luces: funciona en URP y Built-in
        if (shader == null)
        {
            Debug.LogError("ConeVisualizer: no se encontró el shader 'Sprites/Default'.", this);
            enabled = false;
            return;
        }

        BuildMeshes();

        coneMaterial = new Material(shader) { renderQueue = 3000 };
        fillMaterial = new Material(shader) { renderQueue = 3001 }; // Se dibuja encima del cono base

        CreateLayer("ConoVision", coneMesh, coneMaterial);
        fillRenderer = CreateLayer("ConoRelleno", fillMesh, fillMaterial);

        currentColor = patrolColor;
    }

    // Se ejecuta después de que el enemigo ya se movió y giró en este frame
    private void LateUpdate()
    {
        ComputeRays();
        UpdateGeometry();
        UpdateColors();
    }

    // ---------------- CONSTRUCCIÓN ----------------

    private void BuildMeshes()
    {
        rayDirections = new Vector3[rayCount + 1];
        rayDistances = new float[rayCount + 1];

        // 1 vértice en el enemigo + 1 por cada rayo. Los triángulos forman un "abanico".
        int vertexCount = rayCount + 2;
        coneVertices = new Vector3[vertexCount];
        fillVertices = new Vector3[vertexCount];

        int[] triangles = new int[rayCount * 3];
        for (int i = 0; i < rayCount; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        coneMesh = CreateMesh("ConoMesh", vertexCount, triangles);
        fillMesh = CreateMesh("RellenoMesh", vertexCount, triangles);
    }

    private Mesh CreateMesh(string meshName, int vertexCount, int[] triangles)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.MarkDynamic(); // Avisa a Unity que la vamos a modificar en cada frame
        mesh.vertices = new Vector3[vertexCount];
        mesh.triangles = triangles;
        return mesh;
    }

    // Crea un objeto hijo con la malla, para poder dibujarla
    private MeshRenderer CreateLayer(string layerName, Mesh mesh, Material material)
    {
        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);
        layer.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = layer.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return meshRenderer;
    }

    // ---------------- CÁLCULO DEL CONO ----------------

    // Tira rayos en abanico (mismo origen, ángulo y alcance que EnemyVision)
    private void ComputeRays()
    {
        Vector3 eye = transform.position + Vector3.up * vision.EyeHeight;
        float half = vision.ViewAngle * 0.5f;
        float yaw = transform.eulerAngles.y;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)rayCount);
            Vector3 direction = Quaternion.Euler(0f, yaw + angle, 0f) * Vector3.forward;

            rayDirections[i] = direction;
            rayDistances[i] = GetClearDistance(eye, direction);
        }
    }

    // Hasta dónde llega el rayo antes de chocar con algo que tape la vista
    private float GetClearDistance(Vector3 origin, Vector3 direction)
    {
        float maxDistance = vision.ViewDistance;
        int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, maxDistance,
                                            ~0, QueryTriggerInteraction.Ignore);

        Transform player = vision.Player;
        float nearest = maxDistance;

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;

            if (hitCollider.transform.IsChildOf(transform)) continue;               // El propio enemigo
            if (player != null && hitCollider.transform.IsChildOf(player)) continue; // El jugador no recorta el cono
            if (hitCollider.GetComponentInParent<EnemyAI>() != null) continue;       // Otros enemigos tampoco

            nearest = Mathf.Min(nearest, hitBuffer[i].distance);
        }

        return nearest;
    }

    private void UpdateGeometry()
    {
        float suspicion = ai.Suspicion;
        float floorY = transform.position.y - agent.baseOffset + floorOffset;

        Vector3 coneOrigin = new Vector3(transform.position.x, floorY, transform.position.z);
        Vector3 fillOrigin = coneOrigin + Vector3.up * 0.01f;

        coneVertices[0] = transform.InverseTransformPoint(coneOrigin);
        fillVertices[0] = transform.InverseTransformPoint(fillOrigin);

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 direction = rayDirections[i];
            float distance = rayDistances[i];

            // Los vértices se guardan en coordenadas locales del enemigo
            coneVertices[i + 1] = transform.InverseTransformPoint(coneOrigin + direction * distance);
            fillVertices[i + 1] = transform.InverseTransformPoint(fillOrigin + direction * (distance * suspicion));
        }

        coneMesh.vertices = coneVertices;
        coneMesh.RecalculateBounds();

        fillMesh.vertices = fillVertices;
        fillMesh.RecalculateBounds();
    }

    // ---------------- COLORES ----------------

    private void UpdateColors()
    {
        // Cono base: color del estado, con transición suave
        currentColor = Color.Lerp(currentColor, GetStateColor(),
                                  1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime));

        Color coneColor = currentColor;
        coneColor.a = coneAlpha;
        coneMaterial.color = coneColor;

        // Relleno: de amarillo a rojo a medida que sube la sospecha
        Color fillColor = Color.Lerp(alertColor, chaseColor, ai.Suspicion);
        fillColor.a = fillAlpha;
        fillMaterial.color = fillColor;

        fillRenderer.enabled = ai.Suspicion > 0.01f;
    }

    private Color GetStateColor()
    {
        switch (ai.CurrentState)
        {
            case EnemyState.Alert:       return alertColor;
            case EnemyState.Investigate: return investigateColor;
            case EnemyState.Chase:       return chaseColor;
            default:                     return patrolColor;
        }
    }

    // Limpieza: las mallas y materiales creados por código no se borran solos
    private void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        if (fillMesh != null) Destroy(fillMesh);
        if (coneMaterial != null) Destroy(coneMaterial);
        if (fillMaterial != null) Destroy(fillMaterial);
    }
}
