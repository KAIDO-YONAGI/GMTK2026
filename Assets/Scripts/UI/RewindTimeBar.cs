using UnityEngine;
using UnityEngine.UI;

public class RewindTimeBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Gradient colorGradient;

    private RectTransform fillRect;
    private float fullWidth;

    float MaxSec => ReincarnationManager.Instance.MaxRewindBaseSeconds;
    float MinSec => ReincarnationManager.Instance.MinRewindBaseSeconds;

    private void Awake()
    {
        fillRect = fillImage.rectTransform;
        fullWidth = fillRect.sizeDelta.x;
    }

    private void Update()
    {
        if (AgeManager.instance == null || ReincarnationManager.Instance == null) return;

        float agingRate = 1f - AgeManager.instance.AgeNormalized;
        float rewindableSeconds = Mathf.Lerp(MinSec, MaxSec, agingRate);
        float fillAmount = rewindableSeconds / MaxSec;

        Vector2 size = fillRect.sizeDelta;
        size.x = fullWidth * fillAmount;
        fillRect.sizeDelta = size;
        fillImage.color = colorGradient.Evaluate(AgeManager.instance.AgeNormalized);
    }
}
