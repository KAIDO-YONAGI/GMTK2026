using UnityEngine;

/// <summary>
/// 平台预制件的可配置脚本。
/// 在检查器中拖入精灵图时同步到 SpriteRenderer；切换触发器时同步到 Collider2D。
/// 仅用于预制件实例化后的快速配置，避免每次都要 GetComponents。
/// </summary>
[ExecuteAlways]
public class PlatformConfigurator : MonoBehaviour
{
    [Header("精灵图")]
    public Sprite sprite;

    [Header("碰撞体")]
    public bool isTrigger = false;

    SpriteRenderer spriteRenderer;
    Collider2D col;

    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (col == null) col = GetComponent<Collider2D>();

        // 拖入 Sprite 时同步到 SpriteRenderer
        if (sprite != null && spriteRenderer != null && spriteRenderer.sprite != sprite)
            spriteRenderer.sprite = sprite;

        // 切换 isTrigger
        if (col != null && col.isTrigger != isTrigger)
            col.isTrigger = isTrigger;
    }
}
