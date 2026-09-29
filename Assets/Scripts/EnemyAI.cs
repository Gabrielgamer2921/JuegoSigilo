using UnityEngine;
using UnityEngine.AI; // Necesario para NavMeshAgent

/// <summary>
/// Cerebro del enemigo. Tiene dos estados:
///  - Patrol: camina de waypoint en waypoint.
///  - Chase:  persigue al jugador (o a donde lo vio por última vez).
/// Usa EnemyVision para saber si detectó al jugador.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyVision))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Patrol, Chase }

    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;        // Puntos por los que pasa (en orden)
    [SerializeField] private float patrolSpeed = 2f;       // Velocidad al patrullar
    [SerializeField] private float waitTime = 1.5f;        // Segundos que espera en cada punto
    [SerializeField] private float arriveDistance = 0.3f;  // Qué tan cerca debe estar para "llegar"

    [Header("Persecución")]
    [SerializeField] private float chaseSpeed = 4.5f;      // Velocidad al perseguir
    [SerializeField] private float catchDistance = 1.3f;   // A esta distancia te "atrapa"
    [SerializeField] private float loseSightTime = 3f;     // Segundos sin verte antes de rendirse

    private NavMeshAgent agent;
    private EnemyVision vision;

    private State state = State.Patrol;
    private int currentIndex;
    private bool waiting;
    private float waitTimer;

    private Vector3 lastKnownPosition;  // Dónde vio al jugador por última vez
    private float timeSinceSeen;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        vision = GetComponent<EnemyVision>();
        agent.speed = patrolSpeed;
    }

    private void Start()
    {
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

        switch (state)
        {
            case State.Patrol:
                if (detected) StartChase();
                else Patrol();
                break;

            case State.Chase:
                Chase(detected);
                break;
        }
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

    // ---------------- PERSECUCIÓN ----------------

    private void StartChase()
    {
        state = State.Chase;
        agent.speed = chaseSpeed;
        waiting = false;
        timeSinceSeen = 0f;
        lastKnownPosition = vision.Player.position;

        // Avisamos al GameManager que nos detectaron (él decide si eso ya es derrota)
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDetected();
    }

    private void Chase(bool detected)
    {
        if (detected)
        {
            timeSinceSeen = 0f;
            lastKnownPosition = vision.Player.position;
        }
        else
        {
            timeSinceSeen += Time.deltaTime;
        }

        // Va hacia donde lo vio por última vez (no "adivina" dónde está ahora)
        agent.SetDestination(lastKnownPosition);

        // ¿Nos alcanzó?
        float distanceToPlayer = Vector3.Distance(transform.position, vision.Player.position);
        if (distanceToPlayer <= catchDistance && GameManager.Instance != null)
            GameManager.Instance.Lose("¡TE ATRAPARON!");

        // ¿Perdió al jugador hace rato? Vuelve a patrullar
        if (timeSinceSeen >= loseSightTime)
            ReturnToPatrol();
    }

    private void ReturnToPatrol()
    {
        state = State.Patrol;
        agent.speed = patrolSpeed;
        waiting = false;
        agent.SetDestination(waypoints[currentIndex].position);
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
    }
}
