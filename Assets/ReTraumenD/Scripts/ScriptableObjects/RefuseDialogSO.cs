using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RefuseDialogSO", menuName = "Dialog/RefuseDialogSO", order = 1)]
public class RefuseDialogSO : DialogSO
{
    public bool isDefaultChat;
    //标记当前的拒绝策略对话是不是默认对话
    //如果放到了列表里，就会在拒绝策略都被执行之后依旧拒绝，实现主分支单次对话
    public List<CharacterSO> requireCharacters;
    public List<Item> requireItems;
}
