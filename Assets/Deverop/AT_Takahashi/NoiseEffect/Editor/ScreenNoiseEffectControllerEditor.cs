#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// ScreenNoiseEffectController用のカスタムInspector。
///
/// Play Mode中に、Inspectorからプリセットを選択して
/// Play / Stopを実行できる。
/// </summary>
[CustomEditor(typeof(ScreenNoiseEffectController))]
public sealed class ScreenNoiseEffectControllerEditor
    : Editor
{
    private SerializedProperty _presetsProperty;
    private SerializedProperty _selectedIndexProperty;

    private void OnEnable()
    {
        _presetsProperty =
            serializedObject.FindProperty("_presets");

        _selectedIndexProperty =
            serializedObject.FindProperty(
                "_selectedPresetIndex"
            );
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12f);

        DrawSeparator();

        EditorGUILayout.LabelField(
            "Effect Preview",
            EditorStyles.boldLabel
        );

        DrawPresetPopup();

        EditorGUILayout.Space(6f);

        ScreenNoiseEffectController controller =
            (ScreenNoiseEffectController)target;

        DrawPlayStopButtons(controller);

        EditorGUILayout.Space(4f);

        DrawPreviousNextButtons(controller);

        EditorGUILayout.Space(12f);

        DrawSeparator();

        EditorGUILayout.LabelField(
            "Setup",
            EditorStyles.boldLabel
        );

        DrawSetupButtons(controller);

        EditorGUILayout.Space(10f);

        DrawSeparator();

        DrawRuntimeStatus(controller);

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(6f);

            EditorGUILayout.HelpBox(
                "EffectのPlay / Stopは、" +
                "Play Mode中に実行してください。",
                MessageType.Info
            );
        }
    }

    private void DrawPresetPopup()
    {
        if (_presetsProperty == null ||
            _selectedIndexProperty == null)
        {
            return;
        }

        int presetCount =
            _presetsProperty.arraySize;

        if (presetCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "プリセットが登録されていません。",
                MessageType.Warning
            );

            return;
        }

        string[] presetNames =
            new string[presetCount];

        for (int i = 0; i < presetCount; i++)
        {
            SerializedProperty element =
                _presetsProperty
                    .GetArrayElementAtIndex(i);

            SerializedProperty displayName =
                element.FindPropertyRelative(
                    "_displayName"
                );

            string name = displayName.stringValue;

            presetNames[i] =
                string.IsNullOrWhiteSpace(name)
                    ? $"Preset {i}"
                    : name;
        }

        int currentIndex = Mathf.Clamp(
            _selectedIndexProperty.intValue,
            0,
            presetCount - 1
        );

        int newIndex = EditorGUILayout.Popup(
            "操作対象",
            currentIndex,
            presetNames
        );

        if (newIndex == currentIndex)
        {
            return;
        }

        _selectedIndexProperty.intValue =
            newIndex;

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPlayStopButtons(
        ScreenNoiseEffectController controller
    )
    {
        using (
            new EditorGUI.DisabledScope(
                !Application.isPlaying
            )
        )
        {
            using (
                new EditorGUILayout.HorizontalScope()
            )
            {
                Color originalColor =
                    GUI.backgroundColor;

                GUI.backgroundColor =
                    new Color(
                        0.4f,
                        1f,
                        0.55f
                    );

                if (GUILayout.Button(
                    "Play",
                    GUILayout.Height(32f)
                ))
                {
                    controller.Play();
                }

                GUI.backgroundColor =
                    new Color(
                        1f,
                        0.55f,
                        0.45f
                    );

                if (GUILayout.Button(
                    "Stop",
                    GUILayout.Height(32f)
                ))
                {
                    controller.Stop();
                }

                GUI.backgroundColor =
                    originalColor;
            }

            Color stopAllOriginalColor =
                GUI.backgroundColor;

            GUI.backgroundColor =
                new Color(
                    1f,
                    0.35f,
                    0.35f
                );

            if (GUILayout.Button(
                "すべてStop",
                GUILayout.Height(26f)
            ))
            {
                controller.StopAll();
            }

            GUI.backgroundColor =
                stopAllOriginalColor;
        }
    }

    private static void DrawPreviousNextButtons(
        ScreenNoiseEffectController controller
    )
    {
        using (
            new EditorGUI.DisabledScope(
                !Application.isPlaying
            )
        )
        {
            using (
                new EditorGUILayout.HorizontalScope()
            )
            {
                if (GUILayout.Button("前を再生"))
                {
                    controller.PlayPrevious();
                }

                if (GUILayout.Button("次を再生"))
                {
                    controller.PlayNext();
                }
            }
        }
    }

    private void DrawSetupButtons(
    ScreenNoiseEffectController controller
)
    {
        if (GUILayout.Button(
            "Effect参照を子階層から自動取得"
        ))
        {
            Undo.RecordObject(
                controller,
                "Auto Find Noise Effects"
            );

            controller.AutoFindEffects();

            EditorUtility.SetDirty(controller);

            serializedObject.Update();
        }
    }


    private static void DrawRuntimeStatus(
        ScreenNoiseEffectController controller
    )
    {
        EditorGUILayout.LabelField(
            "Runtime Status",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "選択中",
            controller.SelectedPresetName
        );

        EditorGUILayout.LabelField(
            "再生中",
            controller.PlayingPresetName
        );

        EditorGUILayout.LabelField(
            "状態",
            controller.IsPlaying
                ? "Playing"
                : "Stopped"
        );
    }

    private static void DrawSeparator()
    {
        Rect rect =
            EditorGUILayout.GetControlRect(
                false,
                1f
            );

        EditorGUI.DrawRect(
            rect,
            new Color(
                0.35f,
                0.35f,
                0.35f
            )
        );
    }
}

#endif