using UnityEngine;

public class TrapSpike : MonoBehaviour
{
    [SerializeField] private int damage = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (HealthManager.Instance != null)
            HealthManager.Instance.ChangeHealth(-damage);
    }
}
