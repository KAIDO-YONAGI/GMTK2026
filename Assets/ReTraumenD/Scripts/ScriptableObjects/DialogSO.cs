using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogSO", menuName = "Dialog/DialogNode", order = 1)]
public class DialogSO : ScriptableObject
{
    public CharacterSO mainCharacter;
    public DialogLine[] dialogLines;
    public DialogOption[] nextDialogOptions;

    [Header("Refuse Requirements For Main")]

    public List<RefuseDialogSO> refuseDialogs;

    [Header("Conditional Requirements For Sub")]
    public bool onlyTriggeredOnce;//用于在子对话设置该分支只能进入一次
    public DialogSO parentDialog;//在子对话（option）设置，用于标记已完成的对话，会阻止进入主分支

}
[System.Serializable]
public class DialogLine
{
    public CharacterSO speaker;
    [TextArea(3, 10)] public string text;
}
[System.Serializable]
public class DialogOption
{
    public string optionText;
    public DialogSO nextDialogNode;
}
[System.Serializable]
public class Item
{
    public ItemSO itemSO;
    public int quantity;
}


/*
 * ============================================================
 * DialogSO —— 对话节点配置说明
 * ============================================================
 * 用途：表示对话树中的一个节点（一段主对话）。
 * 创建：Project 窗口右键 > Create > Dialog > DialogNode，
 *      会生成一个 fileName 为 "DialogSO" 的资源文件，
 *      建议重命名为有业务含义的名字（如 NPC_Tavern_Greeting）。
 * 挂载位置：无需挂载到 GameObject，纯数据资源；由对话系统按引用读取。
 *
 * 依赖的 SO：
 *   - CharacterSO      说话人角色（含名字 + 头像），路径 Dialog/Character
 *   - DialogSO         自身互引用，构成对话树（父→子 option）
 *   - RefuseDialogSO   拒绝策略对话（继承自本类），用于条件拦截
 *   - ItemSO           物品引用（结构体 Item 用到），路径 GameLootSO/ItemSO
 *
 * 配置要点（Inspector 字段）：
 *
 * 【主对话内容】
 *   - mainCharacter       : 主线对话的主要角色（可空，仅做标记/方便筛选）。
 *   - dialogLines[]       : 本节点按顺序播放的台词数组。
 *       ├ speaker : 该句话的说话人（引用某个 CharacterSO，可每句不同）。
 *       └ text    : 台词正文（TextArea 3~10 行，可换行）。
 *   - nextDialogOptions[] : 该节点结束后展示给玩家的分支选项。
 *       ├ optionText    : 选项按钮上显示的文字。
 *       └ nextDialogNode: 选中该项后跳转到的下一个 DialogSO（叶子节点留空）。
 *
 * 【Refuse Requirements For Main —— 主线进入/拒绝条件】
 *   - refuseDialogs       : 拒绝策略列表。
 *       └ RefuseDialogSO（见 RefuseDialogSO.cs）：
 *         · isDefaultChat    : 是否默认对话（所有拒绝策略执行后仍拒绝→主线单次）。
 *         · requireCharacters: 需要队伍/场景中存在这些角色，否则拒绝。
 *         · requireItems     : 需要背包中有这些物品（结构体 Item：itemSO + quantity），否则拒绝。
 *
 * 【Conditional Requirements For Sub —— 子对话（option 目标）专用】
 *   - onlyTriggeredOnce   : 勾选后，该分支只能进入一次（再次选择会被拦截）。
 *   - parentDialog        : 在【作为子对话】的节点上配置，
 *                          指回它的父 DialogSO；用于标记“此分支已完成”，
 *                          可阻止玩家再次进入主分支或重复触发。
 *
 * 推荐配置流程：
 *   1) 先创建好所需 CharacterSO（Dialog/Character）与 ItemSO。
 *   2) 为每个对话节点创建一个 DialogSO，填 dialogLines。
 *   3) 在 nextDialogOptions 里引用“下一个 DialogSO”，逐层把对话树串起来。
 *   4) 若主线需条件门槛，在 refuseDialogs 里挂 RefuseDialogSO；
 *      若某分支需一次性/父子标记，勾 onlyTriggeredOnce 并填 parentDialog。
 *
 * 注意事项：
 *   - 结构体 Item 定义在本文件末尾，被 RefuseDialogSO 复用，勿与 ItemSO 混淆。
 *   - dialogLines 与 nextDialogOptions 可同时为空（纯过渡节点）或只留其一。
 *   - SO 之间是引用关系，重命名/移动资源会断引用，建议改用 GUID 友好的方式或用 GuidSO。
 * ============================================================
 */