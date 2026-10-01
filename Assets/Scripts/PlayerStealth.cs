using UnityEngine;
using UnityEngine.InputSystem;

public enum Stance { Crouch, Walk, Run }

[RequireComponent(typeof(Rigidbody))]
public class PlayerStealth : MonoBehaviour
{
    [Header("Velocidades (m/s)")]
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 6f;

    [Header("Radio de ruido (m)")]
    [SerializeField] private float crouchNoise = 2f;
    [SerializeField] private float walkNoise = 5f;
    [SerializeField] private float runNoise = 10f;
    [SerializeField] private float noiseInterval = 0.3f;

    [Header("Agacharse")]
    [SerializeField, Range(0.4f, 1f)] private float crouchHeightScale = 0.65f;

    [Header("Anillo de ruido en el piso")]
    [SerializeField] private bool showNoiseRing = true;
    [SerializeField] private float ringSmoothing = 10f;
    [SerializeField] private Color crouchColor = new Color(0.4f, 1f, 0.6f, 0.5f);
    [SerializeField] private Color walkColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color runColor = new Color(1f, 0.4f, 0.3f, 0.65f);

    public Stance CurrentStance { get; private set; } = Stance.Walk;
    public float CurrentSpeed { get; private set; }
    public float CurrentNoiseRadius { get; private set; }

    private const int RingSegments = 48;

    private Rigidbody rb;
    private PlayerHiding hiding;
    private bool crouching;
    private float standingScaleY;
    private float noiseTimer;
    private float stepTimer;

    private LineRenderer ring;
    private float ringRadius;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hiding = GetComponent<PlayerHiding>();
        standingScaleY = transform.localScale.y;
        CurrentSpeed = walkSpeed;

        CreateRing();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        Keyboard kb = Keyboard.current;
        bool hidden = hiding != null && hiding.IsHidden;

        if (kb != null && kb.cKey.wasPressedThisFrame && !hidden)
            SetCrouching(!crouching);

        if (hidden)
        {
            CurrentSpeed = 0f;
            CurrentNoiseRadius = 0f;
            UpdateRing();
            return;
        }

        bool running = kb != null && kb.leftShiftKey.isPressed && !crouching;

        if (crouching) CurrentStance = Stance.Crouch;
        else if (running) CurrentStance = Stance.Run;
        else CurrentStance = Stance.Walk;

        switch (CurrentStance)
        {
            case Stance.Crouch: CurrentSpeed = crouchSpeed; break;
            case Stance.Run:    CurrentSpeed = runSpeed;    break;
            default:            CurrentSpeed = walkSpeed;   break;
        }

        Vector3 flatVelocity = rb.linearVelocity;
        flatVelocity.y = 0f;
        bool moving = flatVelocity.sqrMagnitude > 0.25f;

        CurrentNoiseRadius = moving ? GetNoiseRadius(CurrentStance) : 0f;

        if (moving)
        {
            noiseTimer -= Time.deltaTime;
            if (noiseTimer <= 0f)
            {
                NoiseSystem.Emit(transform.position, CurrentNoiseRadius);
                noiseTimer = noiseInterval;
            }
        }
        else
        {
            noiseTimer = 0f;
        }

        if (moving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                AudioManager.PlayFootstep(GetStepVolume(CurrentStance));
                stepTimer = GetStepInterval(CurrentStance);
            }
        }
        else
        {
            stepTimer = 0f;
        }

        UpdateRing();
    }

    private float GetStepInterval(Stance stance)
    {
        switch (stance)
        {
            case Stance.Crouch: return 0.65f;
            case Stance.Run:    return 0.3f;
            default:            return 0.45f;
        }
    }

    private float GetStepVolume(Stance stance)
    {
        switch (stance)
        {
            case Stance.Crouch: return 0.25f;
            case Stance.Run:    return 1f;
            default:            return 0.6f;
        }
    }

    private float GetNoiseRadius(Stance stance)
    {
        switch (stance)
        {
            case Stance.Crouch: return crouchNoise;
            case Stance.Run:    return runNoise;
            default:            return walkNoise;
        }
    }

    private void SetCrouching(bool value)
    {
        if (crouching == value) return;
        crouching = value;

        float oldScaleY = transform.localScale.y;
        float newScaleY = value ? standingScaleY * crouchHeightScale : standingScaleY;

        Vector3 scale = transform.localScale;
        scale.y = newScaleY;
        transform.localScale = scale;

        Vector3 position = rb.position;
        position.y += newScaleY - oldScaleY;
        rb.position = position;
        transform.position = position;
    }

    private void CreateRing()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        GameObject ringObject = new GameObject("AnilloRuido");
        ringObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = RingSegments;
        ring.widthMultiplier = 0.08f;
        ring.alignment = LineAlignment.TransformZ;
        ring.material = new Material(shader);
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;
    }

    private void UpdateRing()
    {
        if (ring == null) return;

        ringRadius = Mathf.Lerp(ringRadius, CurrentNoiseRadius, 1f - Mathf.Exp(-ringSmoothing * Time.deltaTime));

        bool visible = showNoiseRing && ringRadius > 0.3f;
        ring.enabled = visible;
        if (!visible) return;

        Color color;
        switch (CurrentStance)
        {
            case Stance.Crouch: color = crouchColor; break;
            case Stance.Run:    color = runColor;    break;
            default:            color = walkColor;   break;
        }
        ring.startColor = color;
        ring.endColor = color;

        float footY = transform.position.y - transform.localScale.y + 0.05f;
        Vector3 center = new Vector3(transform.position.x, footY, transform.position.z);

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            ring.SetPosition(i, center + offset);
        }
    }

    private void OnDestroy()
    {
        if (ring != null)
        {
            Destroy(ring.material);
            Destroy(ring.gameObject);
        }
    }
}
