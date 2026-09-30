using UnityEngine;

/// <summary>
/// Zona de escondite. Es un trigger: cuando el jugador entra, PlayerHiding se entera
/// y le permite esconderse con la tecla E.
/// Este script solo marca el lugar; quien maneja el escondite es PlayerHiding.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
    // Punto donde queda el jugador al esconderse (el centro de la zona)
    public Vector3 HidePosition => transform.position;

    // Reset se ejecuta una sola vez, al agregar el componente en el editor
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerHiding player = other.GetComponentInParent<PlayerHiding>();
        if (player != null) player.SetNearbySpot(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerHiding player = other.GetComponentInParent<PlayerHiding>();
        if (player != null) player.ClearNearbySpot(this);
    }
}
