using UnityEngine;

/// <summary>
/// StartingMenu 控制器：根据本次运行是否已通关（GoalTrigger.CompletedThisSession）
/// 决定是否显示 "Again and Again..." 文本。整个运行时保留。
/// 挂到 MenuCanvas 上。
/// </summary>
public class StartingMenuController : MonoBehaviour
{
    [SerializeField] private GameObject againText;

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
