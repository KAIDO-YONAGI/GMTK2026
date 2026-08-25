using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家移动表现组件。
/// 负责朝向、移动与落地粒子，以及回溯加速 UI，不参与动作和物理决策。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerMovementFeedback : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ParticleSystem runParticleSystem;

    [Header("Landing Particles")]
    [SerializeField] private ParticleSystem landParticlePrefab;
    [SerializeField] private int landParticlePrewarmCount = 3;

    [Header("Speed Boost Feedback")]
    [SerializeField] private CanvasGroup speedUpFeedbackGroup;
    [SerializeField, Min(0f)] private float speedUpFadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float speedUpFadeOutDuration = 0.4f;

    private ObjectPool<ParticleSystem> landParticlePool;
    // 正在播放的落地粒子列表，场景重置时统一回收。
    private readonly List<ParticleSystem> activeLandParticles = new List<ParticleSystem>();
    private Coroutine speedUpFeedbackRoutine;
    // 朝向唯一权威值：回溯快照记录它，恢复时经 RestoreFacing 写回。
    private bool isFacingLeft;

    public bool IsFacingLeft => isFacingLeft;

    private void Awake()
    {
        ResolveComponents();
        // 初始朝向以 SpriteRenderer 当前翻转状态为准，保证与场景摆放一致。
        isFacingLeft = spriteRenderer != null && spriteRenderer.flipX;
        InitializeSpeedFeedback();
        SetupLandingParticlePool();
    }

    private void OnEnable()
    {
        ResolveComponents();

        if (playerMovement == null)
            return;

        playerMovement.Landed += PlayLandParticle;
        playerMovement.SpeedBoostStarted += PlaySpeedUpFeedback;
        playerMovement.StateReset += ResetFeedback;
    }

    private void OnDisable()
    {
        if (playerMovement != null)
        {
            playerMovement.Landed -= PlayLandParticle;
            playerMovement.SpeedBoostStarted -= PlaySpeedUpFeedback;
            playerMovement.StateReset -= ResetFeedback;
        }

        ResetFeedback();
    }

    /// <summary>每帧表现同步：朝向跟随输入，跑步粒子按移动状态开关。</summary>
    private void Update()
    {
        if (playerMovement == null)
            return;

        float horizontalInput = playerMovement.HorizontalInput;
        // 回溯冻结期间 CanMove 为 false，不会覆盖 RestoreFacing 恢复的朝向。
        if (playerMovement.CanMove && Mathf.Abs(horizontalInput) > 0.01f)
            SetFacingLeft(horizontalInput < 0f);

        bool shouldPlayRunParticles =
            playerMovement.CanMove &&
            playerMovement.IsGrounded &&
            Mathf.Abs(horizontalInput) > 0.01f;

        if (runParticleSystem == null)
            return;

        if (shouldPlayRunParticles)
        {
            if (!runParticleSystem.isPlaying)
                runParticleSystem.Play();
        }
        else if (runParticleSystem.isPlaying)
        {
            runParticleSystem.Stop();
        }
    }

    /// <summary>兜底解析组件。SpriteRenderer 只查玩家根对象（本项目挂在 Player 根上）。</summary>
    private void ResolveComponents()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
            playerMovement = transform.root.GetComponentInChildren<PlayerMovement>(true);
        if (spriteRenderer == null)
            spriteRenderer = transform.root.GetComponent<SpriteRenderer>();
    }

    /// <summary>回溯恢复时设置玩家朝向，不依赖当前输入。</summary>
    public void RestoreFacing(bool facingLeft)
    {
        SetFacingLeft(facingLeft);
    }

    /// <summary>同时写权威字段与 SpriteRenderer.flipX，保证逻辑与画面一致。</summary>
    private void SetFacingLeft(bool facingLeft)
    {
        isFacingLeft = facingLeft;
        if (spriteRenderer != null)
            spriteRenderer.flipX = facingLeft;
    }

    private void InitializeSpeedFeedback()
    {
        if (speedUpFeedbackGroup == null)
            return;

        speedUpFeedbackGroup.alpha = 0f;
        speedUpFeedbackGroup.interactable = false;
        speedUpFeedbackGroup.blocksRaycasts = false;
    }

    private void SetupLandingParticlePool()
    {
        if (landParticlePrefab == null)
            return;

        ParticleSystem.MainModule main = landParticlePrefab.main;
        main.useUnscaledTime = true;
        landParticlePool = new ObjectPool<ParticleSystem>(
            landParticlePrefab,
            landParticlePrewarmCount,
            transform);
    }

    /// <summary>落地粒子从对象池取用，脱离父级在世界空间播放，播完自动归还。</summary>
    private void PlayLandParticle()
    {
        if (landParticlePool == null)
            return;

        ParticleSystem particle = landParticlePool.Get();
        if (particle == null)
            return;

        particle.transform.SetParent(null);
        particle.transform.position = playerMovement.GroundPosition;
        particle.Play();
        activeLandParticles.Add(particle);
        StartCoroutine(ReturnLandParticleWhenDone(particle));
    }

    /// <summary>等粒子播完后归还对象池；ResetFeedback 的 StopAllCoroutines 会一并终止未完成的等待。</summary>
    private IEnumerator ReturnLandParticleWhenDone(ParticleSystem particle)
    {
        while (particle != null && particle.isPlaying)
            yield return null;

        if (particle != null)
            landParticlePool.Return(particle);
        activeLandParticles.Remove(particle);
    }

    private void PlaySpeedUpFeedback()
    {
        if (speedUpFeedbackGroup == null)
            return;

        if (speedUpFeedbackRoutine != null)
            StopCoroutine(speedUpFeedbackRoutine);

        speedUpFeedbackRoutine = StartCoroutine(PlaySpeedUpFeedbackRoutine());
    }

    /// <summary>加速 UI 流程：淡入 → 持续到 buff 结束 → 淡出。</summary>
    private IEnumerator PlaySpeedUpFeedbackRoutine()
    {
        speedUpFeedbackGroup.alpha = 0f;
        yield return FadeSpeedUpFeedback(0f, 1f, speedUpFadeInDuration);

        while (playerMovement != null && playerMovement.IsSpeedBoostActive)
            yield return null;

        yield return FadeSpeedUpFeedback(1f, 0f, speedUpFadeOutDuration);
        speedUpFeedbackRoutine = null;
    }

    /// <summary>用未缩放时间做淡入淡出，暂停/时间缩放时 UI 仍按真实时间过渡。</summary>
    private IEnumerator FadeSpeedUpFeedback(float startAlpha, float targetAlpha, float duration)
    {
        if (duration <= 0f)
        {
            speedUpFeedbackGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            speedUpFeedbackGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        speedUpFeedbackGroup.alpha = targetAlpha;
    }

    /// <summary>停掉所有表现（粒子、UI、协程）并回收活跃落地粒子，用于禁用与场景重置。</summary>
    private void ResetFeedback()
    {
        StopAllCoroutines();
        speedUpFeedbackRoutine = null;

        if (runParticleSystem != null)
            runParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (speedUpFeedbackGroup != null)
            speedUpFeedbackGroup.alpha = 0f;

        for (int i = activeLandParticles.Count - 1; i >= 0; i--)
        {
            ParticleSystem particle = activeLandParticles[i];
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            landParticlePool?.Return(particle);
        }

        activeLandParticles.Clear();
    }
}
