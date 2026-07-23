using UnityEngine;

/// <summary>
/// 一次动画播放的参数。
/// 使用时建议从 AnimationPlayParameters.Default 开始修改需要的字段。
/// </summary>
[System.Serializable]
public struct AnimationPlayParameters
{
    /// <summary>动画播放速度倍率。1 为正常速度，0 为暂停。</summary>
    public float speedScale;

    /// <summary>动画开始播放时的归一化时间，范围为 0 到 1。</summary>
    public float startNormalizedTime;

    /// <summary>是否循环播放。循环播放不会触发完成回调。</summary>
    public bool loop;

    /// <summary>Animator Layer 索引，默认使用第 0 层。</summary>
    public int layer;

    /// <summary>
    /// 默认播放参数：正常速度、从头播放、不循环、使用第 0 层。
    /// </summary>
    public static AnimationPlayParameters Default => new AnimationPlayParameters
    {
        speedScale = 1f,
        startNormalizedTime = 0f,
        loop = false,
        layer = 0
    };
}
