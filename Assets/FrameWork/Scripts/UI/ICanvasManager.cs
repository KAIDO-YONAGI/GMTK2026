using UnityEngine;

public interface ICanvasManager
{
    /// <summary>本面板订阅的开关事件资产（需同时挂到 UIManager 的 toggleCanvasEvents 列表）。</summary>
    ToggleCanvasEventSO ToggleCanvasEvent { get; }

    /// <summary>场景加载完成事件，面板必须在加载完成后恢复为 inactive 状态。</summary>
    VoidEventSO SceneLoadedEvent { get; }

    /// <summary>当前面板获得焦点时，是否允许通过 ESC 键关闭。</summary>
    bool CloseOnEscape => true;

    /// <summary>打开时是否阻塞全局面板输入（UIManager 吞掉所有面板切换按键，含 ESC）。
    /// 互斥配置不在这里声明——由 UIManager 组件上的互斥面板列表统一配置。</summary>
    bool BlocksGlobalInput => false;

    public void ToggleCanvas(
        CanvasGroup canvasGroup,
        Canvas canvas,
        MyEnums.CanvasToToggle canvasToToggle,
        bool state)//统一调用两个函数的接口函数，用于回调画布状态和真正设置优先级
    {
        SetCanvaState(canvasGroup, canvasToToggle, state);
        RefreshCanvaOrder(canvas, canvasToToggle, state);
        // Debug.Log("toggle&order");
    }

    void SetCanvaInactive(
        CanvasGroup canva,
        MyEnums.CanvasToToggle canvasToToggle)
    {
        SetCanvaState(canva, canvasToToggle, false);
    }

    void SetCanvaState(
          CanvasGroup canva,
          MyEnums.CanvasToToggle canvasToToggle,
          bool state)//如果操作成功了，那就连同互斥组与阻塞声明一起告知manager
    {
        canva.alpha = state ? 1 : 0;
        canva.blocksRaycasts = state;
        canva.interactable = state;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ReportCanvasState(
                canvasToToggle, state, CloseOnEscape, BlocksGlobalInput);
        }
    }

    void RefreshCanvaOrder(
       Canvas canvas,
       MyEnums.CanvasToToggle canvasToToggle,
       bool state)
    {
        int order = UIManager.Instance != null
            // 按打开顺序链表分配降序排序优先级，顶层（链表末尾）最高。
            ? UIManager.Instance.GetCanvasOrder(canvasToToggle, state)
            : UIManager.DefaultOrder;
        if (canvas == null)
        {
            return;
        }

        canvas.sortingOrder = order;
    }
}
