using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CourseSpline))]
public class CourseSplineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Add Point"))
        {
            foreach (Object targetObject in targets)
            {
                AddPoint((CourseSpline)targetObject);
            }
        }
    }

    private static void AddPoint(CourseSpline spline)
    {
        SerializedObject serializedSpline = new SerializedObject(spline);
        SerializedProperty containerProperty = serializedSpline.FindProperty("pointsContainer");
        Transform container = containerProperty.objectReferenceValue as Transform;
        if (container == null)
        {
            container = spline.transform;
        }

        CourseSplinePoint[] existingPoints = container.GetComponentsInChildren<CourseSplinePoint>();

        Vector3 spawnPosition;
        Vector3 direction = Vector3.forward;

        if (existingPoints.Length >= 2)
        {
            Vector3 last = existingPoints[existingPoints.Length - 1].transform.position;
            Vector3 secondLast = existingPoints[existingPoints.Length - 2].transform.position;
            Vector3 delta = last - secondLast;
            if (delta.sqrMagnitude > 0.0001f)
            {
                direction = delta.normalized;
            }

            spawnPosition = last + direction * spline.DefaultAddPointSpacing;
        }
        else if (existingPoints.Length == 1)
        {
            spawnPosition = existingPoints[0].transform.position + direction * spline.DefaultAddPointSpacing;
        }
        else
        {
            spawnPosition = container.position;
        }

        GameObject newPoint = new GameObject($"Point_{existingPoints.Length:000}");
        Undo.RegisterCreatedObjectUndo(newPoint, "Add Course Spline Point");
        Undo.SetTransformParent(newPoint.transform, container, "Add Course Spline Point");
        newPoint.transform.position = spawnPosition;
        Undo.AddComponent<CourseSplinePoint>(newPoint);

        Selection.activeGameObject = newPoint;
        EditorUtility.SetDirty(container.gameObject);
    }
}
