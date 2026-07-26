using System.Collections;
using System.Collections.Generic;
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
    // 刚体通过 Inspector 拖拽引用，允许移动脚本挂在 Player 的子物体上。
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private ParticleSystem runParticleSystem;

    [Header("Landing Particle")]
    [SerializeField] private ParticleSystem landParticlePrefab;
    [SerializeField] private int landParticlePrewarmCount = 3;
    private ObjectPool<ParticleSystem> landParticlePool;
    private readonly List<ParticleSystem> activeLandParticles = new();

    [Header("Input Events")]
    [SerializeField] private VoidEventSO startInputEvent;
    [SerializeField] private VoidEventSO stopInputEvent;

    [Header("Scene Load")]
    [Tooltip("场景加载完成事件。每次切场景后清空刚体速度，避免上一场景的残留速度带到新场景。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("Rewind Speed Boost")]
    [SerializeField] private VoidEventSO reincarnationEndEvent;
    [Tooltip("Speed multiplier after rewind. 1.5 means 150% of the normal speed.")]
    [SerializeField, Min(1f)] private float rewindSpeedMultiplier = 1.5f;
    [Tooltip("Speed boost duration after rewind, in seconds.")]
    [SerializeField, Min(0f)] private float rewindSpeedBoostDuration = 2f;

    [Header("Speed Up Feedback")]
    [SerializeField] private CanvasGroup speedUpFeedbackGroup;
    [SerializeField, Min(0f)] private float speedUpFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float speedUpFadeOutDuration = 0.4f;

    [Header("Sprite")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Ground Check")]
    public LayerMask groundLayer;            // 地面 Layer（Ground/Platform 等）
    public Transform groundCheck;            // 脚下检测点（Player 底部）
    public float groundCheckRadius = 0.1f;   // 检测圆半径
    private float agingRate = 1f;
    
    bool isGrounded;
    bool wasGrounded = true;
    public bool IsGrounded => isGrounded;
    public float HorizontalInput => Input.GetAxisRaw("Horizontal");
    private bool pendingJump;
    public bool TryConsumeJump()
    {
        if (!pendingJump) return false;
        pendingJump = false;
        return true;
    }
    bool isInputEnabled = true;
    float speedBoostEndTime = float.NegativeInfinity;
    Coroutine speedUpFeedbackRoutine;

    /// <summary>
    /// 回溯加速 buff 当前是否生效。
    /// 这是 buff 状态的单一判断入口：所有读取 buff 是否激活的地方都走这里，
    /// 不要再用「倍率 &gt; 1」之类的反推条件，避免判定标准分裂。
    /// </summary>
    public bool IsSpeedBoostActive => Time.time < speedBoostEndTime;

    /// <summary>
    /// 当前水平移动速度，包含衰老减速、回溯加速倍率和濒死减速。
    /// </summary>
    public float CurrentMoveSpeed
    {
        get => moveSpeed * agingRate * GetSpeedMultiplier() * GetDyingMultiplier();
    }

    /// <summary>
    /// 濒死状态下的移动/跳跃倍率。读 HealthManager.IsDying，
    /// 作为衰老减速和加速 buff 之外的独立倍率，在 agingRate 的 0.3 下限钳制之后生效。
    /// </summary>
    float GetDyingMultiplier()
    {
        return HealthManager.Instance != null && HealthManager.Instance.IsDying ? 0.1f : 1f;
    }

    void Awake()
    {
        // 反馈画布始终保持激活，只通过 CanvasGroup 控制可见性。
        // 这样不会因为 SetActive 影响反馈协程或其他 UI 组件。
        if (speedUpFeedbackGroup != null)
        {
            speedUpFeedbackGroup.alpha = 0f;
            speedUpFeedbackGroup.interactable = false;
            speedUpFeedbackGroup.blocksRaycasts = false;
        }
        SetupLandingParticlePool();
    }

    private void SetupLandingParticlePool()
    {
        if (landParticlePrefab == null) return;

        var main = landParticlePrefab.main;
        main.useUnscaledTime = true;

        landParticlePool = new ObjectPool<ParticleSystem>(landParticlePrefab, landParticlePrewarmCount, transform);
    }

    void OnEnable()
    {
        // 回溯系统和输入系统通过 VoidEventSO 通知移动脚本状态变化。
        if (startInputEvent != null)
            startInputEvent.VoidEvent += OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent += OnStopInput;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
        // 监听场景加载完成事件：每次切场景清空刚体速度，防止残留位移。
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 组件禁用时解除所有事件订阅，避免事件资产继续调用已禁用对象。
        if (startInputEvent != null)
            startInputEvent.VoidEvent -= OnStartInput;
        if (stopInputEvent != null)
            stopInputEvent.VoidEvent -= OnStopInput;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;

        // 离开场景或禁用组件时，清除加速状态和反馈显示。
        speedBoostEndTime = float.NegativeInfinity;
        speedUpFeedbackRoutine = null;

        if (speedUpFeedbackGroup != null)
            speedUpFeedbackGroup.alpha = 0f;
    }

    void OnStartInput()
    {
        // 回溯结束后由事件恢复玩家输入。
        isInputEnabled = true;
    }

    void OnStopInput()
    {
        // 回溯开始时停止读取输入，但保留竖直方向的物理速度。
        isInputEnabled = false;

        // 清除最后一次水平输入留下的速度，竖直方向继续交给物理系统。
        if (rb != null)
            rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    void OnReincarnationEnd()
    {
        // 加速 buff 生效期间不重复触发：避免多次回溯结束导致 buff 时间不断被刷新/延长。
        if (IsSpeedBoostActive)
            return;

        // 加速时间从回溯结束这一刻开始计算。
        speedBoostEndTime = Time.time + rewindSpeedBoostDuration;

        if (speedUpFeedbackGroup == null)
            return;

        // 如果上一次反馈还没结束，先停止它，再从淡入重新播放。
        if (speedUpFeedbackRoutine != null)
            StopCoroutine(speedUpFeedbackRoutine);

        speedUpFeedbackRoutine = StartCoroutine(PlaySpeedUpFeedback());
    }

    void OnSceneLoaded()
    {
        // 每次切场景加载完成后清空上一场景残留的移动状态。
        isInputEnabled = true;
        pendingJump = false;
        isGrounded = false;
        wasGrounded = true;
        agingRate = 1f;
        speedBoostEndTime = float.NegativeInfinity;

        if (rb != null)
            rb.velocity = Vector2.zero;

        if (runParticleSystem != null)
            runParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        StopAllCoroutines();
        speedUpFeedbackRoutine = null;

        if (speedUpFeedbackGroup != null)
        {
            speedUpFeedbackGroup.alpha = 0f;
            speedUpFeedbackGroup.interactable = false;
            speedUpFeedbackGroup.blocksRaycasts = false;
        }

        for (int i = activeLandParticles.Count - 1; i >= 0; i--)
        {
            var ps = activeLandParticles[i];
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                landParticlePool?.Return(ps);
            }
        }
        activeLandParticles.Clear();
    }

    void Update()
    {
        if (!isInputEnabled) return;

        // Update 负责读取输入和刷新移动状态，不直接写入刚体速度。
        UpdateMovementState();
        HandleJumpInput();
    }

    void FixedUpdate()
    {
        if (!isInputEnabled) return;

        // 刚体速度只在物理帧中写入，避免和物理系统在同一帧竞争。
        ApplyMovementVelocity();
    }

    void UpdateMovementState()
    {
        if (HealthManager.Instance.IsDead && HealthManager.Instance.CurrentHealth <= 0f)
        {
            // 死亡时不再处理移动状态，避免残留速度。
            if (rb != null)
                rb.velocity = Vector2.zero;
            return;
        }
        
        // 地面检测：未配置 groundCheck 时假设始终在地面（demo 模式，玩家可空中跳）
        isGrounded = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : true;

        if (!wasGrounded && isGrounded)
            PlayLandParticle();
        wasGrounded = isGrounded;

        agingRate = 1f - AgeManager.instance.AgeNormalized;
        agingRate = Mathf.Max(0.4f, Mathf.Sqrt(agingRate));
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        if (spriteRenderer != null && horizontal != 0f)
            spriteRenderer.flipX = horizontal < 0f;

        if(isGrounded && horizontal != 0)
        {
            if(!runParticleSystem.isPlaying)
                runParticleSystem.Play();
        }
        else
        {
            if(runParticleSystem.isPlaying)
                runParticleSystem.Stop();
        }

    }

    private void PlayLandParticle()
    {
        if (landParticlePool == null) return;
        ParticleSystem ps = landParticlePool.Get();
        if (ps == null) return;

        ps.transform.SetParent(null);
        ps.transform.position = groundCheck != null ? groundCheck.position : transform.position;
        ps.Play();
        activeLandParticles.Add(ps);
        StartCoroutine(ReturnLandParticleWhenDone(ps));
    }

    private IEnumerator ReturnLandParticleWhenDone(ParticleSystem ps)
    {
        while (ps.isPlaying)
            yield return null;

        landParticlePool.Return(ps);
        activeLandParticles.Remove(ps);
    }

    void HandleJumpInput()
    {
        if (HealthManager.Instance.IsDead && HealthManager.Instance.CurrentHealth <= 0f)
        {
            // 死亡时不再处理移动状态，避免残留速度。
            if (rb != null)
                rb.velocity = Vector2.zero;
            return;
        }
        
        if (rb == null || !Input.GetButtonDown("Jump") || !isGrounded)
            return;

        pendingJump = true;
    }

    public void ExecuteJump()
    {
        if (rb == null) return;

        Vector2 velocity = rb.velocity;
        velocity.y = jumpForce * agingRate * GetDyingMultiplier();
        rb.velocity = velocity;
    }

    void ApplyMovementVelocity()
    {
        if (HealthManager.Instance.IsDead && HealthManager.Instance.CurrentHealth <= 0f)
        {
            // 死亡时不再处理移动状态，避免残留速度。
            if (rb != null)
                rb.velocity = Vector2.zero;
            return;
        }
        
        if (rb == null)
            return;

        // 水平速度由输入、当前年龄减速和回溯加速倍率共同决定。
        // y 速度交给物理系统保留，避免覆盖重力和跳跃。
        float x = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector2(x * CurrentMoveSpeed, rb.velocity.y);
    }

    float GetSpeedMultiplier()
    {
        // 倍率本身只描述「生效时是几倍」，是否生效交给 IsSpeedBoostActive。
        return IsSpeedBoostActive
            ? Mathf.Max(1f, rewindSpeedMultiplier)
            : 1f;
    }

    IEnumerator PlaySpeedUpFeedback()
    {
        // 文字淡入后保持显示，直到真实速度 Buff 结束，再淡出。
        speedUpFeedbackGroup.alpha = 0f;
        yield return FadeSpeedUpFeedback(0f, 1f, speedUpFadeInDuration);

        while (IsSpeedBoostActive)
            yield return null;

        yield return FadeSpeedUpFeedback(1f, 0f, speedUpFadeOutDuration);
        speedUpFeedbackRoutine = null;
    }

    IEnumerator FadeSpeedUpFeedback(float startAlpha, float targetAlpha, float duration)
    {
        // 支持时长为 0 的配置，避免除零并允许瞬间切换透明度。
        if (duration <= 0f)
        {
            speedUpFeedbackGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 使用非缩放时间，让 UI 淡入淡出不受暂停影响。
            elapsed += Time.unscaledDeltaTime;
            speedUpFeedbackGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        speedUpFeedbackGroup.alpha = targetAlpha;
    }
}
