using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el escondite del jugador.
///  - Si estás dentro de una zona (HidingSpot) y apretás E, te escondés.
///  - Escondido: no te movés y los enemigos no te ven ni te sienten.
///  - Apretás E de nuevo para salir.
/// Otros scripts (EnemyVision, PlayerMovement) leen IsHidden para saber qué hacer.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerHiding : MonoBehaviour
{
    [Header("Apariencia mientras está escondido")]
    [SerializeField, Range(0f, 1f)] private float hiddenDarkness = 0.6f; // 0 = igual, 1 = negro

    public bool IsHidden { get; private set; }

    private Rigidbody rb;
    private HidingSpot nearbySpot;   // Zona en la que estamos parados (si hay alguna)

    private Renderer[] renderers;
    private Color[] originalColors;
    private GUIStyle promptStyle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Guardamos los colores originales para poder oscurecer y restaurar
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    // HidingSpot nos avisa cuando entramos o salimos de una zona
    public void SetNearbySpot(HidingSpot spot) { nearbySpot = spot; }
    public void ClearNearbySpot(HidingSpot spot)
    {
        if (nearbySpot == spot) nearbySpot = null;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return; // Partida terminada

        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        if (IsHidden) ExitHiding();
        else if (nearbySpot != null) EnterHiding(nearbySpot);
    }

    private void EnterHiding(HidingSpot spot)
    {
        IsHidden = true;

        // Nos ubicamos en el centro de la zona (conservando la altura)
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

    // Oscurece al jugador mientras está escondido (feedback visual)
    private void SetTint(bool hidden)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = hidden
                ? Color.Lerp(originalColors[i], Color.black, hiddenDarkness)
                : originalColors[i];
        }
    }

    // Mensaje en pantalla (después lo reemplazamos por una UI real)
    private void OnGUI()
    {
        if (Time.timeScale == 0f) return;

        string text = null;
        if (IsHidden) text = "ESCONDIDO   -   [E] Salir";
        else if (nearbySpot != null) text = "[E] Esconderse";
        if (text == null) return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(0, Screen.height * 0.8f, Screen.width, 50), text, promptStyle);
    }
}
