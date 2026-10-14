using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseLine))]
public class CourseLineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild"))
        {
            foreach (Object targetObject in targets)
            {
                CourseLine courseLine = (CourseLine)targetObject;
                Undo.RecordObject(courseLine, "Rebuild CourseLine");
                courseLine.Rebuild();
            }
        }
    }
}

