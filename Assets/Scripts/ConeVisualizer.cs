using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyVision), typeof(EnemyAI), typeof(NavMeshAgent))]
public class ConeVisualizer : MonoBehaviour
{
    [Header("Malla")]
    [SerializeField, Range(10, 120)] private int rayCount = 50;
    [SerializeField] private float floorOffset = 0.03f;

    [Header("Colores del cono según el estado")]
    [SerializeField] private Color patrolColor = new Color(0.2f, 1f, 0.45f);
    [SerializeField] private Color alertColor = new Color(1f, 0.9f, 0.1f);
    [SerializeField] private Color investigateColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color chaseColor = new Color(1f, 0.15f, 0.15f);

    [Header("Transparencia y suavizado")]
    [SerializeField, Range(0f, 1f)] private float coneAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.45f;
    [SerializeField] private float colorLerpSpeed = 8f;

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

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("ConeVisualizer: no se encontró el shader 'Sprites/Default'.", this);
            enabled = false;
            return;
        }

        BuildMeshes();

        coneMaterial = new Material(shader) { renderQueue = 3000 };
        fillMaterial = new Material(shader) { renderQueue = 3001 };

        CreateLayer("ConoVision", coneMesh, coneMaterial);
        fillRenderer = CreateLayer("ConoRelleno", fillMesh, fillMaterial);

        currentColor = patrolColor;
    }

    private void LateUpdate()
    {
        ComputeRays();
        UpdateGeometry();
        UpdateColors();
    }

    private void BuildMeshes()
    {
        rayDirections = new Vector3[rayCount + 1];
        rayDistances = new float[rayCount + 1];

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
        mesh.MarkDynamic();
        mesh.vertices = new Vector3[vertexCount];
        mesh.triangles = triangles;
        return mesh;
    }

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

            if (hitCollider.transform.IsChildOf(transform)) continue;
            if (player != null && hitCollider.transform.IsChildOf(player)) continue;
            if (hitCollider.GetComponentInParent<EnemyAI>() != null) continue;

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

            coneVertices[i + 1] = transform.InverseTransformPoint(coneOrigin + direction * distance);
            fillVertices[i + 1] = transform.InverseTransformPoint(fillOrigin + direction * (distance * suspicion));
        }

        coneMesh.vertices = coneVertices;
        coneMesh.RecalculateBounds();

        fillMesh.vertices = fillVertices;
        fillMesh.RecalculateBounds();
    }

    private void UpdateColors()
    {
        currentColor = Color.Lerp(currentColor, GetStateColor(),
                                  1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime));

        Color coneColor = currentColor;
        coneColor.a = coneAlpha;
        coneMaterial.color = coneColor;

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

    private void OnDestroy()
    {
        if (coneMesh != null) Destroy(coneMesh);
        if (fillMesh != null) Destroy(fillMesh);
        if (coneMaterial != null) Destroy(coneMaterial);
        if (fillMaterial != null) Destroy(fillMaterial);
    }
}
