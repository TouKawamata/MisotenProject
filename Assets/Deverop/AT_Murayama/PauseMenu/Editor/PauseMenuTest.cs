using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AT_Murayama.PauseMenu.Editor
{
    public class PauseMenuTest
    {
        private const string LogOutputPath = "Assets/Deverop/AT_Murayama/PauseMenu/pausemenu_test_log.txt";

        [Test]
        public void Test_InitialStateAndUiBuild()
        {
            GameObject go = new GameObject("Test_PauseMenu_Init") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.EnsureUiBuilt();

                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(menu.IsCalibrating);

                Canvas canvas = go.GetComponentInChildren<Canvas>(true);
                Assert.IsNotNull(canvas, "PauseMenu Canvas が構築されていません");
                Assert.GreaterOrEqual(canvas.sortingOrder, 200, "sortingOrder が既存UI(110)より前面にありません");
                Assert.IsFalse(canvas.enabled, "構築直後は Canvas が非表示であるべきです");

                VerticalLayoutGroup vlg = go.GetComponentInChildren<VerticalLayoutGroup>(true);
                Assert.IsNotNull(vlg, "MainMenuButtons の VerticalLayoutGroup が見つかりません");
                Assert.IsTrue(vlg.childControlHeight, "VerticalLayoutGroup.childControlHeight が false のためボタン高さがはみ出す恐れがあります");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_UserRequest_OpenClose_And_ButtonVisibility()
        {
            GameObject go = new GameObject("Test_PauseMenu_User") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.Open(EPauseReason.UserRequest);

                Assert.IsTrue(menu.IsOpen);
                Assert.IsFalse(menu.IsCalibrating);
                Assert.AreEqual(EPauseReason.UserRequest, menu.CurrentReason);
                Assert.AreEqual(4, menu.VisibleButtonCount);
                Assert.AreEqual(PauseMenu.MenuButtonType.Resume, menu.SelectedButtonType);

                menu.Close();
                Assert.IsFalse(menu.IsOpen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_TrackingLost_CannotCloseWithCloseApi_AndHidesResume()
        {
            GameObject go = new GameObject("Test_PauseMenu_TrackingLost") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.Open(EPauseReason.TrackingLost);

                Assert.IsTrue(menu.IsOpen);
                Assert.AreEqual(EPauseReason.TrackingLost, menu.CurrentReason);
                Assert.AreEqual(3, menu.VisibleButtonCount);
                Assert.AreEqual(PauseMenu.MenuButtonType.Calibrate, menu.SelectedButtonType);

                // Close()（Esc・戻る相当）を呼んでも閉じないこと
                menu.Close();
                Assert.IsTrue(menu.IsOpen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_UpgradeUserPauseToTrackingLost_DoesNotDowngrade()
        {
            GameObject go = new GameObject("Test_PauseMenu_Upgrade") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.Open(EPauseReason.UserRequest);
                Assert.AreEqual(4, menu.VisibleButtonCount);

                // UserRequest で開いている最中に TrackingLost が来た場合は昇格する
                menu.Open(EPauseReason.TrackingLost);
                Assert.AreEqual(EPauseReason.TrackingLost, menu.CurrentReason);
                Assert.AreEqual(3, menu.VisibleButtonCount);

                // TrackingLost 中に UserRequest を呼んでも降格しない
                menu.Open(EPauseReason.UserRequest);
                Assert.AreEqual(EPauseReason.TrackingLost, menu.CurrentReason);
                Assert.AreEqual(3, menu.VisibleButtonCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_KeyboardSelectionWrapping()
        {
            GameObject go = new GameObject("Test_PauseMenu_Select") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.Open(EPauseReason.UserRequest);

                // 並び順：Resume -> Calibrate -> Restart -> ReturnToTitle
                Assert.AreEqual(PauseMenu.MenuButtonType.Resume, menu.SelectedButtonType);
                menu.MoveSelection(1);
                Assert.AreEqual(PauseMenu.MenuButtonType.Calibrate, menu.SelectedButtonType);
                menu.MoveSelection(1);
                Assert.AreEqual(PauseMenu.MenuButtonType.Restart, menu.SelectedButtonType);
                menu.MoveSelection(1);
                Assert.AreEqual(PauseMenu.MenuButtonType.ReturnToTitle, menu.SelectedButtonType);
                menu.MoveSelection(1);
                Assert.AreEqual(PauseMenu.MenuButtonType.Resume, menu.SelectedButtonType);
                menu.MoveSelection(-1);
                Assert.AreEqual(PauseMenu.MenuButtonType.ReturnToTitle, menu.SelectedButtonType);

                // TrackingLost 時は Resume をスキップして3項目循環
                menu.Open(EPauseReason.TrackingLost);
                Assert.AreEqual(PauseMenu.MenuButtonType.Calibrate, menu.SelectedButtonType);
                menu.MoveSelection(-1);
                Assert.AreEqual(PauseMenu.MenuButtonType.ReturnToTitle, menu.SelectedButtonType);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_CalibrationFlow_TransitionAndConfirm()
        {
            GameObject go = new GameObject("Test_PauseMenu_Calib") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.Open(EPauseReason.TrackingLost);

                menu.BeginCalibration();
                Assert.IsTrue(menu.IsOpen);
                Assert.IsTrue(menu.IsCalibrating);

                bool confirmed = menu.TryConfirmCalibration();
                Assert.IsTrue(confirmed);
                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(menu.IsCalibrating);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_SceneNamesAndBuildSettingsRegistered()
        {
            Assert.AreEqual("GameScene", SceneNames.Game);
            Assert.AreEqual("TitleScene", SceneNames.Title);

            bool hasGameScene = false;
            bool hasTitleScene = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                if (scene.path.EndsWith("/" + SceneNames.Game + ".unity", StringComparison.Ordinal))
                {
                    hasGameScene = true;
                }
                else if (scene.path.EndsWith("/" + SceneNames.Title + ".unity", StringComparison.Ordinal))
                {
                    hasTitleScene = true;
                }
            }

            Assert.IsTrue(hasGameScene, "EditorBuildSettings に GameScene が有効登録されていません");
            Assert.IsTrue(hasTitleScene, "EditorBuildSettings に TitleScene が有効登録されていません");
        }

        [Test]
        public void Test_EditorPreviewModes_CleanRestore()
        {
            GameObject go = new GameObject("Test_PauseMenu_Preview") { hideFlags = HideFlags.DontSave };
            try
            {
                PauseMenu menu = go.AddComponent<PauseMenu>();
                menu.PreviewStateInEditor(PauseMenu.EditorPreviewMode.UserPause);
                Assert.IsTrue(menu.IsOpen);
                Assert.IsFalse(menu.IsCalibrating);

                menu.PreviewStateInEditor(PauseMenu.EditorPreviewMode.TrackingLostPause);
                Assert.AreEqual(EPauseReason.TrackingLost, menu.CurrentReason);

                menu.PreviewStateInEditor(PauseMenu.EditorPreviewMode.Calibrating);
                Assert.IsTrue(menu.IsCalibrating);

                menu.PreviewStateInEditor(PauseMenu.EditorPreviewMode.Hidden);
                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(menu.IsCalibrating);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static bool RunAllTestsAndSaveLog()
        {
            PauseMenuTest tester = new PauseMenuTest();
            StringBuilder sb = new StringBuilder();
            int passed = 0;
            int failed = 0;

            sb.AppendLine("=== PauseMenu Test Results ===");

            void RunTest(string name, Action testAction)
            {
                try
                {
                    testAction();
                    sb.AppendLine($"[PASS] {name}");
                    passed++;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[FAIL] {name} -> {ex}");
                    failed++;
                }
            }

            RunTest("Test_InitialStateAndUiBuild", tester.Test_InitialStateAndUiBuild);
            RunTest("Test_UserRequest_OpenClose_And_ButtonVisibility", tester.Test_UserRequest_OpenClose_And_ButtonVisibility);
            RunTest("Test_TrackingLost_CannotCloseWithCloseApi_AndHidesResume", tester.Test_TrackingLost_CannotCloseWithCloseApi_AndHidesResume);
            RunTest("Test_UpgradeUserPauseToTrackingLost_DoesNotDowngrade", tester.Test_UpgradeUserPauseToTrackingLost_DoesNotDowngrade);
            RunTest("Test_KeyboardSelectionWrapping", tester.Test_KeyboardSelectionWrapping);
            RunTest("Test_CalibrationFlow_TransitionAndConfirm", tester.Test_CalibrationFlow_TransitionAndConfirm);
            RunTest("Test_SceneNamesAndBuildSettingsRegistered", tester.Test_SceneNamesAndBuildSettingsRegistered);
            RunTest("Test_EditorPreviewModes_CleanRestore", tester.Test_EditorPreviewModes_CleanRestore);

            sb.AppendLine("==============================");
            sb.AppendLine($"SUMMARY: {passed} Passed, {failed} Failed");

            string dir = Path.GetDirectoryName(LogOutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(LogOutputPath, sb.ToString(), Encoding.UTF8);

            if (failed > 0)
            {
                Debug.LogError(sb.ToString());
                return false;
            }

            Debug.Log(sb.ToString());
            return true;
        }
    }
}
