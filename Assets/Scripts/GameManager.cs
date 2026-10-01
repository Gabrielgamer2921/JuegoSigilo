using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Won, Lost }

    [Header("Reglas")]
    [SerializeField] private bool loseWhenDetected = false;

    public GameState State { get; private set; } = GameState.Playing;
    public string ResultMessage { get; private set; } = "";

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;

        if (FindFirstObjectByType<HudUI>() == null)
            gameObject.AddComponent<HudUI>();
    }

    public void Win()
    {
        End(GameState.Won, "¡GANASTE!");
    }

    public void Lose(string reason)
    {
        End(GameState.Lost, reason);
    }

    public void OnPlayerDetected()
    {
        if (loseWhenDetected)
            Lose("¡TE DETECTARON!");
    }

    private void End(GameState newState, string text)
    {
        if (State != GameState.Playing) return;

        State = newState;
        ResultMessage = text;

        AudioManager.PlayResult(newState == GameState.Won);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (State != GameState.Playing
            && Keyboard.current != null
            && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
