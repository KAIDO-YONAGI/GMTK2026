using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyEnums;
public class ButtonSceneToggler : MonoBehaviour
{
    public SceneLoadEventSO loadEventSO;
    public List<GameSceneSO> sceneToLoad = new List<GameSceneSO>();
    public CanvasGroup ButtonCanvas;
    public Vector3 newPosition;
    public bool isToFade = true;
    [Header("Retry Event")]
    public VoidEventSO retryEventSO;//需要在每个场景内配置retrymanager配合使用
    //不挂就是
    public void HandleSceneToggle()//editor内由button组件绑定
    {
        ButtonCanvas.alpha = 0;
        ButtonCanvas.interactable = false;
        ButtonCanvas.blocksRaycasts = false;

        if (sceneToLoad != null && sceneToLoad.Count > 0 &&
            sceneToLoad[0] != null && sceneToLoad[0].sceneType != MyEnums.SceneType.Retry)
        {
            loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
        }
        else
        {
            TimeManager.instance.ForceResumeGame();
            retryEventSO.OnEventRaised();//事件引用位于每个场景内RetryManager
        }
    }

}
