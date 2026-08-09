using UnityEngine;

/// <summary>
/// 使用 Unity 原生 Animator 播放动画的实现。
/// 该组件只需要和 Animator 挂在同一个 GameObject 上，不需要额外 Inspector 配置。
/// </summary>
/// <remarks>
/// 重要约定：传入的 AnimationClip.name 必须与 Animator Controller 中对应的 State.name 一致。
/// 例如 Attack.anim 对应的 Clip 名称为 Attack，Animator State 也必须命名为 Attack。
/// </remarks>
public class UnityAnimatorPlayer : AnimationPlayerBase
{
    private Animator animator;
    private int stateHash;
    private int activeLayer;

    private void Awake()
    {
        // UnityAnimatorPlayer 与 Animator 必须位于同一个 GameObject。
        animator = GetComponent<Animator>();
    }

    protected override bool PlayClip(AnimationClip clip, AnimationPlayParameters parameters)
    {
        // Animator.Play 接收的是 State，而不是 AnimationClip，因此使用 Clip 名称查找 State。
        if (!TryGetAnimator(parameters.layer, out int layer))
            return false;

        string stateName = clip.name;
        stateHash = Animator.StringToHash(animator.GetLayerName(layer) + "." + stateName);

        if (!animator.HasState(layer, stateHash))
        {
            // 播放失败通常表示 Animator State 没有按 Clip 名称命名，或 State 不在指定 Layer。
            Debug.LogError(
                $"Animation State '{stateName}' was not found on Animator layer {layer}. " +
                "The Animator State name must match the AnimationClip name.",
                this);
            return false;
        }

        activeLayer = layer;
        animator.speed = parameters.speedScale;
        animator.Play(stateHash, activeLayer, parameters.startNormalizedTime);
        return true;
    }

    protected override void StopClip()
    {
        if (animator != null)
            animator.speed = 0f;
    }

    protected override void PauseClip()
    {
        if (animator != null)
            animator.speed = 0f;
    }

    protected override void ResumeClip()
    {
        if (animator != null)
            animator.speed = SpeedScale;
    }

    protected override void SetSpeedScale(float value)
    {
        if (animator != null)
            animator.speed = value;
    }

    protected override float GetNormalizedTime()
    {
        // Animator 的 normalizedTime 在非循环动画结束时会达到 1 以上。
        if (!TryGetAnimator(activeLayer, out _))
            return 0f;

        return animator.GetCurrentAnimatorStateInfo(activeLayer).normalizedTime;
    }

    protected override bool IsPlaybackStateActive()
    {
        // 如果其他逻辑切换了 State，则当前播放视为被中断。
        if (!TryGetAnimator(activeLayer, out _))
            return false;

        return animator.GetCurrentAnimatorStateInfo(activeLayer).fullPathHash == stateHash;
    }

    protected override bool RestartClip(float normalizedTime)
    {
        // 循环动画每轮重新从指定归一化时间开始。
        if (!TryGetAnimator(activeLayer, out _))
            return false;

        animator.Play(stateHash, activeLayer, normalizedTime);
        return true;
    }

    private bool TryGetAnimator(int requestedLayer, out int layer)
    {
        layer = 0;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            // 允许组件在 Awake 时序变化后再次尝试获取 Animator。
            Debug.LogError("UnityAnimatorPlayer requires an Animator on the same GameObject.", this);
            return false;
        }

        if (animator.layerCount == 0)
            return false;

        layer = Mathf.Clamp(requestedLayer, 0, animator.layerCount - 1);
        return true;
    }
}
