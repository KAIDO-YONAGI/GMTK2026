using UnityEngine;

public class HealthManager : MonoBehaviour
{
    // 单例：玩家生命值全局唯一，方便外部访问
    public static HealthManager Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [SerializeField] private int currentHealth;

    [Header("Death Event")]
    [SerializeField] private DeathEventSO deathEvent;

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
    /// 修改生命值。正数治疗，负数伤害。
    /// 自动做上下界保护：不会超过 maxHealth，不会低于 0。
    /// 降到 0 时触发死亡事件（UnnormalDead），仅触发一次。
    /// </summary>
    public void ChangeHealth(int amount)
    {
        if (isDead) return; // 已死亡则不再扣血/治疗

        currentHealth += amount;

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
    }

    private bool isDead = false;

    private void OnDie()
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
    }
}
