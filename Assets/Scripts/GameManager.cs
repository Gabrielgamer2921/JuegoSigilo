using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el estado general de la partida: jugando, ganó o perdió.
/// Otros scripts le avisan (Win / Lose / OnPlayerDetected) y él decide qué hacer.
/// El texto en pantalla usa OnGUI (simple, sin Canvas). Más adelante lo reemplazamos por una UI real.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Won, Lost }

    [Header("Reglas")]
    [Tooltip("ON: perdés apenas te detectan (Nota 4). OFF: perdés cuando un enemigo te alcanza (Nota 7).")]
    [SerializeField] private bool loseWhenDetected = false;

    public GameState State { get; private set; } = GameState.Playing;

    private string message;
    private GUIStyle titleStyle;
    private GUIStyle subStyle;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; // Por si veníamos de una partida terminada (que congela el tiempo)
    }

    // ---- Métodos que llaman otros scripts ----

    public void Win()
    {
        End(GameState.Won, "¡GANASTE!");
    }

    public void Lose(string reason)
    {
        End(GameState.Lost, reason);
    }

    // El enemigo avisa que detectó al jugador. Solo perdemos si la regla está activada.
    public void OnPlayerDetected()
    {
        if (loseWhenDetected)
            Lose("¡TE DETECTARON!");
    }

    // ---- Internos ----

    private void End(GameState newState, string text)
    {
        if (State != GameState.Playing) return; // Ya terminó, ignoramos avisos repetidos

        State = newState;
        message = text;

        Time.timeScale = 0f;                      // Congela el juego
        Cursor.lockState = CursorLockMode.None;   // Devolvemos el mouse
        Cursor.visible = true;
    }

    private void Update()
    {
        // Con timeScale = 0, Update sigue corriendo, así que podemos leer la tecla R
        if (State != GameState.Playing
            && Keyboard.current != null
            && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void OnGUI()
    {
        if (State == GameState.Playing) return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = Color.white;
        }

        titleStyle.normal.textColor = State == GameState.Won ? Color.green : Color.red;

        float y = Screen.height * 0.35f;
        GUI.Label(new Rect(0, y, Screen.width, 80), message, titleStyle);
        GUI.Label(new Rect(0, y + 90, Screen.width, 40), "Presioná R para reiniciar", subStyle);
    }
}
