using System;
using UnityEngine;

/// <summary>
/// 动画播放器的统一接口。具体播放方式由实现类决定。
/// </summary>
public interface IAnimationPlayer
{
    // Play 会清理上一段播放遗留的回调，因此应在 Play 之后注册回调。
    bool Play(AnimationClip clip);
    bool Play(AnimationClip clip, AnimationPlayParameters parameters = default);

    /// <summary>停止当前动画并清理回调，不触发完成回调。</summary>
    void Stop();

    /// <summary>暂停当前动画，保留当前位置和回调。</summary>
    void Pause();

    /// <summary>从暂停位置继续播放当前动画。</summary>
    void Resume();

    /// <summary>当前是否处于播放状态。</summary>
    bool IsPlaying { get; }

    /// <summary>当前播放或最近一次播放的动画 Clip。</summary>
    AnimationClip CurrentClip { get; }

    /// <summary>当前动画的播放速度倍率。</summary>
    float SpeedScale { get; set; }

    /// <summary>
    /// 在动画跨过指定归一化时间时触发回调。
    /// once 为 true 时只触发一次，为 false 时循环动画每轮都可触发。
    /// </summary>
    void AddNormalizedTimeCallback(float normalizedTime, Action callback, bool once = true);

    /// <summary>在非循环动画自然播放结束时触发回调。</summary>
    void AddCompleteCallback(Action callback);

    /// <summary>只清理回调，不停止当前动画。</summary>
    void ClearCallbacks();
}
