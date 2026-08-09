using UnityEngine;

[System.Serializable]
public struct AudioItem
{
    public AudioClip clip;
    [Range(0f, 1f)] public float weight;
}

[System.Serializable]
public class SFXEntry
{
    public MyEnums.SFXType type;
    public AudioItem[] items;
}

[System.Serializable]
public class BGMEntry
{
    public MyEnums.BGMType type;
    public AudioItem[] items;
}

[CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/AudioConfig", order = 0)]
public class AudioConfigSO : ScriptableObject
{
    public SFXEntry[] sfxEntries;
    public BGMEntry[] bgmEntries;

    public AudioClip GetSFXClip(MyEnums.SFXType type)
    {
        foreach (var entry in sfxEntries)
        {
            if (entry.type == type && entry.items != null && entry.items.Length > 0)
                return PickWeighted(entry.items);
        }
        return null;
    }

    public AudioClip GetBGMClip(MyEnums.BGMType type)
    {
        foreach (var entry in bgmEntries)
        {
            if (entry.type == type && entry.items != null && entry.items.Length > 0)
                return PickWeighted(entry.items);
        }
        return null;
    }

    private AudioClip PickWeighted(AudioItem[] items)
    {
        float total = 0f;
        foreach (var item in items) total += item.weight;
        float r = Random.Range(0f, total);
        foreach (var item in items)
        {
            r -= item.weight;
            if (r <= 0f) return item.clip;
        }
        return items[items.Length - 1].clip;
    }
}
