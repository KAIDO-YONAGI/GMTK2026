using System;
using UnityEngine;

/// <summary>
/// 冰块标记组件 + 可破坏回调。
/// 兼容现有用法：FlyingSpike / PickupSpike 通过 TryGetComponent&lt;Ice&gt; 检测到冰后直接 Destroy，
/// OnDestroy 会自动触发 OnBroken 回调，水源点据此更新单元状态（碎冰→变回水）。
/// </summary>
public class Ice : MonoBehaviour
{
    /// <summary>冰块被破坏时触发（参数：被破坏的冰块 GameObject，即本物体）。</summary>
    public event Action<GameObject> OnBroken;

    // 标记是否已显式破碎过，避免 OnDestroy 时重复通知（场景切换销毁也算）
    private bool isBroken;

    /// <summary>显式破碎入口：通知 OnBroken 后销毁自身。</summary>
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;
        try { OnBroken?.Invoke(gameObject); } catch (Exception e) { Debug.LogException(e); }
        if (this != null) Destroy(gameObject);
    }

    /// <summary>被动销毁也通知一次（FlyingSpike 等直接 Destroy 走这条路径）。</summary>
    private void OnDestroy()
    {
        if (isBroken) return;
        isBroken = true;
        try { OnBroken?.Invoke(gameObject); } catch { /* 对象已在销毁，仅吞异常 */ }
    }
}
