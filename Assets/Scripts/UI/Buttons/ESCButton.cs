using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ESCButton : MonoBehaviour
{
    public Button escButton;
    [SerializeField] private MyEnums.CanvasToToggle canvasToESC;
    private void OnEnable() {
        escButton.onClick.AddListener(OnESC);
    }
    private void OnDisable()
    {
        escButton.onClick.RemoveAllListeners();
    }
    private void OnESC()
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        UIManager.Instance.RequestCanvasClose(canvasToESC);
    }


}
