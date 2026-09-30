using UnityEngine;

/// <summary>
/// Detección del enemigo. Tiene DOS zonas:
///
/// 1) VISIÓN (Raycast): el enemigo ve al jugador si
///      - está a menos de 'viewDistance'
///      - está dentro del ángulo de visión (cono frontal)
///      - NO hay ningún obstáculo entre los dos
///
/// 2) CERCANÍA (SphereCollider trigger): si el jugador entra en este círculo
///    el enemigo lo "siente", aunque esté de espaldas o no lo vea.
///
/// Este script solo DETECTA y expone el resultado (CanSeePlayer, PlayerInProximity).
/// Qué hace el enemigo con eso lo decide otro script (EnemyAI).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class EnemyVision : MonoBehaviour
{
    [Header("Visión (Raycast)")]
    [SerializeField] private float viewDistance = 10f;               // Alcance máximo de la vista
    [SerializeField, Range(10f, 180f)] private float viewAngle = 90f; // Apertura total del cono
    [SerializeField] private float eyeHeight = 0.5f;                 // Altura de los "ojos" sobre el centro

    [Header("Zona de cercanía (SphereCollider)")]
    [SerializeField] private float proximityRadius = 2.5f;

    [Header("Feedback de debug (ahora el color lo maneja EnemyAI)")]
    [SerializeField] private bool showDebugColor = false;
    [SerializeField] private Renderer bodyRenderer;                  // Si está vacío lo busca solo
    [SerializeField] private Color proximityColor = Color.yellow;
    [SerializeField] private Color spottedColor = Color.white;

    // Resultados que leen otros scripts
    public bool CanSeePlayer { get; private set; }
    public bool PlayerInProximity { get; private set; }
    public Transform Player => player;
    public float ViewDistance => viewDistance;
    public float ViewAngle => viewAngle;
    public float EyeHeight => eyeHeight;

    private Transform player;
    private SphereCollider proximityZone;
    private Color originalColor;

    private void Awake()
    {
        proximityZone = GetComponent<SphereCollider>();
        proximityZone.isTrigger = true;          // Trigger: detecta, pero no bloquea el paso
        proximityZone.radius = proximityRadius;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("EnemyVision: no se encontró ningún objeto con el Tag 'Player'.", this);
            enabled = false;
            return;
        }
        player = playerObject.transform;

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        // .material crea una copia propia para este enemigo (no cambia a los demás)
        originalColor = bodyRenderer.material.color;
    }

    private void Update()
    {
        CanSeePlayer = CheckVision();
        UpdateFeedbackColor();
    }

    private bool CheckVision()
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position - eyePosition;

        // 1) ¿Está demasiado lejos?
        if (toPlayer.magnitude > viewDistance) return false;

        // 2) ¿Está dentro del cono? (comparamos el ángulo solo en el plano horizontal)
        Vector3 flatDirection = toPlayer;
        flatDirection.y = 0f;
        if (Vector3.Angle(transform.forward, flatDirection) > viewAngle * 0.5f) return false;

        // 3) ¿Hay algo en el medio? Tiramos un rayo hacia el jugador.
        //    El rayo golpea lo PRIMERO que encuentra. Si es el jugador, lo vemos.
        //    Si es una pared u otro objeto, nos tapa la vista.
        if (Physics.Raycast(eyePosition, toPlayer.normalized, out RaycastHit hit,
                            viewDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.transform.IsChildOf(player);
        }

        return false;
    }

    // ---- Zona de cercanía: Unity avisa solo cuando algo entra o sale del trigger ----
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) PlayerInProximity = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) PlayerInProximity = false;
    }

    private void UpdateFeedbackColor()
    {
        if (!showDebugColor || bodyRenderer == null) return;

        if (CanSeePlayer)
            bodyRenderer.material.color = spottedColor;
        else if (PlayerInProximity)
            bodyRenderer.material.color = proximityColor;
        else
            bodyRenderer.material.color = originalColor;
    }

    // Mantiene el círculo del trigger sincronizado al tocar valores en el Inspector
    private void OnValidate()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.isTrigger = true;
            sphere.radius = proximityRadius;
        }
    }

    // Dibuja el cono y la zona de cercanía en la Scene view (con Gizmos activados)
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;

        // Zona de cercanía
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);
        Gizmos.DrawWireSphere(origin, proximityRadius);

        // Cono de visión
        Gizmos.color = CanSeePlayer ? Color.red : Color.cyan;
        float half = viewAngle * 0.5f;
        Vector3 leftEdge = Quaternion.Euler(0f, -half, 0f) * transform.forward * viewDistance;
        Vector3 rightEdge = Quaternion.Euler(0f, half, 0f) * transform.forward * viewDistance;
        Gizmos.DrawLine(origin, origin + leftEdge);
        Gizmos.DrawLine(origin, origin + rightEdge);

        // Arco que une los dos bordes
        int steps = 20;
        Vector3 previous = origin + leftEdge;
        for (int i = 1; i <= steps; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)steps);
            Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * transform.forward * viewDistance;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        // Línea hacia el jugador mientras el juego corre
        if (Application.isPlaying && player != null)
        {
            Gizmos.color = CanSeePlayer ? Color.red : Color.gray;
            Gizmos.DrawLine(origin + Vector3.up * eyeHeight, player.position);
        }
    }
}