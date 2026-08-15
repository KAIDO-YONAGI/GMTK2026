using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidSO : ScriptableObject
{
    [SerializeField] private string guid;

    public string Guid => guid;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(guid))
        {
            guid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
