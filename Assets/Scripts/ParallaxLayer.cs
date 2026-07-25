using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax Settings")]
    [Tooltip("(0,0)=static far bg, (0.5,0.5)=midground half speed, (1,1)=normal scene, (2,1)=fast foreground")]
    [SerializeField] private Vector2 parallaxFactor = Vector2.one;

    [Header("Auto Tile")]
    [Tooltip("Enable to wrap horizontally for seamless tiling")]
    [SerializeField] private bool autoTile;
    [Tooltip("Width of one tile segment")]
    [SerializeField] private float tileWidth = 5f;

    private Vector3 startPosition;
    private Vector3 cameraStartPosition;
    private Vector3? lastAppliedPosition;

    void LateUpdate()
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) return;

        // 检测到手改过位置 → 重新绑定基准
        if (!lastAppliedPosition.HasValue ||
            Vector3.Distance(transform.position, lastAppliedPosition.Value) > 0.001f)
        {
            cameraStartPosition = cam.position;
            startPosition = transform.position;
        }

        Vector3 cameraDelta = cam.position - cameraStartPosition;
        Vector3 offset = new Vector3(
            cameraDelta.x * (1f - parallaxFactor.x),
            cameraDelta.y * (1f - parallaxFactor.y),
            0f
        );

        Vector3 pos = startPosition + offset;

        if (autoTile && tileWidth > 0f)
        {
            float hw = tileWidth * 0.5f;
            float xOffset = pos.x - startPosition.x;
            xOffset = Mathf.Repeat(xOffset + hw, tileWidth) - hw;
            pos.x = startPosition.x + xOffset;
        }

        lastAppliedPosition = pos;
        transform.position = pos;
    }
}