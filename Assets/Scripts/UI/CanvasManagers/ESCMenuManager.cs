using UnityEngine;

public class ESCMenuManager : MonoBehaviour, ICanvasManager
{
    public CanvasGroup ESCGroup;
    public ToggleCanvasEventSO toggleESCEvent;

    [Header("UIManager 集成")]
    [Tooltip("Whether Escape may close this panel while it is focused.")]
    [SerializeField] private bool closeOnEscape = true;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = false;

    private Canvas escCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleESCEvent;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        escCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        toggleESCEvent.toggleCanvasEvent += OnESC;
        toggleESCEvent.focusEvent += OnFocus;
    }
    private void OnDisable()
    {
        toggleESCEvent.toggleCanvasEvent -= OnESC;
        toggleESCEvent.focusEvent -= OnFocus;
    }

    private void OnESC(bool state)
    {
        if (state)
            TimeManager.Instance.PauseGame();
        else
            TimeManager.Instance.ResumeGame();

        ((ICanvasManager)this).ToggleCanvas(
            ESCGroup, escCanvas, MyEnums.CanvasToToggle.ESC, state);
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(
            escCanvas, MyEnums.CanvasToToggle.ESC, true);
    }

    /// <summary>Resume 按钮的回调：请求 UIManager 关闭 ESC 面板</summary>
    public void OnResumeButton()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
    }

    /// <summary>Menu 按钮的回调：返回主菜单（通过 SceneChanger 触发）</summary>
    public void OnReturnToMenuButton()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
        // 触发场景切换到 StartingMenu（通过 SceneLoadEventSO）
        var sc = SceneChanger.Instance;
        if (sc != null && sc.loadEventSO != null && sc.initScene != null && sc.initScene.Count > 0)
            sc.loadEventSO.RaiseLoadRequestEvent(sc.initScene, Vector3.zero, true);
    }

}
