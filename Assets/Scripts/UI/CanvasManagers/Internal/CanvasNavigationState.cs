using System.Collections.Generic;

/// <summary>
/// UIManager 的画布导航状态。
/// 负责打开顺序、当前焦点以及关闭后的焦点恢复。
/// </summary>
internal sealed class CanvasNavigationState
{
    private readonly LinkedList<MyEnums.CanvasToToggle> openOrder =
        new LinkedList<MyEnums.CanvasToToggle>();

    public MyEnums.CanvasToToggle CurrentFocus { get; private set; } =
        MyEnums.CanvasToToggle.Default;

    public MyEnums.CanvasToToggle LastOpen =>
        openOrder.Last != null
            ? openOrder.Last.Value
            : MyEnums.CanvasToToggle.Default;

    public bool HasOpenCanvas => openOrder.Count > 0;

    public bool IsFocused(MyEnums.CanvasToToggle canvas)
    {
        return CurrentFocus == canvas;
    }

    public bool IsOpen(MyEnums.CanvasToToggle canvas)
    {
        LinkedListNode<MyEnums.CanvasToToggle> currentNode =
            openOrder.First;

        while (currentNode != null)
        {
            if (currentNode.Value == canvas)
                return true;

            currentNode = currentNode.Next;
        }

        return false;
    }

    public void ReportState(
        MyEnums.CanvasToToggle canvas,
        bool isOpen)
    {
        Remove(canvas);

        if (isOpen)
            openOrder.AddLast(canvas);
    }

    public void SetFocus(MyEnums.CanvasToToggle canvas)
    {
        CurrentFocus = canvas;
    }

    public void FocusLastOpen()
    {
        CurrentFocus = LastOpen;
    }

    public void Reset()
    {
        CurrentFocus = MyEnums.CanvasToToggle.Default;
        openOrder.Clear();
    }

    private void Remove(MyEnums.CanvasToToggle canvas)
    {
        LinkedListNode<MyEnums.CanvasToToggle> currentNode =
            openOrder.First;

        while (currentNode != null)
        {
            LinkedListNode<MyEnums.CanvasToToggle> nextNode =
                currentNode.Next;

            if (currentNode.Value == canvas)
            {
                openOrder.Remove(currentNode);
                break;
            }

            currentNode = nextNode;
        }
    }
}
