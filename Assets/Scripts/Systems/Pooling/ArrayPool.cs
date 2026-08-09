using System.Collections.Generic;

/// <summary>
/// 值类型数组的通用对象池。
/// 参照项目已有的 ObjectPool&lt;T&gt;（Component 池）风格，但池化的是 T[] 数组而非 GameObject，
/// 用于复用回溯倒放时由快照队列转出的数组，消除每次 StartRewind 的 T[] 分配。
/// T 约束为 struct：回溯系统的快照（PlayerSnapshot/SpikeSnapshot 等）都是值类型。
/// </summary>
public class ArrayPool<T> where T : struct
{
    // 池中每个槽位记录一个可复用数组及其当前容量
    private struct PooledArray
    {
        public T[] array;
    }

    private readonly Stack<T[]> _pool;
    private readonly int _maxSize;

    /// <summary>当前池中可用数组数量</summary>
    public int Count => _pool.Count;

    /// <summary>累计创建的数组总数（含已归还的）</summary>
    public int TotalCreated { get; private set; }

    public ArrayPool(int prewarmCount = 0, int prewarmCapacity = 0, int maxSize = 16)
    {
        _maxSize = maxSize > 0 ? maxSize : 16;
        _pool = new Stack<T[]>(prewarmCount);
        for (int i = 0; i < prewarmCount; i++)
        {
            _pool.Push(new T[prewarmCapacity > 0 ? prewarmCapacity : 0]);
            TotalCreated++;
        }
    }

    /// <summary>
    /// 借出一个容量不小于 minCapacity 的数组。
    /// 池中有数组则复用（容量不足时扩容）；池空且未达上限则新建；达上限时强制新建（不阻塞调用方）。
    /// 注意：返回数组实际 Length 可能大于 minCapacity，调用方必须用显式 count 而非 Length。
    /// </summary>
    public T[] Get(int minCapacity)
    {
        T[] arr;
        if (_pool.Count > 0)
        {
            arr = _pool.Pop();
            if (arr == null || arr.Length < minCapacity)
            {
                arr = new T[minCapacity];
            }
        }
        else
        {
            arr = new T[minCapacity];
            TotalCreated++;
        }
        return arr;
    }

    /// <summary>归还一个数组供下次复用。null 静默丢弃。池满则丢弃（交给 GC）。</summary>
    public void Return(T[] arr)
    {
        if (arr == null) return;
        if (_pool.Count < _maxSize)
        {
            _pool.Push(arr);
        }
    }

    /// <summary>清空池中所有数组（归还给 GC）</summary>
    public void Clear()
    {
        _pool.Clear();
        TotalCreated = 0;
    }
}
