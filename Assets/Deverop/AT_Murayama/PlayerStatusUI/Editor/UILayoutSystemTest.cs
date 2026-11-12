#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AT_Murayama.PlayerStatusUI;

namespace AT_Murayama.PlayerStatusUI.Editor
{
    public class UILayoutSystemTest
    {
        private static readonly string LogOutputPath = "Assets/Deverop/AT_Murayama/PlayerStatusUI/test_results_ui_layout.txt";

        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            string triggerPath = System.IO.Path.Combine(Application.dataPath, "../Temp/RunUILayoutTests.trigger");
            if (System.IO.File.Exists(triggerPath))
            {
                try { System.IO.File.Delete(triggerPath); } catch {}
                EditorApplication.delayCall += () => RunAllTestsAndSaveLog();
            }
        }

        [Test]
        public void Test_ScreenCornerEnumValues()
        {
            Assert.AreEqual(0, (int)PlayerPoseStatusUIController.ScreenCorner.BottomRight);
            Assert.AreEqual(1, (int)PlayerPoseStatusUIController.ScreenCorner.BottomLeft);
            Assert.AreEqual(2, (int)PlayerPoseStatusUIController.ScreenCorner.TopRight);
            Assert.AreEqual(3, (int)PlayerPoseStatusUIController.ScreenCorner.TopLeft);
            Assert.AreEqual(4, (int)PlayerPoseStatusUIController.ScreenCorner.BottomCenter);
            Assert.AreEqual(5, (int)PlayerPoseStatusUIController.ScreenCorner.TopCenter);
            Assert.AreEqual(6, (int)PlayerPoseStatusUIController.ScreenCorner.Center);
            Assert.AreEqual(7, (int)PlayerPoseStatusUIController.ScreenCorner.Custom);
        }

        [Test]
        public void Test_MinimapScreenAnchorEnumValues()
        {
            Assert.AreEqual(0, (int)CourseMinimapController.MinimapScreenAnchor.BottomLeft);
            Assert.AreEqual(1, (int)CourseMinimapController.MinimapScreenAnchor.BottomRight);
            Assert.AreEqual(2, (int)CourseMinimapController.MinimapScreenAnchor.TopLeft);
            Assert.AreEqual(3, (int)CourseMinimapController.MinimapScreenAnchor.TopRight);
            Assert.AreEqual(4, (int)CourseMinimapController.MinimapScreenAnchor.BottomCenter);
            Assert.AreEqual(5, (int)CourseMinimapController.MinimapScreenAnchor.TopCenter);
            Assert.AreEqual(6, (int)CourseMinimapController.MinimapScreenAnchor.Center);
            Assert.AreEqual(7, (int)CourseMinimapController.MinimapScreenAnchor.Custom);
        }

