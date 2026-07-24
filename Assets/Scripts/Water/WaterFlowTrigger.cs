using UnityEngine;

/// <summary>
/// 水流启动触发器：玩家进入区域时调用引用挂载的水源点 StartFlowing()。
/// 参照 RewindGuideTrigger 模式：RequireComponent Collider2D + OnTriggerEnter2D + CompareTag("Player") + Gizmos 可视化。
/// 把对应 WaterSource 实例拖入 Inspector 的 target 字段即可。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaterFlowTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("被触发的水源点。玩家进入此 trigger 区域时调用其 StartFlowing()。")]
    [SerializeField] private WaterSource target;

    [Tooltip("是否只触发一次（默认 true）。设为 false 则每次进入都会重新尝试启动。")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;
    private Collider2D triggerArea;

    private void Awake()
    {
        triggerArea = GetComponent<Collider2D>();
        triggerArea.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;
        if (target == null)
        {
            Debug.LogWarning("[WaterFlowTrigger] target 未配置: " + name, this);
            return;
        }
        hasTriggered = true;
        target.StartFlowing();
    }

    /// <summary>外部调用入口（压力板链路等可调此方法替代玩家碰撞触发）。</summary>
    public void Trigger()
    {
        if (triggerOnce && hasTriggered) return;
        if (target == null) return;
        hasTriggered = true;
        target.StartFlowing();
    }

    // 编辑器可视化触发区域（蓝色框，方便摆放）
    private void OnDrawGizmos()
    {
        // 脚本被禁用时不画 Gizmos，Scene 视图里只显示当前生效的 trigger 类型
        if (!enabled) return;
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        var box = col as BoxCollider2D;
        if (box != null)
        {
            Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
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
