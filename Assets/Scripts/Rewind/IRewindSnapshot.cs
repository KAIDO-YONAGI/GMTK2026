/// <summary>
/// 回溯快照标记接口。所有快照结构体必须实现，提供时间戳。
/// 用作 RewindableBase 泛型方法的约束，保证裁剪/倒放算法能按时间戳操作。
/// </summary>
public interface IRewindSnapshot
{
    /// <summary>该快照记录时的游戏时间（Time.time）。</summary>
    float Time { get; }
}
