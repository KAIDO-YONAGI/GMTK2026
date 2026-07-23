/// <summary>
/// 可回溯对象的统一接口。具体快照类型与捕获/还原逻辑由实现类决定。
/// 参照项目已有的 IAnimationPlayer 模式：接口定义契约，RewindableBase 提供通用实现。
/// </summary>
public interface IRewindable
{
    /// <summary>当前是否处于回溯或冷却阶段。回溯期间不记录新快照。</summary>
    bool IsRewinding { get; }

    /// <summary>当前已记录的快照数量。</summary>
    int SnapshotCount { get; }

    /// <summary>记录一帧快照。通常在 FixedUpdate 调用。</summary>
    void RecordSnapshot();

    /// <summary>启动一次回溯。通常由按键或事件触发。</summary>
    void StartRewind();
}
