#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoostEffectPreviewController))]
public class BoostEffectPreviewControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoostEffectPreviewController controller = (BoostEffectPreviewController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("【演出アニメーションのワンクリックテスト】", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Play (ブースト開始)", GUILayout.Height(30)))
        {
            controller.TestPlay();
        }
        if (GUILayout.Button("Stop (ブースト終了)", GUILayout.Height(30)))
        {
            controller.TestStop();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        if (GUILayout.Button("スライダーをゼロにリセット"))
        {
            controller.radialBlurStrength = 0f;
            controller.speedLineStrength = 0f;
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif

