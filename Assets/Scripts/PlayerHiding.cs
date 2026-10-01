using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHiding : MonoBehaviour
{
    [Header("Apariencia mientras está escondido")]
    [SerializeField, Range(0f, 1f)] private float hiddenDarkness = 0.6f;

    public bool IsHidden { get; private set; }

    private Rigidbody rb;
    private HidingSpot nearbySpot;

    private Renderer[] renderers;
    private Color[] originalColors;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    public void SetNearbySpot(HidingSpot spot) { nearbySpot = spot; }
    public void ClearNearbySpot(HidingSpot spot)
    {
        if (nearbySpot == spot) nearbySpot = null;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (IsHidden)
        {
            ExitHiding();
            AudioManager.PlayHide();
        }
        else if (nearbySpot != null)
        {
            EnterHiding(nearbySpot);
            AudioManager.PlayHide();
        }
    }

    private void EnterHiding(HidingSpot spot)
    {
        IsHidden = true;

        Vector3 position = spot.HidePosition;
        position.y = transform.position.y;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector3.zero;

        SetTint(true);
    }

    private void ExitHiding()
    {
        IsHidden = false;
        SetTint(false);
    }

    private void SetTint(bool hidden)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = hidden
                ? Color.Lerp(originalColors[i], Color.black, hiddenDarkness)
                : originalColors[i];
        }
    }

    public string PromptText
    {
        get
        {
            if (IsHidden) return "ESCONDIDO   -   [E] Salir";
            if (nearbySpot != null) return "[E] Esconderse";
            return "";
        }
    }
}
