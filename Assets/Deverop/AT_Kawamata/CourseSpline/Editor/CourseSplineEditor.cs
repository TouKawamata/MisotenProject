using System.Text;
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

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "制御点のTransformのRotationは使われません。傾きは制御点のBank Angleで指定します。\n" +
            "下のボタンで、Rotationが付いている制御点の傾きをBank Angleへ移し、Rotationを0に戻します（Rotationが0の点は変更しません）。\n" +
            "垂直に近い区間の回避策として付けたRotationは、変換した後でBank Angleを0に戻してください。",
            MessageType.Info);

        if (GUILayout.Button("RotationをBank Angleへ変換"))
        {
            foreach (Object targetObject in targets)
            {
                ConvertRotationsToBankAngles((CourseSpline)targetObject);
            }
        }
    }

    // 制御点のRotationの上方向を、その点での「バンクをかける前の上方向」から測った角度に直してBank Angleへ入れる。
    private static void ConvertRotationsToBankAngles(CourseSpline spline)
    {
        int count = spline.PointCount;
        Vector3[] forwards = new Vector3[count];
        Vector3[] baseUps = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            if (!spline.TryGetBaseFrameAtPoint(i, out forwards[i], out baseUps[i]))
            {
                Debug.LogWarning($"{spline.name}: 制御点{i}の向きを取得できなかったため、変換を中止しました", spline);
                return;
            }
        }

        Undo.SetCurrentGroupName("Convert Rotation To Bank Angle");
        int undoGroup = Undo.GetCurrentGroup();

        StringBuilder log = new StringBuilder($"{spline.name}: RotationをBank Angleへ変換しました\n");
        float previousBank = 0f;

        for (int i = 0; i < count; i++)
        {
            CourseSplinePoint point = spline.GetControlPoint(i);
            Transform pointTransform = point.transform;

            if (pointTransform.localRotation == Quaternion.identity)
            {
                previousBank = point.BankAngle;
                continue;
            }

            Vector3 projectedUp = Vector3.ProjectOnPlane(pointTransform.up, forwards[i]);
            float bank = projectedUp.sqrMagnitude > 0.000001f
                ? Vector3.SignedAngle(baseUps[i], projectedUp, forwards[i])
                : 0f;

            // 従来は点と点の間を近い方へ回していたので、前の点との差が±180°に収まるように合わせる。
            bank = previousBank + Mathf.DeltaAngle(previousBank, bank);
            bank = Mathf.Round(bank * 10f) / 10f;

            Undo.RecordObject(point, "Convert Rotation To Bank Angle");
            Undo.RecordObject(pointTransform, "Convert Rotation To Bank Angle");
            point.BankAngle = bank;
            pointTransform.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(point);

            log.AppendLine($"  {point.name}: {bank:F1}°");
            previousBank = bank;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log(log.ToString(), spline);
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
