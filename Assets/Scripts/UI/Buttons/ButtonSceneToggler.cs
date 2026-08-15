using System.Collections.Generic;
using UnityEngine;
using MyEnums;

/// <summary>
/// 通用场景切换按钮：隐藏自身画布并请求加载目标场景组。
/// 重试语义已解离到 RetryButton（由 PersistentScene 上的 RetryManager 统一监听），
/// 本按钮不再承载重试分支与场景内匹配逻辑（对齐 ARPG 提交 491c88e）。
/// </summary>
public class ButtonSceneToggler : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public List<GameSceneSO> sceneToLoad = new List<GameSceneSO>();
    public CanvasGroup ButtonCanvas;
    public Vector3 newPosition;
    public bool isToFade = true;

    public void HandleSceneToggle()// 由编辑器内的 Button 组件绑定
    {
        ButtonCanvas.alpha = 0;
        ButtonCanvas.interactable = false;
        ButtonCanvas.blocksRaycasts = false;

        if (sceneToLoad != null && sceneToLoad.Count > 0 && sceneToLoad[0] != null)
        {
            loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
        }
        else
        {
            Debug.LogWarning("[ButtonSceneToggler] sceneToLoad 未配置。重试按钮请改用 RetryButton 组件。", this);
        }
    }
}
