using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class EnemyHearing : MonoBehaviour
{
    [SerializeField] private float hearingMultiplier = 1f;

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
        Vector3 offset = position - transform.position;
        offset.y = 0f;

        if (offset.magnitude > radius * hearingMultiplier) return;

        ai.HearNoise(position);
    }
}
