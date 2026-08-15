using System;
using UnityEngine;
[CreateAssetMenu(fileName = "ToggleCanvasEventSO", menuName = "Events/ToggleCanvasEventSO", order = 0)]
//创建好之后不要忘了绑定到UIManager如果有键盘按键输入，可以一块绑定，没有也不影响
public class ToggleCanvasEventSO : ScriptableObject
{
    public event Action<bool> toggleCanvasEvent;
    /// <summary>
    /// 画布 focus 事件：只调整画布的排序优先级
    /// 也可以组合canvasState=true 用于实现画布组互斥 适合在ICanvasManager里放一个状态枚举来规范
    /// 与 toggleCanvasEvent 分离，避免复用 open 语义来传达置顶/降级。
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
