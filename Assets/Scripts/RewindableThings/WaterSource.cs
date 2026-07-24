using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 水源管理器：水流机关的核心，继承 RewindableBase 实现回溯。
///
/// 职责：
/// 1. 流水调度：StartFlowing 后每隔 flowInterval 向下一格（cellSize 步进）生成一个 WaterFlowUnit。
///    - 命中 Ground/Platform → 停止继续向下生成（已有单元继续推进到 Stage2 / 可结冰）。
///    - 命中 PressurePlate（tag）→ 调其 Activate() 后停止。
/// 2. 状态推进：所有 Stage1 单元出生 stage1ToStage2Seconds 秒后自动切 Stage2。
/// 3. 结冰：StartFreezing 启动 freezeDelay 倒计时，到点把所有 Stage2 单元切 Frozen（带碰撞 + Ice 标记）。
/// 4. 碎冰：Ice 被销毁时回调 NotifyUnitBroken → 该单元切 Broken（关碰撞、不再结冰）。
///
/// 回溯策略（事件驱动稀疏快照，区别于 FlyingSpike 的每帧快照）：
/// 只在状态转变时刻（单元出生 / Stage1→Stage2 / 结冰 / 碎冰）记录快照，
/// 避免每帧记录大量单元造成内存与 GC 压力。其它无变化时间不存快照。
/// 订阅 ReincarnationBegEvent/EndEvent/RewindSnapshotAppliedEvent 与玩家回溯联动。
/// </summary>
[RequireComponent(typeof(Transform))]
public class WaterSource : RewindableBase
{
    // ===== 流水参数 =====
    [Header("Flow Spawn")]
    [Tooltip("水流单元预制件（生成模板）。")]
    [SerializeField] private WaterFlowUnit unitTemplate;

    [Tooltip("生成水流单元的父节点（一般为 WaterGrid 子物体）。")]
    [SerializeField] private Transform spawnRoot;

    [Tooltip("格子大小（向下生成单元的间距）。")]
    [SerializeField] private float cellSize = 1f;

    [Tooltip("每次向下生成单元的时间间隔（秒）。")]
    [SerializeField] private float flowInterval = 0.3f;

    [Tooltip("单元出生多少秒后从 Stage1 推进到 Stage2。")]
    [SerializeField] private float stage1ToStage2Seconds = 1f;

    [Header("Freeze")]
    [Tooltip("StartFreezing 触发后多少秒把所有 Stage2 单元结冰。")]
    [SerializeField] private float freezeDelay = 2f;

    [Header("Collision Detection")]
    [Tooltip("地面检测层（命中即停止向下生成）。默认应含 Ground、Platform。")]
    [SerializeField] private LayerMask groundMask = 0;

    [Header("Rewind")]
    [Tooltip("回溯播放倍速。1 = 1:1 实时倒放。")]
    [SerializeField] private float rewindSpeed = 1f;

