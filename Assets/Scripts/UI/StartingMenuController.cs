using UnityEngine;

/// <summary>
/// StartingMenu 控制器：根据本次运行是否已通关（GoalTrigger.CompletedThisSession）
/// 决定是否显示 "Again and Again..." 文本。整个运行时保留。
/// 挂到 MenuCanvas 上。
///
/// 注意：菜单画布本身为场景驱动（随 StartingMenu 场景加载而可见），
/// 不参与 UIManager 的开合/焦点栈，避免 ESC 误关菜单；
/// 此处仅声明 Menu 互斥组并暴露 ToggleCanvasEvent 供后续需要时接入。
/// </summary>
public class StartingMenuController : MonoBehaviour, ICanvasManager
{
    [SerializeField] private GameObject againText;

    [Tooltip("菜单开关事件（预留；当前菜单显隐由场景加载驱动，不自动上报状态）。")]
    [SerializeField] private ToggleCanvasEventSO menuToggleEvent;

    public ToggleCanvasEventSO ToggleCanvasEvent => menuToggleEvent;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = false;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void OnEnable()
    {
        RefreshAgainText();
    }

    /// <summary>每次 MenuCanvas 启用时刷新 AgainText 显隐（覆盖从 Ending 回到菜单后再次进入的情况）。</summary>
    private void RefreshAgainText()
    {
        if (againText == null) return;
        againText.SetActive(GoalTrigger.CompletedThisSession);
    }
}
