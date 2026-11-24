using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools/Jammer/リング妨害のテストシーンを作る：
// TestCourse.unityをコピーしてJammer/Scenes/JammerRingTest.unityを作り、プレイヤーのスタート位置から先のコース中心（メインのコース）に
// 加速リングを等間隔で並べる。プレイヤーの妨害弾は最初から「リングを減速リングに」を選んだ状態にする（Qで撃つと、前の一番近い加速リングを狙う）。
// 元のTestCourse.unityは変えない。テストシーンがすでにあるときは、作り直すか確認する（作り直すと、テストシーンで変えた内容は消える）。
public static class JammerRingTestSceneCreator
{
    private const string SourceScenePath = "Assets/Deverop/AT_Kawamata/TestCourse.unity";
    private const string RootFolder = "Assets/Deverop/AT_Kawamata/Jammer";
    private const string SceneFolder = RootFolder + "/Scenes";
    private const string ScenePath = SceneFolder + "/JammerRingTest.unity";
    private const string RingPrefabPath = "Assets/Deverop/AT_Minakuchi/Prefab/AccelerationRing.prefab";

    // 弾は90m/sで6秒（540m）飛ぶので、1つ先のリングには届く間隔にする。
    private const int RingCount = 8;
    private const float FirstRingDistance = 200f;
    private const float RingSpacing = 150f;

    [MenuItem("Tools/Jammer/リング妨害のテストシーンを作る")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        GameObject ringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPrefabPath);
        if (ringPrefab == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            Debug.LogError($"JammerRingTestSceneCreator: {SourceScenePath} か {RingPrefabPath} が見つかりません");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            if (!EditorUtility.DisplayDialog("リング妨害のテストシーン",
                    $"{ScenePath} はすでにあります。作り直しますか？\n（このシーンで変えた内容は消えます）", "作り直す", "やめる"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(ScenePath);
        }

        if (!AssetDatabase.IsValidFolder(SceneFolder))
        {
            AssetDatabase.CreateFolder(RootFolder, "Scenes");
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
        {
            Debug.LogError($"JammerRingTestSceneCreator: {SourceScenePath} を {ScenePath} にコピーできませんでした");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlayerManager player = Object.FindFirstObjectByType<PlayerManager>();
        CourseSpline course = FindMainCourse();
        if (player == null || course == null)
        {
            Debug.LogError("JammerRingTestSceneCreator: シーンにPlayerManagerか、RaceManagerのコースが見つかりません");
            return;
        }

        int ringCount = PlaceRings(scene, ringPrefab, course, player.transform.position);
        SelectRingJammer(player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"JammerRingTestSceneCreator: {ScenePath} を作りました（加速リング{ringCount}個。Playして、Qで妨害弾を撃つ）",
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
    }

    // RaceManagerが使うコース。分岐があるコースはメインのコースに並べる。
    private static CourseSpline FindMainCourse()
    {
        RaceManager raceManager = Object.FindFirstObjectByType<RaceManager>();
        if (raceManager == null)
        {
            return Object.FindFirstObjectByType<CourseSpline>();
        }

        SerializedObject serialized = new SerializedObject(raceManager);
        CourseNetwork network = serialized.FindProperty("_courseNetwork").objectReferenceValue as CourseNetwork;
        if (network != null && network.MainSpline != null)
        {
            return network.MainSpline;
        }

        return serialized.FindProperty("_courseSpline").objectReferenceValue as CourseSpline;
    }

    // スタート位置からFirstRingDistance先を1つ目に、RingSpacingおきに並べる。ループしないコースは終点の手前で止める。
    private static int PlaceRings(Scene scene, GameObject ringPrefab, CourseSpline course, Vector3 startPosition)
    {
        // 距離テーブルを最新にしておく（Editモードで開いた直後でも位置を取れるように）。
        course.TryGetBaseFrameAtPoint(0, out _, out _);

        GameObject root = new GameObject("JammerTestRings");
        SceneManager.MoveGameObjectToScene(root, scene);

        float startDistance = course.FindNearestDistance(startPosition);
        int placed = 0;
        for (int i = 0; i < RingCount; i++)
        {
            float distance = startDistance + FirstRingDistance + RingSpacing * i;
            if (!course.IsLoop && distance >= course.TotalLength)
            {
                break;
            }

            GameObject ring = (GameObject)PrefabUtility.InstantiatePrefab(ringPrefab, scene);
            ring.name = $"AccelerationRing_{i + 1:00}";
            ring.transform.SetParent(root.transform, false);
            ring.transform.SetPositionAndRotation(
                course.EvaluatePositionByDistance(distance),
                Quaternion.LookRotation(course.EvaluateForwardByDistance(distance), course.EvaluateUpByDistance(distance)));
            placed++;
        }

        return placed;
    }

    // プレイヤーの発射台で、リングを狙う種類（RingSwapJammerType）を選んでおく。
    private static void SelectRingJammer(PlayerManager player)
    {
        JammerLauncher launcher = player.GetComponentInChildren<JammerLauncher>(true);
        if (launcher == null)
        {
            Debug.LogWarning("JammerRingTestSceneCreator: プレイヤーにJammerLauncherがありません（Tools/Jammer/初期セットアップ を先に）", player);
            return;
        }

        for (int i = 0; i < launcher.Types.Count; i++)
        {
            if (launcher.Types[i] is RingSwapJammerType)
            {
                SerializedObject serialized = new SerializedObject(launcher);
                serialized.FindProperty("_currentTypeIndex").intValue = i;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        Debug.LogWarning("JammerRingTestSceneCreator: プレイヤーの発射台のTypesに「リングを減速リングに」がありません", launcher);
    }
}
