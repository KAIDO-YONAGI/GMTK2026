using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "SceneLoadEventSO", menuName = "Events/SceneLoadEventSO", order = 0)]
public class SceneLoadEventSO : ScriptableObject
{
    public UnityAction<List<GameSceneSO>, Vector3, bool> LoadRequestEvent;
    /// <summary>
    /// 场景加载
    /// </summary>
    /// <param name="scenes">要加载的场景列表</param>
    /// <param name="position">传送到新场景的位置</param>
    /// <param name="isToFade">要不要动画过渡</param>
    public void RaiseLoadRequestEvent(List<GameSceneSO> scenes, Vector3 position, bool isToFade)
    {
        LoadRequestEvent?.Invoke(scenes, position, isToFade);
    }
}
