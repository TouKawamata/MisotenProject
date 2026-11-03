using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseLine4Sides))]
public class CourseLine4SidesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild（メッシュ再生成）", GUILayout.Height(30)))
        {
            foreach (Object targetObject in targets)
            {
                CourseLine4Sides courseLine = (CourseLine4Sides)targetObject;
                Undo.RecordObject(courseLine, "Rebuild CourseLine4Sides");
                courseLine.Rebuild();
            }
        }
    }
}

