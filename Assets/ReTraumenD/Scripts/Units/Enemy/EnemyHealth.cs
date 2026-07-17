using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int currentHealth;
    public int maxHealth;
    public int expReward=2;

    public delegate void MonsterDefeated(int exp);//观察者模式
    public static event MonsterDefeated OnDefeated;

    private void Start()
    {
        currentHealth = maxHealth;
    }
    public void ChangeHealth(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        else if (currentHealth <= 0)
        {
            OnDefeated(expReward);//事件被触发
            Destroy(gameObject);
        }
    }
}