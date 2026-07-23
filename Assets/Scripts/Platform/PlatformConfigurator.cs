using UnityEngine;

/// <summary>
/// 平台 Prefab 的可配置脚本。
/// 在 Inspector 拖入 Sprite 时同步到 SpriteRenderer；切换 Trigger 时同步到 Collider2D。
/// 仅用于预制件实例化后的快速配置，避免每次都要 GetComponents。
/// </summary>
[ExecuteAlways]
public class PlatformConfigurator : MonoBehaviour
{
    [Header("Sprite")]
    public Sprite sprite;

    [Header("Collider")]
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
