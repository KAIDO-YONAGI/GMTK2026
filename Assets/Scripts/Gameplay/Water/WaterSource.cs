using System.Collections.Generic;
using UnityEngine;
using WaterMechanism;

/// <summary>
/// 水源管理器：水流机关的核心，继承 RewindableBase 实现回溯。
///
/// 本组件按职责拆分为多个 partial 文件：
/// 1. WaterSource.Flow.cs：流水生成、碰撞检测、单元成熟与拖尾。
/// 2. WaterSource.Freezing.cs：结冰请求、冻结推进、碎冰与粒子/音效。
/// 3. WaterSource.Rewind.cs：快照记录、倒放还原与轮回事件联动。
///
/// 回溯策略：
/// WaterFlowTrigger 触发时先记录一帧“未开启记录”的边界快照，再开始逐物理帧记录。
/// 记录开关本身也包含在快照中，因此回溯越过触发边界后会恢复为停止记录状态。
/// 订阅 ReincarnationBegEvent/EndEvent/RewindSnapshotAppliedEvent 与玩家回溯联动。
/// </summary>
[RequireComponent(typeof(Transform))]
public partial class WaterSource : RewindableBase
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

    [Tooltip("Particle effect played when a water unit freezes or breaks.")]
    [SerializeField] private ParticleSystem iceParticlePrefab;

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

    // 复用命中列表，允许同一格水流同时激活路径上的多个压力板。
    private readonly List<PressurePlate> hitPressurePlates = new List<PressurePlate>();

    // 当前向下生成计数（下一个单元应位于 sourcePosition - stepCount * cellSize）
    private int stepCount;

    private bool isFlowing; // 是否在向下扩散

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

    // Trigger 触发前不记录；该开关随快照回溯。
    private bool isSnapshotRecording;

    // ===== 清理：销毁时连带单元一起销毁 =====
    private void OnDestroy()
    {
        StopAllCoroutines();

        // 协程被中断时补归还借出的倒放数组，避免泄漏。
        if (borrowedPlaybackArray != null)
        {
            snapshotArrayPool.Return(borrowedPlaybackArray);
            borrowedPlaybackArray = null;
        }

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null) Destroy(units[i].gameObject);
        }

        units.Clear();
    }
}
