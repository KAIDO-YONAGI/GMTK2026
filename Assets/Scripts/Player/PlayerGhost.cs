using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerGhost : MonoBehaviour, IPoolable
{
    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public SpriteRenderer Renderer => _renderer;

    public void OnPoolGet()
    {
        gameObject.SetActive(true);
    }

    public void OnPoolReturn()
    {
        gameObject.SetActive(false);
    }
}
