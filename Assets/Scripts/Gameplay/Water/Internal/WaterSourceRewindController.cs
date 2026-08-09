using System;
using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的快照管理器。
/// 负责时间窗口裁剪、快照捕获、回溯还原以及未来单元清理。
/// </summary>
internal sealed class WaterSourceRewindController
{
    private const float RecordingBoundaryOffset = 0.0001f;

    private readonly WaterSource owner;
    private readonly WaterSourceRuntimeState state;
    private readonly Func<float> getRewindableSeconds;
    private readonly Queue<WaterSourceSnapshot> snapshots =
        new Queue<WaterSourceSnapshot>();
    private readonly ArrayPool<WaterSourceSnapshot> snapshotArrayPool =
        new ArrayPool<WaterSourceSnapshot>();

    private WaterSourceSnapshot[] borrowedPlaybackArray;

    public int SnapshotCount => snapshots.Count;

    public WaterSourceRewindController(
        WaterSource owner,
        WaterSourceRuntimeState state,
        Func<float> getRewindableSeconds)
    {
        this.owner = owner;
        this.state = state;
        this.getRewindableSeconds = getRewindableSeconds;
    }

    public void BeginSnapshotRecording()
    {
        if (state.IsSnapshotRecording)
            return;

        EnqueueSnapshot(Time.time - RecordingBoundaryOffset);
        state.IsSnapshotRecording = true;
    }

    public void RecordSnapshot(bool isOperationLocked)
    {
        if (isOperationLocked || !state.IsSnapshotRecording)
            return;

        EnqueueSnapshot(Time.time);
    }

    public WaterSourceSnapshot[] BorrowPlayback(out int count)
    {
        count = snapshots.Count;
        if (count == 0)
            return null;

        ReleasePlaybackArray();
        borrowedPlaybackArray = snapshotArrayPool.Get(count);
        snapshots.CopyTo(borrowedPlaybackArray, 0);
        return borrowedPlaybackArray;
    }

    public void ApplySnapshot(WaterSourceSnapshot snapshot)
    {
        WaterSourceUnitSnapshot[] states = snapshot.UnitStates;
        int minLength = Mathf.Min(states.Length, state.Units.Count);

        for (int i = 0; i < minLength; i++)
        {
            WaterFlowUnit unit = state.Units[i];
            if (unit == null)
                continue;

            WaterSourceUnitSnapshot unitState = states[i];
            unit.gameObject.SetActive(true);
            unit.ForcePhase((WaterFlowUnit.WaterPhase)unitState.PhaseIndex);
            unit.transform.position = unitState.Position;
        }

        for (int i = minLength; i < state.Units.Count; i++)
        {
            if (state.Units[i] != null)
                state.Units[i].gameObject.SetActive(false);
        }

        state.HasFlowedOnce = snapshot.HasFlowedOnce;
        state.IsFlowing = snapshot.IsFlowing;
        state.StepCount = snapshot.StepCount;
        state.FreezeState = snapshot.FreezeState;
        state.IsSnapshotRecording = snapshot.IsSnapshotRecording;
    }

    public void HideUnitsBornAfter(float snapshotTime)
    {
        for (int i = 0; i < state.Units.Count; i++)
        {
            WaterFlowUnit unit = state.Units[i];
            if (unit != null && unit.BirthTime > snapshotTime)
                unit.gameObject.SetActive(false);
        }
    }

    public void RemoveDiscardedUnits()
    {
        for (int i = state.Units.Count - 1; i >= 0; i--)
        {
            WaterFlowUnit unit = state.Units[i];
            if (unit == null)
            {
                state.Units.RemoveAt(i);
                continue;
            }

            if (unit.gameObject.activeSelf)
                continue;

            state.Units.RemoveAt(i);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(unit.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(unit.gameObject);
        }
    }

    public void ClearSnapshots()
    {
        snapshots.Clear();
        ReleasePlaybackArray();
    }

    public void Dispose()
    {
        ClearSnapshots();
    }

    private void EnqueueSnapshot(float snapshotTime)
    {
        WaterSourceSnapshot snapshot = new WaterSourceSnapshot
        {
            Time = snapshotTime,
            UnitStates = CaptureAllUnits(),
            HasFlowedOnce = state.HasFlowedOnce,
            IsFlowing = state.IsFlowing,
            StepCount = state.StepCount,
            FreezeState = state.FreezeState,
            IsSnapshotRecording = state.IsSnapshotRecording
        };
        snapshots.Enqueue(snapshot);

        float expireBefore = snapshot.Time - Mathf.Max(0f, getRewindableSeconds());
        while (snapshots.Count > 0 && snapshots.Peek().Time < expireBefore)
        {
            snapshots.Dequeue();
        }
    }

    private WaterSourceUnitSnapshot[] CaptureAllUnits()
    {
        WaterSourceUnitSnapshot[] unitSnapshots =
            new WaterSourceUnitSnapshot[state.Units.Count];
        float unitStateTime = Time.time;

        for (int i = 0; i < state.Units.Count; i++)
        {
            WaterFlowUnit unit = state.Units[i];
            unitSnapshots[i] = new WaterSourceUnitSnapshot
            {
                Time = unitStateTime,
                Position = unit != null ? unit.transform.position : Vector3.zero,
                PhaseIndex = unit != null
                    ? (int)unit.Phase
                    : (int)WaterFlowUnit.WaterPhase.Broken,
                UnitBirthTime = unit != null ? unit.BirthTime : 0f
            };
        }

        return unitSnapshots;
    }

    private void ReleasePlaybackArray()
    {
        if (borrowedPlaybackArray == null)
            return;

        snapshotArrayPool.Return(borrowedPlaybackArray);
        borrowedPlaybackArray = null;
    }
}

internal struct WaterSourceUnitSnapshot : IRewindSnapshot
{
    public float Time { get; set; }
    public Vector3 Position;
    public int PhaseIndex;
    public float UnitBirthTime;
}

internal struct WaterSourceSnapshot : IRewindSnapshot
{
    public float Time { get; set; }
    public WaterSourceUnitSnapshot[] UnitStates;
    public bool HasFlowedOnce;
    public bool IsFlowing;
    public int StepCount;
    public FreezeState FreezeState;
    public bool IsSnapshotRecording;
}
