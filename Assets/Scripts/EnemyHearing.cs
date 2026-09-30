using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class EnemyHearing : MonoBehaviour
{
    [SerializeField] private float hearingMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float wallMuffle = 0.5f;
    [SerializeField] private float earHeight = 0.5f;

    private EnemyAI ai;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
    }

    private void OnEnable()
    {
        NoiseSystem.NoiseEmitted += OnNoise;
    }

    private void OnDisable()
    {
        NoiseSystem.NoiseEmitted -= OnNoise;
    }

    private void OnNoise(Vector3 position, float radius)
    {
        Vector3 origin = transform.position + Vector3.up * earHeight;
        Vector3 toNoise = position - origin;
        float distance = toNoise.magnitude;

        float reach = radius * hearingMultiplier;
        if (distance > reach) return;

        bool blocked = Physics.Raycast(origin, toNoise.normalized, out RaycastHit hit, distance,
                                       ~0, QueryTriggerInteraction.Ignore)
                       && hit.distance < distance - 0.75f;

        if (blocked && distance > reach * wallMuffle) return;

        ai.HearNoise(position);
    }
}
