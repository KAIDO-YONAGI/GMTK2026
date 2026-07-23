using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家轮回回溯：继承 RewindableBase，实现 Player 专属的快照抓取/还原/冻结。
/// R 键触发 VoidEventSO 后启动倒放，按真实时间把玩家位置和血量倒回过去。
/// </summary>
public class ReincarnationManager : RewindableBase
{
    // 单例：玩家轮回回溯全局唯一，方便外部访问
    public static ReincarnationManager Instance { get; private set; }

    [Header("Send Reincarnation Message")]
    [SerializeField] private VoidEventSO reincarnationEvent;

    [Header("Rewind Time Window")]
    [Tooltip("完全年轻（agingRate=1）时的可回溯秒数。")]
    [SerializeField] private float maxRewindBaseSeconds = 8f;

    [Tooltip("完全衰老（agingRate=0）时的可回溯秒数。即使老到极致也保留最小窗口。")]
    [SerializeField] private float minRewindBaseSeconds = 1f;

    // 外部只读访问：回溯窗口上下限（UI 显示、外部系统查询用）
    public float MaxRewindBaseSeconds => maxRewindBaseSeconds;
    public float MinRewindBaseSeconds => minRewindBaseSeconds;

    [Header("Rewind Playback")]
    [Tooltip("回溯播放倍速。1 = 与记录时 1:1 实时倒放；数值越大倒带越快。按真实流逝时间驱动，非固定步长。")]
    [SerializeField] private float rewindSpeed = 1f;

    [Tooltip("回溯播放结束后、恢复记录新快照前的冷却秒数。期间输入立即恢复，只是不记录。")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    // 当前衰老速率：年轻→1，老→0。每物理帧从 AgeManager 更新。
    private float agingRate = 1f;

    // 当前可回溯秒数：按 agingRate 在 [min, max] 之间线性插值。
    private float RewindableSeconds => Mathf.Lerp(minRewindBaseSeconds, maxRewindBaseSeconds, agingRate);

    // 玩家位置 + 血量快照（值类型，避免 GC 压力）
    private struct PlayerSnapshot : IRewindSnapshot
    {
        public float time;
        public Vector3 position;
        public int health;

        // IRewindSnapshot：暴露时间戳供基类裁剪/倒放使用
        public float Time => time;
    }

    // 快照队列（容器留子类，基类只提供算法）
    private readonly Queue<PlayerSnapshot> snapshots = new Queue<PlayerSnapshot>();

    // 玩家根 Transform（位置来源）和移动脚本（冻结输入用）
    private Transform playerTransform;
    private PlayerMovement playerMovement;

    // IRewindable：暴露当前快照数量
    public override int SnapshotCount => snapshots.Count;

    private void Awake()
    {
        // 单例设置：重复实例时销毁自身，保证全局唯一
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerTransform = transform.root;
        playerMovement = playerTransform.GetComponent<PlayerMovement>();
    }

    private void OnDestroy()
    {
        // 防止被销毁的单例指针悬挂
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // 回溯期间（播放+冷却）不接受新的触发
        if (isRewinding) return;

        // 按下 R 键触发轮回空事件 + 启动回溯（引用未拖入时跳过，避免空引用）
        if (Input.GetKeyDown(KeyCode.R) && reincarnationEvent != null)
        {
            reincarnationEvent.OnEventRaised();
            StartRewind();
        }
    }

    /// <summary>每个物理帧更新：刷新衰老速率、抓快照、入队并裁剪。</summary>
    private void FixedUpdate()
    {
        // 回溯进行中不记录新快照，避免与回放互相干扰
        if (isRewinding) return;

        // 1. 更新衰老速率：年轻→1，老→0（AgeManager.instance 已做 Clamp01）
        agingRate = Mathf.Clamp01(1f - AgeManager.instance.AgeNormalized);

        // 2. 抓取本帧快照（位置来自玩家根，血量来自单例 HealthManager）
        var snap = new PlayerSnapshot
        {
            time = Time.time,
            position = playerTransform.position,
            health = HealthManager.Instance != null ? HealthManager.Instance.CurrentHealth : 0
        };

        // 3. 入队并按可回溯时间窗口裁剪过期快照（算法在基类）
        EnqueueAndTrim(snapshots, snap, RewindableSeconds);
    }

    // ===== IRewindable =====
    public override void RecordSnapshot()
    {
        // 接口完整性占位：本类的记录在 FixedUpdate 里完成，不通过此方法触发。
    }

    public override void StartRewind()
    {
        // 防重入 + 空队列保护
        if (isRewinding) return;
        if (snapshots.Count == 0) return;

        // 队列转数组（按时间升序），交给基类倒放协程；时间参数由本类（玩家专属）传入
        StartCoroutine(RewindBackwards(snapshots.ToArray(), ApplySnapshot, rewindSpeed, rewindCooldownSeconds));
    }

    // ===== RewindableBase 钩子：冻结/解冻/清理（由 RewindBackwards 内部调用）=====
    protected override void OnFreeze()
    {
        // 冻结玩家输入
        if (playerMovement != null) playerMovement.enabled = false;
    }

    protected override void OnUnfreeze()
    {
        // 倒放结束立即恢复输入（冷却期间玩家可操作，只是不记录新快照）
        if (playerMovement != null) playerMovement.enabled = true;
    }

    protected override void ClearSnapshots()
    {
        snapshots.Clear();
    }

    protected override bool ShouldContinueRewind()
    {
        // 按住 R 才继续倒放，松开后由基类结束倒放并恢复玩家输入
        return Input.GetKey(KeyCode.R);
    }

    /// <summary>把单个快照应用回玩家身上（位置瞬切 + 血量直接设置，绕过死亡/治疗事件）。</summary>
    private void ApplySnapshot(PlayerSnapshot snap)
    {
        // 恢复位置
        playerTransform.position = snap.position;

        // 恢复血量（走轮回专用接口，绕过死亡/治疗事件）
        if (HealthManager.Instance != null)
        {
            HealthManager.Instance.SetHealthForReincarnation(snap.health);
        }
    }
}
