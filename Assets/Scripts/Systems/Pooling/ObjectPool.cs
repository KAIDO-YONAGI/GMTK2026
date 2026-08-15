using System.Collections.Generic;
using UnityEngine;

/// <summary>对象池化接口：池对象实现此接口可接收 Get/Return 回调</summary>
public interface IPoolable
{
    void OnPoolGet();
    void OnPoolReturn();
}

/// <summary>通用泛型对象池，支持预热预制体</summary>
public class ObjectPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Queue<T> _pool = new();
    private readonly int _maxSize;

    /// <summary>当前池中可用对象数量</summary>
    public int Count => _pool.Count;

    /// <summary>累计创建的对象总数（含已销毁）</summary>
    public int TotalCreated { get; private set; }

    public ObjectPool(T prefab, int prewarmCount, Transform parent = null, int maxSize = 100)
    {
        _prefab = prefab;
        _parent = parent;
        _maxSize = maxSize;
        Prewarm(prewarmCount);
    }

    /// <summary>预热：预先创建指定数量对象并存入池中</summary>
    private void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var obj = CreateNew();
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    /// <summary>从池中借出一个对象，池空且未达上限时自动创建</summary>
    public T Get()
    {
        T obj;
        if (_pool.Count > 0)
        {
            obj = _pool.Dequeue();
            obj.gameObject.SetActive(true);
        }
        else if (TotalCreated < _maxSize)
        {
            obj = CreateNew();
            obj.gameObject.SetActive(true);
        }
        else
        {
            return null;
        }

        (obj as IPoolable)?.OnPoolGet();
        return obj;
    }

    /// <summary>将对象归还池中</summary>
    public void Return(T obj)
    {
        if (obj == null) return;
        (obj as IPoolable)?.OnPoolReturn();
        obj.gameObject.SetActive(false);
        // 父级正在反激活时跳过 SetParent，避免 "Cannot set parent while activating/deactivating"
        if (_parent != null && _parent.gameObject.activeInHierarchy)
            obj.transform.SetParent(_parent);
        _pool.Enqueue(obj);
    }

    private T CreateNew()
    {
        var obj = Object.Instantiate(_prefab, _parent);
        obj.name = $"{_prefab.name}_{TotalCreated}";
        TotalCreated++;
        return obj;
    }

    /// <summary>清空池中所有对象</summary>
    public void Clear()
    {
        while (_pool.Count > 0)
        {
            var obj = _pool.Dequeue();
            //场景卸载可能已把池内对象销毁，跳过已销毁的避免 MissingReferenceException
            if (obj != null)
                Object.Destroy(obj.gameObject);
        }
        TotalCreated = 0;
    }
}
