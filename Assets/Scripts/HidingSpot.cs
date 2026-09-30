using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
    public Vector3 HidePosition => transform.position;

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
