using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动画播放器的通用实现基类。
/// 负责播放状态、速度、暂停/恢复、归一化时间回调和完成回调管理。
/// Unity 具体动画组件的调用由子类实现。
/// </summary>
public abstract class AnimationPlayerBase : MonoBehaviour, IAnimationPlayer
{
    // 归一化时间回调的内部记录。
    private sealed class NormalizedTimeCallback
    {
        public float normalizedTime;
        public Action callback;
        public bool once;
        public bool triggered;
    }

    private readonly List<NormalizedTimeCallback> normalizedTimeCallbacks =
        new List<NormalizedTimeCallback>();

    private Action completeCallback;
    private AnimationClip currentClip;
    private AnimationPlayParameters currentParameters;
    private float speedScale = 1f;
    private float previousNormalizedTime;
    private bool isPlaying;
    private bool isPaused;

    /// <summary>当前是否正在播放动画。</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>当前播放或最近一次播放的 Clip。</summary>
    public AnimationClip CurrentClip => currentClip;

    /// <summary>
    /// 当前播放速度倍率。设置为 0 可以暂停动画。
    /// 具体速度应用由子类的 SetSpeedScale 实现。
    /// </summary>
    public float SpeedScale
    {
        get => speedScale;
        set
        {
            speedScale = Mathf.Max(0f, value);
            if (isPlaying && !isPaused)
                SetSpeedScale(speedScale);
        }
    }

    /// <summary>使用默认参数播放一段动画。</summary>
    public bool Play(AnimationClip clip)
    {
        return Play(clip, AnimationPlayParameters.Default);
    }

    /// <summary>
    /// 使用指定参数播放一段动画。
    /// 新的播放会停止旧播放并清理旧回调。
    /// </summary>
    public bool Play(AnimationClip clip, AnimationPlayParameters parameters)
    {
        if (clip == null)
            return false;

        Stop();

        currentParameters = parameters;
        currentParameters.speedScale = Mathf.Max(0f, currentParameters.speedScale);
        currentParameters.startNormalizedTime = Mathf.Clamp01(currentParameters.startNormalizedTime);
        currentParameters.layer = Mathf.Max(0, currentParameters.layer);
        speedScale = currentParameters.speedScale;
        previousNormalizedTime = currentParameters.startNormalizedTime - 0.0001f;

        if (!PlayClip(clip, currentParameters))
        {
            currentClip = null;
            return false;
        }

        currentClip = clip;
        isPlaying = true;
        isPaused = false;
        return true;
    }

    public void Stop()
    {
        if (currentClip != null)
            StopClip();

        isPlaying = false;
        isPaused = false;
        currentClip = null;
        ClearCallbacks();
    }

    public void Pause()
    {
        if (!isPlaying || isPaused)
            return;

        PauseClip();
        isPaused = true;
    }

    public void Resume()
    {
        if (!isPlaying || !isPaused)
            return;

        ResumeClip();
        isPaused = false;
    }

    public void AddNormalizedTimeCallback(float normalizedTime, Action callback, bool once = true)
    {
        if (callback == null)
            return;

        normalizedTimeCallbacks.Add(new NormalizedTimeCallback
        {
            normalizedTime = Mathf.Clamp01(normalizedTime),
            callback = callback,
            once = once
        });
    }

    public void AddCompleteCallback(Action callback)
    {
        if (callback != null)
            completeCallback += callback;
    }

    public void ClearCallbacks()
    {
        normalizedTimeCallbacks.Clear();
        completeCallback = null;
    }

    protected virtual void Update()
    {
        if (!isPlaying || isPaused)
            return;

        if (!IsPlaybackStateActive())
        {
            InterruptPlayback();
            return;
        }

        // 使用跨越判断而不是判断某一帧是否刚好等于目标时间，避免低帧率时漏掉回调。
        float normalizedTime = Mathf.Max(0f, GetNormalizedTime());
        InvokeNormalizedTimeCallbacks(normalizedTime);

        if (!isPlaying)
            return;

        if (currentParameters.loop)
        {
            // Animator 的 normalizedTime 到达 1 后重新从 0 播放，形成代码层循环。
            if (normalizedTime >= 1f)
            {
                if (!RestartClip(0f))
                {
                    InterruptPlayback();
                    return;
                }

                previousNormalizedTime = -0.0001f;
            }
            else
            {
                previousNormalizedTime = normalizedTime;
            }

            return;
        }

        if (normalizedTime >= 1f)
        {
            // 非循环动画结束后停在最后一帧。
            // freezeOnComplete=true 时调 StopClip()（设 speed=0，冻结渲染）；
            // false 时让 Animator 自然保持最后一帧（用于跳跃/落地等预览动画）。
            if (currentParameters.freezeOnComplete)
                StopClip();
            isPlaying = false;
            isPaused = false;

            Action callback = completeCallback;
            ClearCallbacks();
            callback?.Invoke();
            return;
        }

        previousNormalizedTime = normalizedTime;
    }

    protected virtual void OnDisable()
    {
        Stop();
    }

    private void InvokeNormalizedTimeCallbacks(float currentNormalizedTime)
    {
        if (normalizedTimeCallbacks.Count == 0)
            return;

        // 复制列表，允许回调自身清理或追加回调。
        NormalizedTimeCallback[] callbacks = normalizedTimeCallbacks.ToArray();
        int firstCycle = Mathf.FloorToInt(previousNormalizedTime);
        int lastCycle = Mathf.FloorToInt(currentNormalizedTime);

        foreach (NormalizedTimeCallback entry in callbacks)
        {
            if (!normalizedTimeCallbacks.Contains(entry) || entry.callback == null)
                continue;

            if (entry.once && entry.triggered)
                continue;

            for (int cycle = firstCycle; cycle <= lastCycle; cycle++)
            {
                float targetTime = cycle + entry.normalizedTime;
                if (targetTime <= previousNormalizedTime || targetTime > currentNormalizedTime)
                    continue;

                if (entry.once)
                    entry.triggered = true;

                entry.callback.Invoke();
                break;
            }
        }
    }

    private void InterruptPlayback()
    {
        isPlaying = false;
        isPaused = false;
        currentClip = null;
        ClearCallbacks();
    }

    // 以下抽象方法由具体播放后端实现，例如 UnityAnimatorPlayer。
    protected abstract bool PlayClip(AnimationClip clip, AnimationPlayParameters parameters);
    protected abstract void StopClip();
    protected abstract void PauseClip();
    protected abstract void ResumeClip();
    protected abstract void SetSpeedScale(float value);
    protected abstract float GetNormalizedTime();
    protected abstract bool IsPlaybackStateActive();
    protected abstract bool RestartClip(float normalizedTime);
}
