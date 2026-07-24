using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 压力板：水流末端检测到时调用 Activate()，触发 Inspector 配置的 UnityEvent。
/// 设计为被动触发器（自身不监听物理），由 WaterSource 通过 CompareTag("PressurePlate") + GetComponent 调用。
/// 用法：在 onActivate 里拖入机关方法（如 SpikeLauncher.Activate），水流流到压力板就触发机关。
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("被激活时触发。在水流末端命中此压力板时调用。")]
    [SerializeField] private UnityEvent onActivate;

    [Tooltip("被解除时触发。可选，目前水流系统不主动调用，预留接口。")]
    [SerializeField] private UnityEvent onDeactivate;

    /// <summary>激活压力板（水流命中时由 WaterSource 调用）。</summary>
    public void Activate()
    {
        onActivate?.Invoke();
    }

    /// <summary>解除激活（预留接口）。</summary>
    public void Deactivate()
    {
        onDeactivate?.Invoke();
    }
}
