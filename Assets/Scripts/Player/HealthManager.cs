using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthManager : MonoBehaviour
{
    // 单例：玩家生命值全局唯一，方便外部访问
    public static HealthManager Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [SerializeField] private int currentHealth;

    [Header("Death Event")]
    [SerializeField] private DeathEventSO_ deathEvent;

    [SerializeField] private VoidEventSO SceneLoadedEvent;

    [Header("Health Changed Event")]
    [Tooltip("血量变化时广播当前血量值（IntEventSO）。血量 HUD 订阅它来更新显示。")]
    [SerializeField] private IntEventSO healthChangedEvent;
    
    [Header("受伤粒子")]
    [SerializeField] private ParticleSystem hurtParticlePrefab;
    [SerializeField] private int hurtParticlePrewarmCount = 3;

    [Header("Shake Event")]
    [SerializeField] private ShakeEventSO shakeEvent;
    [SerializeField] private CameraShake.ShakeParameters hitShakeParams = new CameraShake.ShakeParameters
    {
        intensity = 0.5f,
        duration = 0.3f,
        frequency = 1f
    };
    
    

    private ObjectPool<ParticleSystem> hurtParticlePool;
    private readonly List<ParticleSystem> activeHurtParticles = new();

    // 当前生命值只读访问（外部查询用，修改请走 ChangeHealth）
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        // 单例设置：重复实例时销毁自身，保证全局唯一
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentHealth = maxHealth;
        SetupHurtParticlePool();
    }

    private void SetupHurtParticlePool()
    {
        if (hurtParticlePrefab == null) return;

        // 令预制体各实例使用 unscaled time，保证暂停时仍能正常播放
        var main = hurtParticlePrefab.main;
        main.useUnscaledTime = true;

        hurtParticlePool = new ObjectPool<ParticleSystem>(hurtParticlePrefab, hurtParticlePrewarmCount, transform);
    }

    private void PlayHurtParticle()
    {
        if (hurtParticlePool == null) return;
        ParticleSystem ps = hurtParticlePool.Get();
        if (ps == null) return;

        ps.transform.SetParent(null);
        ps.transform.position = transform.position;
        ps.Play();
        activeHurtParticles.Add(ps);
        StartCoroutine(ReturnParticleWhenDone(ps));
    }

    private IEnumerator ReturnParticleWhenDone(ParticleSystem ps)
    {
        // 等待粒子播放完成
        while (ps.isPlaying)
            yield return null;

        hurtParticlePool.Return(ps);
        activeHurtParticles.Remove(ps);
    }

    private void OnEnable()
    {
        if (SceneLoadedEvent != null)
        {
            SceneLoadedEvent.VoidEvent += OnSceneLoaded;
        }
    }
    
    private void OnDisable()
    {
        if (SceneLoadedEvent != null)
        {
            SceneLoadedEvent.VoidEvent -= OnSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        // 防止被销毁的单例指针悬挂
        if (Instance == this)
        {
            Instance = null;
        }

        hurtParticlePool?.Clear();
    }

    /// <summary>
    /// 修改生命值。正数治疗，负数伤害。
    /// 自动做上下界保护：不会超过 maxHealth，不会低于 0。
    /// 降到 0 时触发死亡事件（UnnormalDead），仅触发一次。
    /// </summary>
    public void ChangeHealth(int amount)
    {
        if (isDead) return; // 已死亡则不再扣血/治疗

        currentHealth += amount;
        
        if (amount < 0)
        {
            PlayHurtParticle();

            if (shakeEvent != null)
                shakeEvent.RaiseShakeEvent(hitShakeParams);
        }

        // 上界保护
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 下界保护：扣到 0 触发死亡
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDie();
        }

        // 广播最新血量供 UI 更新（治疗/扣血都触发）
        RaiseHealthChanged();
    }

    private bool isDead = false;

    public void OnDie()
    {
        if (isDead) return;
        isDead = true;

        // 发送死亡事件：非正常死亡（被怪物打死、坠落等）
        if (deathEvent != null)
        {
            deathEvent.RaiseDeathEvent(MyEnums.DeathType.UnnormalDead);
        }
        else
        {
            Debug.LogWarning("[HealthManager] deathEvent 未赋值，无法触发死亡事件。请在 Inspector 拖入 DeathEventSO。");
        }
    }

    /// <summary>
    /// 重试关卡时恢复满血并清除死亡状态，不触发治疗或死亡事件。
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        RaiseHealthChanged(); // 重试回满血：广播更新 UI
    }

    /// <summary>
    /// 回溯/轮回专用：直接把血量设置到指定值，绕过死亡判定。
    /// 血量会钳制到 [0, maxHealth]。若目标血量 > 0，同时清除死亡状态（复活）。
    /// </summary>
    public void SetHealthForReincarnation(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        if (currentHealth > 0)
        {
            isDead = false; // 回到有血的状态：复活
        }
        RaiseHealthChanged(); // 回溯还原血量：广播更新 UI
    }

    /// <summary>广播当前血量值给订阅者（血量 HUD 等）。事件未配置时安全跳过。</summary>
    private void RaiseHealthChanged()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.OnEventRaised(currentHealth);
    }


    private void OnSceneLoaded()
    {
        StopAllCoroutines();
        for (int i = activeHurtParticles.Count - 1; i >= 0; i--)
        {
            var ps = activeHurtParticles[i];
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                hurtParticlePool.Return(ps);
            }
        }
        activeHurtParticles.Clear();
    }
}
