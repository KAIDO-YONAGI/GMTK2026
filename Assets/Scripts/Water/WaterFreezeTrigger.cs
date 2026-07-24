using UnityEngine;

/// <summary>
/// 水流结冰触发器：玩家进入区域时调用引用挂载的水源点 StartFreezing()。
/// 与 WaterFlowTrigger 结构一致，仅触发的方法不同（结冰倒计时）。
/// 结冰由水源点统一管理：到点把所有 Stage2 单元切到 Frozen。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaterFreezeTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("被触发的水源点。玩家进入此 trigger 区域时调用其 StartFreezing()。")]
    [SerializeField] private WaterSource target;

    [Tooltip("是否只触发一次（默认 true）。")]
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
            Debug.LogWarning("[WaterFreezeTrigger] target 未配置: " + name, this);
            return;
        }
        hasTriggered = true;
        target.StartFreezing();
    }

    /// <summary>外部调用入口。</summary>
    public void Trigger()
    {
        if (triggerOnce && hasTriggered) return;
        if (target == null) return;
        hasTriggered = true;
        target.StartFreezing();
    }

    // 编辑器可视化触发区域（青色框，与 WaterFlowTrigger 区分）
    private void OnDrawGizmos()
    {
        // 脚本被禁用时不画 Gizmos，Scene 视图里只显示当前生效的 trigger 类型
        if (!enabled) return;
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.3f);
        var box = col as BoxCollider2D;
        if (box != null)
        {
            Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
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
