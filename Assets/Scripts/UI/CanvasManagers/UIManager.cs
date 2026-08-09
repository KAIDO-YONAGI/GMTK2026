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

    private readonly CanvasInputState inputState = new CanvasInputState();
    private readonly CanvasNavigationState navigationState =
        new CanvasNavigationState();

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
            canvas == navigationState.CurrentFocus &&
            navigationState.IsOpen(canvas))
        {
            return;
        }

        ApplyFocusChange(canvas);
    }

    // 用于外部切换请求的画布/默认状态。
    public void RequestCanvasToggle(MyEnums.CanvasToToggle canvas)
    {
        inputState.Request(canvas);
    }

    public void RequestCanvasClose(MyEnums.CanvasToToggle canvas)
    {
        if (canvas == MyEnums.CanvasToToggle.Default ||
            !navigationState.IsOpen(canvas))
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

        navigationState.ReportState(canvas, state);
    }

    public bool IsCanvasFocused(MyEnums.CanvasToToggle canvas)
    {
        return navigationState.IsFocused(canvas);
    }

    private void ToggleCanvas()
    {
        // 读取已注册的输入绑定；未注册的画布仍可使用RequestCanvasToggle。
        foreach (var binding in inputBindings)
        {
            if (Input.GetButtonDown(binding.buttonName))
                inputState.Request(binding.canvas);
            // 外部请求和按键按下都可以触发切换。
        }

        if (inputState.IsRequested(MyEnums.CanvasToToggle.ESC))
        {
            HandleESCInput();
            ResetInputState();
            return;
        }

        if (navigationState.LastOpen == MyEnums.CanvasToToggle.ESC)
        {
            ResetInputState();
            return;
        }

        MyEnums.CanvasToToggle requestedCanvas =
            MyEnums.CanvasToToggle.Default;
        foreach (var binding in inputBindings)
        {
            if (binding.canvas == MyEnums.CanvasToToggle.ESC)
            {
                continue;
            }

            if (inputState.IsRequested(binding.canvas))
            {
                requestedCanvas = binding.canvas;

                break;// 只处理本帧的第一个输入。
            }
        }

        if (requestedCanvas != MyEnums.CanvasToToggle.Default)
        {
            ApplyFocusChange(requestedCanvas);
        }

        ResetInputState();
    }

    private void HandleESCInput()
    {
        if (!navigationState.HasOpenCanvas)
        {
            RaiseCanvasEvent(MyEnums.CanvasToToggle.ESC, true);
            return;
        }

        CloseLastCanvas();
    }

    private void CloseLastCanvas()
    {
        MyEnums.CanvasToToggle canvasToClose = navigationState.LastOpen;
        if (canvasToClose == MyEnums.CanvasToToggle.Default)
        {
            return;
        }

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
        bool wasTargetOpen = navigationState.IsOpen(target);
        MyEnums.CanvasToToggle previousFocus =
            navigationState.CurrentFocus;
        navigationState.SetFocus(target);

        if (previousFocus != MyEnums.CanvasToToggle.Default &&//Default意味着没有面板打开
            previousFocus != target &&
            navigationState.IsOpen(previousFocus))//不是当前目标，并且打开，再次发送true通知画布降低order
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
        if (navigationState.CurrentFocus != closedCanvas)
        {
            return;
        }

        navigationState.FocusLastOpen();
        MyEnums.CanvasToToggle currentFocus =
            navigationState.CurrentFocus;

        if (currentFocus != MyEnums.CanvasToToggle.Default)
        {
            RaiseCanvasEvent(currentFocus, true);
        }
    }

    private void ResetInputState()
    {
        inputState.Reset();
    }

    private void ResetCanvas()
    {
        navigationState.Reset();

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
