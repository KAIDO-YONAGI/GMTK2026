using UnityEngine;

public class ESCMenuManager : MonoBehaviour
{
    public CanvasGroup ESCGroup;
    public ToggleCanvasEventSO toggleESCEvent;
    private void OnEnable()
    {
        toggleESCEvent.toggleCanvasEvent += OnESC;
    }
    private void OnDisable()
    {
        toggleESCEvent.toggleCanvasEvent -= OnESC;

    }
    private void OnESC(bool state)
    {

        if (state)
        {
            TimeManager.instance.PauseGame();
            ESCGroup.alpha = 1;
            ESCGroup.interactable = true;
            ESCGroup.blocksRaycasts = true;
        }
        else
        {
            TimeManager.instance.ResumeGame();
            ESCGroup.alpha = 0;
            ESCGroup.interactable = false;
            ESCGroup.blocksRaycasts = false;
        }

        UIManager.instance.ReportCanvasState(MyEnums.CanvasToToggle.ESC, state);
    }
}

