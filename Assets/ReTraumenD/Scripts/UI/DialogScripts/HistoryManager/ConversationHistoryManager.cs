using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConversationHistoryManager : MonoBehaviour
{
    public static ConversationHistoryManager instance;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(gameObject);
    }
    private HashSet<CharacterSO> charactersHasChated = new();

    private HashSet<int> dialogsHasChated = new();

    public void RecordCharacter(CharacterSO character)
    {
        charactersHasChated.Add(character);
    }
    public bool HasChatedWith(CharacterSO character)
    {
        return charactersHasChated.Contains(character);
    }

    public void RecordDialogHasChated(DialogSO dialog)
    {
        dialogsHasChated.Add(dialog.GetInstanceID());
    }
    public bool HasDialogChated(DialogSO dialog)
    {
        return dialogsHasChated.Contains(dialog.GetInstanceID());
    }
}
