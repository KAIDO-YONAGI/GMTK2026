using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可回溯对象的通用实现基类（非泛型，Inspector 友好）。
/// 只负责回溯算法本身（倒放循环、裁剪、冷却框架），不持有任何时间窗口/倍速/衰老字段——
/// 这些是子类专属的（玩家用 agingRate 算窗口，机关可能用固定常量），由子类计算后作为参数传入。
/// 提供两个可复用的泛型算法零件：
///   - EnqueueAndTrim&lt;T&gt;：入队并按时间窗口裁剪过期快照
///   - RewindBackwards&lt;T&gt;：按真实时间倒放 + 冷却
/// 队列容器（Queue&lt;T&gt;）与具体快照类型由子类持有，符合项目「接口 + 抽象基类 + 具体子类」风格。
/// 参照 AnimationPlayerBase 的设计：公共逻辑放基类，具体捕获/还原/冻结通过抽象钩子交给子类。
/// </summary>
public abstract class RewindableBase : MonoBehaviour, IRewindable
{
    // 回溯倒放进行中标记。倒放结束后立即置 false，供 UI 和外部逻辑准确判断。
    protected bool isRewinding = false;

    // 回溯冷却标记：冷却期间不记录新快照，也不能再次启动回溯。
    protected bool isRewindCoolingDown = false;

    // 当前已倒回的游戏时间量（rewoundGameTime = elapsed × rewindSpeed）。回溯期间每帧更新，非回溯时为 0。
    protected float currentRewoundGameTime = 0f;

    /// <summary>当前是否正在倒放快照。</summary>
    public bool IsRewinding => isRewinding;

    /// <summary>当前已倒回的游戏时间量（秒）。倒放期间每帧更新；非倒放时为 0。</summary>
    public float CurrentRewoundGameTime => currentRewoundGameTime;

    /// <summary>当前是否处于倒放或冷却中。子类用此阻止记录和重复启动。</summary>
    protected bool IsRewindOperationLocked => isRewinding || isRewindCoolingDown;

    /// <summary>当前已记录的快照数量。由子类根据自身队列实现。</summary>
    public abstract int SnapshotCount { get; }

    // ===== IRewindable：记录与启动回溯的具体实现交给子类 =====
    public abstract void RecordSnapshot();
    public abstract void StartRewind();

    // ===== 子类必须实现的冻结/清理钩子（由 RewindBackwards 内部调用）=====
    /// <summary>回溯开始时冻结对象（如禁用输入、停止 AI）。在倒放循环前调用。</summary>
    protected abstract void OnFreeze();
    /// <summary>倒放循环结束时解冻对象（如恢复输入）。在冷却等待前调用。</summary>
    protected abstract void OnUnfreeze();
    /// <summary>冷却结束后清理已消费的快照队列。</summary>
    protected abstract void ClearSnapshots();
    /// <summary>当前是否应继续倒放。默认一直播放到最早快照，子类可按输入状态提前结束。</summary>
    protected virtual bool ShouldContinueRewind()
    {
        return true;
    }

    /// <summary>
    /// 入队一帧快照，并按可回溯时间窗口裁剪掉所有过期的旧快照。
    /// 子类在 FixedUpdate 里抓完快照后调用此方法。
    /// </summary>
    /// <typeparam name="T">快照类型，必须实现 IRewindSnapshot 提供 Time。</typeparam>
    /// <param name="queue">子类持有的快照队列。</param>
    /// <param name="snapshot">本帧抓取的快照（应已填入 Time = Time.time）。</param>
    /// <param name="rewindableSeconds">当前可回溯秒数（通常传 RewindableSeconds）。</param>
    protected static void EnqueueAndTrim<T>(Queue<T> queue, T snapshot, float rewindableSeconds) where T : struct, IRewindSnapshot
    {
        queue.Enqueue(snapshot);

        // 严格按可回溯时间裁剪：丢弃所有超出窗口的旧快照
        float expireBefore = snapshot.Time - rewindableSeconds;
        while (queue.Count > 0 && queue.Peek().Time < expireBefore)
        {
            queue.Dequeue();
        }
    }

