using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 粒子中央管理器。
/// 全部动态粒子由本组件统一池化生成与回收，实例常驻本对象层级下，不再散落到场景根。
/// 调用方只通过 SpawnParticleEventSO（位置 + 粒子类型）请求生成，不直接持有粒子 prefab。
/// </summary>
public class ParticlesManager : YSingleton<ParticlesManager>
{
    /// <summary>一种粒子类型的配置与其对象池。</summary>
    [System.Serializable]
    public class ParticleEntry
    {
        [Tooltip("该条目服务的粒子类型。")] public MyEnums.ParticleType type;
        [Tooltip("该类型对象池使用的粒子预制件。")] public ParticleSystem prefab;
        [Tooltip("初始化对象池时预创建的实例数量。")] public int prewarmCount = 3;
        [Tooltip("勾选后，游戏暂停时粒子仍继续播放。")] public bool useUnscaledTime;
    }

    /// <summary>类型到“配置 + 池”的运行时索引。</summary>
    private sealed class ParticlePool
    {
        public ParticleEntry entry;
        public ObjectPool<ParticleSystem> pool;
    }

    [Header("事件")]
    [Tooltip("生成请求通道：世界坐标 + 粒子类型。")]
    [SerializeField] private SpawnParticleEventSO spawnParticleEvent;

    [Tooltip("旧场景卸载前触发；在此回收全部活跃粒子。")]
    [SerializeField] private SceneLoadEventSO loadEventSO;

    [Header("粒子条目")]
    [SerializeField] private ParticleEntry[] entries;

    private readonly Dictionary<MyEnums.ParticleType, ParticlePool> pools =
        new Dictionary<MyEnums.ParticleType, ParticlePool>();

    // 已借出未归还的粒子；切场/重试时统一回收
    private readonly List<ParticlePool> activePools = new List<ParticlePool>();
    private readonly List<ParticleSystem> activeParticles = new List<ParticleSystem>();

    // 未配置类型的警告只报一次，避免高频请求刷屏
    private readonly HashSet<MyEnums.ParticleType> warnedUnconfigured =
        new HashSet<MyEnums.ParticleType>();

    protected override void OnSingletonInitialized()
    {
        SetupPools();
    }

    private void OnEnable()
    {
        if (spawnParticleEvent != null)
            spawnParticleEvent.SpawnParticleEvent += OnSpawnParticleRequested;
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent += OnSceneLoadRequested;
    }

    private void OnDisable()
    {
        if (spawnParticleEvent != null)
            spawnParticleEvent.SpawnParticleEvent -= OnSpawnParticleRequested;
        if (loadEventSO != null)
            loadEventSO.LoadRequestEvent -= OnSceneLoadRequested;
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<MyEnums.ParticleType, ParticlePool> pair in pools)
            pair.Value.pool?.Clear();
        pools.Clear();
    }

    private void SetupPools()
    {
        pools.Clear();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            ParticleEntry entry = entries[i];
            if (entry == null || entry.prefab == null || pools.ContainsKey(entry.type))
                continue;

            // 池实例统一挂在本管理器层级下，父级是常驻场景对象，切关不会被连带卸载
            pools.Add(entry.type, new ParticlePool
            {
                entry = entry,
                pool = new ObjectPool<ParticleSystem>(
                    entry.prefab,
                    Mathf.Max(0, entry.prewarmCount),
                    transform)
            });
        }
    }

    /// <summary>事件入口：从对应池借出实例，摆到请求位置并播放。</summary>
    private void OnSpawnParticleRequested(Vector3 position, MyEnums.ParticleType type)
    {
        if (!pools.TryGetValue(type, out ParticlePool particlePool))
        {
            if (warnedUnconfigured.Add(type))
                Debug.LogWarning("[ParticlesManager] No entry configured for particle type: " + type);
            return;
        }

        ParticleSystem particle = particlePool.pool.Get();
        if (particle == null)
            return; // 池达上限：丢弃本次请求（与 AudioManager 超限行为一致）

        // useUnscaledTime 写在实例上，不脏改 prefab 资产
        particle.transform.position = position;
        particle.transform.rotation = particlePool.entry.prefab.transform.rotation;
        ParticleSystem.MainModule main = particle.main;
        main.useUnscaledTime = particlePool.entry.useUnscaledTime;
        particle.Play(true);

        activePools.Add(particlePool);
        activeParticles.Add(particle);
        StartCoroutine(ReturnWhenDone(particle, particlePool.pool));
    }

    private IEnumerator ReturnWhenDone(ParticleSystem particle, ObjectPool<ParticleSystem> pool)
    {
        // 用 IsAlive 而不是 isPlaying 判定结束：burst 粒子的 duration 可能远长于实际可见时间
        // （如 HurtParticle duration=5s、粒子 0.8s 就消亡），IsAlive 在最后一个粒子消亡后立即转 false。
        while (particle != null && particle.IsAlive(true))
            yield return null;

        if (particle == null)
            yield break;

        pool.Return(particle);
        int index = activeParticles.IndexOf(particle);
        if (index >= 0)
        {
            activeParticles.RemoveAt(index);
            activePools.RemoveAt(index);
        }
    }

    /// <summary>切场请求（同步、早于旧场景卸载，与 UIManager 复位同一时序）：回收全部活跃粒子。</summary>
    private void OnSceneLoadRequested(List<GameSceneSO> scenes, Vector3 position, bool isToFade)
    {
        StopAllCoroutines();

        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            ParticleSystem particle = activeParticles[i];
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            activePools[i].pool.Return(particle);
        }

        activeParticles.Clear();
        activePools.Clear();
    }
}
