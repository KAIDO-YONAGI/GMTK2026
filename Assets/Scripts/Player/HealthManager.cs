using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthManager : YSingleton<HealthManager>
{
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

    // 濒死状态：机关扣血到 0 后进入。玩家以极低移速行动，可回溯回血自救。
    // 不同于 isDead（真正死亡），濒死时游戏继续运行、不弹 GameOver 面板。
    private bool isDying = false;
    public bool IsDying => isDying;
    public bool IsDead => isDead;

    protected override void OnSingletonInitialized()
    {
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
        hurtParticlePool?.Clear();
    }

    /// <summary>
    /// 修改生命值。正数治疗，负数伤害。
    /// 自动做上下界保护：不会超过 maxHealth，不会低于 0。
    /// 机关扣血到 0 时进入濒死状态（isDying），不直接死亡——玩家可回溯回血自救。
    /// </summary>
    public void ChangeHealth(int amount)
    {
        if (isDead) return; // 已死亡则不再扣血/治疗

        currentHealth += amount;
        
        if (amount < 0)
        {
            PlayHurtParticle();
            AudioManager.Instance?.PlaySFX(MyEnums.SFXType.Hurt);

            if (shakeEvent != null)
                shakeEvent.RaiseShakeEvent(hitShakeParams);
        }

        // 上界保护
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // 下界保护：机关扣血到 0 进入濒死（而非直接死亡）。
        // 玩家以极低移速行动，可回溯回血自救；放弃自救时由濒死提示的 Give Up 按钮触发 OnDie。
        // 幂等：濒死期间继续受伤不会重复触发。
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (!isDying) isDying = true;
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
    /// 重试关卡时恢复满血并清除死亡/濒死状态，不触发治疗或死亡事件。
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        isDying = false;
        RaiseHealthChanged(); // 重试回满血：广播更新 UI
    }

    /// <summary>
    /// 回溯/轮回专用：直接把血量设置到指定值，绕过死亡判定。
    /// 血量会钳制到 [0, maxHealth]。若目标血量 > 0，同时清除死亡和濒死状态（复活）。
    /// </summary>
    public void SetHealthForReincarnation(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
        if (currentHealth > 0)
        {
            isDead = false;  // 回到有血的状态：复活
            isDying = false; // 退出濒死
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
        //每次换场景回满血（订阅 sceneLoadedEvent；retry 重载场景时同样生效），并清除死亡/濒死状态
        ResetHealth();

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
