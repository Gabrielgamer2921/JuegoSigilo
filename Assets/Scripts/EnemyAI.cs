using System;
using UnityEngine;
using UnityEngine.AI; // Necesario para NavMeshAgent

/// <summary>
/// Los estados del enemigo. Es público para que otros scripts (conos, sonido, UI)
/// puedan leerlo.
/// </summary>
public enum EnemyState { Patrol, Alert, Investigate, Chase }

/// <summary>
/// Cerebro del enemigo (máquina de estados):
///
///  Patrol      Camina de waypoint en waypoint.
///  Alert       Detectó algo. Se queda parado mirando y su medidor de SOSPECHA sube de a poco.
///              Si el medidor se llena -> Chase. Si te pierde de vista -> Investigate.
///  Investigate Va al último lugar donde te vio, mira alrededor y, si no hay nada, vuelve a patrullar
///              (retomando el camino hacia el waypoint que venía).
///  Chase       Está seguro de haberte visto: te persigue.
///
/// El medidor de sospecha (0 a 1) NO se reinicia al cambiar de estado: baja lentamente con el tiempo.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyVision))]
public class EnemyAI : MonoBehaviour
{
    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;        // Puntos por los que pasa (en orden)
    [SerializeField] private float patrolSpeed = 2f;       // Velocidad al patrullar
    [SerializeField] private float waitTime = 1.5f;        // Segundos que espera en cada punto
    [SerializeField] private float arriveDistance = 0.3f;  // Qué tan cerca debe estar para "llegar"

    [Header("Sospecha (estado Alerta)")]
    [SerializeField] private float suspicionFillTime = 3f;      // Segundos de vista continua para llenar el medidor (a velocidad normal)
    [SerializeField] private float closeRateMultiplier = 2f;    // Multiplicador cuando estás cerca (se llena más rápido)
    [SerializeField] private float farRateMultiplier = 0.5f;    // Multiplicador cuando estás lejos (se llena más lento)
    [SerializeField] private float suspicionDecayTime = 8f;     // Segundos para que el medidor baje de 1 a 0 si no ve nada
    [SerializeField] private float lookTurnSpeed = 120f;        // Qué tan rápido gira hacia donde te vio (grados/s)
    [SerializeField] private float alertLoseTime = 0.6f;        // Segundos sin verte antes de ir a revisar
    [SerializeField, Range(0f, 1f)] private float investigateThreshold = 0.2f; // Sospecha mínima para molestarse en revisar

    [Header("Investigar")]
    [SerializeField] private float reactionPause = 1f;          // Segundos que se frena a pensar "¿qué fue eso?" al ver algo nuevo
    [SerializeField] private float investigateSpeed = 3f;       // Velocidad al ir a revisar
    [SerializeField] private float searchTime = 4f;             // Segundos que mira alrededor al llegar
    [SerializeField] private float searchSweepAngle = 60f;      // Cuánto gira la cabeza a cada lado
    [SerializeField] private float searchSweepSpeed = 1.5f;     // Qué tan rápido barre con la vista

    [Header("Persecución")]
    [SerializeField] private float chaseSpeed = 3.5f;      // Velocidad al perseguir
    [SerializeField] private float catchDistance = 1.3f;   // A esta distancia te "atrapa"
    [SerializeField] private float loseSightTime = 2f;     // Segundos sin verte antes de dejar de correr

    [Header("Feedback en el cuerpo (opcional: los conos ya muestran el estado)")]
    [SerializeField] private bool showBodyColor = false;
    [SerializeField] private Renderer bodyRenderer;        // Si está vacío lo busca solo
    [SerializeField] private Color alertColor = Color.yellow;
    [SerializeField] private Color investigateColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color chaseColor = Color.white;

    // Lo que pueden leer otros scripts
    public EnemyState CurrentState { get; private set; } = EnemyState.Patrol;
    public event Action<EnemyState> StateChanged;

    /// <summary>Medidor de sospecha: 0 = tranquilo, 1 = seguro de haberte visto.</summary>
    public float Suspicion => suspicion;

    private NavMeshAgent agent;
    private EnemyVision vision;

    // Patrulla
    private int currentIndex;      // Waypoint AL QUE se dirige (o en el que espera)
    private bool waiting;
    private float waitTimer;

    // Sospecha
    private float suspicion;
    private float alertLostTimer;
    private Vector3 lastKnownPosition;  // Dónde vio al jugador por última vez

    // Investigar
    private bool searching;             // false = yendo al punto, true = mirando alrededor
    private float searchTimer;
    private float searchStartYaw;
    private float reactionTimer;        // Mayor a 0 mientras hace la pausa "¿qué fue eso?"
    private bool wasDetected;           // Si te detectaba en el frame anterior (para saber cuándo aparece una señal NUEVA)

    // Persecución
    private float timeSinceSeen;

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
        // El enemigo detecta al jugador si lo ve O si está en su zona de cercanía
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

    // ---------------- MEDIDOR DE SOSPECHA ----------------

    private void UpdateSuspicion(bool detected)
    {
        // Durante la persecución el medidor queda al máximo
        if (CurrentState == EnemyState.Chase)
        {
            if (detected) suspicion = 1f;
            return;
        }

        if (detected)
            suspicion += GetDetectionRate() * Time.deltaTime;
        else if (CurrentState != EnemyState.Investigate) // Mientras investiga sigue desconfiado: no baja
            suspicion -= Time.deltaTime / suspicionDecayTime;

        suspicion = Mathf.Clamp01(suspicion);
    }

