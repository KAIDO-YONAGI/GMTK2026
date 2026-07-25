using UnityEngine;

public class ESCMenuManager : MonoBehaviour
{
    public CanvasGroup ESCGroup;
    public ToggleCanvasEventSO toggleESCEvent;

    [Header("Retry")]
    // 引用 Assets/SOs/Events/RetryEvent.asset（与 GameOver 面板共用同一 SO，由每场景的 RetryManager 监听）
    public VoidEventSO retryEventSO;
    private void OnEnable()
    {
        toggleESCEvent.toggleCanvasEvent += OnESC;
    }
    private void OnDisable()
    {
        toggleESCEvent.toggleCanvasEvent -= OnESC;

    }
    private void OnESC(bool state)
    {

        if (state)
        {
            TimeManager.instance.PauseGame();
            ESCGroup.alpha = 1;
            ESCGroup.interactable = true;
            ESCGroup.blocksRaycasts = true;
        }
        else
        {
            TimeManager.instance.ResumeGame();
            ESCGroup.alpha = 0;
            ESCGroup.interactable = false;
            ESCGroup.blocksRaycasts = false;
        }

        UIManager.instance.ReportCanvasState(MyEnums.CanvasToToggle.ESC, state);
    }

    /// <summary>Resume 按钮的回调：请求 UIManager 关闭 ESC 面板</summary>
    public void OnResumeButton()
    {
        if (UIManager.instance != null)
            UIManager.instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
    }

    /// <summary>Menu 按钮的回调：返回主菜单（通过 SceneChanger 触发）</summary>
    public void OnReturnToMenuButton()
    {
        if (UIManager.instance != null)
            UIManager.instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
        // 触发场景切换到 StartingMenu（通过 SceneLoadEventSO）
        var sc = SceneChanger.instance;
        if (sc != null && sc.loadEventSO != null && sc.initScene != null && sc.initScene.Count > 0)
            sc.loadEventSO.RaiseLoadRequestEvent(sc.initScene, Vector3.zero, true);
    }

    /// <summary>Retry 按钮的回调：关闭 ESC 面板并触发当前场景重载（对齐 GameOver 面板的 Retry）</summary>
    public void OnRetryButton()
    {
        if (UIManager.instance != null)
            UIManager.instance.RequestCanvasClose(MyEnums.CanvasToToggle.ESC);
        // 对齐 ButtonSceneToggler 的 retry 分支：强制清零 pauseCount，避免残留暂停
        if (TimeManager.instance != null)
            TimeManager.instance.ForceResumeGame();
        // 由每场景的 RetryManager 接管：ResetHealth + 重载当前场景
        if (retryEventSO != null)
            retryEventSO.OnEventRaised();
    }
}

