using System;
using UnityEngine;

/// <summary>
/// 带 float 参数的通用事件通道（参照 VoidEventSO / DeathEventSO 模式）。
/// 用于广播一个浮点值，例如回溯倒放时每帧广播当前快照的时间戳。
/// </summary>
[CreateAssetMenu(fileName = "FloatEventSO", menuName = "Events/FloatEventSO", order = 0)]
public class FloatEventSO : ScriptableObject
{
    public event Action<float> FloatEvent;

    public void OnEventRaised(float value)
    {
        FloatEvent?.Invoke(value);
    }
}
