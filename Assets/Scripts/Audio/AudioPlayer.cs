using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    private AudioSource _source;
    private AudioManager _manager;

    public void Init(AudioManager manager)
    {
        _manager = manager;
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        gameObject.SetActive(false);
    }

    public void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        _source.clip = clip;
        _source.volume = volume;
        _source.pitch = pitch;
        gameObject.SetActive(true);
        _source.Play();
        StartCoroutine(WaitForEnd());
    }

    private System.Collections.IEnumerator WaitForEnd()
    {
        while (_source.isPlaying)
            yield return null;
        _manager.ReturnPlayer(this);
    }

    public void StopAndReturn()
    {
        StopAllCoroutines();
        if (_source != null) _source.Stop();
        _manager.ReturnPlayer(this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