        [Test]
        public void Test_PlayerStatusUI_SetScreenCornerPreset()
        {
            GameObject go = new GameObject("Test_PlayerPoseStatusUI_Corner") { hideFlags = HideFlags.DontSave };
            try
            {
                PlayerPoseStatusUIController controller = go.AddComponent<PlayerPoseStatusUIController>();
                controller.UIMargin = new Vector2(28f, 28f);

                controller.SetScreenCornerPreset(PlayerPoseStatusUIController.ScreenCorner.BottomCenter, true);
                Assert.AreEqual(PlayerPoseStatusUIController.ScreenCorner.BottomCenter, controller.ScreenCornerPosition);
                Assert.AreEqual(0f, controller.UIMargin.x, 0.001f, "BottomCenter horizontal offset must reset to 0 for exact centering.");
                Assert.AreEqual(28f, controller.UIMargin.y, 0.001f, "BottomCenter vertical margin must be preserved.");

                controller.SetScreenCornerPreset(PlayerPoseStatusUIController.ScreenCorner.BottomRight, true);
                Assert.AreEqual(PlayerPoseStatusUIController.ScreenCorner.BottomRight, controller.ScreenCornerPosition);
                Assert.AreEqual(28f, controller.UIMargin.x, 0.001f, "BottomRight horizontal margin must restore to 28.");
                Assert.AreEqual(28f, controller.UIMargin.y, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_Minimap_SetScreenAnchor()
        {
            GameObject go = new GameObject("Test_CourseMinimap_Anchor") { hideFlags = HideFlags.DontSave };
            try
            {
                CourseMinimapController controller = go.AddComponent<CourseMinimapController>();
                controller.UIMargin = new Vector2(32f, 32f);

                controller.SetScreenAnchor(CourseMinimapController.MinimapScreenAnchor.BottomCenter, true);
                Assert.AreEqual(CourseMinimapController.MinimapScreenAnchor.BottomCenter, controller.GetScreenAnchor());
                Assert.AreEqual(0f, controller.UIMargin.x, 0.001f, "BottomCenter horizontal offset must reset to 0.");
                Assert.AreEqual(32f, controller.UIMargin.y, 0.001f, "BottomCenter vertical margin must be preserved.");

                controller.SetScreenAnchor(CourseMinimapController.MinimapScreenAnchor.BottomLeft, true);
                Assert.AreEqual(CourseMinimapController.MinimapScreenAnchor.BottomLeft, controller.GetScreenAnchor());
                Assert.AreEqual(32f, controller.UIMargin.x, 0.001f, "BottomLeft horizontal margin must restore to 32.");
                Assert.AreEqual(32f, controller.UIMargin.y, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_Minimap_ModeBMiniaturePresets()
        {
            GameObject go = new GameObject("Test_CourseMinimap_ModeB") { hideFlags = HideFlags.DontSave };
            try
            {
                CourseMinimapController controller = go.AddComponent<CourseMinimapController>();

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.BottomLeft);
                Assert.AreEqual(-0.42f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(-0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.BottomRight);
                Assert.AreEqual(0.42f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(-0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.BottomCenter);
                Assert.AreEqual(0.0f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(-0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.TopRight);
                Assert.AreEqual(0.42f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.TopLeft);
                Assert.AreEqual(-0.42f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.TopCenter);
                Assert.AreEqual(0.0f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(0.24f, controller.MiniatureScreenOffset.y, 0.01f);

                controller.ApplyModeBMiniaturePreset(CourseMinimapController.MinimapScreenAnchor.Center);
                Assert.AreEqual(0.0f, controller.MiniatureScreenOffset.x, 0.01f);
                Assert.AreEqual(0.0f, controller.MiniatureScreenOffset.y, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_PlayerStatusUI_ModeBProperties()
        {
            GameObject go = new GameObject("Test_PlayerPoseStatusUI_ModeB") { hideFlags = HideFlags.DontSave };
            try
            {
                PlayerPoseStatusUIController controller = go.AddComponent<PlayerPoseStatusUIController>();

                controller.ModeBForwardDistance = 1.5f;
                Assert.AreEqual(1.5f, controller.ModeBForwardDistance, 0.001f);
                controller.ModeBForwardDistance = 999f; // test clamping
                Assert.AreEqual(3.0f, controller.ModeBForwardDistance, 0.001f);

                controller.ModeBMiniatureScale = 0.12f;
                Assert.AreEqual(0.12f, controller.ModeBMiniatureScale, 0.001f);
                controller.ModeBMiniatureScale = -1f; // test clamping
                Assert.AreEqual(0.01f, controller.ModeBMiniatureScale, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_PlayerStatusUI_Apply03SceneDefaults_PreservesBottomCenter()
        {
            GameObject go = new GameObject("Test_PlayerPoseStatusUI_03Defaults") { hideFlags = HideFlags.DontSave };
            try
            {
                PlayerPoseStatusUIController controller = go.AddComponent<PlayerPoseStatusUIController>();
                controller.ScreenCornerPosition = PlayerPoseStatusUIController.ScreenCorner.BottomCenter;
                controller.Apply03SceneDefaults();

                Assert.AreEqual(PlayerPoseStatusUIController.ScreenCorner.BottomCenter, controller.ScreenCornerPosition);
                Assert.AreEqual(0f, controller.UIMargin.x, 0.001f);
                Assert.AreEqual(28f, controller.UIMargin.y, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_PlayerPoseStatusUI_StateColorOverride()
        {
            GameObject go = new GameObject("Test_PlayerPoseStatusUI_ColorOverride") { hideFlags = HideFlags.DontSave };
            try
            {
                PlayerPoseStatusUIController controller = go.AddComponent<PlayerPoseStatusUIController>();

                // Default check
                Assert.IsTrue(controller.EnableStateColorOverride, "Default EnableStateColorOverride should be true.");

                // SetStateColorOverride & property toggle
                controller.SetStateColorOverride(false);
                Assert.IsFalse(controller.EnableStateColorOverride);

                controller.EnableStateColorOverride = true;
                Assert.IsTrue(controller.EnableStateColorOverride);

                // Original Material Tint settings
                controller.TintOriginalMaterialOnState = true;
                Assert.IsTrue(controller.TintOriginalMaterialOnState);

                controller.OriginalMaterialTintIntensity = 0.5f;
                Assert.AreEqual(0.5f, controller.OriginalMaterialTintIntensity, 0.001f);

                controller.OriginalMaterialTintIntensity = 1.5f; // Clamping test
                Assert.AreEqual(1.0f, controller.OriginalMaterialTintIntensity, 0.001f);

                controller.OriginalMaterialTintIntensity = -0.5f; // Clamping test
                Assert.AreEqual(0.0f, controller.OriginalMaterialTintIntensity, 0.001f);

                // Sync Hologram Colors
                controller.SyncHologramColorsWithState = false;
                Assert.IsFalse(controller.SyncHologramColorsWithState);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_Minimap_WholeCourseRotationControls()
        {
            GameObject go = new GameObject("Test_CourseMinimap_WholeCourseRot") { hideFlags = HideFlags.DontSave };
            try
            {
                CourseMinimapController controller = go.AddComponent<CourseMinimapController>();

                // Set initial rotation
                controller.SetWholeCourseMapRotation(0f, true);
                Assert.AreEqual(0f, controller.WholeCourseMapRotation, 0.01f);

                // Set arbitrary rotation
                controller.SetWholeCourseMapRotation(90f, true);
                Assert.AreEqual(90f, controller.WholeCourseMapRotation, 0.01f);

                // Delta rotation
                controller.RotateWholeCourseMap(45f, true);
                Assert.AreEqual(135f, controller.WholeCourseMapRotation, 0.01f);

                // Wrap-around positive (> 360)
                controller.SetWholeCourseMapRotation(380f, true);
                Assert.AreEqual(20f, controller.WholeCourseMapRotation, 0.01f);

                // Wrap-around negative (< 0)
                controller.SetWholeCourseMapRotation(-30f, true);
                Assert.AreEqual(330f, controller.WholeCourseMapRotation, 0.01f);

                // Reset
                controller.ResetWholeCourseMapRotation(true);
                Assert.AreEqual(0f, controller.WholeCourseMapRotation, 0.01f);

                // Rotate in WholeCourse toggle
                controller.RotateInWholeCourse = true;
                Assert.IsTrue(controller.RotateInWholeCourse);
                controller.RotateInWholeCourse = false;
                Assert.IsFalse(controller.RotateInWholeCourse);

                // Pitch angle clamping (defined range: 10 to 85 deg)
                controller.PitchAngle = 45f;
                Assert.AreEqual(45f, controller.PitchAngle, 0.01f);
                controller.PitchAngle = 5f; // Clamped to min 10
                Assert.AreEqual(10f, controller.PitchAngle, 0.01f);
                controller.PitchAngle = 100f; // Clamped to max 85
                Assert.AreEqual(85f, controller.PitchAngle, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_SmartSignage_SweepPhaseOffsetAndStaggering()
        {
            GameObject go = new GameObject("Test_SmartSignage_Phase") { hideFlags = HideFlags.DontSave };
            try
            {
                SmartSignageController controller = go.AddComponent<SmartSignageController>();

                // Auto stagger default
                Assert.IsTrue(controller.AutoStaggerPhaseByPosition, "Default AutoStaggerPhaseByPosition should be true.");

                // Set and get SweepPhaseOffset
                controller.SweepPhaseOffset = 0.45f;
                Assert.AreEqual(0.45f, controller.SweepPhaseOffset, 0.001f);

                // Method setter
                controller.SetSweepPhaseOffset(0.75f);
                Assert.AreEqual(0.75f, controller.SweepPhaseOffset, 0.001f);

                // Clamping
                controller.SweepPhaseOffset = 1.8f;
                Assert.AreEqual(1.0f, controller.SweepPhaseOffset, 0.001f);
                controller.SweepPhaseOffset = -0.5f;
                Assert.AreEqual(0.0f, controller.SweepPhaseOffset, 0.001f);

                // Randomize
                controller.RandomizeSweepPhase();
                Assert.IsTrue(controller.SweepPhaseOffset >= 0.0f && controller.SweepPhaseOffset <= 1.0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_SmartArrowSignage_SweepPhaseOffsetAndStaggering()
        {
            GameObject go = new GameObject("Test_SmartArrowSignage_Phase") { hideFlags = HideFlags.DontSave };
            try
            {
                SmartArrowSignageController controller = go.AddComponent<SmartArrowSignageController>();

                // Auto stagger default
                Assert.IsTrue(controller.AutoStaggerPhaseByPosition, "Default AutoStaggerPhaseByPosition should be true.");

                // Set and get SweepPhaseOffset
                controller.SweepPhaseOffset = 0.35f;
                Assert.AreEqual(0.35f, controller.SweepPhaseOffset, 0.001f);

                // Method setter
                controller.SetSweepPhaseOffset(0.65f);
                Assert.AreEqual(0.65f, controller.SweepPhaseOffset, 0.001f);

                // Clamping
                controller.SweepPhaseOffset = 2.0f;
                Assert.AreEqual(1.0f, controller.SweepPhaseOffset, 0.001f);
                controller.SweepPhaseOffset = -1.0f;
                Assert.AreEqual(0.0f, controller.SweepPhaseOffset, 0.001f);

                // Randomize
                controller.RandomizeSweepPhase();
                Assert.IsTrue(controller.SweepPhaseOffset >= 0.0f && controller.SweepPhaseOffset <= 1.0f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_GhostRim_StaticNoPulseMaterial()
        {
            // 1. UI専用 静止版マテリアルのアセット存在・パラメータ確認
            string uiStaticPath = PlayerPoseStatusUIController.GetGhostRimMaterialAssetPath(true);
            Material uiStaticMat = AssetDatabase.LoadAssetAtPath<Material>(uiStaticPath);
            Assert.IsNotNull(uiStaticMat, $"UI Static GhostRim Material should exist at {uiStaticPath}");
            Assert.AreEqual("Custom/Hologram/ModelHologram_GhostRim", uiStaticMat.shader.name);
            Assert.AreEqual(0f, uiStaticMat.GetFloat("_BreatheAmount"), 0.001f, "UI Static GhostRim must have BreatheAmount = 0");
            Assert.AreEqual(0f, uiStaticMat.GetFloat("_EnableBreathePulse"), 0.001f, "UI Static GhostRim must have EnableBreathePulse = 0");

            // 2. 3Dモデル用 静止版マテリアルのアセット存在・パラメータ確認
            string modelStaticPath = PlayerPoseStatusUIController.GetModelGhostRimMaterialAssetPath(true);
            Material modelStaticMat = AssetDatabase.LoadAssetAtPath<Material>(modelStaticPath);
            Assert.IsNotNull(modelStaticMat, $"Model Static GhostRim Material should exist at {modelStaticPath}");
            Assert.AreEqual("Custom/Hologram/ModelHologram_GhostRim", modelStaticMat.shader.name);
            Assert.AreEqual(0f, modelStaticMat.GetFloat("_BreatheAmount"), 0.001f, "Model Static GhostRim must have BreatheAmount = 0");
            Assert.AreEqual(0f, modelStaticMat.GetFloat("_EnableBreathePulse"), 0.001f, "Model Static GhostRim must have EnableBreathePulse = 0");

            // 3. コントローラー側 パルスON/OFF制御APIの検証
            GameObject go = new GameObject("Test_PlayerPoseStatusUI_GhostRim") { hideFlags = HideFlags.DontSave };
            try
            {
                PlayerPoseStatusUIController controller = go.AddComponent<PlayerPoseStatusUIController>();
                Material testRuntimeMat = new Material(uiStaticMat.shader);

                // パルス無効化 (Static)
                controller.DisableGhostRimPulse = true;
                Assert.IsTrue(controller.DisableGhostRimPulse);
                controller.ApplyGhostRimPulseSetting(testRuntimeMat);
                Assert.AreEqual(0f, testRuntimeMat.GetFloat("_BreatheAmount"), 0.001f);
                Assert.AreEqual(0f, testRuntimeMat.GetFloat("_EnableBreathePulse"), 0.001f);

                // パルス再有効化 (Pulse)
                controller.DisableGhostRimPulse = false;
                Assert.IsFalse(controller.DisableGhostRimPulse);
                controller.ApplyGhostRimPulseSetting(testRuntimeMat);
                Assert.AreEqual(0.28f, testRuntimeMat.GetFloat("_BreatheAmount"), 0.001f);
                Assert.AreEqual(1.0f, testRuntimeMat.GetFloat("_EnableBreathePulse"), 0.001f);

                // カスタムマテリアルとしての割り当て連動テスト（Staticマテリアル割り当て時にDisableGhostRimPulseが自動でtrueになるか）
                string uiPulsePath = PlayerPoseStatusUIController.GetGhostRimMaterialAssetPath(false);
                Material uiPulseMat = AssetDatabase.LoadAssetAtPath<Material>(uiPulsePath);
                Assert.IsNotNull(uiPulseMat, $"UI Pulse GhostRim Material should exist at {uiPulsePath}");

                controller.AssignMaterialAsset(uiStaticMat);
                Assert.AreEqual(PlayerPoseStatusUIController.MaterialDisplayMode.CustomMaterial, controller.MaterialMode);
                Assert.AreEqual(uiStaticMat, controller.CustomMaterial);
                Assert.AreEqual(PlayerPoseStatusUIController.UIHologramPattern.GhostRim, controller.HologramPattern);
                Assert.IsTrue(controller.DisableGhostRimPulse, "Assigning Static GhostRim material should set DisableGhostRimPulse = true");

                // Pulseマテリアル割り当て時にDisableGhostRimPulseが自動でfalseになるか
                controller.AssignMaterialAsset(uiPulseMat);
                Assert.IsFalse(controller.DisableGhostRimPulse, "Assigning Pulse GhostRim material should set DisableGhostRimPulse = false");

                // AdoptCurrentHologram(true) で既存のCustomMaterialがパルス版に破壊されないか
                controller.AssignMaterialAsset(uiStaticMat);
                controller.AdoptCurrentHologram(true);
                Assert.AreEqual(uiStaticMat, controller.CustomMaterial, "AdoptCurrentHologram(true) must preserve the assigned static custom material");

                // AdoptCurrentHologram(false) でDisableGhostRimPulse==trueのとき静止版が採用されるか
                controller.ApplyHologramPattern(PlayerPoseStatusUIController.UIHologramPattern.GhostRim);
                controller.DisableGhostRimPulse = true;
                controller.AdoptCurrentHologram(false);
                Assert.AreEqual(uiStaticMat, controller.CustomMaterial, "AdoptCurrentHologram(false) with DisableGhostRimPulse=true must adopt static material");

                UnityEngine.Object.DestroyImmediate(testRuntimeMat);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// バッチ実行および結果ログ出力用ランナー
        /// </summary>
        public static bool RunAllTestsAndSaveLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine($"[UILayoutSystemTest] 実行日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("==================================================");

            int pass = 0;
            int fail = 0;

            void RunTest(string testName, Action testAction)
            {
                try
                {
                    testAction();
                    sb.AppendLine($"[PASS] {testName}");
                    pass++;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[FAIL] {testName}: {ex.Message}");
                    fail++;
                }
            }

            var tester = new UILayoutSystemTest();
            RunTest("Test_ScreenCornerEnumValues", tester.Test_ScreenCornerEnumValues);
            RunTest("Test_MinimapScreenAnchorEnumValues", tester.Test_MinimapScreenAnchorEnumValues);
            RunTest("Test_PlayerStatusUI_SetScreenCornerPreset", tester.Test_PlayerStatusUI_SetScreenCornerPreset);
            RunTest("Test_Minimap_SetScreenAnchor", tester.Test_Minimap_SetScreenAnchor);
            RunTest("Test_Minimap_ModeBMiniaturePresets", tester.Test_Minimap_ModeBMiniaturePresets);
            RunTest("Test_PlayerStatusUI_ModeBProperties", tester.Test_PlayerStatusUI_ModeBProperties);
            RunTest("Test_PlayerStatusUI_Apply03SceneDefaults_PreservesBottomCenter", tester.Test_PlayerStatusUI_Apply03SceneDefaults_PreservesBottomCenter);
            RunTest("Test_PlayerPoseStatusUI_StateColorOverride", tester.Test_PlayerPoseStatusUI_StateColorOverride);
            RunTest("Test_Minimap_WholeCourseRotationControls", tester.Test_Minimap_WholeCourseRotationControls);
            RunTest("Test_SmartSignage_SweepPhaseOffsetAndStaggering", tester.Test_SmartSignage_SweepPhaseOffsetAndStaggering);
            RunTest("Test_SmartArrowSignage_SweepPhaseOffsetAndStaggering", tester.Test_SmartArrowSignage_SweepPhaseOffsetAndStaggering);
            RunTest("Test_GhostRim_StaticNoPulseMaterial", tester.Test_GhostRim_StaticNoPulseMaterial);

            sb.AppendLine("==================================================");
            sb.AppendLine($"合計: {pass + fail} 件 ｜ 成功: {pass} 件 ｜ 失敗: {fail} 件");
            sb.AppendLine("==================================================");

            string fullLog = sb.ToString();
            Debug.Log(fullLog);

            try
            {
                File.WriteAllText(LogOutputPath, fullLog, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UILayoutSystemTest] ログ出力失敗: {ex.Message}");
            }

            return fail == 0;
        }
    }
}
#endif
