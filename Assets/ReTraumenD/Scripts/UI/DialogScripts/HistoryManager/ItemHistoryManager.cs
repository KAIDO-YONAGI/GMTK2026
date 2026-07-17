using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class ItemHistoryManager : MonoBehaviour
{
    public static ItemHistoryManager instance;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(gameObject);
    }

    private Dictionary<ItemSO, int> itemHasPicked = new();

    public void RecordItem(ItemSO item, int quantity)
    {
        if (itemHasPicked.ContainsKey(item))
        {
            itemHasPicked[item] += quantity;
        }
        else itemHasPicked.Add(item, quantity);
        // Debug.Log(item.itemName);
        // Debug.Log(itemHasPicked.Count);
        // Debug.Log(itemHasPicked[item]);

    }

    public bool HasPickedOverAmount(ItemSO item, int amount)//实际上会记录是否捡过和数量，当前数量归零的并不会删除
    {
        if (itemHasPicked.ContainsKey(item) && itemHasPicked[item] >= amount)
            return true;

        return false;
    }
    public int GetItemQuantity(ItemSO item)
    {
        if (itemHasPicked.TryGetValue(item, out int amount))
            return amount;
        return 0;
    }
}
