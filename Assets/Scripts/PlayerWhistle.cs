using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWhistle : MonoBehaviour
{
    [Header("Silbido")]
    [SerializeField] private float whistleRadius = 12f;
    [SerializeField] private float whistleCooldown = 2.5f;

    [Header("Feedback")]
    [SerializeField] private Color pulseColor = new Color(1f, 0.95f, 0.5f, 0.8f);
    [SerializeField] private float pulseDuration = 0.7f;

    private float cooldownTimer;

    public float Cooldown => whistleCooldown;
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.qKey.wasPressedThisFrame || cooldownTimer > 0f) return;

        cooldownTimer = whistleCooldown;
        NoiseSystem.Emit(transform.position, whistleRadius);
        AudioManager.PlayWhistle();

        float footY = transform.position.y - transform.localScale.y + 0.05f;
        Vector3 center = new Vector3(transform.position.x, footY, transform.position.z);
        NoisePulse.Spawn(center, whistleRadius, pulseColor, pulseDuration);
    }
}
