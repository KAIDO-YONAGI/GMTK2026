using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatsCanvasManager : MonoBehaviour, ICanvasManager
{
    public static StatsCanvasManager instance;
    public GameObject[] statsSlots;
    public CanvasGroup statsCanvas;

    public ToggleCanvasEventSO toggleStatsEvent;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleStatsEvent;
    private Canvas canvas;

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        statsCanvas.alpha = 0;
        canvas = statsCanvas.GetComponent<Canvas>();
    }

    private void Start()
    {
        UpdateAllStats();
    }

    private void OnEnable()
    {
        toggleStatsEvent.toggleCanvasEvent += OnToggleStatsEvent;
    }

    private void OnDisable()
    {
        toggleStatsEvent.toggleCanvasEvent -= OnToggleStatsEvent;
    }

    private void OnToggleStatsEvent(bool state)
    {
        UpdateAllStats();
        ((ICanvasManager)this).ToggleCanvas(statsCanvas, canvas, MyEnums.CanvasToToggle.Stats, state);
    }

    public void UpdateDamage()
    {
        statsSlots[0].GetComponentInChildren<TMP_Text>().text = "Damage:" + StatsManager.instance.GetDamage();
        // 注意：Components是复数形式，返回的是数组。
    }

    public void UpdateSpeed()
    {
        statsSlots[1].GetComponentInChildren<TMP_Text>().text = "Speed:" + StatsManager.instance.GetSpeed();
    }

    public void UpdateAllStats()
    {
        UpdateDamage();
        UpdateSpeed();
    }
}
