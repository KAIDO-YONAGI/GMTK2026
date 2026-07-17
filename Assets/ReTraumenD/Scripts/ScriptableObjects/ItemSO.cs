using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "New Item",menuName = "GameLootSO/ItemSO")]//添加后使得SO在项目文件夹中可被创建
public class ItemSO : ScriptableObject
{
    public string itemName;
    [TextArea] public string itemDescription;//textarea增大文本框
    public Sprite icon;

    public bool isGold;
    public bool isEXP;

    public int stackableSize;

    [Header("Stats")]
    public int currentHealth;
    public int maxHealth;
    public int speed;
    public int damage;
    [Header("Temporary Items")]
    public float duration; 
}
