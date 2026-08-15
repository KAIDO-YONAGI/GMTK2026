using UnityEngine;

public class NPCChat : MonoBehaviour
{
    private Rigidbody2D rb;
    public Animator chatAnimator;
    public DialogSO dialogSO;
    public ToggleCanvasEventSO toggleDialogEvent;

    private bool openDialogRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.DisableButtons();
        }
    }

    private void OnEnable()
    {
        openDialogRequested = false;

        if (toggleDialogEvent != null)
        {
            toggleDialogEvent.toggleCanvasEvent += OnToggleDialogEvent;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        if (chatAnimator != null)
        {
            chatAnimator.Play("Chat");
        }
    }

    private void OnDisable()
    {
        openDialogRequested = false;

        if (toggleDialogEvent != null)
        {
            toggleDialogEvent.toggleCanvasEvent -= OnToggleDialogEvent;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (chatAnimator != null)
        {
            chatAnimator.Play("Idle");
        }

        if (DialogManager.Instance != null)
        {
            DialogManager.Instance.ForeceEndDialog();
        }
    }
    private void OnToggleDialogEvent(bool state)
    {
        if (DialogManager.Instance == null)
        {
            return;
        }

        if (!state)
        {
            openDialogRequested = false;
            DialogManager.Instance.ForeceEndDialog();
            return;
        }

        openDialogRequested = true;
    }
    private void Update()
    {
        if (DialogManager.Instance == null)
        {
            return;
        }

        if (openDialogRequested)
        {
            openDialogRequested = false;

            if (dialogSO != null && !DialogManager.Instance.isDialogActive)
            {
                DialogManager.Instance.StartDialog(dialogSO);
            }
        }

        if (DialogManager.Instance.isDialogActive && Input.GetMouseButtonDown(0))
        {
            DialogManager.Instance.AdvanceDialog();
        }
    }
}
