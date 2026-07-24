using UnityEngine;

/// <summary>
/// 水流单元状态机：管理单个格子的水流表现。
/// 三个子节点分别对应四相：
///   Stage1（少量水动画） → Stage2（满水动画） → Frozen（冰，带 Ice 标记 + 碰撞） → Broken（碎冰后一滩水，无碰撞）。
/// 单元本身不自驱相位推进（由 WaterSource 统一计时），只暴露 SetPhase / ForcePhase 供水源调用。
/// 水流阶段（Stage1/Stage2）始终无碰撞，仅 Frozen 阶段启用 BoxCollider2D。
/// </summary>
public class WaterFlowUnit : MonoBehaviour
{
    public enum WaterPhase
    {
        Stage1, // 少量水（默认初始相位）
        Stage2, // 满水
        Frozen, // 结冰（带碰撞）
        Broken  // 碎冰后（一滩水、可通行）
    }

    [Header("Child References")]
    [Tooltip("少量水阶段动画子节点（含 SpriteRenderer + Animator）。")]
    [SerializeField] private GameObject stage1Object;
    [Tooltip("满水阶段动画子节点（含 SpriteRenderer + Animator）。")]
    [SerializeField] private GameObject stage2Object;
    [Tooltip("冰贴图子节点（含 SpriteRenderer + Ice + BoxCollider2D）。")]
    [SerializeField] private GameObject iceObject;

    /// <summary>当前相位（只读）。</summary>
    public WaterPhase Phase { get; private set; }

    /// <summary>本单元出生时间（Time.time），由 WaterSource 在 Init 时设置。</summary>
    public float BirthTime { get; private set; }

    /// <summary>
    /// 是否锁定在 Stage1 不自动推进到 Stage2。
    /// 用于拖尾单元：贴地的视觉装饰，始终保持少量水动画，不演变成满水。
    /// </summary>
    public bool LockAtStage1 { get; set; }

    private Ice iceComponent;
    private BoxCollider2D iceCollider;

    private void Awake()
    {
        if (iceObject != null)
        {
            iceComponent = iceObject.GetComponent<Ice>();
            iceCollider = iceObject.GetComponent<BoxCollider2D>();
        }
        ApplyPhaseVisual(WaterPhase.Stage1);
    }

    /// <summary>初始化：记录出生时间、注册碎冰回调。</summary>
    /// <param name="owner">所属水源点，用于碎冰后通知（可为 null 表示不需要通知）。</param>
    public void Init(WaterSource owner)
    {
        BirthTime = Time.time;
        if (iceComponent != null && owner != null)
        {
            // 捕获 owner 到局部变量，避免闭包持有 owner 句柄在销毁后还在调用
            var ownerRef = owner;
            // 冰被击碎（Ice.Break()）时通知水源点。IceImg 子物体不被销毁，
            // NotifyUnitBroken 把单元切回 Stage2（水），后续可再次结冰（水↔冰循环）。
            iceComponent.OnBroken += _ => ownerRef.NotifyUnitBroken(this);
        }
    }

    /// <summary>切换相位（会触发副作用：冰阶段启用碰撞，碎冰关闭碰撞）。</summary>
    public void SetPhase(WaterPhase phase)
    {
        Phase = phase;
        ApplyPhaseVisual(phase);
    }

    /// <summary>回溯还原专用：直接切相位且不触发任何额外副作用（仅视觉/碰撞按相位同步）。</summary>
    public void ForcePhase(WaterPhase phase)
    {
        Phase = phase;
        ApplyPhaseVisual(phase);
    }

    /// <summary>按相位统一切换三个子节点激活与冰碰撞。对已销毁的子物体跳过（防 OnDestroy 回调链报错）。</summary>
    private void ApplyPhaseVisual(WaterPhase phase)
    {
        bool s1 = phase == WaterPhase.Stage1;
        bool s2 = phase == WaterPhase.Stage2 || phase == WaterPhase.Broken; // 碎冰后视觉用满水（一滩水）
        bool ice = phase == WaterPhase.Frozen;

        // 子物体可能正被外部销毁（如 FlyingSpike 击碎 IceImg），操作前判 null 且仅在状态不同时 SetActive。
        SafeSetActive(stage1Object, s1);
        SafeSetActive(stage2Object, s2);
        SafeSetActive(iceObject, ice);
        if (iceCollider != null) iceCollider.enabled = ice; // 仅冰阶段启用碰撞
    }

    /// <summary>安全 SetActive：物体为 null 或正在销毁时跳过，避免 "being destroyed" 报错。</summary>
    private static void SafeSetActive(GameObject obj, bool value)
    {
        if (obj == null) return;
        if (obj.activeSelf == value) return; // 状态一致就不操作，减少销毁中报错概率
        obj.SetActive(value);
    }
}
