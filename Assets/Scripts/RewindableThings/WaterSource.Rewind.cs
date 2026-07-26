using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// WaterSource 的回溯职责：快照捕获、倒放还原、轮回事件联动与未来单元清理。
/// </summary>
public partial class WaterSource
{
    // 把“未记录”状态放在触发边界前，确保倒放算法能准确跨回该状态。
    private const float RecordingBoundaryOffset = 0.0001f;

    // ===== 快照结构 =====
    // 单单元快照（值类型，避免 GC）
    private struct UnitSnapshot : IRewindSnapshot
    {
        public float time;          // 此单元最新一次状态转变时间（用于时间窗口裁剪）
        public Vector3 position;    // 单元世界坐标
        public int phaseIndex;      // 相位（int，方便 struct 携带）
        public float unitBirthTime; // 该单元出生时间（回溯时判断是否应隐藏）

        public float Time => time;
    }

    // 水源整体快照（每次状态转变时记录整张单元表 + 水源级状态）
    private struct WaterSourceSnapshot : IRewindSnapshot
    {
        public float time;                  // 记录时刻
        public UnitSnapshot[] unitStates;   // 全部单元状态快照
        // 水源级状态：回溯时要一并还原，决定"回溯后是否继续流/能否再被触发"
        public bool hasFlowedOnce;          // 是否已触发过流水
        public bool isFlowing;              // 是否正在流
        public int stepCount;               // 当前向下生成的格数（决定继续流的位置）
        public FreezeState freezeState;     // 冻结状态机（Idle/Requested/Frozen）
        public bool isSnapshotRecording;    // 此时是否已开启水源快照记录

        public float Time => time;
    }

    private readonly Queue<WaterSourceSnapshot> snapshots = new Queue<WaterSourceSnapshot>();

    // 倒放数组对象池：复用 StartRewind 时由队列转出的数组，消除每次回溯的分配。
    private readonly ArrayPool<WaterSourceSnapshot> snapshotArrayPool = new ArrayPool<WaterSourceSnapshot>();
    private WaterSourceSnapshot[] borrowedPlaybackArray;

    /// <summary>
    /// Trigger 首次生效时开启记录。
    /// 开启前先补一帧边界快照，使回溯可以恢复到“尚未记录、尚未流水”的状态。
    /// </summary>
    private void BeginSnapshotRecording()
    {
        if (isSnapshotRecording) return;

        EnqueueSnapshot(Time.time - RecordingBoundaryOffset);
        isSnapshotRecording = true;
    }

