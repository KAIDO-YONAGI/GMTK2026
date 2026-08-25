using System;
using UnityEngine;

public class HealthManager : YSingleton<HealthManager>
{
    [Header("生命值")]
    [SerializeField] private int maxHealth = 3;

    [SerializeField] private int currentHealth;

    [Header("死亡事件")]
    [SerializeField] private DeathEventSO_ deathEvent;

    [SerializeField] private VoidEventSO SceneLoadedEvent;

    [Header("生命值变更事件")]
    [Tooltip("血量变化时广播当前血量值（IntEventSO）。血量 HUD 订阅它来更新显示。")]
    [SerializeField] private IntEventSO healthChangedEvent;
    
    [Header("受伤粒子")]
    [Tooltip("受击粒子的生成请求事件，由中央粒子管理器统一池化播放。")]
    [SerializeField] private SpawnParticleEventSO spawnParticleEvent;

    [Header("震动事件")]
    [SerializeField] private ShakeEventSO shakeEvent;
    [SerializeField] private CameraShake.ShakeParameters hitShakeParams = new CameraShake.ShakeParameters
    {
        intensity = 0.5f,
        duration = 0.3f,
        frequency = 1f
    };
    
    

    // 当前生命值只读访问（外部查询用，修改请走 ChangeHealth）
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // 濒死状态：机关扣血到 0 后进入。玩家以极低移速行动，可回溯回血自救。
    // 不同于 isDead（真正死亡），濒死时游戏继续运行，不弹出 GameOver 面板。
    private bool isDying = false;
    public bool IsDying => isDying;
    public bool IsDead => isDead;

    protected override void OnSingletonInitialized()
    {
        currentHealth = maxHealth;
    }

    /// <summary>受击时向中央粒子管理器请求在自身位置播放受击粒子。</summary>
    private void PlayHurtParticle()
    {
        spawnParticleEvent?.RaiseSpawnParticleEvent(transform.position, MyEnums.ParticleType.Hurt);
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
        // 玩家以极低移速行动，可回溯回血自救；放弃自救时由濒死提示的放弃按钮触发 OnDie。
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

    /// <summary>向订阅者广播当前血量值（如生命值界面）。事件未配置时安全跳过。</summary>
    private void RaiseHealthChanged()
    {
        if (healthChangedEvent != null)
            healthChangedEvent.OnEventRaised(currentHealth);
    }


    private void OnSceneLoaded()
    {
        // 每次换场景回满血（订阅 sceneLoadedEvent；重试重载场景时同样生效），并清除死亡和濒死状态。
        // 活跃粒子的回收由 ParticlesManager 订阅场景加载请求统一处理，这里不再管粒子。
        ResetHealth();
    }
}
