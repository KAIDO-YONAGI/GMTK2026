using UnityEngine;

public class BounceSpring : MonoBehaviour
{
    [SerializeField] private float bounceForce = 15f;
    [SerializeField] private float cooldown = 0.2f;
    [SerializeField] private Animator animator;
    [SerializeField] private string bounceAnim = "Bounce";

    private float cooldownTimer;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        if (cooldownTimer > 0f) return;

        Rigidbody2D rb = collision.collider.attachedRigidbody;
        if (rb == null) return;

        Vector3 velocity = rb.velocity;
        Vector3 alongSpring = Vector3.Project(velocity, transform.up);
        Vector3 perpendicular = velocity - alongSpring;
        rb.velocity = perpendicular + transform.up * bounceForce;

        cooldownTimer = cooldown;

        if (animator != null)
            animator.Play(bounceAnim, -1, 0f);
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }
}
