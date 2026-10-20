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

        EditorGUILayout.Space(8);

        if (controller.isActiveAndEnabled)
        {
            EditorGUILayout.HelpBox("【プレビュー反映中（有効）】スライダーの強さが画面にリアルタイム反映されています（※ゲーム実行中もスライダー操作で確認可能）。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("【プレビュー停止中（無効）】スクリプトのチェックがOFFのため、演出の強さは 0 にリセットされています（※通常のブースト動作を邪魔しません）。", MessageType.Warning);
        }

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
            controller.ResetGlobals();
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif

