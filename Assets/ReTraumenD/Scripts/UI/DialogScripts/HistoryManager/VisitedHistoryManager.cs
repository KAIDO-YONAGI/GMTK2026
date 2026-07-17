using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisitedHistoryManager : MonoBehaviour
{
    public static VisitedHistoryManager instance;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(gameObject);
    }
}
