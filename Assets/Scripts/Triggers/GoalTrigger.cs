using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 通关触发器 + 通关总线（合并自原 GoalManager）。
///
/// 两种使用方式，同一个脚本：
/// 1. 全局单例：挂到 PersistentScene 的一个 GameObject 上，配置字段，
///    作为 TriggerEnding() 的执行入口（不挂 Collider2D）。
/// 2. 关卡触发器：挂到每个关卡的终点 GameObject 上，带 Collider2D（isTrigger），
///    玩家碰到后调用 GoalTrigger.Instance.TriggerEnding()。
///
/// 触发流程：TriggerEnding() → 写通关标记 + 加载 StartingMenu（isToFade=true）
/// → 场景加载完成触发 sceneLoadedEvent → 本脚本 OnSceneLoaded 发 endingToggleEventSO
/// → EndingManager（此时已在 StartingMenu 里订阅好）收到事件显示 Ending 面板。
/// </summary>
public class GoalTrigger : MonoBehaviour
{
    public static GoalTrigger Instance { get; private set; }

    /// <summary>通关标记的 PlayerPrefs 键名；StartingMenuController 读它决定是否显示 "Again and Again..."。</summary>
    public const string CompletedPrefKey = "GameCompleted";

    /// <summary>本次运行期间是否已通关（点击 The End 后置 true）。
    /// StartingMenuController 用它决定是否在开始界面显示 "Again and Again..."，整个运行时保留。</summary>
    public static bool CompletedThisSession { get; set; }

    [Header("Scene Load")]
    [SerializeField] private List<GameSceneSO> menuScene = new List<GameSceneSO>();
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Header("Ending Event")]
    [Tooltip("EndingManager 订阅的开关事件，通关时发出（EndingManager 会和 loaded 事件双判后才显示面板）。")]
    [SerializeField] private ToggleCanvasEventSO endingToggleEventSO;

    [SerializeField] private bool isToFade;

    public void TriggerEnding()
    {

        PlayerPrefs.SetInt(CompletedPrefKey, 1);
        PlayerPrefs.Save();

        // 发 ending 事件（EndingManager 会和 loaded 事件双判后才显示面板）
        if (endingToggleEventSO != null)
            endingToggleEventSO.RaiseToggleCanvasEvent(true);

        // 加载 StartingMenu；SceneChanger 会处理 PauseGame/ForceResumeGame/Fade
        if (loadEventSO != null)
            loadEventSO.RaiseLoadRequestEvent(menuScene, Vector3.zero, isToFade);
    }
    // ===== 关卡触发器入口（带 Collider2D 的关卡实例用）=====

    private void OnTriggerEnter2D(Collider2D collision)
    {
            TriggerEnding(); // 兜底：若未配置全局单例，关卡实例自己触发
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return; // 全局单例实例没有 Collider2D，不画 Gizmos

        // 绿色 = 通关，与 DeathTrigger 的红色区分
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.3f);
        var box = col as BoxCollider2D;
        if (box != null)
        {
            Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
            return;
        }
        var circle = col as CircleCollider2D;
        if (circle != null)
        {
            Vector3 center = transform.position + (Vector3)circle.offset;
            Gizmos.DrawSphere(center, circle.radius);
        }
    }
}
