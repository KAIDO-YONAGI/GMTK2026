using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家位置、血量与动作状态的单帧回溯数据。</summary>
public struct PlayerRewindSnapshot : IRewindSnapshot
{
    public float time;
    public Vector3 position;
    public int health;
    public PlayerActionState actionState;
    public bool facingLeft;

    public float Time => time;
}

/// <summary>
/// 玩家回溯快照容器。
/// 统一负责入队裁剪、已存时长计算，以及倒放数组的借还生命周期。
/// </summary>
public sealed class PlayerRewindHistory
{
    // 队头最旧、队尾最新；每次入队后按可回溯窗口裁掉过期快照。
    private readonly Queue<PlayerRewindSnapshot> snapshots = new Queue<PlayerRewindSnapshot>();
    private readonly ArrayPool<PlayerRewindSnapshot> playbackArrayPool = new ArrayPool<PlayerRewindSnapshot>();

    // 借出的倒放数组，倒放协程读完后归还对象池。
    private PlayerRewindSnapshot[] borrowedPlaybackArray;
    private float latestSnapshotTime;

    public int Count => snapshots.Count;

    /// <summary>当前已存档的可回溯时长（最新快照时间减最旧快照时间）。</summary>
    public float StoredSeconds
    {
        get
        {
            if (snapshots.Count == 0)
                return 0f;

            return Mathf.Max(0f, latestSnapshotTime - snapshots.Peek().time);
        }
    }

    /// <summary>入队新快照，并裁掉超出可回溯窗口的旧快照。</summary>
    public void Record(PlayerRewindSnapshot snapshot, float rewindableSeconds)
    {
        snapshots.Enqueue(snapshot);

        float expireBefore = snapshot.time - Mathf.Max(0f, rewindableSeconds);
        while (snapshots.Count > 0 && snapshots.Peek().time < expireBefore)
            snapshots.Dequeue();

        latestSnapshotTime = snapshot.time;
    }

    /// <summary>把队列快照拷入借出的数组供倒放协程顺序读取；再次借出前先归还旧数组。</summary>
    public PlayerRewindSnapshot[] BorrowPlayback(out int count)
    {
        count = snapshots.Count;
        if (count == 0)
            return null;

        ReleasePlayback();
        borrowedPlaybackArray = playbackArrayPool.Get(count);
        snapshots.CopyTo(borrowedPlaybackArray, 0);
        return borrowedPlaybackArray;
    }

    /// <summary>清空快照并归还借出的倒放数组（回溯冷却结束后调用）。</summary>
    public void Clear()
    {
        snapshots.Clear();
        latestSnapshotTime = 0f;
        ReleasePlayback();
    }

    private void ReleasePlayback()
    {
        if (borrowedPlaybackArray == null)
            return;

        playbackArrayPool.Return(borrowedPlaybackArray);
        borrowedPlaybackArray = null;
    }
}