    /// <summary>
    /// 按真实时间倒放的回溯协程。从最新快照向最早快照逐帧还原，rewindSpeed=1 时为 1:1 实时倒放。
    /// 倒放完成后立即结束回溯状态，再进入冷却（cooldownSeconds）。冷却期间不记录新快照。
    /// 时间参数（倍速、冷却）由子类传入，基类不持有任何时间窗口字段——机关等其它子类可传自己的常量。
    /// 子类在 StartRewind 里调 StartCoroutine(RewindBackwards(...))。
    /// </summary>
    /// <param name="snapshots">已转为数组的快照（通常 snapshots.ToArray()），按时间升序。</param>
    /// <param name="apply">把单个快照应用回对象的方法（通常是 ApplySnapshot）。</param>
    /// <param name="rewindSpeed">回溯播放倍速。1 = 1:1 实时倒放。</param>
    /// <param name="getMaxRewindSeconds">倒放期间实时获取当前可回溯上限的函数。</param>
    /// <param name="cooldownSeconds">倒放结束后、恢复记录前的冷却秒数。</param>
    protected IEnumerator RewindBackwards<T>(
        T[] snapshots,
        Action<T> apply,
        float rewindSpeed,
        Func<float> getMaxRewindSeconds,
        float cooldownSeconds) where T : struct, IRewindSnapshot
    {
        isRewinding = true;
        currentRewoundGameTime = 0f;
        OnFreeze();

        int idx = snapshots.Length - 1;
        float latestTime = snapshots[idx].Time;     // 回溯起点的游戏时间
        float rewindStartReal = Time.time;           // 回溯开始的真实时间

        // 先落到最新一帧
        apply(snapshots[idx]);

        // 按真实时间推进：elapsed × rewindSpeed = 已倒回的游戏时间量
        while (idx > 0)
        {
            yield return null;

            // 允许子类在按键松开等情况下提前结束倒放
            if (!ShouldContinueRewind())
            {
                // 松开即停：立即归零倒回时间，避免冷却期间的延迟残留
                currentRewoundGameTime = 0f;
                break;
            }

            float maxRewindSeconds = Mathf.Max(0f, getMaxRewindSeconds());
            float elapsedRewoundGameTime = (Time.time - rewindStartReal) * rewindSpeed;
            float desiredTargetTime = latestTime - elapsedRewoundGameTime;

            // 快照的有效范围相对当前时刻计算，倒放期间旧快照会持续过期。
            float oldestAllowedTime = Time.time - maxRewindSeconds;
            float targetTime = Mathf.Max(desiredTargetTime, oldestAllowedTime);
            currentRewoundGameTime = Mathf.Max(0f, Time.time - targetTime);

            // 每帧从完整快照数组定位，窗口缩短时可回到较新的有效快照。
            int targetIndex = FindFirstSnapshotAtOrAfter(snapshots, targetTime);
            if (targetIndex != idx)
            {
                idx = targetIndex;
                apply(snapshots[idx]);
            }

            // 当前窗口已用尽，不再访问任何更早的快照。
            if (desiredTargetTime <= oldestAllowedTime)
            {
                break;
            }
        }

        // ===== 倒放完成，进入冷却 =====

        // 倒放结束当帧解除回溯状态，避免 UI 和外部逻辑继续显示一秒的回溯。
        currentRewoundGameTime = 0f;
        isRewinding = false;

        // 倒放结束立即恢复输入；冷却期间只暂停记录新快照和重复触发。
        OnUnfreeze();
        isRewindCoolingDown = true;
        if (cooldownSeconds > 0f)
        {
            yield return new WaitForSeconds(cooldownSeconds);
        }

        // 冷却结束：清空已消费的队列，解除冷却锁。
        ClearSnapshots();
        isRewindCoolingDown = false;
    }

    /// <summary>返回时间戳不早于目标时间的第一个快照索引。</summary>
    private static int FindFirstSnapshotAtOrAfter<T>(T[] snapshots, float targetTime) where T : struct, IRewindSnapshot
    {
        int low = 0;
        int high = snapshots.Length - 1;
        int result = high;

        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (snapshots[middle].Time >= targetTime)
            {
                result = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return result;
    }
}