    [Tooltip("回溯结束后恢复记录前的冷却秒数。")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    [Header("Reincarnation Events")]
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;
    [Tooltip("玩家回溯 apply 快照后广播的时间戳事件。用于判断单元是否早于出生时间。")]
    [SerializeField] private FloatEventSO snapshotAppliedEvent;

    // ===== 运行时状态 =====
    // 所有已生成的单元（按生成顺序，垂直从上到下）。索引 0 是最靠近水源的一格。
    private readonly List<WaterFlowUnit> units = new List<WaterFlowUnit>();

    // 当前向下生成计数（下一个单元应位于 sourcePosition - stepCount * cellSize）
    private int stepCount;

    private bool isFlowing;     // 是否在向下扩散
    private bool isFrozen;      // 整体是否已结冰（防止重复结冰）

    private Coroutine flowCoroutine;
    private Coroutine advanceCoroutine;
    private Coroutine freezeCoroutine;

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

    // 水源整体快照（每次状态转变时记录整张单元表）
    private struct WaterSourceSnapshot : IRewindSnapshot
    {
        public float time;                  // 记录时刻
        public UnitSnapshot[] unitStates;   // 全部单元状态快照

        public float Time => time;
    }

    private readonly Queue<WaterSourceSnapshot> snapshots = new Queue<WaterSourceSnapshot>();

    // ===== 公有接口 =====

    /// <summary>启动流水（多次调用幂等）。</summary>
    public void StartFlowing()
    {
        if (isFlowing) return;
        if (IsRewindOperationLocked) return;
        isFlowing = true;
        flowCoroutine = StartCoroutine(FlowLoop());
        advanceCoroutine = StartCoroutine(AdvanceLoop());
        RecordSnapshot(); // 状态转变：开始流水
    }

    /// <summary>启动结冰倒计时（多次调用幂等）。</summary>
    public void StartFreezing()
    {
        if (isFrozen) return;
        if (IsRewindOperationLocked) return;
        if (freezeCoroutine != null) StopCoroutine(freezeCoroutine);
        freezeCoroutine = StartCoroutine(FreezeAfterDelay());
    }

    /// <summary>由 WaterFlowUnit 的 Ice.OnBroken 回调调用：单单元碎冰。</summary>
    public void NotifyUnitBroken(WaterFlowUnit unit)
    {
        if (unit == null) return;
        unit.ForcePhase(WaterFlowUnit.WaterPhase.Broken);
        RecordSnapshot(); // 状态转变：碎冰
    }

    // ===== 流水协程：每隔 flowInterval 向下一格生成单元 =====
    private IEnumerator FlowLoop()
    {
        while (isFlowing && !IsRewindOperationLocked)
        {
            stepCount++;
            Vector3 nextPos = (spawnRoot != null ? spawnRoot.position : transform.position)
                              + Vector3.down * (cellSize * stepCount);

            Collider2D hit = Physics2D.OverlapPoint(nextPos, groundMask);
            if (hit != null)
            {
                // 命中地面 / 平台：停止向下生成（已有单元继续 Stage2 推进、可结冰）
                isFlowing = false;
                break;
            }

            // 命中压力板（按 tag 筛选）：激活后停止
            Collider2D plateHit = Physics2D.OverlapPoint(nextPos);
            if (plateHit != null && plateHit.CompareTag("PressurePlate"))
            {
                var plate = plateHit.GetComponent<PressurePlate>();
                if (plate != null) plate.Activate();
                isFlowing = false;
                break;
            }

            SpawnUnitAt(nextPos);
            yield return new WaitForSeconds(flowInterval);
        }

        isFlowing = false;
        flowCoroutine = null;
    }

    // ===== 状态推进协程：轮询所有 Stage1 单元到点切 Stage2 =====
    private IEnumerator AdvanceLoop()
    {
        while (true)
        {
            if (IsRewindOperationLocked) { yield return null; continue; }

            bool anyChanged = false;
            float now = Time.time;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null) continue;
                if (u.Phase == WaterFlowUnit.WaterPhase.Stage1 &&
                    now - u.BirthTime >= stage1ToStage2Seconds)
                {
                    u.SetPhase(WaterFlowUnit.WaterPhase.Stage2);
                    anyChanged = true;
                }
            }
            if (anyChanged) RecordSnapshot(); // 状态转变：Stage1→Stage2

            yield return new WaitForSeconds(0.1f); // 10Hz 轮询足够
        }
    }

    // ===== 结冰协程：倒计时到点把所有 Stage2 单元切 Frozen =====
    private IEnumerator FreezeAfterDelay()
    {
        float elapsed = 0f;
        while (elapsed < freezeDelay)
        {
            if (!IsRewindOperationLocked) elapsed += Time.deltaTime;
            yield return null;
        }

        isFrozen = true;
        bool anyChanged = false;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (u.Phase == WaterFlowUnit.WaterPhase.Stage2)
            {
                u.SetPhase(WaterFlowUnit.WaterPhase.Frozen);
                anyChanged = true;
            }
        }
        if (anyChanged) RecordSnapshot(); // 状态转变：结冰
        freezeCoroutine = null;
    }

    private void SpawnUnitAt(Vector3 worldPos)
    {
        if (unitTemplate == null)
        {
            Debug.LogError("[WaterSource] unitTemplate 未配置，无法生成水流单元。", this);
            isFlowing = false;
            return;
        }
        WaterFlowUnit unit = Instantiate(unitTemplate, worldPos, Quaternion.identity, spawnRoot != null ? spawnRoot : transform);
        unit.Init(this);
        units.Add(unit);
        RecordSnapshot(); // 状态转变：单元出生
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

    private void OnReincarnationBeg() => StartRewind();

    private void OnReincarnationEnd()
    {
        StopAllCoroutines();
        OnUnfreeze();
        if (this == null) return;
        ClearSnapshots();
        isRewinding = false;
        isRewindCoolingDown = false;
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        // 单元出生时间晚于快照时间 → 该单元不应存在，隐藏
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (u.BirthTime > snapshotTime) u.gameObject.SetActive(false);
        }
    }

    // ===== RewindableBase 实现 =====
    public override int SnapshotCount => snapshots.Count;

    public override void RecordSnapshot()
    {
        if (IsRewindOperationLocked) return;

        var snap = new WaterSourceSnapshot
        {
            time = Time.time,
            unitStates = CaptureAllUnits()
        };
        EnqueueAndTrim(snapshots, snap, GetRewindableSeconds());
    }

    private UnitSnapshot[] CaptureAllUnits()
    {
        var arr = new UnitSnapshot[units.Count];
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            arr[i] = new UnitSnapshot
            {
                time = Time.time,
                position = u != null ? u.transform.position : Vector3.zero,
                phaseIndex = u != null ? (int)u.Phase : (int)WaterFlowUnit.WaterPhase.Broken,
                unitBirthTime = u != null ? u.BirthTime : 0f
            };
        }
        return arr;
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked) return;
        if (snapshots.Count == 0) return;
        StartCoroutine(RewindBackwards(snapshots.ToArray(), ApplySnapshot, rewindSpeed,
            GetRewindableSeconds, rewindCooldownSeconds));
    }

    private void ApplySnapshot(WaterSourceSnapshot snap)
    {
        // 还原策略：以快照中存在的单元为基准，对齐每个单元的相位；
        // 不在快照中的多余单元（出生晚于快照时间）隐藏。
        var states = snap.unitStates;
        int minLen = Mathf.Min(states.Length, units.Count);
        for (int i = 0; i < minLen; i++)
        {
            var u = units[i];
            if (u == null) continue;
            var st = states[i];
            u.gameObject.SetActive(true);
            u.ForcePhase((WaterFlowUnit.WaterPhase)st.phaseIndex);
            u.transform.position = st.position;
        }
        // 隐藏快照之外的多余单元
        for (int i = minLen; i < units.Count; i++)
        {
            if (units[i] != null) units[i].gameObject.SetActive(false);
        }
    }

    private float GetRewindableSeconds()
    {
        if (ReincarnationManager.Instance != null)
            return ReincarnationManager.Instance.CurrentRewindableSeconds;
        return 8f;
    }

    protected override bool ShouldContinueRewind() => Input.GetKey(KeyCode.R);

    protected override void OnFreeze()
    {
        // 回溯期间暂停所有调度协程（RewindBackwards 在协程外执行，不会自动暂停我们的协程）
        if (flowCoroutine != null) { StopCoroutine(flowCoroutine); flowCoroutine = null; }
        if (advanceCoroutine != null) { StopCoroutine(advanceCoroutine); advanceCoroutine = null; }
        if (freezeCoroutine != null) { StopCoroutine(freezeCoroutine); freezeCoroutine = null; }
        isFlowing = false;
    }

    protected override void OnUnfreeze()
    {
        // 回溯结束后不自动恢复流水（避免行为不可预期），保持当前状态。
        // 如需恢复，可由 trigger 重新触发 StartFlowing。
    }

    protected override void ClearSnapshots()
    {
        snapshots.Clear();
    }

    // ===== 清理：销毁时连带单元一起销毁 =====
    private void OnDestroy()
    {
        StopAllCoroutines();
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null) Destroy(units[i].gameObject);
        }
        units.Clear();
    }
}
