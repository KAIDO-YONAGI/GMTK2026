using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public static int FocusOrder = 90;
    public static int DefaultOrder = 5;

    [Header("Events")]
    public SceneLoadEventSO loadEventSO;
    public List<ToggleCanvasEventSO> toggleCanvasEvents;

    [Header("Input Bindings")]
    public List<CanvasInputBinding> inputBindings;
    private bool isAnyCanvasOpen;

    private MyEnums.CanvasToToggle canvasToToggle
        = MyEnums.CanvasToToggle.Default;
    private MyEnums.CanvasToToggle LastOpenCanvas =>
        canvasOpenOrder.Last != null
            ? canvasOpenOrder.Last.Value
            : MyEnums.CanvasToToggle.Default;
    private Dictionary<MyEnums.CanvasToToggle, bool> inputState = new();
    //用来合并外部（代码调用）输入
    private LinkedList<MyEnums.CanvasToToggle> canvasOpenOrder = new();
    //用来存已打开画布的链表，顺序访问，但是可以依赖枚举任意删除节点
    private MyEnums.CanvasToToggle currentFocusCanvas
        = MyEnums.CanvasToToggle.Default;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (MyEnums.CanvasToToggle canvas in
            Enum.GetValues(typeof(MyEnums.CanvasToToggle)))
        {
            inputState[canvas] = false;
        }

    }

    private void OnEnable()
    {
        loadEventSO.LoadRequestEvent += OnLoadScene;
    }

    private void OnDisable()
    {
        loadEventSO.LoadRequestEvent -= OnLoadScene;
    }

    private void OnLoadScene(GameSceneSO arg0, Vector3 arg1, bool arg2)
    //UIManager作为跨场景持久单例，不会随场景卸载而disable，因此需要订阅场景加载事件来主动重置画布状态。
    //LoadRequestEvent是同步委托，UIManager的ExecutionOrder(-100)早于SceneChanger，所以OnLoadScene
    //会在SceneChanger开始异步卸载/加载流程之前同步执行，确保所有UI面板在场景过渡动画和旧场景卸载前被关闭。
    //另外也有异步等待操作能为这里争取时间，但是还是要注意可能会导致冲突的时序问题
    {
        ResetCanvas();
    }
    private void Update()
    {
        ToggleCanvas();
    }
    public void HandleFocus(MyEnums.CanvasToToggle canvas)//拖拽脚本的输入，用于完成focus调整
    {
        if (canvas == MyEnums.CanvasToToggle.Default ||
                canvas == currentFocusCanvas && IsCanvasOpen(canvas))
            return;

        ApplyFocusChange(canvas);
    }

    // 用于外部切换请求的画布/默认状态。
    public void RequestCanvasToggle(MyEnums.CanvasToToggle canvas)
    {
        if (!inputState.ContainsKey(canvas))
        {
            return;
        }

        inputState[canvas] = true;
    }

    public void RequestCanvasClose(MyEnums.CanvasToToggle canvas)
    {
        if (canvas == MyEnums.CanvasToToggle.Default || !IsCanvasOpen(canvas))
        {
            return;
        }

        RaiseCanvasEvent(canvas, false);
        RefreshFocusAfterClose(canvas);
    }
    // 状态回调，画布报告的真实开启/关闭状态。
    public void ReportCanvasState(MyEnums.CanvasToToggle canvas, bool state)
    {
        if (canvas == MyEnums.CanvasToToggle.Default)
        {
            return;
        }

        UpdateCanvasOpenOrder(canvas, state);

        canvasToToggle = LastOpenCanvas;
        isAnyCanvasOpen = canvasOpenOrder.Count > 0;
    }

    public bool IsCanvasFocused(MyEnums.CanvasToToggle canvas)
    {
        return currentFocusCanvas == canvas;
    }

    private void ToggleCanvas()
    {
        // 读取已注册的输入绑定；未注册的画布仍可使用RequestCanvasToggle。
        foreach (var binding in inputBindings)
        {
            bool pressed = Input.GetButtonDown(binding.buttonName);
            inputState[binding.canvas] = inputState[binding.canvas] || pressed;
            // 外部请求和按键按下都可以触发切换。
        }

        if (inputState[MyEnums.CanvasToToggle.ESC])
        {
            HandleESCInput();
            ResetInputState();
            return;
        }

        if (LastOpenCanvas == MyEnums.CanvasToToggle.ESC)
        {
            ResetInputState();
            return;
        }

        canvasToToggle = MyEnums.CanvasToToggle.Default;
        foreach (var binding in inputBindings)
        {
            if (binding.canvas == MyEnums.CanvasToToggle.ESC)
            {
                continue;
            }

            if (inputState[binding.canvas])
            {
                canvasToToggle = binding.canvas;

                break;// 只处理本帧的第一个输入。
            }
        }

        if (canvasToToggle != MyEnums.CanvasToToggle.Default)
        {
            ApplyFocusChange(canvasToToggle);
        }

        ResetInputState();
    }

    private void HandleESCInput()
    {
        if (!isAnyCanvasOpen)
        {
            RaiseCanvasEvent(MyEnums.CanvasToToggle.ESC, true);
            return;
        }

        CloseLastCanvas();
    }

    private void CloseLastCanvas()
    {
        if (canvasOpenOrder.Last == null)
        {
            return;
        }

        MyEnums.CanvasToToggle canvasToClose = canvasOpenOrder.Last.Value;
        RaiseCanvasEvent(canvasToClose, false);
        RefreshFocusAfterClose(canvasToClose);
    }

    private void RaiseCanvasEvent(MyEnums.CanvasToToggle target, bool state)
    {
        foreach (var eventSO in toggleCanvasEvents)
        {
            if (eventSO.canvasToToggle != target)
            {
                continue;
            }

            eventSO.RaiseToggleCanvasEvent(state);
            return;
        }
    }

    private void ApplyFocusChange(MyEnums.CanvasToToggle target)
    {
        bool wasTargetOpen = IsCanvasOpen(target);
        MyEnums.CanvasToToggle previousFocus = currentFocusCanvas;
        currentFocusCanvas = target;//标记当前focus，作为画布组设置优先、默认order的依据

        if (previousFocus != MyEnums.CanvasToToggle.Default &&//Default意味着没有面板打开
            previousFocus != target &&
            IsCanvasOpen(previousFocus))//不是当前目标，并且打开，再次发送true通知画布降低order
        {
            RaiseCanvasEvent(previousFocus, true);
        }

        if (!wasTargetOpen || wasTargetOpen && previousFocus != target)
        //处理两种情况。首先是没打开的画布，在这打开。其次是已经打开但是不在顶层的提升到顶层
        {
            RaiseCanvasEvent(target, true);
        }
    }

    private void RefreshFocusAfterClose(MyEnums.CanvasToToggle closedCanvas)
    {
        if (currentFocusCanvas != closedCanvas)
        {
            return;
        }

        currentFocusCanvas = LastOpenCanvas;

        if (currentFocusCanvas != MyEnums.CanvasToToggle.Default)
        {
            RaiseCanvasEvent(currentFocusCanvas, true);
        }
    }

    private void UpdateCanvasOpenOrder(MyEnums.CanvasToToggle canvas, bool state)
    {
        RemoveCanvasNode(canvas);//没有就不删，有的话删掉再加，确保链表的末尾那个始终是在最上层的

        if (state)
        {
            canvasOpenOrder.AddLast(canvas);
        }
    }

    private bool IsCanvasOpen(MyEnums.CanvasToToggle canvas)
    {
        LinkedListNode<MyEnums.CanvasToToggle> currentNode = canvasOpenOrder.First;

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

    private void RemoveCanvasNode(MyEnums.CanvasToToggle canvas)
    {
        LinkedListNode<MyEnums.CanvasToToggle> currentNode = canvasOpenOrder.First;

        while (currentNode != null)
        {
            LinkedListNode<MyEnums.CanvasToToggle> nextNode = currentNode.Next;

            if (currentNode.Value == canvas)
            {
                canvasOpenOrder.Remove(currentNode);

                break;
            }

            currentNode = nextNode;
        }
    }

    private void ResetInputState()
    {
        var keys = new List<MyEnums.CanvasToToggle>(inputState.Keys);
        foreach (var key in keys)
        {
            inputState[key] = false;
        }
    }

    private void ResetCanvas()
    {
        canvasToToggle = MyEnums.CanvasToToggle.Default;
        currentFocusCanvas = MyEnums.CanvasToToggle.Default;
        isAnyCanvasOpen = false;
        canvasOpenOrder.Clear();

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
    public MyEnums.CanvasToToggle canvas;
    public string buttonName;
}
