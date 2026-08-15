using System;
using System.Collections.Generic;
using MyEnums;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class UIManager : YSingleton<UIManager>
{
    // 常量保留转发，避免破坏 ICanvasManager.RefreshCanvaOrder 对 UIManager.DefaultOrder 的引用。
    public const int FocusOrder = CanvasFocusStack.FocusOrder;
    public const int DefaultOrder = CanvasFocusStack.DefaultOrder;

    [Header("事件")]
    [Tooltip("场景加载请求事件：收到换场景请求时复位并关闭所有面板。")]
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Tooltip("界面管理器可驱动的面板开关事件列表。按键唤起、ESC 关闭和切场景复位都从这里发信号；面板的开关事件资源必须列在此处，否则无法由界面管理器唤起。")]
    [SerializeField] private List<ToggleCanvasEventSO> toggleCanvasEvents;

    [Header("输入绑定")]
    [Tooltip("按键绑定（旧输入系统）。每项对应一个面板和按键；ESC 走特殊通道：无面板打开时唤起 ESC 菜单，有面板时关闭最上层。")]
    [SerializeField] private List<CanvasInputBinding> inputBindings;

    [Header("互斥面板")]
    [Tooltip("互斥面板列表：列在此处的面板互相互斥——任一打开时自动关闭其它已打开的互斥面板；未列出的可与任意面板共存")]
    [SerializeField] private List<CanvasToToggle> mutexCanvases = new List<CanvasToToggle>();

    // 画布焦点栈：纯 C# 逻辑（打开顺序链表、焦点和排序优先级计算）。
    private readonly CanvasFocusStack focusStack = new();

    // 各画布上报的阻塞声明（ReportCanvasState 时登记）
    private readonly Dictionary<CanvasToToggle, bool> canvasBlocksInput = new();
    private readonly Dictionary<CanvasToToggle, bool> canvasCloseOnEscape = new();

    // 外部（代码/按键）输入合并到这里
    private readonly Dictionary<CanvasToToggle, bool> inputState = new();

    protected override void OnSingletonInitialized()
    {
        foreach (CanvasToToggle canvas in
                 Enum.GetValues(typeof(CanvasToToggle)))
        {
            inputState[canvas] = false;
        }

        // 接线：纯逻辑栈通过回调驱动 SO 事件，保持自身不依赖 ToggleCanvasEventSO。
        focusStack.OnCanvasToggleRequested = RaiseCanvasEvent;
        focusStack.OnFocusRefreshRequested = RaiseFocusEvent;
    }

    private void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadScene;
    }

    private void OnDisable()
    {
        loadEventSO.LoadRequestEvent -= OnLoadScene;
    }

    private void OnLoadScene(List<GameSceneSO> arg0, Vector3 arg1, bool arg2)
    // UIManager 作为跨场景持久单例，不会随场景卸载而失活，因此需要订阅场景加载事件以主动重置画布状态。
    // LoadRequestEvent 是同步委托，UIManager 的执行顺序 (-100) 早于 SceneChanger，因此 OnLoadScene
    // 会在 SceneChanger 开始异步卸载和加载流程前同步执行，确保所有 UI 面板在过渡动画和旧场景卸载前关闭。
    // 虽然异步等待也能为这里争取时间，但仍需注意潜在的时序冲突。
    {
        ResetCanvas();
    }

    private void Update()
    {
        ToggleCanvas();
    }

    public void HandleFocus(CanvasToToggle canvas) // 拖拽脚本的输入，用于调整焦点
    {
        focusStack.HandleFocus(canvas);
    }

    // 用于外部切换请求的画布/默认状态。
    public void RequestCanvasToggle(CanvasToToggle canvas)
    {
        if (!inputState.ContainsKey(canvas))
        {
            return;
        }

        inputState[canvas] = true;
    }

    public void RequestCanvasClose(CanvasToToggle canvas)
    {
        focusStack.RequestClose(canvas);
    }

    // 状态回调：画布报告真实的开启或关闭状态；互斥由本组件的 mutexCanvases 列表解析，阻塞声明由面板传入。
    public void ReportCanvasState(
        CanvasToToggle canvas,
        bool state,
        bool closeOnEscape = true,
        bool blocksGlobalInput = false)
    {
        if (canvas == CanvasToToggle.Default)
        {
            return;
        }

        canvasCloseOnEscape[canvas] = closeOnEscape;
        canvasBlocksInput[canvas] = blocksGlobalInput;

        // 互斥：开启时自动关闭其它已打开的互斥面板。
        if (state && IsMutexCanvas(canvas))
        {
            CloseOtherMutexCanvases(canvas);
        }

        focusStack.ReportState(canvas, state);
    }

    public bool IsCanvasFocused(CanvasToToggle canvas)
    {
        return focusStack.IsFocused(canvas);
    }

    // 根据打开顺序链表计算排序优先级（转发给焦点栈），由 ICanvasManager.RefreshCanvaOrder 调用。
    public int GetCanvasOrder(CanvasToToggle canvas, bool state)
    {
        return focusStack.GetCanvasOrder(canvas, state);
    }

    private void CloseOtherMutexCanvases(CanvasToToggle opening)
    {
        // 遍历副本：RequestClose 会经由面板回调修改焦点栈。
        foreach (CanvasToToggle openCanvas in focusStack.GetOpenCanvases())
        {
            // 仅关闭可关闭面板：对“仅上报面板”发出无效关闭会导致焦点栈与真实显隐状态错位。
            if (openCanvas != opening && IsMutexCanvas(openCanvas) && IsClosableCanvas(openCanvas))
            {
                focusStack.RequestClose(openCanvas);
            }
        }
    }

    // 是否为互斥面板：列在 mutexCanvases 里即参与互斥，未列出则可与任意面板共存。
    private bool IsMutexCanvas(CanvasToToggle canvas)
    {
        return mutexCanvases != null && mutexCanvases.Contains(canvas);
    }

    // 是否为可关闭面板：开关事件在 toggleCanvasEvents 列表中，可被按键唤起、ESC 关闭、切场景复位和互斥关闭。
    // 不在列表中的面板即使上报状态（仅上报层级），UIManager 也不主动关闭它，显隐由其自身逻辑决定。
    private bool IsClosableCanvas(CanvasToToggle canvas)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO != null && eventSO.canvasToToggle == canvas)
                return true;
        }

        return false;
    }

    // 是否有"阻塞全局输入"的画布处于打开状态（按焦点栈开放列表推导，避免计数漂移）。
    private bool IsGlobalInputBlocked()
    {
        foreach (CanvasToToggle openCanvas in focusStack.GetOpenCanvases())
        {
            if (canvasBlocksInput.TryGetValue(openCanvas, out bool blocks) && blocks)
            {
                return true;
            }
        }

        return false;
    }

    private void ToggleCanvas()
    {
        // 读取已注册的输入绑定；未注册的画布仍可使用 RequestCanvasToggle。
        foreach (var binding in inputBindings)
        {
            bool pressed = binding.key != KeyCode.None && Input.GetKeyDown(binding.key);
            inputState[binding.canvas] = inputState[binding.canvas] || pressed;
            // 外部请求和按键按下都可以触发切换。
        }

        // 阻塞面板（如 GameOver 和 Ending）打开时，吞掉所有面板切换输入（含 ESC 与外部切换请求）。
        // 注意：RequestCanvasClose 不受影响，阻塞面板自身的关闭按钮仍可直接关闭它。
        if (IsGlobalInputBlocked())
        {
            ResetInputState();
            return;
        }

        if (inputState[CanvasToToggle.ESC])
        {
            CanvasToToggle top = focusStack.LastOpenCanvas;
            bool canCloseTop =
                top != CanvasToToggle.Default &&
                IsClosableCanvas(top) &&
                (!canvasCloseOnEscape.TryGetValue(top, out bool closeOnEscape) || closeOnEscape);

            if (canCloseTop)
                focusStack.HandleESCOrCloseTop();
            else
                focusStack.HandleESCOrOpen();
            ResetInputState();
            return;
        }

        if (focusStack.LastOpenCanvas == CanvasToToggle.ESC)
        {
            ResetInputState();
            return;
        }

        CanvasToToggle canvasToToggle = CanvasToToggle.Default;
        foreach (var binding in inputBindings)
        {
            if (binding.canvas == CanvasToToggle.ESC)
            {
                continue;
            }

            if (inputState[binding.canvas])
            {
                canvasToToggle = binding.canvas;

                break; // 只处理本帧的第一个输入。
            }
        }

        if (canvasToToggle != CanvasToToggle.Default)
        {
            focusStack.ApplyFocusChange(canvasToToggle);
        }

        ResetInputState();
    }

    private void RaiseCanvasEvent(CanvasToToggle target, bool state)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO.canvasToToggle == target)
            {
                eventSO.RaiseToggleCanvasEvent(state);
                return;
            }
        }
    }

    private void RaiseFocusEvent(CanvasToToggle target)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO.canvasToToggle == target)
            {
                eventSO.RaiseFocusEvent();
                return;
            }

        }
    }

    private void ResetInputState()
    {
        var keys = new List<CanvasToToggle>(inputState.Keys);
        foreach (var key in keys)
        {
            inputState[key] = false;
        }
    }

    private void ResetCanvas()
    {
        focusStack.Clear();

        foreach (var eventSO in toggleCanvasEvents)
        {
            eventSO.RaiseToggleCanvasEvent(false);
        }

        ResetInputState();
    }
}

[Serializable]
public class CanvasInputBinding
{
    public CanvasToToggle canvas;
    public KeyCode key = KeyCode.None;
}
