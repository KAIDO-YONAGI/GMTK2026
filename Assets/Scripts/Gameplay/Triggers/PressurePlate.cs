using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class PressurePlate : MonoBehaviour
{
    [Header("动画")]
    [Tooltip("压力板动画控制器。可选；配置后会在激活或解除时播放对应动画。")]
    [SerializeField] private Animator animator;
    [Tooltip("激活时播放的动画片段名称。")]
    [SerializeField] private string activateClipName = "Activate";
    [Tooltip("解除时播放的动画片段名称。")]
    [SerializeField] private string deactivateClipName = "Deactivate";

    [Header("首次激活时销毁自身")]
    [Tooltip("勾选后，首次激活时销毁自身。")]
    [SerializeField] private bool destroyOnActivate;

    [Header("事件")]
    [Tooltip("压力板被激活时触发。水流末端命中它或玩家进入触发区域时调用。")]
    [SerializeField] private UnityEvent onActivate;

    [Tooltip("压力板解除激活时触发。玩家离开触发区域时调用。")]
    [SerializeField] private UnityEvent onDeactivate;

    private Collider2D triggerArea;
    private bool isActivated;

    private void Awake()
    {
        triggerArea = GetComponent<Collider2D>();
        triggerArea.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        Activate();
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        Deactivate();
    }

    /// <summary>激活压力板（水流命中时由 WaterSource 调用，或玩家进入触发器时自动调用）。</summary>
    public void Activate()
    {
        bool wasActivated = isActivated;
        isActivated = true;
        if (animator != null) animator.Play(activateClipName);
        onActivate?.Invoke();
        if (!wasActivated)
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.PressurePlate);
        if (destroyOnActivate) Destroy(gameObject);
    }

    /// <summary>解除激活（玩家离开触发器时自动调用，或由外部手动调用）。</summary>
    public void Deactivate()
    {
        isActivated = false;
        if (animator != null) animator.Play(deactivateClipName);
        onDeactivate?.Invoke();
    }

    // private void OnDrawGizmos()
    // {
    //     var col = GetComponent<Collider2D>();
    //     if (col == null) return;
    //
    //     Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
    //     var box = col as BoxCollider2D;
    //     if (box != null)
    //     {
    //         Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
    //         Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
    //         Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
    //         return;
    //     }
    //     var circle = col as CircleCollider2D;
    //     if (circle != null)
    //     {
    //         Vector3 center = transform.position + (Vector3)circle.offset;
    //         Gizmos.DrawSphere(center, circle.radius);
    //     }
    // }
}
