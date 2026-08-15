using System;
using System.Collections.Generic;
using MyEnums;

/// <summary>
/// 画布焦点栈：纯 C# 逻辑，负责维护已打开画布的顺序、当前焦点、sortingOrder 计算。
/// 从 UIManager 拆出，不依赖 MonoBehaviour / ToggleCanvasEventSO，
/// 对外通过回调（OnCanvasToggleRequested / OnFocusRefreshRequested）驱动事件，
/// 便于脱离 Unity 生命周期做单元测试。
/// </summary>
public class CanvasFocusStack
{
    public const int FocusOrder = 90;
    public const int DefaultOrder = 5;
    private const int OrderStep = 10; // 相邻打开画布之间的 order 差值

    //用来合并外部（代码调用）输入
    private readonly LinkedList<CanvasToToggle> canvasOpenOrder = new();

    //用来存已打开画布的链表，顺序访问，但是可以依赖枚举任意删除节点
    private CanvasToToggle currentFocusCanvas = CanvasToToggle.Default;

    private bool isAnyCanvasOpen;

    /// <summary>请求真正开/关某画布（对应 UIManager.RaiseCanvasEvent）。</summary>
    public Action<CanvasToToggle, bool> OnCanvasToggleRequested;

    /// <summary>请求刷新某画布的 sortingOrder（对应 UIManager.RaiseFocusEvent）。</summary>
    public Action<CanvasToToggle> OnFocusRefreshRequested;

    public bool IsAnyCanvasOpen => isAnyCanvasOpen;

    public CanvasToToggle CurrentFocus => currentFocusCanvas;

    public CanvasToToggle LastOpenCanvas =>
        canvasOpenOrder.Last != null
            ? canvasOpenOrder.Last.Value
            : CanvasToToggle.Default;

    /// <summary>拖拽/外部 focus 调整入口（对应原 UIManager.HandleFocus）。</summary>
    public void HandleFocus(CanvasToToggle canvas)
    {
        if (canvas == CanvasToToggle.Default ||
            canvas == currentFocusCanvas && IsCanvasOpen(canvas))
            return;

        ApplyFocusChange(canvas);
    }

    /// <summary>外部请求关闭某画布（对应原 UIManager.RequestCanvasClose）。</summary>
    public void RequestClose(CanvasToToggle canvas)
    {
        if (canvas == CanvasToToggle.Default || !IsCanvasOpen(canvas))
        {
            return;
        }

        RaiseCanvasEvent(canvas, false);
        RefreshFocusAfterClose(canvas);
    }

    /// <summary>ESC 行为：无面板则开 ESC，有面板则关栈顶（对应原 HandleESCInput/CloseLastCanvas）。</summary>
    public void HandleESCOrCloseTop()
    {
        if (!isAnyCanvasOpen)
        {
            RaiseCanvasEvent(CanvasToToggle.ESC, true);
            return;
        }

        if (canvasOpenOrder.Last == null)
        {
            return;
        }

        CanvasToToggle canvasToClose = canvasOpenOrder.Last.Value;
        RaiseCanvasEvent(canvasToClose, false);
        RefreshFocusAfterClose(canvasToClose);
    }

    /// <summary>状态回调，画布报告的真实开启/关闭状态（对应原 UIManager.ReportState）。</summary>
    public void ReportState(CanvasToToggle canvas, bool state)
    {
        if (canvas == CanvasToToggle.Default)
        {
            return;
        }

        UpdateCanvasOpenOrder(canvas, state);
        RefreshAllCanvasOrders();

        isAnyCanvasOpen = canvasOpenOrder.Count > 0;
    }

    public bool IsFocused(CanvasToToggle canvas)
    {
        return currentFocusCanvas == canvas;
    }

    /// <summary>请求把某画布提到焦点（对应原 UIManager.ApplyFocusChange）。</summary>
    public void ApplyFocusChange(CanvasToToggle target)
    {
        bool wasTargetOpen = IsCanvasOpen(target);
        CanvasToToggle previousFocus = currentFocusCanvas;
        currentFocusCanvas = target; //标记当前focus，作为画布组设置优先、默认order的依据

        if (!wasTargetOpen)
        {
            // 没打开的画布：用 open 事件真正打开
            //（宿主侧 SetCanvaState → ReportState 会更新 open-order 链表并全量刷新 order）
            RaiseCanvasEvent(target, true);
        }
        else if (previousFocus != target)
        {
            // 已打开但不在顶层：移到链表末尾后全量刷新 order
            UpdateCanvasOpenOrder(target, true);
            RefreshAllCanvasOrders();
        }
    }

    private void RefreshFocusAfterClose(CanvasToToggle closedCanvas)
    {
        if (currentFocusCanvas != closedCanvas)
        {
            return;
        }

        // 关闭时 ReportState 已触发 RefreshAllCanvasOrders，其余打开画布的 order 已按新链表刷新
        currentFocusCanvas = LastOpenCanvas;
    }

    private void UpdateCanvasOpenOrder(CanvasToToggle canvas, bool state)
    {
        RemoveCanvasNode(canvas); //没有就不删，有的话删掉再加，确保链表的末尾那个始终是在最上层的

        if (state)
        {
            canvasOpenOrder.AddLast(canvas);
        }
    }

    // 根据 open-order 链表计算排序优先级：顶层（链表末尾）最高，向下按链表顺序递减
    public int GetCanvasOrder(CanvasToToggle canvas, bool state)
    {
        if (!state) return DefaultOrder;

        int index = GetOpenOrderIndex(canvas);
        if (index < 0) return DefaultOrder;

        int stepsFromTop = canvasOpenOrder.Count - 1 - index;
        return Math.Max(DefaultOrder, FocusOrder - stepsFromTop * OrderStep);
    }

    private int GetOpenOrderIndex(CanvasToToggle canvas)
    {
        int index = 0;
        LinkedListNode<CanvasToToggle> currentNode = canvasOpenOrder.First;
        while (currentNode != null)
        {
            if (currentNode.Value == canvas) return index;
            currentNode = currentNode.Next;
            index++;
        }
        return -1;
    }

    // 链表顺序变化（打开/关闭/提升）后，通知所有已打开画布按新顺序刷新 order
    private void RefreshAllCanvasOrders()
    {
        foreach (var canvas in canvasOpenOrder)
        {
            OnFocusRefreshRequested?.Invoke(canvas);
        }
    }

    public bool IsCanvasOpen(CanvasToToggle canvas)
    {
        LinkedListNode<CanvasToToggle> currentNode = canvasOpenOrder.First;

        while (currentNode != null)
        {
            if (currentNode.Value == canvas)
            {
                return true;
            }

            currentNode = currentNode.Next;
        }

        return false;
    }

    private void RemoveCanvasNode(CanvasToToggle canvas)
    {
        LinkedListNode<CanvasToToggle> currentNode = canvasOpenOrder.First;

        while (currentNode != null)
        {
            LinkedListNode<CanvasToToggle> nextNode = currentNode.Next;

            if (currentNode.Value == canvas)
            {
                canvasOpenOrder.Remove(currentNode);

                break;
            }

            currentNode = nextNode;
        }
    }

    private void RaiseCanvasEvent(CanvasToToggle target, bool state)
    {
        OnCanvasToggleRequested?.Invoke(target, state);
    }

    /// <summary>清空栈状态（对应原 UIManager.ResetCanvas 的纯状态部分；事件广播由宿主负责）。</summary>
    public void Clear()
    {
        currentFocusCanvas = CanvasToToggle.Default;
        isAnyCanvasOpen = false;
        canvasOpenOrder.Clear();
    }
}
