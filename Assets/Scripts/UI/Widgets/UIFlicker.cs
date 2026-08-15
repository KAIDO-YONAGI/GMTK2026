using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 为界面文本提供持续淡入淡出（闪烁）效果。
/// 用 Mathf.PingPong 在 [minAlpha, maxAlpha] 之间循环，无需 Animator/DOTween。
/// 挂到任意带 TMP_Text 或 UnityEngine.UI.Text 的节点上即可。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class UIFlicker : MonoBehaviour
{
    [Header("闪烁")]
    [Tooltip("一个完整淡入+淡出周期的时长（秒）。")]
    [SerializeField] private float period = 1.5f;

    [Tooltip("透明度闪烁下限（不完全透明，保持可读）。")]
    [Range(0f, 1f)] [SerializeField] private float minAlpha = 0.2f;

    [Tooltip("透明度闪烁上限。")]
    [Range(0f, 1f)] [SerializeField] private float maxAlpha = 1f;

    [Tooltip("勾选后使用 Time.unscaledTime，确保暂停（timeScale=0）时仍闪烁。菜单场景通常无需勾选。")]
    [SerializeField] private bool useUnscaledTime = false;

    private TMP_Text _text;
    private Color _baseColor;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _baseColor = _text.color;
    }

    private void Update()
    {
        // PingPong(0→period) 在 [0, period] 间往返，结果归一化到 [0,1]。
        float t = Mathf.PingPong((useUnscaledTime ? Time.unscaledTime : Time.time) / period, 1f);
        // 用正弦平滑，避免端点突变（更接近"淡入淡出"而非"线性开关"）
        float eased = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);
        float a = Mathf.Lerp(minAlpha, maxAlpha, eased);

        _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, a);
    }

    private void OnDisable()
    {
        // 禁用或隐藏时恢复原始透明度，避免残留在半透明状态。
        if (_text != null)
            _text.color = _baseColor;
    }
}
