using UnityEngine;

/// <summary>
/// 挂在 PersistentScene 的根 GameObject 上，
/// Awake 时调用 DontDestroyOnLoad，让整个 PersistentScene 常驻。
/// 这样里面挂的 UIManager/TimeManager/SceneChanger 等单例跨场景不丢失。
/// </summary>
public class PersistentRoot : MonoBehaviour
{
    private static PersistentRoot instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
