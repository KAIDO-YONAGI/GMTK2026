using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlayerMovement 的只读调试面板。
/// 在默认 Inspector 末尾追加一段「Debug / 真实速度」区域，
/// 实时显示经过衰老、加速 buff 缩放后的派生值，方便调参时核对实际速度。
/// 仅用于编辑器显示，不修改任何运行时逻辑。
/// </summary>
[CustomEditor(typeof(PlayerMovement))]
public class PlayerMovementEditor : Editor
{
    // PlayerMovement 的私有字段名，通过反射读取运行时实时值。
    const string AgingRateField = "agingRate";
    const string SpeedBoostEndTimeField = "speedBoostEndTime";
    const string IsGroundedField = "isGrounded";
    const string IsInputEnabledField = "isInputEnabled";

    // 控制 Play 模式下调试面板持续刷新的开关。
    bool liveRefresh = true;

    void OnEnable()
    {
        // Play 模式下注册每帧重绘，让 Inspector 里的运行时实时值持续刷新。
        // 否则只有鼠标悬停在 Inspector 上时 Unity 才会重绘面板，切到 Game 视图后数值会冻结。
        EditorApplication.update += HandleEditorUpdate;
    }

    void OnDisable()
    {
        // 面板销毁（切到别的物体 / 重新编译）时务必解绑，避免悬挂回调导致泄漏或报错。
        EditorApplication.update -= HandleEditorUpdate;
    }

    void HandleEditorUpdate()
    {
        if (!liveRefresh || !Application.isPlaying)
            return;

        // 仅当当前正选中该 target 时才请求重绘，避免无谓地刷新所有 Inspector。
        if (target != null && Selection.activeGameObject == ((PlayerMovement)target).gameObject)
            Repaint();
    }

    public override void OnInspectorGUI()
    {
        // 先绘制默认 Inspector（所有 [SerializeField] 字段）。
        DrawDefaultInspector();

        var movement = (PlayerMovement)target;
        if (movement == null)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug / 真实速度", EditorStyles.boldLabel);

        // 开关：可手动关掉实时刷新（关掉后数值不再随帧变化，省去不必要的 Inspector 重绘）。
        liveRefresh = EditorGUILayout.Toggle("实时刷新", liveRefresh);

        // 没进入 Play 模式时这些派生值意义不大，提示一下但仍显示默认值。
        GUI.enabled = Application.isPlaying;

        EditorGUI.indentLevel++;

        // 真实水平移动速度：moveSpeed * agingRate * buff倍率。
        EditorGUILayout.LabelField("当前真实速度 (CurrentMoveSpeed)", movement.CurrentMoveSpeed.ToString("F3"));

        // 加速 buff 是否生效（含剩余时间）。IsSpeedBoostActive 是属性，直接读取。
        bool boostActive = movement.IsSpeedBoostActive;
        EditorGUILayout.LabelField("加速 Buff 生效", boostActive ? "是" : "否");
        if (boostActive)
        {
            float end = GetFloat(movement, SpeedBoostEndTimeField);
            EditorGUILayout.LabelField("Buff 剩余 (s)", Mathf.Max(0f, end - Time.time).ToString("F2"));
        }

        // 拆解出影响真实速度的各分量，便于定位是哪个环节导致速度变化。
        EditorGUILayout.LabelField("衰老系数 (agingRate)", GetFloat(movement, AgingRateField).ToString("F3"));
        EditorGUILayout.LabelField("是否落地 (isGrounded)", GetBool(movement, IsGroundedField) ? "是" : "否");
        EditorGUILayout.LabelField("输入已启用 (isInputEnabled)", GetBool(movement, IsInputEnabledField) ? "是" : "否");

        EditorGUI.indentLevel--;
        GUI.enabled = true;
    }

    // 通过反射读取实例的私有/实例字段，避免为调试显示把这些字段改成 public。
    static float GetFloat(Object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null && field.GetValue(instance) is float value ? value : 0f;
    }

    static bool GetBool(Object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null && field.GetValue(instance) is bool value && value;
    }
}
