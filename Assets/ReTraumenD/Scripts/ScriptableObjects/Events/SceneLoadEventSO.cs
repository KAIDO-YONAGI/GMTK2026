using UnityEngine;
using UnityEngine.Events;


[CreateAssetMenu(fileName = "SceneLoadEventSO", menuName = "Events/SceneLoadEventSO", order = 0)]
public class SceneLoadEventSO : ScriptableObject
{
    public UnityAction<GameSceneSO, Vector3, bool> LoadRequestEvent;
    /// <summary>
    /// 场景加载
    /// </summary>
    /// <param name="Location">去哪个场景</param>
    /// <param name="position">传送到新场景的位置</param>
    /// <param name="isToFade">要不要动画过渡</param>
    public void RaiseLoadRequestEvent(GameSceneSO scene, Vector3 position, bool isToFade)
    {
        LoadRequestEvent?.Invoke(scene, position, isToFade);
    }
}
