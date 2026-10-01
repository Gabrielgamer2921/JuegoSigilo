using System;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Patrol, Alert, Investigate, Chase }

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyVision))]
public class EnemyAI : MonoBehaviour
{
    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waitTime = 1.5f;
    [SerializeField] private float arriveDistance = 0.3f;
    [SerializeField] private bool faceWaypointDirection = false;

    [Header("Sospecha (estado Alerta)")]
    [SerializeField] private float suspicionFillTime = 3f;
    [SerializeField] private float closeRateMultiplier = 2f;
    [SerializeField] private float farRateMultiplier = 0.5f;
    [SerializeField] private float suspicionDecayTime = 8f;
    [SerializeField] private float lookTurnSpeed = 120f;
    [SerializeField] private float alertLoseTime = 0.6f;
    [SerializeField, Range(0f, 1f)] private float investigateThreshold = 0.2f;

    [Header("Investigar")]
    [SerializeField] private float reactionPause = 1f;
    [SerializeField] private float investigateSpeed = 3f;
    [SerializeField] private float searchTime = 4f;
    [SerializeField] private float searchSweepAngle = 60f;
    [SerializeField] private float searchSweepSpeed = 1.5f;

    [Header("Persecución")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private float loseSightTime = 2f;

    [Header("Oído")]
    [SerializeField] private float noiseSuspicionStep = 0.1f;
    [SerializeField, Range(0f, 1f)] private float noiseSuspicionCap = 0.6f;
    [SerializeField] private float noiseMemory = 1.5f;

    [Header("Sonido de alerta")]
    [SerializeField] private float alertSoundCooldown = 1.2f;

    [Header("Feedback en el cuerpo (opcional: los conos ya muestran el estado)")]
    [SerializeField] private bool showBodyColor = false;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private Color investigateColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color chaseColor = Color.white;

    public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;
    public event Action<EnemyState> StateChanged;
    public event Action Alerted;

    public float Suspicion => suspicion;

    private NavMeshAgent agent;
    private EnemyVision vision;

    private int currentIndex;
    private bool waiting;
    private float waitTimer;

    private float suspicion;
    private float alertLostTimer;
    private Vector3 lastKnownPosition;

    private bool searching;
    private float searchTimer;
    private float searchStartYaw;
    private float reactionTimer;
    private bool wasDetected;

    private float timeSinceSeen;

    private float lastNoiseTime = -999f;
    private float lastAlertTime = -999f;

    private Color patrolColor;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<EnemyVision>();
        agent.speed = patrolSpeed;
    }

