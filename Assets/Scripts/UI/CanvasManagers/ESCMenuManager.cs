using UnityEngine;

public class ESCMenuManager : MonoBehaviour
{
    public CanvasGroup ESCGroup;
    public ToggleCanvasEventSO toggleESCEvent;
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
}

