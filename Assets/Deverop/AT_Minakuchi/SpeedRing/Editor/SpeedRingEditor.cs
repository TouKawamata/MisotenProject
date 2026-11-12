using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpeedRing))]
[CanEditMultipleObjects]
public sealed class SpeedRingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "_switchTag");

        SerializedProperty switchTag = serializedObject.FindProperty("_switchTag");
        using (new EditorGUI.MixedValueScope(switchTag.hasMultipleDifferentValues))
        {
            EditorGUI.BeginChangeCheck();
            string selectedTag = EditorGUILayout.TagField(
                new GUIContent("切り替え対象タグ", "Untagged では切り替えません。"), switchTag.stringValue);
            if (EditorGUI.EndChangeCheck())
                switchTag.stringValue = selectedTag;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
