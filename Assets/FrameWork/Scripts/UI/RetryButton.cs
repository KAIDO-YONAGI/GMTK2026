using UnityEngine;

/// <summary>
/// 重试按钮：隐藏自身画布、强制解除暂停并发出重试事件。
/// 由 PersistentScene 上的 RetryManager 统一监听并重载当前场景组
/// （对齐 ARPG：重试机制从通用场景切换按钮中解离，不再依赖场景内匹配）。
/// </summary>
public class RetryButton : MonoBehaviour
{
    [SerializeField] private CanvasGroup ButtonCanvas;
    [SerializeField] private VoidEventSO retryEventSO;

    public void HandleRetry()// 由编辑器内的 Button 组件绑定
    {
        if (ButtonCanvas != null)
        {
            ButtonCanvas.alpha = 0;
            ButtonCanvas.interactable = false;
            ButtonCanvas.blocksRaycasts = false;
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.ForceResumeGame();

        if (retryEventSO != null)
            retryEventSO.OnEventRaised();
        else
            Debug.LogError("[RetryButton] Retry Event SO is not assigned.", this);
    }
}
