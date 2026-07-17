using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class NPCStateController : MonoBehaviour
{
    [Header("Component References")]
    public NPCChat chatScript;
    [SerializeField]
    public MyEnums.NPCState DefaultState = MyEnums.NPCState.Patrol;
    private MyEnums.NPCState currentState;

    private void Start()
    {
        SwitchState(DefaultState);
    }
    public void SwitchState(MyEnums.NPCState newState)
    {
        currentState = newState;
        if (chatScript != null) chatScript.enabled = currentState == MyEnums.NPCState.Chat;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            SwitchState(MyEnums.NPCState.Chat);
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            SwitchState(DefaultState);
        }
    }

}
