using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 独立可击破冰块：地图可摆放的机关，不依赖水流结冰机制。
///
/// 状态：完整（Intact，带碰撞阻挡） ↔ 碎裂（Broken，禁用碰撞可通行）。
/// 被 FlyingSpike/PickupSpike 击中时，Ice.Break() 触发 OnBroken → 本组件切碎裂状态。
///
/// 回溯：继承 RewindableBase，状态随回溯还原（完整↔碎裂）。
/// 采用差异快照：只在完整/碎裂状态变化时追加记录，
/// 连续相同状态直接丢弃，避免静态冰块产生重复快照。
/// </summary>
[RequireComponent(typeof(Ice))]
public class IceBlock : RewindableBase
{
    [Header("Visuals")]
    [Tooltip("完整冰块的 SpriteRenderer（碎裂时隐藏）。")]
    [SerializeField] private SpriteRenderer intactRenderer;

    [Tooltip("碎裂冰块的 SpriteRenderer（完整时隐藏）。可不配，碎裂时仅隐藏完整贴图。")]
    [SerializeField] private SpriteRenderer brokenRenderer;

    [Header("Rewind")]
    [Tooltip("回溯播放倍速。1 = 1:1 实时倒放。")]
    [SerializeField] private float rewindSpeed = 1f;

    [Tooltip("回溯结束后恢复记录前的冷却秒数。")]
    [SerializeField] private float rewindCooldownSeconds = 1f;

    [Header("Reincarnation Events")]
    [SerializeField] private VoidEventSO reincarnationBegEvent;
    [SerializeField] private VoidEventSO reincarnationEndEvent;
    
    [Header("Shake Event")]
    [SerializeField] private ShakeEventSO shakeEvent;
    [SerializeField] private CameraShake.ShakeParameters hitShakeParams = new CameraShake.ShakeParameters
    {
        intensity = 5f,
        duration = 0.2f,
        frequency = 0.1f
    };

    private BoxCollider2D blockCollider;
    private Ice ice;
    private bool isBroken;

    private const float TransitionBoundaryOffset = 0.0001f;

    // ===== 快照结构（只保留状态变化）=====
    private struct IceBlockSnapshot : IRewindSnapshot
    {
        public float time;
        public bool isBroken;

        public float Time => time;
    }

    private readonly List<IceBlockSnapshot> snapshots = new List<IceBlockSnapshot>();

    private void Awake()
    {
        ice = GetComponent<Ice>();
        blockCollider = GetComponent<BoxCollider2D>();
    }

    private void OnEnable()
    {
        if (ice != null) ice.OnBroken += OnIceBroken;
        if (reincarnationBegEvent != null) reincarnationBegEvent.VoidEvent += OnReincarnationBeg;
        if (reincarnationEndEvent != null) reincarnationEndEvent.VoidEvent += OnReincarnationEnd;

        // 在首次物理帧前保留初始状态，避免冰块生成后立即被击碎时缺少完整快照。
        RecordSnapshot();
    }

    private void OnDisable()
    {
        if (ice != null) ice.OnBroken -= OnIceBroken;
        if (reincarnationBegEvent != null) reincarnationBegEvent.VoidEvent -= OnReincarnationBeg;
        if (reincarnationEndEvent != null) reincarnationEndEvent.VoidEvent -= OnReincarnationEnd;
    }

    private void FixedUpdate()
    {
        if (IsRewindOperationLocked) return;
        RecordSnapshot();
    }

    /// <summary>Ice.Break() 触发：切碎裂状态并立即补记状态变化。</summary>
    private void OnIceBroken(GameObject _)
    {
        SetBroken(true);
        RecordSnapshot();
        shakeEvent.RaiseShakeEvent(hitShakeParams);
    }

    /// <summary>切换碎裂/完整状态（视觉 + 碰撞）。</summary>
    private void SetBroken(bool broken)
    {
        isBroken = broken;
        if (blockCollider != null) blockCollider.enabled = !broken; // 碎裂时关闭碰撞（可通行）
        if (intactRenderer != null) intactRenderer.gameObject.SetActive(!broken);
        if (brokenRenderer != null) brokenRenderer.gameObject.SetActive(broken);
    }

    // ===== 事件 =====
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

    // ===== RewindableBase 实现 =====
    public override int SnapshotCount => snapshots.Count;

    /// <summary>状态未变化时丢弃本次记录；变化时追加差异快照。</summary>
    public override void RecordSnapshot()
    {
        if (IsRewindOperationLocked) return;

        float now = Time.time;
        TrimExpiredSnapshots(now, GetRewindableSeconds());

        if (snapshots.Count == 0)
        {
            snapshots.Add(new IceBlockSnapshot { time = now, isBroken = isBroken });
            return;
        }

        int lastIndex = snapshots.Count - 1;
        IceBlockSnapshot previous = snapshots[lastIndex];
        if (previous.isBroken == isBroken) return;

        // 把旧状态贴到切换边界前，供基类倒放算法准确跨回变化前状态。
        previous.time = Mathf.Max(previous.time, now - TransitionBoundaryOffset);
        snapshots[lastIndex] = previous;
        snapshots.Add(new IceBlockSnapshot { time = now, isBroken = isBroken });
    }

    public override void StartRewind()
    {
        if (IsRewindOperationLocked) return;
        if (snapshots.Count == 0) return;

        float now = Time.time;
        TrimExpiredSnapshots(now, GetRewindableSeconds());

        IceBlockSnapshot[] playbackSnapshots = snapshots.ToArray();
        int lastIndex = playbackSnapshots.Length - 1;
        IceBlockSnapshot current = playbackSnapshots[lastIndex];
        current.time = now;
        playbackSnapshots[lastIndex] = current;

        StartCoroutine(RewindBackwards(playbackSnapshots, ApplySnapshot, rewindSpeed,
            GetRewindableSeconds, rewindCooldownSeconds));
    }

    /// <summary>裁剪过期变化，仅保留窗口内记录及其前一个边界状态。</summary>
    private void TrimExpiredSnapshots(float now, float rewindableSeconds)
    {
        if (snapshots.Count <= 1) return;

        float expireBefore = now - Mathf.Max(0f, rewindableSeconds);
        int firstRecentIndex = -1;
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].time >= expireBefore)
            {
                firstRecentIndex = i;
                break;
            }
        }

        if (firstRecentIndex < 0)
        {
            IceBlockSnapshot current = snapshots[snapshots.Count - 1];
            snapshots.Clear();
            snapshots.Add(current);
            return;
        }

        int removeCount = Mathf.Max(0, firstRecentIndex - 1);
        if (removeCount > 0)
            snapshots.RemoveRange(0, removeCount);
    }

    private void ApplySnapshot(IceBlockSnapshot snap)
    {
        // 还原碎裂状态：状态不同才切（避免重复 SetActive）
        if (snap.isBroken != isBroken)
        {
            SetBroken(snap.isBroken);
            // 同步 Ice 的 isBroken 标志，保持一致
            if (snap.isBroken) ice?.Break();     // 已碎则幂等
            else ice?.Restore();
        }
    }

    private float GetRewindableSeconds()
    {
        if (ReincarnationManager.Instance != null)
            return ReincarnationManager.Instance.CurrentRewindableSeconds;
        return 8f;
    }

    protected override bool ShouldContinueRewind() => Input.GetKey(KeyCode.R);

    protected override void OnFreeze() { /* 静态冰块无需冻结特殊处理 */ }

    protected override void OnUnfreeze() { /* 无需解冻处理 */ }

    protected override void ClearSnapshots() => snapshots.Clear();
}
