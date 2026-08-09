using UnityEngine;

/// <summary>
/// 水源机关的外观入口和回溯协调器。
///
/// Inspector 配置与对外 API 保留在此组件上，确保已有 Prefab 和触发器引用不变。
/// 具体的流水、冻结和回溯逻辑由组合对象负责。
/// </summary>
[RequireComponent(typeof(Transform))]
public class WaterSource : RewindableBase
{
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

    private WaterSourceRuntimeState runtimeState;
    private WaterFlowController flowController;
    private WaterFreezeController freezeController;
    private WaterSourceRewindController rewindController;

    public override int SnapshotCount =>
        rewindController != null ? rewindController.SnapshotCount : 0;

    private void Awake()
    {
        InitializeControllers();
    }

    private void InitializeControllers()
    {
        if (runtimeState != null)
            return;

        runtimeState = new WaterSourceRuntimeState();
        rewindController = new WaterSourceRewindController(
            this,
            runtimeState,
            GetRewindableSeconds);
        freezeController = new WaterFreezeController(
            this,
            runtimeState,
            () => IsRewindOperationLocked,
            RecordSnapshot,
            freezeDelay,
            iceParticlePrefab);
        flowController = new WaterFlowController(
            this,
            runtimeState,
            () => IsRewindOperationLocked,
            BeginSnapshotRecording,
            RecordSnapshot,
            freezeController.IsFrozen,
            freezeController.PlayIceParticle,
            unitTemplate,
            spawnRoot,
            cellSize,
            flowInterval,
            stage1ToStage2Seconds,
            groundMask);
    }

    private void OnEnable()
    {
        InitializeControllers();

        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent += OnReincarnationEnd;
        if (snapshotAppliedEvent != null)
            snapshotAppliedEvent.FloatEvent += OnSnapshotApplied;
    }

    private void OnDisable()
    {
        if (reincarnationBegEvent != null)
            reincarnationBegEvent.VoidEvent -= OnReincarnationBeg;
        if (reincarnationEndEvent != null)
            reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
        if (snapshotAppliedEvent != null)
            snapshotAppliedEvent.FloatEvent -= OnSnapshotApplied;
    }

    private void FixedUpdate()
    {
        rewindController?.RecordSnapshot(IsRewindOperationLocked);
    }

    /// <summary>启动流水。流过一次后被锁死，回溯后才允许再次触发。</summary>
    public void StartFlowing()
    {
        flowController?.StartFlowing();
    }

    /// <summary>启动结冰倒计时。</summary>
    public void StartFreezing()
    {
        freezeController?.StartFreezing();
    }

    /// <summary>由水流单元的 Ice 回调调用，通知水源该单元已碎冰。</summary>
    public void NotifyUnitBroken(WaterFlowUnit unit)
    {
        freezeController?.NotifyUnitBroken(unit);
    }

    public override void RecordSnapshot()
    {
        rewindController?.RecordSnapshot(IsRewindOperationLocked);
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked || rewindController == null)
            return;

        WaterSourceSnapshot[] playbackSnapshots =
            rewindController.BorrowPlayback(out int count);
        if (playbackSnapshots == null || count == 0)
            return;

        StartCoroutine(RewindBackwards(
            playbackSnapshots,
            count,
            rewindController.ApplySnapshot,
            rewindSpeed,
            GetRewindableSeconds,
            rewindCooldownSeconds));
    }

    private void OnReincarnationBeg()
    {
        StartRewind();
    }

    private void OnReincarnationEnd()
    {
        StopAllCoroutines();
        OnUnfreeze();
        if (this == null)
            return;

        rewindController.RemoveDiscardedUnits();
        rewindController.ClearSnapshots();
        isRewinding = false;
        isRewindCoolingDown = false;

        runtimeState.ClearCoroutineReferences();
        flowController.ResumeAfterRewind();
        freezeController.ResumeAfterRewind();
    }

    private void OnSnapshotApplied(float snapshotTime)
    {
        rewindController?.HideUnitsBornAfter(snapshotTime);
    }

    private void BeginSnapshotRecording()
    {
        rewindController?.BeginSnapshotRecording();
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
        flowController?.StopForRewind();
        freezeController?.StopForRewind();
    }

    protected override void OnUnfreeze()
    {
        // 回溯结束后的协程恢复由 OnReincarnationEnd 统一协调。
    }

    protected override void ClearSnapshots()
    {
        rewindController?.ClearSnapshots();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        flowController?.Dispose();
        freezeController?.Dispose();
        rewindController?.Dispose();

        if (runtimeState == null)
            return;

        for (int i = 0; i < runtimeState.Units.Count; i++)
        {
            if (runtimeState.Units[i] != null)
                Destroy(runtimeState.Units[i].gameObject);
        }

        runtimeState.Units.Clear();
    }
}
