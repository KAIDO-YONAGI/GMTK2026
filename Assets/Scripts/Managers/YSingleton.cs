using UnityEngine;

/// <summary>
/// 抽象单例基类：提供只读 Instance 访问、重复实例销毁、OnDestroy 复位。
/// 用法：class Foo : YSingleton&lt;Foo&gt; { }
/// 需要在单例注册后执行额外初始化时，重写 OnSingletonInitialized()。
/// 各泛型闭包（YSingleton&lt;Foo&gt; / YSingleton&lt;Bar&gt;）的静态 _instance 相互独立。
/// </summary>
public abstract class YSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;

    public static T Instance => _instance;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
        OnSingletonInitialized();
    }

    /// <summary>子类初始化钩子：仅在合法实例的 Awake 中调用一次</summary>
    protected virtual void OnSingletonInitialized() { }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
