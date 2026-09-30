using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform target;

    [Header("Posición")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 1.75f;
    [SerializeField] private float shoulderOffset = 0.8f;

    [Header("Mouse")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float initialPitch = 8f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Suavizado")]
    [SerializeField] private float followSmoothTime = 0.08f;
    [SerializeField] private float rotationSharpness = 25f;
    [SerializeField] private float zoomOutSharpness = 6f;

    [Header("Colisión de cámara")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float yaw;
    private float pitch;

    private Vector3 focusPoint;
    private Vector3 focusVelocity;
    private Quaternion currentRotation;
    private float currentDistance;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private void Start()
    {
        yaw = target.eulerAngles.y;
        pitch = initialPitch;

        focusPoint = target.position + Vector3.up * height;
        currentRotation = Quaternion.Euler(pitch, yaw, 0f);
        currentDistance = distance;

        LockCursor();
    }

    private void LateUpdate()
    {
        HandleCursor();
        HandleMouseLook();

        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        currentRotation = Quaternion.Slerp(
            currentRotation, targetRotation, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));

        Vector3 desiredFocus = target.position + Vector3.up * height;
        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, followSmoothTime);

        Vector3 shoulderDir = currentRotation * Vector3.right * Mathf.Sign(shoulderOffset);
        float shoulderFree = GetFreeDistance(focusPoint, shoulderDir, Mathf.Abs(shoulderOffset));
        Vector3 pivot = focusPoint + shoulderDir * shoulderFree;

        Vector3 backDir = -(currentRotation * Vector3.forward);
        float freeDistance = GetFreeDistance(pivot, backDir, distance);

        if (freeDistance < currentDistance)
            currentDistance = freeDistance;
        else
            currentDistance = Mathf.Lerp(
                currentDistance, freeDistance, 1f - Mathf.Exp(-zoomOutSharpness * Time.deltaTime));

        transform.position = pivot + backDir * currentDistance;
        transform.rotation = currentRotation;
    }

    private float GetFreeDistance(Vector3 origin, Vector3 dir, float maxDistance)
    {
        if (maxDistance <= 0.001f) return 0f;

        int count = Physics.SphereCastNonAlloc(
            origin, collisionRadius, dir, hitBuffer, maxDistance,
            collisionMask, QueryTriggerInteraction.Ignore);

        float free = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider.transform.IsChildOf(target)) continue;
            if (hit.distance <= 0f) continue;
            free = Mathf.Min(free, hit.distance);
        }
        return free;
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        yaw += delta.x * sensitivity;
        pitch -= delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        else if (Cursor.lockState != CursorLockMode.Locked
                 && Mouse.current != null
                 && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
