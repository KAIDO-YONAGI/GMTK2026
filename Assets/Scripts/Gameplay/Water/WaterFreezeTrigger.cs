using System.Collections;
using UnityEngine;

/// <summary>
/// 水流结冰触发器：玩家进入区域时调用引用挂载的水源点 StartFreezing()。
/// 与 WaterFlowTrigger 结构一致，仅触发的方法不同（结冰倒计时）。
/// 结冰由水源点统一管理：到点把所有 Stage2 单元切到 Frozen。
/// 触发器支持重复触发，是否真正结冰由 WaterSource 决定（已结冰则忽略，回溯后才解锁）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WaterFreezeTrigger : MonoBehaviour
{
    [Header("目标水源")]
    [Tooltip("被触发的水源点。玩家进入此触发区域时启动冻结。")]
    [SerializeField] private WaterSource target;

    [Header("延迟")]
    [Tooltip("玩家进入区域后，等待多少秒才真正触发。0 = 立即触发。")]
    [SerializeField] private float delaySeconds = 0f;

    private Collider2D triggerArea;
    private bool isDelaying; // 防止玩家反复进出时重复启动延时协程

    private void Awake()
    {
        triggerArea = GetComponent<Collider2D>();
        triggerArea.isTrigger = true;
    }
    void OnEnable()
    {

    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!enabled) return; // 脚本被禁用时不响应（Unity 默认不拦截 OnTrigger，需手动检查）
        if (!collision.gameObject.CompareTag("Player")) return;
        if (target == null)
        {
            Debug.LogWarning("[WaterFreezeTrigger] target 未配置: " + name, this);
            return;
        }
        // 进入即计时，到点必触发（不管玩家是否还在区域）。延时中则不重复启动。
        if (isDelaying) return;
        if (delaySeconds <= 0f) { target.StartFreezing(); return; }
        StartCoroutine(DelayedFreeze());
    }

    private IEnumerator DelayedFreeze()
    {
        isDelaying = true;
        yield return new WaitForSeconds(delaySeconds);
        isDelaying = false;
        if (target != null) target.StartFreezing();
    }

    /// <summary>外部调用入口（忽略延时）。</summary>
    public void Trigger()
    {
        if (!enabled) return;
        if (target == null) return;
        target.StartFreezing();
    }

    // 编辑器可视化触发区域（青色框，与 WaterFlowTrigger 区分）
    private void OnDrawGizmos()
    {
        // 脚本被禁用时不绘制 Gizmos，场景视图中只显示当前生效的触发器类型。
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
