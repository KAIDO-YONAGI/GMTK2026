using System;
using UnityEngine;

/// <summary>
/// 带 int 参数的通用事件通道（参照 VoidEventSO / FloatEventSO 模式）。
/// 用于广播一个整数值，例如血量变化时广播当前血量。
/// </summary>
[CreateAssetMenu(fileName = "IntEventSO", menuName = "Events/IntEventSO", order = 0)]
public class IntEventSO : ScriptableObject
{
    public event Action<int> IntEvent;

    public void OnEventRaised(int value)
    {
        IntEvent?.Invoke(value);
    }
}
