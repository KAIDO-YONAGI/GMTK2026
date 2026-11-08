using System;
using UnityEngine;

/// <summary>
/// 玩家物理执行组件。
/// 只负责水平速度、跳跃冲量、地面检测与衰老修正；输入采样在 PlayerActionController，
/// 朝向与粒子等表现在 PlayerMovementFeedback。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Rigidbody2D rb;

    [Header("地面检测")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.1f;

    [Header("事件")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;

    [Header("回溯加速")]
    [SerializeField] private float rewindSpeedMultiplier = 1.5f;
    [SerializeField] private float rewindSpeedBoostDuration = 2f;

    // 由 PlayerActionController 每帧写入的缓存输入，物理与表现组件统一读这一份。
    private float horizontalInput;
    // 衰老速度系数：年龄越大移动与跳跃越弱，下限 0.4 防止彻底动不了。
    private float agingRate;
    private bool isGrounded;
    private bool wasGrounded;
    // 输入总开关：回溯冻结时关闭；死亡闸门见 CanMove。
    private bool inputEnabled = true;
    // 回溯结束后的加速 buff 截止时间。
    private float speedBoostEndTime;

    public event Action Landed;
    public event Action SpeedBoostStarted;
    public event Action StateReset;

    public bool IsGrounded => isGrounded;
    public float HorizontalInput => horizontalInput;
    public bool IsInputEnabled => inputEnabled;
    public float AgingRate => agingRate;
    public float SpeedBoostEndTime => speedBoostEndTime;
    public float SpeedBoostRemaining => Mathf.Max(0f, speedBoostEndTime - Time.time);
    public bool IsSpeedBoostActive => Time.time < speedBoostEndTime;
    // 死亡与回溯冻结共用这道闸：移动、跳跃、朝向跟随都要先过它。
    public bool CanMove => inputEnabled && !IsPlayerDead();
    public Vector3 GroundPosition => groundCheck != null ? groundCheck.position : transform.position;
    public float CurrentMoveSpeed => moveSpeed * GetAgeSpeedMultiplier() *
                                     (IsSpeedBoostActive ? Mathf.Max(1f, rewindSpeedMultiplier) : 1f) *
                                     GetDyingMultiplier();

    private void Awake()
    {
        RefreshGroundedState();
        wasGrounded = isGrounded;
    }

    private void OnEnable()
    {
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;

        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnded;
    }

    private void OnDisable()
    {
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;

        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnded;

        horizontalInput = 0f;
        StateReset?.Invoke();
    }

    /// <summary>每帧刷新地面状态与衰老系数，并用接地边沿触发落地事件。</summary>
    private void Update()
    {
        RefreshGroundedState();
        float ageNormalized = AgeManager.Instance != null ? AgeManager.Instance.AgeNormalized : 0f;
        agingRate = Mathf.Max(0.4f, Mathf.Sqrt(1f - ageNormalized));

        // 用上一帧接地状态做边沿判断，只在"离地→落地"瞬间触发一次。
        if (CanMove && isGrounded && !wasGrounded)
        {
            Landed?.Invoke();
        }

        wasGrounded = isGrounded;
    }

    /// <summary>物理帧只写水平速度并保留垂直速度，跳跃/下落完全交给重力。</summary>
    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        float targetVelocityX = CanMove ? horizontalInput * CurrentMoveSpeed : 0f;
        rb.velocity = new Vector2(targetVelocityX, rb.velocity.y);
    }

    /// <summary>接收外部采样的水平输入；不可移动时一律钳为 0，防止残留输入驱动角色。</summary>
    public void SetHorizontalInput(float value)
    {
        horizontalInput = CanMove ? Mathf.Clamp(value, -1f, 1f) : 0f;
    }

    /// <summary>输入开关（回溯冻结/解冻用）；关闭时同步清空缓存输入。</summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            horizontalInput = 0f;
        }
    }

    /// <summary>施加跳跃冲量。跳跃力随衰老与濒死衰减；失败（死亡/离地）返回 false 交由调用方回退。</summary>
    public bool ExecuteJump()
    {
        if (rb == null || !CanMove || !isGrounded)
        {
            return false;
        }

        Vector2 velocity = rb.velocity;
        velocity.y = jumpForce * agingRate * GetDyingMultiplier();
        rb.velocity = velocity;
        isGrounded = false;
        return true;
    }

    /// <summary>以地面检测点为圆心做重叠检测刷新接地状态。</summary>
    private void RefreshGroundedState()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    /// <summary>年龄对移速的乘数（与跳跃力共用 agingRate）。</summary>
    private float GetAgeSpeedMultiplier()
    {
        return agingRate;
    }

    /// <summary>濒死（血量归零待自救）时移速与跳跃力骤降。</summary>
    private static float GetDyingMultiplier()
    {
        return HealthManager.Instance != null && HealthManager.Instance.IsDying ? 0.1f : 1f;
    }

    private static bool IsPlayerDead()
    {
        return HealthManager.Instance != null && HealthManager.Instance.IsDead;
    }

    /// <summary>场景加载完成：清空输入、速度与加速 buff，防止 retry 后残留上一局状态。</summary>
    private void OnSceneLoaded()
    {
        horizontalInput = 0f;
        speedBoostEndTime = 0f;
        agingRate = 1f;
        if (rb != null)
            rb.velocity = Vector2.zero;
        RefreshGroundedState();
        wasGrounded = isGrounded;
        StateReset?.Invoke();
    }

    /// <summary>回溯结束后给予一段加速 buff；buff 未结束时重复回溯不叠加。</summary>
    private void OnReincarnationEnded()
    {
        if (IsSpeedBoostActive)
            return;

        speedBoostEndTime = Time.time + rewindSpeedBoostDuration;
        SpeedBoostStarted?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
