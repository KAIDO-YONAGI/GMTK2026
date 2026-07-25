using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WearableManager : MonoBehaviour
{
    // 单例：装备管理器全局唯一，方便外部访问
    public static WearableManager Instance { get; private set; }

    // 暴露自身 transform 引用，方便外部读取装备挂点位置等
    public Transform SelfTransform { get; private set; }

    [Header("Scene Load")]
    [Tooltip("场景加载完成事件。每次切场景后销毁玩家身上装备的可拾取尖刺，避免上一场景的装备带到新场景。")]
    [SerializeField] private VoidEventSO sceneLoadedEvent;

    private void Awake()
    {
        // 单例设置：重复实例时销毁自身，保证全局唯一
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SelfTransform = transform;
    }

    private void OnEnable()
    {
        // 监听场景加载完成事件：每次切场景清理玩家身上的可拾取尖刺。
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (sceneLoadedEvent != null)
            sceneLoadedEvent.VoidEvent -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // 防止被销毁的单例指针悬挂
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 场景加载完成回调。Player 跨场景不销毁，WearablePoint 及其子物体（装备的尖刺）
    /// 会随之保留到新场景，因此每次切场景后销毁所有装备状态的可拾取尖刺。
    /// </summary>
    private void OnSceneLoaded()
    {
        if (SelfTransform == null) return;

        // 倒序遍历子物体，避免销毁过程中索引错位。
        for (int i = SelfTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = SelfTransform.GetChild(i);
            if (child.TryGetComponent<PickupSpike>(out _))
                Destroy(child.gameObject);
        }
    }
}

