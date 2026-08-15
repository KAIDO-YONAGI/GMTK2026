using UnityEngine;

/// <summary>
/// 仅供 Teleport 样例使用的水平移动控制器，不属于框架运行时。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SamplePlayerController : MonoBehaviour
{
    [Tooltip("样例玩家的水平移动速度。")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        body.velocity = new Vector2(horizontal * moveSpeed, body.velocity.y);
    }
}
