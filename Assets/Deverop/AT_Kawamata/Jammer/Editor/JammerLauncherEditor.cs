using UnityEditor;
using UnityEngine;

// JammerLauncherのInspectorに、弾の種類の切り替えボタンを出す（Typesの要素ごとに1つ。選択中は色付き）。
// Play中はその場で切り替え、Play前はレース開始時の種類（Current Type Index）を書き換える。Play中は残弾・再装填の残り秒も出す。
[CustomEditor(typeof(JammerLauncher))]
public class JammerLauncherEditor : Editor
{
    private static readonly Color SelectedColor = new Color(0.5f, 0.9f, 0.6f);

    private SerializedProperty _types;
    private SerializedProperty _currentTypeIndex;

    private void OnEnable()
    {
        _types = serializedObject.FindProperty("_types");
        _currentTypeIndex = serializedObject.FindProperty("_currentTypeIndex");
    }

    // Play中は残弾・再装填の残り秒数が動くので、描き直し続ける。
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        JammerLauncher launcher = (JammerLauncher)target;
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("妨害弾の種類の切り替え", EditorStyles.boldLabel);

        if (_types.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Typesに弾の種類（JammerTypeProfile）を入れてください。Tools/Jammer/初期セットアップで一式を作れます", MessageType.Info);
        }

        int currentIndex = Application.isPlaying ? launcher.CurrentTypeIndex : _currentTypeIndex.intValue;
        for (int i = 0; i < _types.arraySize; i++)
        {
            JammerTypeProfile type = _types.GetArrayElementAtIndex(i).objectReferenceValue as JammerTypeProfile;
            string label = type != null ? $"{type.DisplayName}{GetShieldLabel(type)}" : "（未設定）";
            DrawTypeButton(launcher, i, label, i == currentIndex, type != null);
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("残弾", $"{launcher.Ammo} / {launcher.MaxAmmo}");
            EditorGUILayout.LabelField("次の回復まで", $"{launcher.ReloadRemainingTime:F1} 秒");
        }
    }

    private void DrawTypeButton(JammerLauncher launcher, int index, string label, bool isSelected, bool isValid)
    {
        Color previousColor = GUI.backgroundColor;
        if (isSelected)
        {
            GUI.backgroundColor = SelectedColor;
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && isValid;
        bool isClicked = GUILayout.Button(label);
        GUI.enabled = previousEnabled;
        GUI.backgroundColor = previousColor;

        if (!isClicked)
        {
            return;
        }

        if (Application.isPlaying)
        {
            launcher.SelectType(index);
            return;
        }

        _currentTypeIndex.intValue = index;
        serializedObject.ApplyModifiedProperties();
    }

    private static string GetShieldLabel(JammerTypeProfile type)
    {
        if (!type.CanBeBlockedByShield)
        {
            return "（シールド不可）";
        }

        return type.PushOnBlock ? "（シールド可・加速）" : "（シールド可）";
    }
}
