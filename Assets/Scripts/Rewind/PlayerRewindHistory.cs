using System.Collections.Generic;
using UnityEngine;

/// <summary>玩家位置与血量的单帧回溯数据。</summary>
public struct PlayerRewindSnapshot : IRewindSnapshot
{
    public float time;
    public Vector3 position;
    public int health;

    public float Time => time;
}

/// <summary>
/// 玩家回溯快照容器。
/// 统一负责入队裁剪、已存时长计算，以及倒放数组的借还生命周期。
/// </summary>
public sealed class PlayerRewindHistory
{
    private readonly Queue<PlayerRewindSnapshot> snapshots = new Queue<PlayerRewindSnapshot>();
    private readonly ArrayPool<PlayerRewindSnapshot> playbackArrayPool = new ArrayPool<PlayerRewindSnapshot>();

    private PlayerRewindSnapshot[] borrowedPlaybackArray;
    private float latestSnapshotTime;

    public int Count => snapshots.Count;

    public float StoredSeconds
    {
        get
        {
            if (snapshots.Count == 0)
                return 0f;

            return Mathf.Max(0f, latestSnapshotTime - snapshots.Peek().time);
        }
    }

    public void Record(PlayerRewindSnapshot snapshot, float rewindableSeconds)
    {
        snapshots.Enqueue(snapshot);

        float expireBefore = snapshot.time - Mathf.Max(0f, rewindableSeconds);
        while (snapshots.Count > 0 && snapshots.Peek().time < expireBefore)
            snapshots.Dequeue();

        latestSnapshotTime = snapshot.time;
    }

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
