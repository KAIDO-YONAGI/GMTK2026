using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡内传送触发器：玩家进入二维触发区域后，通过统一场景加载事件切换完整场景组。
/// </summary>
public class SceneToggler : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("场景切换")]
    [Tooltip("与 SceneChanger 使用同一个场景加载事件资产。")]
    public SceneLoadEventSO loadEventSO;

    [Tooltip("切换完成后的玩家位置。零向量表示使用目标列表首个场景 SO 的初始位置。")]
    public Vector3 newPosition;

    [Tooltip("按顺序叠加加载的完整目标场景组。不要把常驻场景放进此列表。")]
    public List<GameSceneSO> sceneToLoad = new List<GameSceneSO>();

    [Tooltip("是否播放 SceneChanger 配置的淡入淡出过渡。")]
    public bool isToFade = true;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag(PlayerTag))
            return;

        if (loadEventSO == null)
        {
            Debug.LogError("[SceneToggler] 未配置场景加载事件，无法执行传送。", this);
            return;
        }

        if (sceneToLoad == null || sceneToLoad.Count == 0)
        {
            Debug.LogError("[SceneToggler] 未配置目标场景组，无法执行传送。", this);
            return;
        }

        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
