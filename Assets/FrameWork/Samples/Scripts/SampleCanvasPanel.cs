using UnityEngine;

/// <summary>
/// 不依赖玩法代码的示例面板控制器。
/// 可在样例中复用为菜单、濒死、结局或游戏结束面板。
/// </summary>
public class SampleCanvasPanel : MonoBehaviour, ICanvasManager
{
    [Header("面板")]
    [Tooltip("当前实例代表的面板类型。事件资产中的类型应与这里一致。")]
    [SerializeField] private MyEnums.CanvasToToggle canvasType = MyEnums.CanvasToToggle.Default;

    [Tooltip("供 UIManager 驱动当前面板开关与焦点刷新的事件资产。")]
    [SerializeField] private ToggleCanvasEventSO toggleCanvasEvent;

    [Tooltip("控制当前面板显隐和交互的画布组。")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("场景加载完成事件，加载完成后将示例面板设为 inactive。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("界面管理器集成")]
    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = true;

    [Tooltip("面板打开时，是否阻塞 ESC 键及其他全局面板切换输入。")]
    [SerializeField] private bool blocksGlobalInput;

    private Canvas targetCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleCanvasEvent;
    public VoidEventSO SceneLoadedEvent => sceneLoadedEvent;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        targetCanvas = GetComponentInParent<Canvas>();
        SetVisualState(false);
    }

    private void OnEnable()
    {
        if (toggleCanvasEvent != null)
        {
            toggleCanvasEvent.toggleCanvasEvent += OnToggleRequested;
            toggleCanvasEvent.focusEvent += OnFocusRequested;
        }

        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (toggleCanvasEvent != null)
        {
            toggleCanvasEvent.toggleCanvasEvent -= OnToggleRequested;
            toggleCanvasEvent.focusEvent -= OnFocusRequested;
        }

        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        if (canvasGroup == null)
            return;

        if (canvasType == MyEnums.CanvasToToggle.Default)
        {
            SetVisualState(false);
            return;
        }

        ((ICanvasManager)this).SetCanvaInactive(canvasGroup, canvasType);
    }

    private void OnToggleRequested(bool state)
    {
        if (canvasGroup == null || canvasType == MyEnums.CanvasToToggle.Default)
            return;

        ((ICanvasManager)this).ToggleCanvas(
            canvasGroup, targetCanvas, canvasType, state);
    }

    private void OnFocusRequested()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(
            targetCanvas, canvasType, true);
    }

    public void ClosePanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RequestCanvasClose(canvasType);
    }

    private void SetVisualState(bool state)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = state ? 1f : 0f;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }
}
