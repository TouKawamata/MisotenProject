using UnityEditor;
using UnityEngine;

// CourseLineTrueUV の Inspector に Rebuild ボタンを出す（CourseLineEditor と同じ作り）。
[CustomEditor(typeof(CourseLineTrueUV))]
public class CourseLineTrueUVEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild"))
        {
            foreach (Object targetObject in targets)
            {
                CourseLineTrueUV courseLine = (CourseLineTrueUV)targetObject;
                Undo.RecordObject(courseLine, "Rebuild CourseLineTrueUV");
                courseLine.Rebuild();
            }
        }
    }
}

