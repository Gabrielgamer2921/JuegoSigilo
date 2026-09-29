using UnityEngine;

/// <summary>
/// La meta del nivel. Es un trigger: cuando el Player entra, gana la partida.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Goal : MonoBehaviour
{
    // Reset se ejecuta una sola vez, al agregar el componente en el editor
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameManager.Instance != null)
            GameManager.Instance.Win();
    }
}