    // ===== 事件订阅 =====
    private void OnEnable()
    {
        if (reincarnationBegEvent != null) reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null) reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
        if (snapshotAppliedEvent != null) snapshotAppliedEvent.FloatEvent += OnSnapshotApplied;
    }

    private void OnDisable()
    {
        if (reincarnationBegEvent != null) reincarnationBegEvent.VoidEvent -= OnReincarnationBeg;
        if (reincarnationEndEvent != null) reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
        if (snapshotAppliedEvent != null) snapshotAppliedEvent.FloatEvent -= OnSnapshotApplied;
    }

    /// <summary>
    /// Trigger 开启记录后，每物理帧记录快照。
    /// 回溯到 Trigger 边界前时，isSnapshotRecording 会恢复为 false，此后停止记录，
    /// 直到 WaterFlowTrigger 再次触发。
    /// </summary>
    private void FixedUpdate()
    {
        if (IsRewindOperationLocked || !isSnapshotRecording) return;
        RecordSnapshot();
    }

    private void OnReincarnationBeg()
    {
        StartRewind();
    }

    private void OnReincarnationEnd()
    {
        StopAllCoroutines();
        OnUnfreeze();
        if (this == null) return;

        RemoveDiscardedUnits();
        ClearSnapshots();
        isRewinding = false;
        isRewindCoolingDown = false;

        // 协程已被 StopAllCoroutines 停掉，清空引用。
        // 注意：不重置 hasFlowedOnce/freezeState/isFlowing/stepCount/isSnapshotRecording——这些已由 ApplySnapshot
        // 还原到回溯到的那个快照的值，必须保留以正确反映"回溯后的状态"。
        flowCoroutine = null;
        advanceCoroutine = null;
        freezeCoroutine = null;

        // 如果回溯到的快照状态是"正在流"（已触发、未到底），回溯结束后立即从当前 stepCount 继续向下生成。
        // isFlowing 此时是 ApplySnapshot 还原后的值。
        if (isFlowing)
        {
            flowCoroutine = StartCoroutine(FlowLoop());
            advanceCoroutine = StartCoroutine(AdvanceLoop());
        }

        // 请求驱动结冰：回溯结束后，若处于 Requested 态（请求了但尚未真正结冰），
        // 自动重新启动结冰倒计时。覆盖"回溯到结冰前"的场景：水会自动重新结冰，不需玩家再踩 trigger。
        if (FreezeStateMachine.IsPendingFreeze(freezeState) && freezeCoroutine == null)
        {
            freezeCoroutine = StartCoroutine(FreezeAfterDelay());
        }
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        // 单元出生时间晚于快照时间 → 该单元不应存在，隐藏。
        for (int i = 0; i < units.Count; i++)
        {
            WaterFlowUnit unit = units[i];
            if (unit == null) continue;
            if (unit.BirthTime > snapshotTime) unit.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 回溯结束后移除已被隐藏的未来单元。
    /// 若只隐藏不移除，续流会在相同格子追加新单元，下一次回溯又会激活旧单元并造成重叠。
    /// </summary>
    private void RemoveDiscardedUnits()
    {
        for (int i = units.Count - 1; i >= 0; i--)
        {
            WaterFlowUnit unit = units[i];
            if (unit == null)
            {
                units.RemoveAt(i);
                continue;
            }

            if (unit.gameObject.activeSelf) continue;

            units.RemoveAt(i);
            if (Application.isPlaying)
            {
                Destroy(unit.gameObject);
            }
            else
            {
                DestroyImmediate(unit.gameObject);
            }
        }
    }

    // ===== RewindableBase 实现 =====
    public override int SnapshotCount => snapshots.Count;

    public override void RecordSnapshot()
    {
        if (IsRewindOperationLocked || !isSnapshotRecording) return;

        EnqueueSnapshot(Time.time);
    }

    private void EnqueueSnapshot(float snapshotTime)
    {
        WaterSourceSnapshot snapshot = new WaterSourceSnapshot
        {
            time = snapshotTime,
            unitStates = CaptureAllUnits(),
            hasFlowedOnce = hasFlowedOnce,
            isFlowing = isFlowing,
            stepCount = stepCount,
            freezeState = freezeState,
            isSnapshotRecording = isSnapshotRecording
        };
        EnqueueAndTrim(snapshots, snapshot, GetRewindableSeconds());
    }

    private UnitSnapshot[] CaptureAllUnits()
    {
        UnitSnapshot[] unitSnapshots = new UnitSnapshot[units.Count];
        for (int i = 0; i < units.Count; i++)
        {
            WaterFlowUnit unit = units[i];
            unitSnapshots[i] = new UnitSnapshot
            {
                time = Time.time,
                position = unit != null ? unit.transform.position : Vector3.zero,
                phaseIndex = unit != null
                    ? (int)unit.Phase
                    : (int)WaterFlowUnit.WaterPhase.Broken,
                unitBirthTime = unit != null ? unit.BirthTime : 0f
            };
        }

        return unitSnapshots;
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked) return;
        if (snapshots.Count == 0) return;

        // 倒放数组从池借出，CopyTo 填入；协程内部用 count 而非 Length（池借出的 Length 可能更大）。
        int count = snapshots.Count;
        borrowedPlaybackArray = snapshotArrayPool.Get(count);
        snapshots.CopyTo(borrowedPlaybackArray, 0);
        StartCoroutine(RewindBackwards(
            borrowedPlaybackArray,
            count,
            ApplySnapshot,
            rewindSpeed,
            GetRewindableSeconds,
            rewindCooldownSeconds));
    }

    private void ApplySnapshot(WaterSourceSnapshot snapshot)
    {
        // 还原策略：以快照中存在的单元为基准，对齐每个单元的相位；
        // 不在快照中的多余单元（出生晚于快照时间）隐藏。
        UnitSnapshot[] states = snapshot.unitStates;
        int minLength = Mathf.Min(states.Length, units.Count);
        for (int i = 0; i < minLength; i++)
        {
            WaterFlowUnit unit = units[i];
            if (unit == null) continue;

            UnitSnapshot unitState = states[i];
            unit.gameObject.SetActive(true);
            unit.ForcePhase((WaterFlowUnit.WaterPhase)unitState.phaseIndex);
            unit.transform.position = unitState.position;
        }

        // 隐藏快照之外的多余单元。
        for (int i = minLength; i < units.Count; i++)
        {
            if (units[i] != null) units[i].gameObject.SetActive(false);
        }

        // 还原水源级状态：回溯到的那个时刻的 hasFlowedOnce/isFlowing/stepCount/freezeState/
        // isSnapshotRecording。
        // 这里只更新数据，不重启协程（回溯期间协程保持停止，回溯结束后由 OnReincarnationEnd 按
        // 还原后的 isFlowing 决定是否重启 FlowLoop，从当前 stepCount 继续向下生成；
        // 按 freezeState == Requested 决定是否自动重新结冰）。
        hasFlowedOnce = snapshot.hasFlowedOnce;
        isFlowing = snapshot.isFlowing; // 仅记录状态，真正是否继续流由 OnReincarnationEnd 判定
        stepCount = snapshot.stepCount;
        freezeState = snapshot.freezeState;
        isSnapshotRecording = snapshot.isSnapshotRecording;
    }

    private float GetRewindableSeconds()
    {
        float baseSeconds = ReincarnationManager.Instance != null
            ? ReincarnationManager.Instance.CurrentRewindableSeconds
            : 8f;
        return baseSeconds * Mathf.Max(0f, rewindWindowMultiplier);
    }

    protected override bool ShouldContinueRewind()
    {
        return Input.GetKey(KeyCode.R);
    }

    protected override void OnFreeze()
    {
        // 回溯期间暂停所有调度协程（RewindBackwards 在协程外执行，不会自动暂停我们的协程）。
        // isFlowing 等水源级状态会在 ApplySnapshot 逐帧还原为快照值，这里无需手动改。
        if (flowCoroutine != null)
        {
            StopCoroutine(flowCoroutine);
            flowCoroutine = null;
        }

        if (advanceCoroutine != null)
        {
            StopCoroutine(advanceCoroutine);
            advanceCoroutine = null;
        }

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
    }

    protected override void OnUnfreeze()
    {
        // 回溯结束（倒放完成）。水源级状态已由 ApplySnapshot 还原到目标快照。
        // 是否重启 FlowLoop 由 OnReincarnationEnd 根据还原后的 isFlowing 判定，这里不做处理。
    }

    protected override void ClearSnapshots()
    {
        snapshots.Clear();

        // 归还倒放数组到池（与 StartRewind 的借出配对）。
        if (borrowedPlaybackArray != null)
        {
            snapshotArrayPool.Return(borrowedPlaybackArray);
            borrowedPlaybackArray = null;
        }
    }
}
