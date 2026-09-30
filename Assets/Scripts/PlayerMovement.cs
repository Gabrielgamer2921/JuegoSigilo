using UnityEngine;
using UnityEngine.InputSystem; // Namespace del New Input System

[RequireComponent(typeof(Rigidbody), typeof(PlayerStealth))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float turnSpeed = 720f;   // Qué tan rápido gira el cuerpo (grados/s)

    [Header("Referencias")]
    [SerializeField] private Transform cameraTransform; // Si lo dejás vacío usa la Main Camera

    private Rigidbody rb;
    private Vector2 moveInput; // x = izquierda/derecha, y = adelante/atrás
    private PlayerHiding hiding; // Si está escondido, no se mueve
    private PlayerStealth stealth; // Define la velocidad según caminar, correr o agacharse

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hiding = GetComponent<PlayerHiding>();

        stealth = GetComponent<PlayerStealth>();
        if (stealth == null)
        {
            Debug.LogWarning("PlayerMovement: faltaba PlayerStealth en el Player, se agregó automáticamente.", this);
            stealth = gameObject.AddComponent<PlayerStealth>();
        }

        // Evita que el jugador se caiga o rote por choques con las físicas.
        // Nosotros controlamos la rotación a mano.
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    // Update: leemos el teclado (se ejecuta cada frame)
    private void Update()
    {
        moveInput = Vector2.zero;

        // Escondido: no se mueve (moveInput queda en cero)
        if (hiding != null && hiding.IsHidden) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed) moveInput.y += 1f;
        if (kb.sKey.isPressed) moveInput.y -= 1f;
        if (kb.dKey.isPressed) moveInput.x += 1f;
        if (kb.aKey.isPressed) moveInput.x -= 1f;

        // Así en diagonal no vas más rápido que en línea recta
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    // FixedUpdate: aplicamos las físicas (se ejecuta a intervalos fijos)
    private void FixedUpdate()
    {
        // Direcciones "adelante" y "derecha" de la cámara, aplanadas al piso
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 direction = camForward * moveInput.y + camRight * moveInput.x;

        // Seteamos la velocidad. Conservamos la Y para que la gravedad siga funcionando.
        Vector3 velocity = direction * stealth.CurrentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // Giramos el cuerpo suavemente hacia donde nos movemos
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.RotateTowards(
                rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
        }
    }
}