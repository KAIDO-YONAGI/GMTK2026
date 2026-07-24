using System;
using UnityEngine;

/// <summary>
/// 冰块标记组件 + 可破坏回调。
/// 击碎冰块时必须调用 Break()——它只切换"碎裂状态"（设 isBroken + 触发 OnBroken），
/// 不销毁 GameObject。这样冰块（含 IceBlock 独立预制件）的回溯组件得以保留，可被回溯还原。
/// 外部（FlyingSpike / PickupSpike）负责调用 Break()，冰块自身或其管理者监听 OnBroken 来切视觉/碰撞。
/// </summary>
public class Ice : MonoBehaviour
{
    /// <summary>冰块被破坏时触发（参数：被破坏的冰块 GameObject，即本物体）。</summary>
    public event Action<GameObject> OnBroken;

    /// <summary>是否已碎裂。</summary>
    public bool IsBroken => isBroken;

    // 标记是否已显式破碎过
    private bool isBroken;

    /// <summary>
    /// 显式破碎入口：设 isBroken + 触发 OnBroken 事件。
    /// 不销毁 GameObject——碎裂状态由监听者（WaterFlowUnit / IceBlock）处理视觉和碰撞。
    /// 多次调用幂等（已碎裂则忽略）。
    /// </summary>
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;
        try { OnBroken?.Invoke(gameObject); }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>
    /// 重置碎裂状态（回溯还原用）。
    /// 清除 isBroken 标志，让冰块回到"完整"状态。视觉/碰撞由监听者根据 OnBroken 之外的方式同步。
    /// </summary>
    public void Restore()
    {
        isBroken = false;
    }
}
