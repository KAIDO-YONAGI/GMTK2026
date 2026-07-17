using TMPro;
using UnityEngine;

public class HealthCanvasManager : MonoBehaviour
{
    public static HealthCanvasManager instance;
    public TMP_Text healthText;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void UpdateHealthText()
    {
        var stats = StatsManager.instance.GetStats();
        Animator animator = healthText.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play("TextUpdate");
        }
        healthText.text = "HP:" + stats.currentHealth + "/" + stats.maxHealth;
    }
}
