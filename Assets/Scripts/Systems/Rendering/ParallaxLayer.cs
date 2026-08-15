using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Header("视差设置")]
    [Tooltip("(0,0) 为静态远景背景；(0.5,0.5) 为半速中景；(1,1) 为正常场景；(2,1) 为快速前景。")]
    [SerializeField] private Vector2 parallaxFactor = Vector2.one;

    private Vector3 originalPosition;

    void Awake()
    {
        originalPosition = transform.position;
    }

    void LateUpdate()
    {
        Transform cam = Camera.main?.transform;
        if (cam == null) return;

        if (!Application.isPlaying) return;

        Vector3 offset = new Vector3(
            cam.position.x * (1f - parallaxFactor.x),
            cam.position.y * (1f - parallaxFactor.y),
            0f
        );

        transform.position = originalPosition + offset;
    }
}
