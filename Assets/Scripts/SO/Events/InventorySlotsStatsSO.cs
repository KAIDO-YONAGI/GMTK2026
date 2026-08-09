using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "InventorySlotsStatsSO", menuName = "Events/InventorySlotsStatsSO", order = 0)]
public class InventorySlotsStatsSO : ScriptableObject
{
    //均为双向
    public UnityAction<ItemSO, int, int> InventoryUpdateRequestEvent;
    public UnityAction<bool[]> InventoryRespondEvent;
    /// <summary>
    /// 请求更新物品
    /// </summary>
    /// <param name="item">请求的物品</param>
    /// <param name="price">价格</param>
    /// <param name="amount">数量</param>
    /// 用于购买时请求物品栏变更和出售时发送物品信息并校对
    public void RaiseInventoryUpdateRequest(ItemSO item, int price, int amount)
    {
        InventoryUpdateRequestEvent?.Invoke(item, price, amount);
    }
    /// <summary>
    /// 状态交换
    /// </summary>
    /// <param name="stats[0]">商店有无足量可售物品</param>
    /// <param name="stats[1]">有无物品栏空位（或物品栏是否已经更新）</param>
    /// 物品栏请求出售后商店查询并且返回是否有该物品，bool值会决定物品栏管理器的行为，也可用于物品栏是否已经更新
    public void RaiseInventoryRespondEvent(bool[] stats)
    {
        InventoryRespondEvent?.Invoke(stats);
    }

}
