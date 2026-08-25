using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMovement))]
public sealed class PlayerMovementEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            return;
        }

        PlayerMovement movement = (PlayerMovement)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Can Move", movement.CanMove);
            EditorGUILayout.Toggle("Grounded", movement.IsGrounded);
            EditorGUILayout.FloatField("Horizontal Input", movement.HorizontalInput);
            EditorGUILayout.FloatField("Aging Rate", movement.AgingRate);
            EditorGUILayout.FloatField("Current Move Speed", movement.CurrentMoveSpeed);
            EditorGUILayout.Toggle("Speed Boost Active", movement.IsSpeedBoostActive);
            EditorGUILayout.FloatField("Speed Boost Remaining", movement.SpeedBoostRemaining);
        }

        Repaint();
    }
}
