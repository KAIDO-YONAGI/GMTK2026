using UnityEngine;

/// <summary>
/// 开始菜单控制器：根据本次运行是否已通关（GoalTrigger.CompletedThisSession）
/// 决定是否显示 “Again and Again...” 文本。整个运行时保留。
/// 挂在 MenuCanvas 上。
///
/// 注意：菜单画布本身为场景驱动（随 StartingMenu 场景加载而可见），
/// 不参与 UIManager 的开关和焦点栈，避免 ESC 误关菜单；
/// 此处仅声明菜单互斥组并暴露 ToggleCanvasEvent，供后续需要时接入。
/// </summary>
public class StartingMenuController : MonoBehaviour, ICanvasManager
{
    [SerializeField] private GameObject againText;

    [Tooltip("菜单开关事件（预留；当前菜单显隐由场景加载驱动，不自动上报状态）。")]
    [SerializeField] private ToggleCanvasEventSO menuToggleEvent;

    [Tooltip("场景加载完成事件。仅当菜单接入 ToggleCanvasEventSO 时参与 inactive 重置。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Tooltip("菜单画布组。场景驱动的开始菜单不需要填写；接入 ToggleCanvasEventSO 后再填写。")]
    [SerializeField] private CanvasGroup menuCanvasGroup;

    public ToggleCanvasEventSO ToggleCanvasEvent => menuToggleEvent;
    public VoidEventSO SceneLoadedEvent => sceneLoadedEvent;

    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = false;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = false;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void OnEnable()
    {
        RefreshAgainText();

        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        // 开始菜单当前由场景驱动显示，只有接入开关事件后才执行统一 inactive 重置。
        if (menuToggleEvent == null || menuCanvasGroup == null)
            return;

        ((ICanvasManager)this).SetCanvaInactive(
            menuCanvasGroup, MyEnums.CanvasToToggle.Menu);
    }

    /// <summary>每次 MenuCanvas 启用时刷新 AgainText 显隐，覆盖从 Ending 回到菜单后再次进入的情况。</summary>
    private void RefreshAgainText()
    {
        if (againText == null) return;
        againText.SetActive(GoalTrigger.CompletedThisSession);
    }
}
