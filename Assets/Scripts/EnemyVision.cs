using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyVision : MonoBehaviour
{
    [Header("Visión (Raycast)")]
    [SerializeField] private float viewDistance = 10f;
    [SerializeField, Range(10f, 180f)] private float viewAngle = 90f;
    [SerializeField] private float eyeHeight = 0.5f;

    [Header("Zona de cercanía (SphereCollider)")]
    [SerializeField] private float proximityRadius = 2.5f;

    [Header("Feedback de debug (ahora el color lo maneja EnemyAI)")]
    [SerializeField] private bool showDebugColor = false;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color proximityColor = Color.yellow;
    [SerializeField] private Color spottedColor = Color.white;

    public bool CanSeePlayer { get; private set; }
    public bool PlayerInProximity => playerInsideZone && !IsPlayerHidden;
    public bool IsPlayerHidden => hiding != null && hiding.IsHidden;
    public Transform Player => player;
    public float ViewDistance => viewDistance;
    public float ViewAngle => viewAngle;
    public float EyeHeight => eyeHeight;

    private Transform player;
    private SphereCollider proximityZone;
    private PlayerHiding hiding;
    private bool playerInsideZone;
    private Color originalColor;

    private void Awake()
    {
        proximityZone = GetComponent<SphereCollider>();
        proximityZone.isTrigger = true;
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
        hiding = player.GetComponent<PlayerHiding>();

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        originalColor = bodyRenderer.material.color;
    }

    private void Update()
    {
        CanSeePlayer = CheckVision();
        UpdateFeedbackColor();
    }

    private bool CheckVision()
    {
        if (IsPlayerHidden) return false;

        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position - eyePosition;

        if (toPlayer.magnitude > viewDistance) return false;

        Vector3 flatDirection = toPlayer;
        flatDirection.y = 0f;
        if (Vector3.Angle(transform.forward, flatDirection) > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eyePosition, toPlayer.normalized, out RaycastHit hit,
                            viewDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.transform.IsChildOf(player);
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInsideZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInsideZone = false;
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

    private void OnValidate()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.isTrigger = true;
            sphere.radius = proximityRadius;
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f);
        Gizmos.DrawWireSphere(origin, proximityRadius);

        Gizmos.color = CanSeePlayer ? Color.red : Color.cyan;
        float half = viewAngle * 0.5f;
        Vector3 leftEdge = Quaternion.Euler(0f, -half, 0f) * transform.forward * viewDistance;
        Vector3 rightEdge = Quaternion.Euler(0f, half, 0f) * transform.forward * viewDistance;
        Gizmos.DrawLine(origin, origin + leftEdge);
        Gizmos.DrawLine(origin, origin + rightEdge);

        int steps = 20;
        Vector3 previous = origin + leftEdge;
        for (int i = 1; i <= steps; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)steps);
            Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * transform.forward * viewDistance;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        if (Application.isPlaying && player != null)
        {
            Gizmos.color = CanSeePlayer ? Color.red : Color.gray;
            Gizmos.DrawLine(origin + Vector3.up * eyeHeight, player.position);
        }
    }
}
