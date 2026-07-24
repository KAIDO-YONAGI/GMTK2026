using UnityEngine;

/// <summary>
/// 玩家平台跳跃移动脚本。
/// 左右移动用 Input.GetAxisRaw("Horizontal")，跳跃用 Input.GetButtonDown("Jump")（Unity 默认轴，绑定 Space/W/上）。
/// 物理：Rigidbody2D Dynamic，通过 velocity 控制水平移动，保留 y 速度避免覆盖跳跃。
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;

    [Header("Input Events")]
    [SerializeField] private VoidEventSO startInputEvent;
    [SerializeField] private VoidEventSO stopInputEvent;

    [Header("Ground Check")]
    public LayerMask groundLayer;            // 地面 Layer（Ground/Platform 等）
    public Transform groundCheck;            // 脚下检测点（Player 底部）
    public float groundCheckRadius = 0.1f;   // 检测圆半径
    private float agingRate = 1f;

    Rigidbody2D rb;
    bool isGrounded;
    bool isInputEnabled = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (startInputEvent != null)
            startInputEvent.VoidEvent += OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent += OnStopInput;
    }

    void OnDisable()
    {
        if (startInputEvent != null)
            startInputEvent.VoidEvent -= OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent -= OnStopInput;
    }

    void OnStartInput()
    {
        isInputEnabled = true;
    }

    void OnStopInput()
    {
        isInputEnabled = false;

        // 清除最后一次水平输入留下的速度，竖直方向继续交给物理系统。
        if (rb != null)
            rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    void Update()
    {
        if (!isInputEnabled) return;

        // 地面检测：未配置 groundCheck 时假设始终在地面（demo 模式，玩家可空中跳）
        isGrounded = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : true;
        agingRate = 1f - AgeManager.instance.AgeNormalized;
        if (agingRate<0.3f)agingRate=0.3f;
        
        // 跳跃（仅地面时）
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce * agingRate);
        }
    }

    void FixedUpdate()
    {
        if (!isInputEnabled) return;

        // 水平移动：覆盖 x 速度，保留 y 速度（避免覆盖跳跃）
        float x = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector2(x * moveSpeed * agingRate, rb.velocity.y);
    }
}
