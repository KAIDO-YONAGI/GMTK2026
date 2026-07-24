using UnityEngine;

public class PickupSpike : MonoBehaviour
{
    private bool isEquipped;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isEquipped && other.CompareTag("Player"))
        {
            PickUp(other.transform);
            return;
        }

        if (isEquipped && other.TryGetComponent<Ice>(out _))
        {
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
    }

    private void PickUp(Transform player)
    {
        isEquipped = true;
        Transform headSlot = WearableManager.Instance != null ? WearableManager.Instance.SelfTransform : null;
        transform.SetParent(headSlot != null ? headSlot : player);
        transform.localPosition = Vector3.zero;
    }
}
