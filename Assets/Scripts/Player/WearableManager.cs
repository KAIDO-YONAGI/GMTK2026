using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WearableManager : MonoBehaviour
{
    // 单例：装备管理器全局唯一，方便外部访问
    public static WearableManager Instance { get; private set; }

    // 暴露自身 transform 引用，方便外部读取装备挂点位置等
    public Transform SelfTransform { get; private set; }

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

    private void OnDestroy()
    {
        // 防止被销毁的单例指针悬挂
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
