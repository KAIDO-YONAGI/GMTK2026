using UnityEngine;

/// <summary>
/// 重试按钮：隐藏自身画布、强制解除暂停并发出 retryEventSO。
/// 由 PersistentScene 上的 RetryManager 统一监听并重载当前场景组
/// （对齐 ARPG：retry 机制从通用场景切换按钮中解离，不再依赖场景内匹配）。
/// </summary>
public class RetryButton : MonoBehaviour
{
    [SerializeField] private CanvasGroup ButtonCanvas;
    [SerializeField] private VoidEventSO retryEventSO;

    public void HandleRetry()//editor内由button组件绑定
    {
        ButtonCanvas.alpha = 0;
        ButtonCanvas.interactable = false;
        ButtonCanvas.blocksRaycasts = false;

        TimeManager.Instance.ForceResumeGame();
        retryEventSO.OnEventRaised();
    }
}
