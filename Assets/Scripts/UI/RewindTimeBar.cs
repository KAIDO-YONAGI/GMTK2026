using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewindTimeBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image usableImage;
    [SerializeField] private Gradient colorGradient;
    [SerializeField] private SceneLoadEventSO sceneLoadEventSO;
    [SerializeField] private CanvasGroup hudGroup;

    private RectTransform fillRect;
    private RectTransform consumedRect;
    private float fullWidth;

    private bool isRetry;
    // private float lastAppliedSnapshotTime;
    private float lastUsableImageWidth;

    float MaxSec => ReincarnationManager.Instance.MaxRewindBaseSeconds;
    float MinSec => ReincarnationManager.Instance.MinRewindBaseSeconds;

    private float currentUsabelWidth;

    private void Awake()
    {
        fillRect = fillImage.rectTransform;
        consumedRect = usableImage.rectTransform;
        fullWidth = fillRect.sizeDelta.x;
    }

    private void OnEnable()
    {
        if (sceneLoadEventSO != null)
            sceneLoadEventSO.LoadRequestEvent += OnSceneLoad;
    }

    private void OnDisable()
    {
        if (sceneLoadEventSO != null)
            sceneLoadEventSO.LoadRequestEvent -= OnSceneLoad;
    }

    private void OnSceneLoad(List<GameSceneSO> scenes, Vector3 position, bool isToFade)
    {
        if (isRetry)
        {
            isRetry = false;
            hudGroup.alpha = 0;
            return;
        }
        
        if (hudGroup == null) return;
        bool hasLocation = scenes != null && scenes.Exists(s => s != null && s.sceneType == MyEnums.SceneType.Location);
        
        hudGroup.alpha = hasLocation ? 1 : 0;
        hudGroup.interactable = hasLocation;
        hudGroup.blocksRaycasts = hasLocation;
    }
    
    private void Update()
    {
        if (AgeManager.instance == null || ReincarnationManager.Instance == null) return;

        float agingRate = 1f - AgeManager.instance.AgeNormalized;
        float rewindableSeconds = Mathf.Lerp(MinSec, MaxSec, agingRate);
        float fillAmount = agingRate;

        Vector2 size = fillRect.sizeDelta;
        float fillImageWidth = fullWidth * fillAmount;
        size.x = fillImageWidth;
        fillRect.sizeDelta = size;
        fillImage.color = colorGradient.Evaluate(AgeManager.instance.AgeNormalized);

        float storedSeconds = ReincarnationManager.Instance.CurrentStoredSeconds;
        float storeFactor = storedSeconds > 0f && rewindableSeconds > 0f
            ?  storedSeconds / rewindableSeconds : 0f;

        if (ReincarnationManager.Instance.IsRewinding)
        {
            float consumed = Mathf.Clamp(ReincarnationManager.Instance.CurrentRewoundGameTime, 0f, storedSeconds);
            float remaining = Mathf.Max(0f, storedSeconds - consumed);
            float t = storedSeconds > 0f ? remaining / storedSeconds : 0f;
            
            currentUsabelWidth = fillImageWidth * storeFactor * t;
            lastUsableImageWidth = currentUsabelWidth;
        }

        else if (ReincarnationManager.Instance.isRewindCoolingDown)
        {
            currentUsabelWidth = lastUsableImageWidth;
        }
        else
        {
            float target = storedSeconds > 0f && rewindableSeconds > 0f
                ? fillImageWidth * (storedSeconds / rewindableSeconds)
                : 0f;
            currentUsabelWidth = Mathf.Lerp(currentUsabelWidth, target, Time.deltaTime * 8f);
            if (currentUsabelWidth < 0.5f) currentUsabelWidth = 0f;
        }

        size.x = currentUsabelWidth;
        consumedRect.sizeDelta = size;
    }
}
