using UnityEditor;
using UnityEngine;

// CharacterPrototypeManagerのInspectorに、タイプの切り替えボタンを出す。
// Play中はその場で切り替え、Play前はレース開始時のタイプ（Initial Type）を書き換える。
[CustomEditor(typeof(CharacterPrototypeManager))]
public class CharacterPrototypeManagerEditor : Editor
{
    private static readonly Color SelectedColor = new Color(0.5f, 0.9f, 0.6f);

    private SerializedProperty _initialType;

    private void OnEnable()
    {
        _initialType = serializedObject.FindProperty("_initialType");
    }

    // Play中は残弾・再装填の残り秒数が動くので、描き直し続ける。
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CharacterPrototypeManager manager = (CharacterPrototypeManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("タイプの切り替え", EditorStyles.boldLabel);

        DrawTypeButton(manager, ECharacterPrototypeType.NoShieldSlowdown, "シールド中に減速しない");
        DrawTypeButton(manager, ECharacterPrototypeType.WideBoostHit, "ブーストの当たり判定が広い");
        DrawTypeButton(manager, ECharacterPrototypeType.Jammer, "妨害弾");

        if (Application.isPlaying && manager.CurrentType == ECharacterPrototypeType.Jammer)
        {
            EditorGUILayout.LabelField("残弾", $"{manager.JammerAmmo} / {manager.JammerMaxAmmo}");
            EditorGUILayout.LabelField("次の回復まで", $"{manager.JammerReloadRemainingTime:F1} 秒");
        }
    }

    private void DrawTypeButton(CharacterPrototypeManager manager, ECharacterPrototypeType type, string label)
    {
        ECharacterPrototypeType currentType = Application.isPlaying ? manager.CurrentType : (ECharacterPrototypeType)_initialType.enumValueIndex;

        Color previousColor = GUI.backgroundColor;
        if (type == currentType)
        {
            GUI.backgroundColor = SelectedColor;
        }

        bool isClicked = GUILayout.Button(label);
        GUI.backgroundColor = previousColor;

        if (!isClicked)
        {
            return;
        }

        if (Application.isPlaying)
        {
            manager.ChangeType(type);
            return;
        }

        serializedObject.Update();
        _initialType.enumValueIndex = (int)type;
        serializedObject.ApplyModifiedProperties();
    }
}
