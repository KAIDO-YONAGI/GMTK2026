using System;
using UnityEngine;
[CreateAssetMenu(fileName = "ToggleCanvasEventSO", menuName = "Events/ToggleCanvasEventSO", order = 0)]
// 创建后不要忘记绑定到 UIManager；若有键盘按键输入可一并绑定，没有也不影响。
public class ToggleCanvasEventSO : ScriptableObject
{
    public event Action<bool> toggleCanvasEvent;
    /// <summary>
    /// 画布焦点事件：只调整画布的排序优先级。
    /// 也可以组合 canvasState=true 实现画布组互斥，适合在 ICanvasManager 中放一个状态枚举进行规范。
    /// 与 toggleCanvasEvent 分离，避免复用“打开”语义来表达置顶或降级。
    /// </summary>
    public event Action focusEvent;
    public MyEnums.CanvasToToggle canvasToToggle;
    public void RaiseToggleCanvasEvent(bool state)
    {
        toggleCanvasEvent?.Invoke(state);
    }
    public void RaiseFocusEvent()
    {
        focusEvent?.Invoke();
    }
}
