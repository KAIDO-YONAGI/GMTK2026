using UnityEngine;

/// <summary>
/// 回溯导引触发器：玩家进入区域显示导引 Canvas，离开区域隐藏。
/// 需要把两个 VoidEventSO 拖入 Inspector。挂在带 Collider2D(isTrigger=true) 的 GameObject 上。
/// 参照项目 Elevation_Entry 的 OnTriggerEnter2D + tag=="Player" 模式。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RewindGuideTrigger : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("玩家进入区域时触发（显示导引）。")]
    [SerializeField] private VoidEventSO showEvent;

    [Tooltip("玩家离开区域时触发（隐藏导引）。")]
    [SerializeField] private VoidEventSO exitEvent;

    private Collider2D triggerArea;

    private void Awake()
    {
        // 确保 Collider2D 是 trigger 模式
        triggerArea = GetComponent<Collider2D>();
        triggerArea.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (showEvent != null) showEvent.OnEventRaised();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (exitEvent != null) exitEvent.OnEventRaised();
        }
    }

    // 编辑器可视化触发区域（进入/退出区用不同色，方便摆放）
    private void OnDrawGizmos()
    {
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
            return;
        }
    }
}