    private void Start()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null)
            patrolColor = bodyRenderer.material.color;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{name}: no tiene waypoints asignados, se queda quieto.", this);
            enabled = false;
            return;
        }

        currentIndex = 0;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    private void Update()
    {
        bool detected = vision.CanSeePlayer || vision.PlayerInProximity;
        if (detected) lastKnownPosition = vision.Player.position;

        UpdateSuspicion(detected);

        switch (CurrentState)
        {
            case EnemyState.Patrol:
                if (detected) SetState(EnemyState.Alert);
                else Patrol();
                break;

            case EnemyState.Alert:
                Alert(detected);
                break;

            case EnemyState.Investigate:
                Investigate(detected);
                break;

            case EnemyState.Chase:
                Chase(detected);
                break;
        }
    }

    private void UpdateSuspicion(bool detected)
    {
        if (CurrentState == EnemyState.Chase)
        {
            if (detected) suspicion = 1f;
            return;
        }

        if (detected)
            suspicion += GetDetectionRate() * Time.deltaTime;
        else if (CurrentState != EnemyState.Investigate)
            suspicion -= Time.deltaTime / suspicionDecayTime;

        suspicion = Mathf.Clamp01(suspicion);
    }

    private float GetDetectionRate()
    {
        float multiplier;

        if (vision.PlayerInProximity)
        {
            multiplier = closeRateMultiplier;
        }
        else
        {
            float distance = Vector3.Distance(transform.position, vision.Player.position);
            float t = Mathf.Clamp01(distance / vision.ViewDistance);
            multiplier = Mathf.Lerp(closeRateMultiplier, farRateMultiplier, t);
        }

        return multiplier / suspicionFillTime;
    }

    private void SetState(EnemyState newState)
    {
        if (newState == CurrentState) return;
        CurrentState = newState;

        switch (newState)
        {
            case EnemyState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                searching = false;
                waiting = false;

                agent.SetDestination(waypoints[currentIndex].position);
                break;

            case EnemyState.Alert:
                if (waiting)
                {
                    waiting = false;
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                }
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                alertLostTimer = 0f;
                RaiseAlerted();
                break;

            case EnemyState.Investigate:
                agent.isStopped = false;
                agent.speed = investigateSpeed;
                wasDetected = false;
                reactionTimer = 0f;
                searching = false;
                SetDestinationSafe(lastKnownPosition);
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                timeSinceSeen = 0f;

                if (GameManager.Instance != null)
                    GameManager.Instance.OnPlayerDetected();
                break;
        }

        ApplyStateColor();
        StateChanged?.Invoke(newState);
    }

    private void Patrol()
    {
        if (agent.pathPending) return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;

            if (faceWaypointDirection)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, waypoints[currentIndex].rotation, lookTurnSpeed * Time.deltaTime);
            }

            if (waitTimer <= 0f)
            {
                waiting = false;

                currentIndex = (currentIndex + 1) % waypoints.Length;
                agent.SetDestination(waypoints[currentIndex].position);
            }
            return;
        }

        if (agent.remainingDistance <= agent.stoppingDistance + arriveDistance)
        {
            waiting = true;
            waitTimer = waitTime;
        }
    }

    private void Alert(bool detected)
    {
        LookTowards(lastKnownPosition);

        if (suspicion >= 1f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        if (detected)
        {
            alertLostTimer = 0f;
            return;
        }

        alertLostTimer += Time.deltaTime;
        if (alertLostTimer >= alertLoseTime)
        {
            SetState(suspicion >= investigateThreshold ? EnemyState.Investigate : EnemyState.Patrol);
        }
    }

    private void LookTowards(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, lookTurnSpeed * Time.deltaTime);
    }

    private void Investigate(bool detected)
    {
        bool newSighting = detected && !wasDetected;
        wasDetected = detected;

        if (detected && suspicion >= 1f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        if (newSighting)
            StartReaction();

        if (reactionTimer > 0f)
        {
            reactionTimer -= Time.deltaTime;
            LookTowards(lastKnownPosition);

            if (reactionTimer <= 0f)
            {
                agent.isStopped = false;
                SetDestinationSafe(lastKnownPosition);
            }
            return;
        }

        if (detected)
        {
            searching = false;
            agent.isStopped = false;
            SetDestinationSafe(lastKnownPosition);
            return;
        }

        if (!searching)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + arriveDistance)
            {
                searching = true;
                searchTimer = 0f;
                searchStartYaw = transform.eulerAngles.y;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }

        searchTimer += Time.deltaTime;
        float sweep = Mathf.Sin(searchTimer * searchSweepSpeed) * searchSweepAngle;
        transform.rotation = Quaternion.Euler(0f, searchStartYaw + sweep, 0f);

        if (searchTimer >= searchTime)
            SetState(EnemyState.Patrol);
    }

    private void StartReaction()
    {
        reactionTimer = reactionPause;
        searching = false;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        RaiseAlerted();
    }

    private void RaiseAlerted()
    {
        if (Time.time - lastAlertTime < alertSoundCooldown) return;

        lastAlertTime = Time.time;
        Alerted?.Invoke();
    }

    public void HearNoise(Vector3 position)
    {
        if (!enabled) return;
        if (CurrentState == EnemyState.Alert || CurrentState == EnemyState.Chase) return;

        lastKnownPosition = position;

        if (suspicion < noiseSuspicionCap)
            suspicion = Mathf.Min(noiseSuspicionCap, suspicion + noiseSuspicionStep);

        bool freshNoise = Time.time - lastNoiseTime > noiseMemory;
        lastNoiseTime = Time.time;

        if (CurrentState == EnemyState.Patrol)
        {
            if (waiting)
            {
                waiting = false;
                currentIndex = (currentIndex + 1) % waypoints.Length;
            }

            SetState(EnemyState.Investigate);
            StartReaction();
            return;
        }

        if (freshNoise)
        {
            StartReaction();
        }
        else if (reactionTimer <= 0f)
        {
            searching = false;
            agent.isStopped = false;
            SetDestinationSafe(position);
        }
    }

    private void Chase(bool detected)
    {
        timeSinceSeen = detected ? 0f : timeSinceSeen + Time.deltaTime;

        SetDestinationSafe(lastKnownPosition);

        float distanceToPlayer = Vector3.Distance(transform.position, vision.Player.position);
        if (distanceToPlayer <= catchDistance && !vision.IsPlayerHidden && GameManager.Instance != null)
            GameManager.Instance.Lose("¡TE ATRAPARON!");

        if (timeSinceSeen >= loseSightTime)
        {
            suspicion = Mathf.Min(suspicion, 0.6f);
            SetState(EnemyState.Investigate);
        }
    }

    private void SetDestinationSafe(Vector3 target)
    {
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(target);
    }

    private void ApplyStateColor()
    {
        if (!showBodyColor || bodyRenderer == null) return;

        switch (CurrentState)
        {
            case EnemyState.Patrol: bodyRenderer.material.color = patrolColor; break;
            case EnemyState.Alert: bodyRenderer.material.color = alertColor; break;
            case EnemyState.Investigate: bodyRenderer.material.color = investigateColor; break;
            case EnemyState.Chase: bodyRenderer.material.color = chaseColor; break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.25f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(waypoints[i].position, next.position);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }
    }
}