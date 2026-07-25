using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax Settings")]
    [Tooltip("(0,0)=static far bg, (0.5,0.5)=midground half speed, (1,1)=normal scene, (2,1)=fast foreground")]
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
