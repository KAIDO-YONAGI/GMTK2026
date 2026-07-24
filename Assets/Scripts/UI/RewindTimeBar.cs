using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RewindTimeBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image consumedImage;
    [SerializeField] private Gradient colorGradient;
    [SerializeField] private SceneLoadEventSO sceneLoadEventSO;
    [SerializeField] private CanvasGroup hudGroup;

    private RectTransform fillRect;
    private RectTransform consumedRect;
    private float fullWidth;

    private bool isRetry; 

    float MaxSec => ReincarnationManager.Instance.MaxRewindBaseSeconds;
    float MinSec => ReincarnationManager.Instance.MinRewindBaseSeconds;

    private float currentConsumedWidth;

    private void Awake()
    {
        fillRect = fillImage.rectTransform;
        consumedRect = consumedImage.rectTransform;
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

        if (ReincarnationManager.Instance.IsRewinding)
        {
            float consumed = Mathf.Clamp(ReincarnationManager.Instance.CurrentRewoundGameTime, 0f, storedSeconds);
            float remaining = Mathf.Max(0f, storedSeconds - consumed);
            float t = storedSeconds > 0f ? remaining / storedSeconds : 0f;
            
            currentConsumedWidth = fillImageWidth * t;
        }
        else
        {
            float target = storedSeconds > 0f && rewindableSeconds > 0f
                ? fillImageWidth * (storedSeconds / rewindableSeconds)
                : 0f;
            currentConsumedWidth = Mathf.Lerp(currentConsumedWidth, target, Time.deltaTime * 8f);
            if (currentConsumedWidth < 0.5f) currentConsumedWidth = 0f;
        }

        size.x = currentConsumedWidth;
        consumedRect.sizeDelta = size;
    }
}
