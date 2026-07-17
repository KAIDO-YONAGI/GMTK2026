using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogManager : MonoBehaviour
{
    public static DialogManager instance;

    [Header("Dialog UI")]
    public CanvasGroup dialogCanvasGroup;
    public Image speakerPortrait;
    public TMP_Text dialogText;
    public TMP_Text speakerNameText;
    public bool isDialogActive;
    public Button[] optionButtons;

    private int currentLineIndex = 0;
    private DialogSO currentDialog;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SetDialogCanvas(false);
        DisableButtons();
    }
    private void OnDisable()
    {
        DisableButtons();
    }
    public void SetDialogCanvas(bool state)
    {
        if (dialogCanvasGroup == null)
        {
            isDialogActive = false;
            return;
        }

        dialogCanvasGroup.alpha = state ? 1 : 0;
        dialogCanvasGroup.interactable = state;
        dialogCanvasGroup.blocksRaycasts = state;
        isDialogActive = state;
        UIManager.instance.ReportCanvasState(MyEnums.CanvasToToggle.Dialog, state);
    }

    public void StartDialog(DialogSO dialog)
    {
        if (dialog == null)
        {
            return;
        }

        if (!MatchConditionsToStartDialog(dialog))
        {
            return;
        }

        SetDialogCanvas(true);
        DisableButtons();
        currentDialog = dialog;
        currentLineIndex = 0;
        ShowDialog();
    }

    public void AdvanceDialog()
    {
        if (currentDialog == null)
        {
            return;
        }

        if (currentLineIndex < currentDialog.dialogLines.Length)
        {
            ShowDialog();
        }
        else if (currentDialog.nextDialogOptions.Length == 0 &&
                 currentLineIndex == currentDialog.dialogLines.Length)
        {
            EndDialog();
            return;
        }

        if (currentDialog != null &&
            currentDialog.dialogLines.Length != 0 &&
            currentDialog.dialogLines.Length == currentLineIndex)
        {
            ShowChoices();
        }
    }

    public void EndDialog()
    {
        SetDialogCanvas(false);
        DisableButtons();

        if (currentDialog != null)
        {
            if (ConversationHistoryManager.instance != null)
            {
                ConversationHistoryManager.instance.RecordCharacter(currentDialog.mainCharacter);
            }

            if (currentDialog.onlyTriggeredOnce &&
                currentDialog.parentDialog != null)
            {
                ConversationHistoryManager.instance.RecordDialogHasChated(currentDialog.parentDialog);
            }
        }

        currentDialog = null;
        currentLineIndex = 0;
    }

    public void ForeceEndDialog()
    {
        SetDialogCanvas(false);
        DisableButtons();
        currentDialog = null;
        currentLineIndex = 0;
    }

    private bool MatchConditionsToStartDialog(DialogSO dialog)
    {
        foreach (var refuse in dialog.refuseDialogs)
        {
            bool shouldRefuse;

            if (refuse.isDefaultChat)
            {
                shouldRefuse = ConversationHistoryManager.instance != null &&
                               ConversationHistoryManager.instance.HasDialogChated(dialog);
            }
            else
            {
                shouldRefuse = !HasRefuseConditions(refuse);
            }

            if (shouldRefuse)
            {
                StartRefuseDialog(refuse);
                return false;
            }
        }

        return true;
    }

    private bool HasRefuseConditions(RefuseDialogSO refuse)
    {
        if (refuse.requireCharacters != null && refuse.requireCharacters.Count > 0)//检测角色对话
        {
            foreach (var character in refuse.requireCharacters)
            {
                if (ConversationHistoryManager.instance == null ||
                    !ConversationHistoryManager.instance.HasChatedWith(character))
                {
                    return false;
                }
            }
        }

        if (refuse.requireItems != null && refuse.requireItems.Count > 0)//检测物品捡到没有
        {
            foreach (var item in refuse.requireItems)
            {
                if (ItemHistoryManager.instance == null ||
                    !ItemHistoryManager.instance.HasPickedOverAmount(item.itemSO, item.quantity))
                {
                    return false;
                }
            }
        }
        //TODO 添加检测位置有没有去过的逻辑

        return true;
    }

    private void StartRefuseDialog(RefuseDialogSO refuse)
    {
        SetDialogCanvas(true);
        DisableButtons();
        currentDialog = refuse;
        currentLineIndex = 0;
        ShowDialog();
    }

    private void ShowChoices()
    {
        if (currentDialog == null || currentDialog.nextDialogOptions.Length == 0)
        {
            return;
        }

        if (optionButtons == null || optionButtons.Length == 0)
        {
            return;
        }

        InitializeButtons();

        for (int i = 0; i < currentDialog.nextDialogOptions.Length; i++)
        {
            if (i >= optionButtons.Length)
            {
                break;
            }

            if (optionButtons[i] == null)
            {
                continue;
            }

            int index = i;
            DialogSO nextDialog = currentDialog.nextDialogOptions[index].nextDialogNode;

            optionButtons[index].onClick.AddListener(() => OnOptionSelected(nextDialog));
        }
    }

    private void ShowDialog()
    {
        if (currentDialog == null || currentDialog.dialogLines.Length == 0)
        {
            EndDialog();
            return;
        }

        if (currentLineIndex < 0 || currentLineIndex >= currentDialog.dialogLines.Length)
        {
            return;
        }

        DialogLine currentLine = currentDialog.dialogLines[currentLineIndex];

        if (speakerPortrait != null)
        {
            speakerPortrait.sprite = currentLine.speaker.characterPortrait;
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = currentLine.speaker.characterName;
        }

        if (dialogText != null)
        {
            dialogText.text = currentLine.text;
        }

        currentLineIndex++;
    }

    public void DisableButtons()
    {
        if (optionButtons == null)
        {
            return;
        }

        foreach (var button in optionButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.gameObject.SetActive(false);
            button.onClick.RemoveAllListeners();
        }
    }

    public void InitializeButtons()
    {
        if (currentDialog == null || optionButtons == null)
        {
            return;
        }

        int nextDialogOptions = currentDialog.nextDialogOptions.Length;
        int buttonQuantity = optionButtons.Length;

        if (nextDialogOptions > buttonQuantity)
        {
            Debug.Log("Dialog options out of button quantity:" + buttonQuantity);
            return;
        }

        for (int i = 0; i < buttonQuantity; i++)
        {
            if (optionButtons[i] == null)
            {
                continue;
            }

            if (i < nextDialogOptions)
            {
                optionButtons[i].interactable = true;
                optionButtons[i].gameObject.SetActive(true);
                TMP_Text optionText = optionButtons[i].GetComponentInChildren<TMP_Text>();

                if (optionText != null)
                {
                    optionText.text = currentDialog.nextDialogOptions[i].optionText;
                }
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnOptionSelected(DialogSO nextDialog)
    {
        if (nextDialog != null)
        {
            StartDialog(nextDialog);
            DisableButtons();
        }
        else
        {
            EndDialog();
        }
    }
}