    // Qué tan rápido sube el medidor: más rápido cuanto más cerca estás
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
            float t = Mathf.Clamp01(distance / vision.ViewDistance); // 0 = pegado, 1 = al límite
            multiplier = Mathf.Lerp(closeRateMultiplier, farRateMultiplier, t);
        }

        return multiplier / suspicionFillTime;
    }

    // ---------------- CAMBIO DE ESTADO ----------------

    private void SetState(EnemyState newState)
    {
        if (newState == CurrentState) return;
        CurrentState = newState;

        // Lo que pasa UNA sola vez al entrar en cada estado
        switch (newState)
        {
            case EnemyState.Patrol:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                searching = false;
                waiting = false;
                // currentIndex ya apunta al waypoint correcto: retoma su camino hacia ahí
                agent.SetDestination(waypoints[currentIndex].position);
                break;

            case EnemyState.Alert:
                // Si lo interrumpimos mientras esperaba en un waypoint, ya pasa al siguiente:
                // así no da media vuelta hacia el punto que acababa de dejar.
                if (waiting)
                {
                    waiting = false;
                    currentIndex = (currentIndex + 1) % waypoints.Length;
                }
                agent.isStopped = true;          // Frena...
                agent.velocity = Vector3.zero;   // ...en seco
                alertLostTimer = 0f;
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

                // Avisamos al GameManager (él decide si detectar ya es derrota)
                if (GameManager.Instance != null)
                    GameManager.Instance.OnPlayerDetected();
                break;
        }

        ApplyStateColor();
        StateChanged?.Invoke(newState);
    }

    // ---------------- PATRULLA ----------------

    private void Patrol()
    {
        // Mientras el agente calcula la ruta, esperamos
        if (agent.pathPending) return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                // Pasamos al siguiente punto (al llegar al último, vuelve al primero)
                currentIndex = (currentIndex + 1) % waypoints.Length;
                agent.SetDestination(waypoints[currentIndex].position);
            }
            return;
        }

        // ¿Llegamos al waypoint actual?
        if (agent.remainingDistance <= agent.stoppingDistance + arriveDistance)
        {
            waiting = true;
            waitTimer = waitTime;
        }
    }

    // ---------------- ALERTA ----------------

    private void Alert(bool detected)
    {
        // Parado, mirando hacia donde vio algo
        LookTowards(lastKnownPosition);

        // Medidor lleno: está seguro
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

        // Dejó de verte. Espera un instante y decide:
        // ¿vio suficiente como para ir a revisar, o fue un descuido?
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

    // ---------------- INVESTIGAR ----------------

    private void Investigate(bool detected)
    {
        // ¿Es una señal NUEVA? (antes no te veía y ahora sí)
        bool newSighting = detected && !wasDetected;
        wasDetected = detected;

        // Si el medidor se llenó, ahora sí está seguro
        if (detected && suspicion >= 1f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        // Señal nueva: se frena un momento a pensar "¿qué fue eso?"
        if (newSighting)
        {
            reactionTimer = reactionPause;
            searching = false;               // Si estaba mirando alrededor, deja de hacerlo
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Durante la pausa: parado, mirando hacia donde te vio
        if (reactionTimer > 0f)
        {
            reactionTimer -= Time.deltaTime;
            LookTowards(lastKnownPosition);

            // Terminó la pausa: ahora sí avanza hacia el punto más reciente
            if (reactionTimer <= 0f)
            {
                agent.isStopped = false;
                SetDestinationSafe(lastKnownPosition);
            }
            return;
        }

        // Ya pasó la pausa y sigue viéndote: avanza hacia el punto actualizado
        if (detected)
        {
            searching = false;
            agent.isStopped = false;
            SetDestinationSafe(lastKnownPosition);
            return;
        }

        // Fase 1: caminar hasta el último punto donde te vio
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

        // Fase 2: mirar alrededor (barrido suave a izquierda y derecha)
        searchTimer += Time.deltaTime;
        float sweep = Mathf.Sin(searchTimer * searchSweepSpeed) * searchSweepAngle;
        transform.rotation = Quaternion.Euler(0f, searchStartYaw + sweep, 0f);

        // No había nada: vuelve a patrullar
        if (searchTimer >= searchTime)
            SetState(EnemyState.Patrol);
    }

    // ---------------- PERSECUCIÓN ----------------

    private void Chase(bool detected)
    {
        timeSinceSeen = detected ? 0f : timeSinceSeen + Time.deltaTime;

        // Va hacia donde lo vio por última vez (no "adivina" dónde está ahora)
        SetDestinationSafe(lastKnownPosition);

        // ¿Nos alcanzó?
        float distanceToPlayer = Vector3.Distance(transform.position, vision.Player.position);
        if (distanceToPlayer <= catchDistance && GameManager.Instance != null)
            GameManager.Instance.Lose("¡TE ATRAPARON!");

        // Te perdió: no vuelve directo a patrullar, va a revisar el último punto
        if (timeSinceSeen >= loseSightTime)
        {
            suspicion = Mathf.Min(suspicion, 0.6f); // Sigue alerta, pero ya no está "seguro"
            SetState(EnemyState.Investigate);
        }
    }

    // Manda al agente a un punto, ajustándolo a la zona caminable más cercana del NavMesh
    private void SetDestinationSafe(Vector3 target)
    {
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(target);
    }

    // ---------------- FEEDBACK ----------------

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

    // Dibuja la ruta en el editor (solo se ve en la Scene view)
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

        // Último punto donde vio al jugador (solo en juego)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.5f);
        }
    }
}