using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Minakuchi.Sky.Editor
{
    public static class SkyTestSetup
    {
        private const string Root = "Assets/Deverop/AT_Minakuchi";

        [MenuItem("水口/空/テストシーンを作成・更新")]
        public static void Install()
        {
            var scene = SceneManager.GetSceneByPath(Root + "/Scene/SkyTestScene.unity");
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(Root + "/Scene/SkyTestScene.unity", OpenSceneMode.Additive);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Sky/TransitionSky.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("空のシェーダーをコンパイルできませんでした。");
            string materialPath = Root + "/Sky/TransitionSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var root = new GameObject("空の制御");
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.AddComponent<SkyTransitionController>();
            Light sun = CreateLight(root.transform, "太陽のライト", 1.1f);
            Light moon = CreateLight(root.transform, "月のライト", 0);
            var settings = new SerializedObject(controller);
            settings.FindProperty("skyTemplate").objectReferenceValue = material;
            settings.FindProperty("sun").objectReferenceValue = sun;
            settings.FindProperty("moon").objectReferenceValue = moon;
            settings.ApplyModifiedPropertiesWithoutUndo();
            string prefabPath = Root + "/Prefab/SkyEnvironment.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<SkyTransitionController>(FindObjectsSortMode.None))
                if (existing.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.GetComponent<SkyTransitionController>().showDemoControls = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<SkyTransitionController>());
            // The test scene's original sun otherwise keeps lighting geometry at midnight.
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.gameObject.scene == scene && light.type == LightType.Directional
                    && !light.transform.IsChildOf(instance.transform))
                    light.enabled = false;
            InstallGates(scene, instance.GetComponent<SkyTransitionController>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("空の導入完了：プレハブとテストシーンを保存しました。");
        }

        private static void InstallGates(Scene scene, SkyTransitionController controller)
        {
            string path = Root + "/Prefab/SkyTransitionPoint.prefab";
            var go = new GameObject("空の切り替えポイント");
            var trigger = go.AddComponent<SkyTransitionTrigger>();
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(20, 20, 3);
            box.isTrigger = true;
            trigger.triggerOnce = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            foreach (var old in UnityEngine.Object.FindObjectsByType<SkyTransitionTrigger>(FindObjectsSortMode.None))
                if (old.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(old.gameObject);
            CreateGate(prefab, scene, controller, "01 雲の量0.1へ（8秒）", 25, false, 0.1f, 8);
            CreateGate(prefab, scene, controller, "02 大気圏ON（15秒・雲0.72）", 260, true, 0.72f, 15);
            CreateGate(prefab, scene, controller, "03 大気圏OFF（6秒・雲0.34）", 530, false, 0.34f, 6);
        }

        private static void CreateGate(GameObject prefab, Scene scene, SkyTransitionController sky, string name,
            float z, bool atmosphere, float clouds, float seconds)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            go.transform.position = new Vector3(0, 0, z);
            go.GetComponent<BoxCollider>().size = new Vector3(80, 20000, 3);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<BoxCollider>());
            var point = go.GetComponent<SkyTransitionTrigger>();
            point.sky = sky;
            point.atmosphereEnabled = atmosphere;
            point.destinationCloudCoverage = clouds;
            point.transitionSeconds = seconds;
            point.overrideTimeOfDay = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Player") point.targetActor = root.transform;
            PrefabUtility.RecordPrefabInstancePropertyModifications(point);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        private static Light CreateLight(Transform parent, string name, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        [MenuItem("水口/空/導入済みアセットを検証")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefab/SkyEnvironment.prefab");
            if (!prefab || prefab.GetComponent<SkyTransitionController>().showDemoControls)
                throw new InvalidOperationException("共通プレハブのテスト操作画面は無効にしてください。");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Sky/TransitionSky.shader");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Debug.Log("空のシェーダー：" + message.message);
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("空のシェーダーの検証に失敗しました。");
            var gate = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefab/SkyTransitionPoint.prefab");
            if (!gate || !gate.GetComponent<BoxCollider>().isTrigger)
                throw new InvalidOperationException("切り替えポイントのコライダーをトリガーにしてください。");
            Debug.Log("空の検証完了：異常は見つかりませんでした。");
        }
    }
}
