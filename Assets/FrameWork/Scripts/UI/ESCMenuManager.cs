using UnityEngine;
using UnityEngine.SceneManagement;

public class ESCMenuManager : MonoBehaviour, ICanvasManager
{
    public CanvasGroup ESCGroup;
    public ToggleCanvasEventSO toggleESCEvent;

    [Header("场景加载完成事件")]
    [Tooltip("场景加载完成事件，加载完成后将 ESC 面板设为 inactive。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    [Header("界面管理器集成")]
    [Tooltip("当前面板获得焦点时，是否允许通过 ESC 键关闭。")]
    [SerializeField] private bool closeOnEscape = true;

    [Tooltip("打开时阻塞全局面板输入：ESC 无法关闭本面板、也无法唤起/关闭其它面板。面板自身按钮不受影响。")]
    [SerializeField] private bool blocksGlobalInput = false;

    private Canvas escCanvas;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleESCEvent;
    public VoidEventSO SceneLoadedEvent => sceneLoadedEvent;
    public bool CloseOnEscape => closeOnEscape;
    public bool BlocksGlobalInput => blocksGlobalInput;

    private void Awake()
    {
        escCanvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        if (toggleESCEvent != null)
        {
            toggleESCEvent.toggleCanvasEvent += OnESC;
            toggleESCEvent.focusEvent += OnFocus;
        }
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }
    private void OnDisable()
    {
        if (toggleESCEvent != null)
        {
            toggleESCEvent.toggleCanvasEvent -= OnESC;
            toggleESCEvent.focusEvent -= OnFocus;
        }
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        ((ICanvasManager)this).SetCanvaInactive(
            ESCGroup, MyEnums.CanvasToToggle.ESC);
    }

    private void OnESC(bool state)
    {
        SceneChanger sceneChanger = SceneChanger.Instance;
        GameSceneSO currentScene = sceneChanger != null
            ? sceneChanger.GetCurrentGameScene()
            : null;
        if (currentScene != null && currentScene.sceneType == MyEnums.SceneType.Menu)
            return;
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

    /// <summary>继续按钮的回调：请求 UIManager 关闭 ESC 面板。</summary>
    public void OnContinueButton()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
    }

    /// <summary>菜单按钮的回调：通过 SceneChanger 返回主菜单。</summary>
    public void OnReturnToMenuButton()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
        // 通过 SceneLoadEventSO 触发切换到 StartingMenu。
        var sc = SceneChanger.Instance;
        if (sc != null && sc.loadEventSO != null && sc.firstSceneToLoad != null && sc.firstSceneToLoad.Count > 0)
            sc.loadEventSO.RaiseLoadRequestEvent(sc.firstSceneToLoad, Vector3.zero, true);
    }

}
