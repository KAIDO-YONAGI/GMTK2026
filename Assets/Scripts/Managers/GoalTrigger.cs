using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 通关触发器和通关事件总线（合并自原 GoalManager）。
///
/// 两种使用方式，同一个脚本：
/// 1. 全局单例：挂到 PersistentScene 的一个 GameObject 上并配置字段，
///    作为 TriggerEnding() 的执行入口（不挂 Collider2D）。
/// 2. 关卡触发器：挂到每个关卡的终点 GameObject 上，带 Collider2D（触发器模式），
///    玩家碰到后调用 GoalTrigger.Instance.TriggerEnding()。
///
/// 触发流程：TriggerEnding() → 写通关标记并加载 StartingMenu（isToFade=true）
/// → 场景加载完成后触发 sceneLoadedEvent → 本脚本 OnSceneLoaded 发出 endingToggleEventSO
/// → EndingManager（此时已在 StartingMenu 里订阅好）收到事件显示 Ending 面板。
/// </summary>
public class GoalTrigger : YSingleton<GoalTrigger>
{

    /// <summary>通关标记的 PlayerPrefs 键名；StartingMenuController 读取它决定是否显示 “Again and Again...”。</summary>
    public const string CompletedPrefKey = "GameCompleted";

    /// <summary>本次运行期间是否已通关（点击结局确认按钮后设为 true）。
    /// StartingMenuController 用它决定是否在开始界面显示 “Again and Again...”，整个运行时保留。</summary>
    public static bool CompletedThisSession { get; set; }

    [Header("场景加载")]
    [SerializeField] private List<GameSceneSO> menuScene = new List<GameSceneSO>();
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Header("结局事件")]
    [Tooltip("结局面板订阅的开关事件。通关时发出；结局面板还会等待场景加载完成事件后才显示。")]
    [SerializeField] private ToggleCanvasEventSO endingToggleEventSO;

    [SerializeField] private bool isToFade;

    public void TriggerEnding()
    {

        PlayerPrefs.SetInt(CompletedPrefKey, 1);
        PlayerPrefs.Save();

        // 发出结局事件；EndingManager 会与场景加载完成事件双重判断后才显示面板。
        if (endingToggleEventSO != null)
            endingToggleEventSO.RaiseToggleCanvasEvent(true);

        // 加载 StartingMenu；SceneChanger 会处理暂停、强制恢复和淡入淡出。
        if (loadEventSO != null)
            loadEventSO.RaiseLoadRequestEvent(menuScene, Vector3.zero, isToFade);
    }
    // ===== 关卡触发器入口（供带 Collider2D 的关卡实例使用）=====

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        TriggerEnding(); // 兜底：若未配置全局单例，由关卡实例自行触发。
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return; // 全局单例实例没有 Collider2D，不绘制 Gizmos。

        // 绿色表示通关，与 DeathTrigger 的红色区分。
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
