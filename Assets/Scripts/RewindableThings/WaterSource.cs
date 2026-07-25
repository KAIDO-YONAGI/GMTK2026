using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// 水源管理器：水流机关的核心，继承 RewindableBase 实现回溯。
///
/// 职责：
/// 1. 流水调度：StartFlowing 后每隔 flowInterval 向下一格（cellSize 步进）生成一个 WaterFlowUnit。
///    - 命中 Ground/Platform → 停止继续向下生成（已有单元继续推进到 Stage2 / 可结冰）。
///    - 命中 PressurePlate（tag）→ 调其 Activate() 后停止。
/// 2. 状态推进：所有 Stage1 单元出生 stage1ToStage2Seconds 秒后自动切 Stage2。
/// 3. 结冰：StartFreezing 启动 freezeDelay 倒计时，到点把所有 Stage2 单元切 Frozen；
///    此后新成熟的水流单元也会直接切 Frozen（带碰撞 + Ice 标记）。
/// 4. 碎冰：Ice 被击碎时回调 NotifyUnitBroken → 该单元切回 Stage2（关碰撞），
///    再次触发结冰后可重新切 Frozen。
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
    [Tooltip("StartFreezing 触发后多少秒进入冻结状态。现有及后续成熟的水流单元都会结冰。")]
    [SerializeField] private float freezeDelay = 2f;

    [Header("Collision Detection")]
    [Tooltip("地面检测层（命中即停止向下生成）。默认应含 Ground、Platform。")]
    [SerializeField] private LayerMask groundMask = 0;

    [Header("Rewind")]
    [Tooltip("回溯播放倍速。1 = 1:1 实时倒放。")]
    [SerializeField] private float rewindSpeed = 1f;

    [Tooltip("回溯结束后恢复记录前的冷却秒数。")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    [Tooltip("可回溯窗口倍率。水源的可回溯秒数 = 玩家回溯窗口 × 此倍率。" +
             "大于 1 时水源能回溯更久（保留更多历史快照，包括触发前的未开始状态）。")]
    [SerializeField] private float rewindWindowMultiplier = 1f;

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
    // 是否已经完成过一次流水（流到底/被阻挡停过）。流过一次后忽略所有 StartFlowing 调用，
    // 直到回溯（OnReincarnationEnd）重置才能被 trigger 再次触发。
    private bool hasFlowedOnce;

    // 冻结状态机（替代原 isFrozen + freezeRequested 两个 bool）。
    // 三态：Idle（未请求）/ Requested（已踩 trigger 倒计时中）/ Frozen（已结冰）。
    // 状态转换走 FreezeStateMachine，避免 bool 组合产生不合法中间态。
    // 踩 trigger → Requested；倒计时到点 → Frozen；冰被击碎 → Idle（需重新踩 trigger）。
    // 回溯时随快照还原：回溯到 Requested 态会自动重新结冰。
    private FreezeState freezeState = FreezeState.Idle;

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

        public float Time => time;
    }

    private readonly Queue<WaterSourceSnapshot> snapshots = new Queue<WaterSourceSnapshot>();

    // ===== 公有接口 =====

    /// <summary>启动流水。流过一次后被锁死（hasFlowedOnce），回溯（OnReincarnationEnd）后才解锁。</summary>
    public void StartFlowing()
    {
        if (hasFlowedOnce) return;  // 已流过一次，忽略直到回溯重置
        if (isFlowing) return;
        if (IsRewindOperationLocked) return;
        isFlowing = true;
        hasFlowedOnce = true;       // 标记已触发，FlowLoop 结束后也不再响应 trigger
        flowCoroutine = StartCoroutine(FlowLoop());
        advanceCoroutine = StartCoroutine(AdvanceLoop());
        RecordSnapshot(); // 状态转变：开始流水
    }

    /// <summary>
    /// 启动结冰倒计时（由 WaterFreezeTrigger 调用）。
    /// 走状态机：踩 trigger → freezeState 转 Requested。
    /// 是否真正启动协程看当前是否已经「冰完整且无碎冰水」——是则幂等忽略，否则启动倒计时。
    /// 回溯时若还原到 Requested 态（请求了但未结冰），OnReincarnationEnd 会自动重新结冰。
    /// </summary>
    public void StartFreezing()
    {
        if (IsRewindOperationLocked) return;

        // 记录玩家意图（Idle→Requested；已 Requested/Frozen 则幂等）。
        FreezeStateMachine.Request(ref freezeState);

        // 已结冰（Frozen）且无 Stage2（碎冰水）需要重冻 → 已是终态，不重复启动协程。
        if (FreezeStateMachine.IsFrozen(freezeState) && !HasStage2Units()) return;
        if (freezeCoroutine != null) return;

        freezeCoroutine = StartCoroutine(FreezeAfterDelay());
    }

    private bool HasStage2Units()
    {
        for (int i = 0; i < units.Count; i++)
        {
            WaterFlowUnit unit = units[i];
            if (unit != null && unit.Phase == WaterFlowUnit.WaterPhase.Stage2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>由 WaterFlowUnit 的 Ice.OnBroken 回调调用：单单元碎冰后变回水（Stage2），可再次结冰。</summary>
    public void NotifyUnitBroken(WaterFlowUnit unit)
    {
        if (unit == null) return;
        // 碎冰后切回 Stage2（满水），IceImg 子物体未销毁，后续可被 WaterFreezeTrigger 再次结冰。
        unit.ForcePhase(WaterFlowUnit.WaterPhase.Stage2);
        // 冰被击碎 = 玩家主动行为，撤回结冰意图：必须重新踩 trigger 才能再次结冰。
        // Frozen→Idle。其他未碎冰的单元视觉上仍是冰；这一格变 Stage2 等待重新被请求结冰。
        FreezeStateMachine.Break(ref freezeState);
        RecordSnapshot(); // 状态转变：碎冰→变回水
    }

    // ===== 流水协程：每隔 flowInterval 向下一格生成单元 =====
    private IEnumerator FlowLoop()
    {
        while (isFlowing && !IsRewindOperationLocked)
        {
            stepCount++;
            Vector3 nextPos = (spawnRoot != null ? spawnRoot.position : transform.position)
                              + Vector3.down * (cellSize * stepCount);

            // 地面检测：从下一格中心向下打 cellSize 距离的射线。
            // 用 Raycast 而非 OverlapPoint 的原因：Tilemap 的 CompositeCollider2D 是空心薄壳，
            // OverlapPoint 要求点落在 collider 实体内部，而格中心常落在薄壳外（哪怕视觉上"在 tile 里"），
            // 导致永不命中。Raycast 检测射线是否穿过 collider 表面，可靠。
            RaycastHit2D groundHit = Physics2D.Raycast(nextPos, Vector2.down, cellSize, groundMask);
            if (groundHit.collider != null)
            {
                // 命中地面 / 平台：这一格贴着地面，必须生成它让水流视觉贴紧地面（不悬空），生成后停止向下扩散。
                // 已生成单元继续推进到 Stage2、可结冰。
                SpawnUnitAt(nextPos);
                // 给最后这格单元挂一个拖尾子物体：向下偏移半格，锁定 Stage1，消除视觉悬空。
                // 拖尾不加入 units 列表、不参与回溯推进（纯视觉装饰，由父单元管理生命周期）。
                WaterFlowUnit lastUnit = units[units.Count - 1];
                SpawnTail(lastUnit);
                isFlowing = false;
                break;
            }

            // 命中压力板（按 tag 筛选）：激活后停止。
            // 压力板是独立 BoxCollider2D，OverlapPoint 对它有效。
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

    // ===== 状态推进协程：轮询 Stage1 单元，成熟后切 Stage2 或 Frozen =====
    private IEnumerator AdvanceLoop()
    {
        while (true)
        {
            if (IsRewindOperationLocked) { yield return null; continue; }

            bool anyChanged = false;
            float now = Time.time;
            // 走状态机判定：已 Frozen 态下，新成熟单元直接进 Frozen 相位；否则进 Stage2。
            bool frozenMode = FreezeStateMachine.IsFrozen(freezeState);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null) continue;
                if (u.Phase == WaterFlowUnit.WaterPhase.Stage1 &&
                    !u.LockAtStage1 &&
                    now - u.BirthTime >= stage1ToStage2Seconds)
                {
                    u.SetPhase(frozenMode
                        ? WaterFlowUnit.WaterPhase.Frozen
                        : WaterFlowUnit.WaterPhase.Stage2);
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

        // 倒计时到点：状态机 Requested→Frozen。此后新成熟单元也直接结冰（AdvanceLoop 据此分流）。
        FreezeStateMachine.ConfirmFrozen(ref freezeState);
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

    /// <summary>
    /// 给最后命中地面的单元挂一个拖尾子物体，消除水流视觉悬空。
    /// 拖尾复用 WaterFlowUnit.prefab，作为父单元的子物体（localPosition.y = -cellSize/2），
    /// 锁定在 Stage1（不推进 Stage2），不加入 units 列表、不参与回溯推进——纯视觉装饰，随父单元销毁。
    /// </summary>
    private void SpawnTail(WaterFlowUnit parentUnit)
    {
        if (unitTemplate == null || parentUnit == null) return;
        WaterFlowUnit tail = Instantiate(unitTemplate, parentUnit.transform);
        tail.transform.localPosition = Vector3.down * (cellSize * 0.5f);
        tail.Init(null); // 不传 owner：拖尾的 Ice 永不到 Frozen，不需要碎冰通知
        tail.LockAtStage1 = true;
        tail.SetPhase(WaterFlowUnit.WaterPhase.Stage1);
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
    /// 每物理帧无条件记录快照。
    /// 必须定时记录（而非只在状态转变时记录）：才能捕获到"水流未开始"等无变化时段的状态，
    /// 回溯时才能还原到触发前的状态。回溯期间（IsRewindOperationLocked）跳过，避免污染。
    /// </summary>
    private void FixedUpdate()
    {
        if (IsRewindOperationLocked) return;
        RecordSnapshot();
    }

    private void OnReincarnationBeg() => StartRewind();

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
        // 注意：不重置 hasFlowedOnce/freezeState/isFlowing/stepCount——这些已由 ApplySnapshot
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
        // 单元出生时间晚于快照时间 → 该单元不应存在，隐藏
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null) continue;
            if (u.BirthTime > snapshotTime) u.gameObject.SetActive(false);
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
        if (IsRewindOperationLocked) return;

        var snap = new WaterSourceSnapshot
        {
            time = Time.time,
            unitStates = CaptureAllUnits(),
            hasFlowedOnce = hasFlowedOnce,
            isFlowing = isFlowing,
            stepCount = stepCount,
            freezeState = freezeState
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

        // 还原水源级状态：回溯到的那个时刻的 hasFlowedOnce/isFlowing/stepCount/freezeState。
        // 这里只更新数据，不重启协程（回溯期间协程保持停止，回溯结束后由 OnReincarnationEnd 按
        // 还原后的 isFlowing 决定是否重启 FlowLoop，从当前 stepCount 继续向下生成；
        // 按 freezeState == Requested 决定是否自动重新结冰）。
        hasFlowedOnce = snap.hasFlowedOnce;
        isFlowing = snap.isFlowing;     // 仅记录状态，真正是否继续流由 OnReincarnationEnd 判定
        stepCount = snap.stepCount;
        freezeState = snap.freezeState;
    }

    private float GetRewindableSeconds()
    {
        float baseSeconds = ReincarnationManager.Instance != null
            ? ReincarnationManager.Instance.CurrentRewindableSeconds
            : 8f;
        return baseSeconds * Mathf.Max(0f, rewindWindowMultiplier);
    }

    protected override bool ShouldContinueRewind() => Input.GetKey(KeyCode.R);

    protected override void OnFreeze()
    {
        // 回溯期间暂停所有调度协程（RewindBackwards 在协程外执行，不会自动暂停我们的协程）。
        // isFlowing 等水源级状态会在 ApplySnapshot 逐帧还原为快照值，这里无需手动改。
        if (flowCoroutine != null) { StopCoroutine(flowCoroutine); flowCoroutine = null; }
        if (advanceCoroutine != null) { StopCoroutine(advanceCoroutine); advanceCoroutine = null; }
        if (freezeCoroutine != null) { StopCoroutine(freezeCoroutine); freezeCoroutine = null; }
    }

    protected override void OnUnfreeze()
    {
        // 回溯结束（倒放完成）。水源级状态已由 ApplySnapshot 还原到目标快照。
        // 是否重启 FlowLoop 由 OnReincarnationEnd 根据还原后的 isFlowing 判定，这里不做处理。
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
