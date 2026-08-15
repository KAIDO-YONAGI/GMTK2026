using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DeathEventSO", menuName = "Events/DeathEventSO", order = 0)]
public class DeathEventSO_ : ScriptableObject
{
    public event Action<MyEnums.DeathType> deathEvent;

    public void RaiseDeathEvent(MyEnums.DeathType type)
    {
        deathEvent?.Invoke(type);
    }
}
