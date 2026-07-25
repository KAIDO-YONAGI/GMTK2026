using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "DeathEventSO", menuName = "Events/DeathEventSO", order = 0)]
public class DeathEventSO_ : ScriptableObject
{
    public UnityAction<MyEnums.DeathType> deathEvent;

    public void RaiseDeathEvent(MyEnums.DeathType type)
    {
        deathEvent?.Invoke(type);
    }
}
