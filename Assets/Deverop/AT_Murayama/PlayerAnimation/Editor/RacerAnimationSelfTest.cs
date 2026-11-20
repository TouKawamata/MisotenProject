#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation.Editor
{
    /// <summary>
    /// PlayerAnimation モジュールの EditMode 自動単体テスト（全9項目）。
    /// [MenuItem] は一切使用せず、インスペクターUIのボタンから呼び出します。
    /// テスト中の一時アセット・一時GameObjectは finally で確実に破棄します。
    /// </summary>
    public static class RacerAnimationSelfTest
    {
        private const string TempTestFolder = "Assets/Deverop/AT_Murayama/PlayerAnimation/Generated/__SelfTestTemp__";

        public struct TestSummary
        {
            public int Passed;
            public int Failed;
            public string Report;
        }

        public static TestSummary RunAllTests()
        {
            int passed = 0;
            int failed = 0;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== PlayerAnimation 自動単体テスト（全9項目） ===");

            RunCase(sb, ref passed, ref failed, "1. 鏡像クォータニオン代数検証（対合性・準同型性・3軸反転方向）", TestMirrorQuaternionMath);
            RunCase(sb, ref passed, ref failed, "2. ボーン名左右スワップ＆born model.fbx 全ボーン対称性検証", TestBoneSymmetryOnBornModel);
            RunCase(sb, ref passed, ref failed, "3. Right Curve.fbx mainRig 欠落検出＆パス正規化検証", TestMainRigPathNormalization);
            RunCase(sb, ref passed, ref failed, "4. 鏡像 LeftCurve 生成＆born model 実サンプリング左右対称誤差検証", TestMirroredLeftCurveRealSamplingAccuracy);
            RunCase(sb, ref passed, ref failed, "5. AnimatorController 案A⇄案B(1D/2D) 切替再構築・GUID維持＆ゴミ0検証", TestAnimatorBuilderGuidAndCleanup);
            RunCase(sb, ref passed, ref failed, "6. ヒステリシス3値判定（チャタリング防止境界テスト）", TestHysteresisEvaluation);
            RunCase(sb, ref passed, ref failed, "7. Animator リグ直上判定（親Model誤アタッチ防止テスト）", TestAnimatorRigRootDetection);
            RunCase(sb, ref passed, ref failed, "8. コースカーブ角速度の符号＆スパイク抑制検証", TestCourseCurveAngularRate);
            RunCase(sb, ref passed, ref failed, "9. 4モード合成・二重加算防止・上限クランプ検証", TestCombineFlightMotionModes);

            // 念のため一時テストフォルダを完全削除
            if (AssetDatabase.IsValidFolder(TempTestFolder))
            {
                AssetDatabase.DeleteAsset(TempTestFolder);
                AssetDatabase.Refresh();
            }

            sb.AppendLine($"=== 結果: {passed} PASS / {failed} FAIL ===");
            string report = sb.ToString();
            if (failed == 0)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }

            return new TestSummary { Passed = passed, Failed = failed, Report = report };
        }

        private static void RunCase(StringBuilder sb, ref int passed, ref int failed, string title, Func<string> body)
        {
            try
            {
                string detail = body();
                passed++;
                sb.AppendLine($"[PASS] {title} ({detail})");
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"[FAIL] {title} -> {ex.Message}");
            }
        }

        private static string TestMirrorQuaternionMath()
        {
            Quaternion a = Quaternion.Euler(27f, 41f, -33f);
            Quaternion b = Quaternion.Euler(-15f, 62f, 18f);

            // 二重鏡像で元に戻る
            Quaternion doubleMirrored = RacerAnimationClipUtility.MirrorQuaternionX(
                RacerAnimationClipUtility.MirrorQuaternionX(a));
            float invErr = Quaternion.Angle(a, doubleMirrored);
            if (invErr > 0.001f)
            {
                throw new Exception($"二重鏡像誤差過大: {invErr:F4}deg");
            }

            // 準同型性: Mirror(A * B) == Mirror(A) * Mirror(B)
            Quaternion lhs = RacerAnimationClipUtility.MirrorQuaternionX(a * b);
            Quaternion rhs = RacerAnimationClipUtility.MirrorQuaternionX(a) * RacerAnimationClipUtility.MirrorQuaternionX(b);
            float homoErr = Quaternion.Angle(lhs, rhs);
            if (homoErr > 0.001f)
            {
                throw new Exception($"積の準同型誤差過大: {homoErr:F4}deg");
            }

            // X軸回転（Pitch）維持・Z軸回転（Roll）反転
            Quaternion pitch = Quaternion.AngleAxis(35f, Vector3.right);
            if (Quaternion.Angle(pitch, RacerAnimationClipUtility.MirrorQuaternionX(pitch)) > 0.001f)
            {
                throw new Exception("Pitch(X軸回り)が維持されていません");
            }

            Quaternion rollRight = Quaternion.AngleAxis(-30f, Vector3.forward);
            Quaternion rollLeft = Quaternion.AngleAxis(30f, Vector3.forward);
            if (Quaternion.Angle(rollLeft, RacerAnimationClipUtility.MirrorQuaternionX(rollRight)) > 0.001f)
            {
                throw new Exception("Roll(Z軸回り)が反転されていません");
            }

            return $"invErr={invErr:F5}deg, homoErr={homoErr:F5}deg";
        }

        private static string TestBoneSymmetryOnBornModel()
        {
            if (RacerAnimationClipUtility.SwapLeftRightBoneName("arm_L0_4_jnt") != "arm_R0_4_jnt")
            {
                throw new Exception("arm_L0_4_jnt のスワップ失敗");
            }

            if (RacerAnimationClipUtility.SwapLeftRightBoneName("finger_R3_2_jnt") != "finger_L3_2_jnt")
            {
                throw new Exception("finger_R3_2_jnt のスワップ失敗");
            }

            if (RacerAnimationClipUtility.SwapLeftRightBoneName("spine_C0_0_jnt") != "spine_C0_0_jnt")
            {
                throw new Exception("中央ボーンが誤変換されました");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RacerAnimationClipUtility.DefaultReferenceModelPath);
            if (model == null)
            {
                throw new Exception($"モデル未発見: {RacerAnimationClipUtility.DefaultReferenceModelPath}");
            }

            Transform[] all = model.GetComponentsInChildren<Transform>(includeInactive: true);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Length; i++)
            {
                names.Add(all[i].name);
            }

            int jntCount = 0;
            int unmatched = 0;
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (!n.EndsWith("_jnt", StringComparison.Ordinal))
                {
                    continue;
                }

                jntCount++;
                string swapped = RacerAnimationClipUtility.SwapLeftRightBoneName(n);
                if (!names.Contains(swapped))
                {
                    unmatched++;
                }
            }

            if (jntCount < 50 || unmatched > 0)
            {
                throw new Exception($"ボーン対称性異常: jntCount={jntCount}, unmatched={unmatched}");
            }

            return $"born model.fbx 全{jntCount}ボーン 100%対称一致";
        }

        private static string TestMainRigPathNormalization()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RacerAnimationClipUtility.DefaultReferenceModelPath);
            string rightPath = $"{RacerAnimationClipUtility.DefaultAnimationFolderPath}/Right Curve.fbx";
            AnimationClip rawRight = RacerAnimationClipUtility.LoadPrimaryAnimationClipFromAsset(rightPath);

            if (model == null || rawRight == null)
            {
                throw new Exception("born model.fbx または Right Curve.fbx が見つかりません");
            }

            bool needsPrefix = RacerAnimationClipUtility.DoesClipNeedMainRigPrefix(rawRight, model);
            AnimationClip normalized = RacerAnimationClipUtility.BuildPathNormalizedClipInMemory(
                rawRight, needsPrefix, loopTime: false, clipName: "__test_norm__");

            try
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(normalized);
                int validBoneBindings = 0;
                int missingBoneBindings = 0;

                for (int i = 0; i < bindings.Length; i++)
                {
                    if (bindings[i].type != typeof(Transform))
                    {
                        continue;
                    }

                    if (bindings[i].path.EndsWith("_jnt", StringComparison.Ordinal))
                    {
                        if (model.transform.Find(bindings[i].path) != null)
                        {
                            validBoneBindings++;
                        }
                        else
                        {
                            missingBoneBindings++;
                        }
                    }
                }

                if (validBoneBindings == 0 || missingBoneBindings > 0)
                {
                    throw new Exception($"補正後パス不一致: valid={validBoneBindings}, missing={missingBoneBindings}");
                }

                return $"needsMainRigPrefix={needsPrefix}, 実在ボーンカーブ={validBoneBindings}件 / 欠損0件";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalized);
            }
        }

        private static string TestMirroredLeftCurveRealSamplingAccuracy()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(RacerAnimationClipUtility.DefaultReferenceModelPath);
            string rightPath = $"{RacerAnimationClipUtility.DefaultAnimationFolderPath}/Right Curve.fbx";
            AnimationClip rawRight = RacerAnimationClipUtility.LoadPrimaryAnimationClipFromAsset(rightPath);

            if (model == null || rawRight == null)
            {
                throw new Exception("born model.fbx または Right Curve.fbx が見つかりません");
            }

            RacerAnimationClipUtility.EnsureAssetFolderExists(TempTestFolder);

            bool needsPrefix = RacerAnimationClipUtility.DoesClipNeedMainRigPrefix(rawRight, model);
            AnimationClip fixedRight = RacerAnimationClipUtility.SaveOrUpdateAnimationClipAsset(
                RacerAnimationClipUtility.BuildPathNormalizedClipInMemory(rawRight, needsPrefix, false, "TempRight"),
                $"{TempTestFolder}/TempRight.anim");

            AnimationClip mirroredLeft = RacerAnimationClipUtility.GenerateMirroredClipAsset(
                fixedRight, model, $"{TempTestFolder}/TempLeft.anim", false, 60f, out _);

            if (mirroredLeft == null)
            {
                throw new Exception("鏡像クリップ生成失敗");
            }

            GameObject goRight = null;
            GameObject goLeft = null;

            try
            {
                goRight = UnityEngine.Object.Instantiate(model, Vector3.zero, Quaternion.identity);
                goLeft = UnityEngine.Object.Instantiate(model, Vector3.zero, Quaternion.identity);
                goRight.hideFlags = HideFlags.HideAndDontSave;
                goLeft.hideFlags = HideFlags.HideAndDontSave;

                float[] testTimes = { 0f, fixedRight.length * 0.5f, fixedRight.length };
                string[] checkPairs =
                {
                    "arm_R0_4_jnt", "arm_L0_4_jnt",
                    "arm_R0_end_jnt", "arm_L0_end_jnt",
                    "leg_R0_4_jnt", "leg_L0_4_jnt",
                    "foot_R0_0_jnt", "foot_L0_0_jnt",
                    "spine_C0_4_jnt", "spine_C0_4_jnt"
                };

                Dictionary<string, Transform> mapR = BuildNameMap(goRight.transform);
                Dictionary<string, Transform> mapL = BuildNameMap(goLeft.transform);

                float maxPosErrorMeters = 0f;

                for (int ti = 0; ti < testTimes.Length; ti++)
                {
                    float t = testTimes[ti];
                    fixedRight.SampleAnimation(goRight, t);
                    mirroredLeft.SampleAnimation(goLeft, t);

                    for (int p = 0; p < checkPairs.Length; p += 2)
                    {
                        Transform trR = mapR[checkPairs[p]];
                        Transform trL = mapL[checkPairs[p + 1]];

                        Vector3 posR = goRight.transform.InverseTransformPoint(trR.position);
                        Vector3 expectedL = new Vector3(-posR.x, posR.y, posR.z);
                        Vector3 actualL = goLeft.transform.InverseTransformPoint(trL.position);

                        float err = Vector3.Distance(expectedL, actualL);
                        if (err > maxPosErrorMeters)
                        {
                            maxPosErrorMeters = err;
                        }
                    }
                }

                // 許容誤差 5mm 未満（実際の座標はほぼ 0.000m 台）
                if (maxPosErrorMeters > 0.005f)
                {
                    throw new Exception($"実サンプリング左右対称誤差が過大: {maxPosErrorMeters:F4}m");
                }

                return $"全チェック関節の最大鏡像座標誤差={maxPosErrorMeters * 1000f:F3}mm";
            }
            finally
            {
                if (goRight != null) UnityEngine.Object.DestroyImmediate(goRight);
                if (goLeft != null) UnityEngine.Object.DestroyImmediate(goLeft);
            }
        }

        private static string TestAnimatorBuilderGuidAndCleanup()
        {
            RacerAnimationClipUtility.EnsureAssetFolderExists(TempTestFolder);

            RacerAnimationProfile profile = ScriptableObject.CreateInstance<RacerAnimationProfile>();
            AnimationClip dummyClip = new AnimationClip { name = "DummyClip" };
            AnimationClip dummyUp = new AnimationClip { name = "DummyUp" };

            try
            {
                profile.GeneratedAssetFolder = TempTestFolder;
                profile.ControllerAssetName = "TestController";
                profile.SetUpClip = dummyClip;
                profile.TakeOffClip = dummyClip;
                profile.FlyClip = dummyClip;
                profile.RightCurveClip = dummyClip;
                profile.LeftCurveClip = dummyClip;

                // 1. 案A（DiscreteStates）生成
                profile.TurnMode = FlightTurnAnimationMode.DiscreteStates;
                AnimatorController ctrl1 = RacerAnimatorBuilder.BuildOrUpdateController(profile, out _);
                string path = AssetDatabase.GetAssetPath(ctrl1);
                string guid1 = AssetDatabase.AssetPathToGUID(path);

                // 2. 案B（1D BlendTree）へ切替再構築
                profile.TurnMode = FlightTurnAnimationMode.BlendTree;
                AnimatorController ctrl2 = RacerAnimatorBuilder.BuildOrUpdateController(profile, out _);
                string guid2 = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ctrl2));
                int blendTreeCount1D = CountSubAssetBlendTrees(path);

                // 3. MoveUp 追加して案B（2D BlendTree）へ再構築
                profile.MoveUpClip = dummyUp;
                RacerAnimatorBuilder.BuildOrUpdateController(profile, out _);
                int blendTreeCount2D = CountSubAssetBlendTrees(path);

                // 4. 案A へ戻して旧 BlendTree サブアセットが 0 個に掃除されるか確認
                profile.TurnMode = FlightTurnAnimationMode.DiscreteStates;
                AnimatorController ctrl4 = RacerAnimatorBuilder.BuildOrUpdateController(profile, out _);
                string guid4 = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ctrl4));
                int blendTreeCountAfterBackToA = CountSubAssetBlendTrees(path);

                if (guid1 != guid2 || guid1 != guid4)
                {
                    throw new Exception($"再構築でGUIDが変化しました: {guid1} -> {guid2} -> {guid4}");
                }

                if (blendTreeCount1D != 1 || blendTreeCount2D != 1 || blendTreeCountAfterBackToA != 0)
                {
                    throw new Exception($"BlendTreeサブアセット数異常: 1D={blendTreeCount1D}, 2D={blendTreeCount2D}, BackToA={blendTreeCountAfterBackToA}");
                }

                return $"GUID完全維持, BlendTree数(1D={blendTreeCount1D}, 2D={blendTreeCount2D}, 案A復帰後={blendTreeCountAfterBackToA})";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dummyClip);
                UnityEngine.Object.DestroyImmediate(dummyUp);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static string TestHysteresisEvaluation()
        {
            const float enter = 0.25f;
            const float exit = 0.12f;

            // 中央(0)からは 0.25 以上で右(+1)、0.20 では 0 のまま
            if (RacerAnimationController.EvaluateAxisWithHysteresis(0.20f, 0, enter, exit) != 0)
                throw new Exception("0→0.20 で誤遷移");
            if (RacerAnimationController.EvaluateAxisWithHysteresis(0.26f, 0, enter, exit) != 1)
                throw new Exception("0→0.26 で右旋回に入らない");

            // 右(+1)からは 0.15 でも右を維持（チャタリング防止）、0.10 で中央(0)に戻る
            if (RacerAnimationController.EvaluateAxisWithHysteresis(0.15f, 1, enter, exit) != 1)
                throw new Exception("+1→0.15 で早戻りした");
            if (RacerAnimationController.EvaluateAxisWithHysteresis(0.10f, 1, enter, exit) != 0)
                throw new Exception("+1→0.10 で中央に戻らない");

            // 右(+1)から一気に左(-0.30)へ振った場合は即 -1
            if (RacerAnimationController.EvaluateAxisWithHysteresis(-0.30f, 1, enter, exit) != -1)
                throw new Exception("+1→-0.30 の切り返し失敗");

            return "境界・保持・切り返し 全ケース正常";
        }

        private static string TestAnimatorRigRootDetection()
        {
            GameObject fakePlayer = new GameObject("__TestFakePlayer__");
            try
            {
                fakePlayer.hideFlags = HideFlags.HideAndDontSave;
                GameObject modelWrapper = new GameObject("Model");
                modelWrapper.transform.SetParent(fakePlayer.transform, false);
                Animator parentAnim = modelWrapper.AddComponent<Animator>();

                GameObject bornModel = new GameObject("born model");
                bornModel.transform.SetParent(modelWrapper.transform, false);
                Animator rigAnim = bornModel.AddComponent<Animator>();

                GameObject mainRig = new GameObject("mainRig");
                mainRig.transform.SetParent(bornModel.transform, false);

                if (RacerAnimationController.IsAnimatorOnRigRoot(parentAnim))
                {
                    throw new Exception("親 Model の Animator を誤って RigRoot と判定しました");
                }

                if (!RacerAnimationController.IsAnimatorOnRigRoot(rigAnim))
                {
                    throw new Exception("子 born model の Animator を RigRoot と判定できませんでした");
                }

                RacerAnimationController ctrl = fakePlayer.AddComponent<RacerAnimationController>();
                Transform found = ctrl.FindBestCharacterModelRoot();
                if (found != bornModel.transform)
                {
                    throw new Exception($"FindBestCharacterModelRoot 誤検出: {(found != null ? found.name : "null")}");
                }

                return "親Model誤アタッチ検出＆子born model自動特定 正常";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fakePlayer);
            }
        }

        private static string TestCourseCurveAngularRate()
        {
            Vector3 up = Vector3.up;
            Vector3 fwd = Vector3.forward;
            const float dt = 0.5f;

            Vector2 rightRate = RacerAnimationController.ComputeCourseCurveRateDegPerSec(
                fwd, Quaternion.Euler(0f, 10f, 0f) * fwd, up, dt);
            if (Mathf.Abs(rightRate.x - 20f) > 0.1f)
            {
                throw new Exception($"右カーブ角速度異常: {rightRate.x:F3}");
            }

            Vector2 leftRate = RacerAnimationController.ComputeCourseCurveRateDegPerSec(
                fwd, Quaternion.Euler(0f, -10f, 0f) * fwd, up, dt);
            if (Mathf.Abs(leftRate.x - (-20f)) > 0.1f)
            {
                throw new Exception($"左カーブ角速度異常: {leftRate.x:F3}");
            }

            Vector2 climbRate = RacerAnimationController.ComputeCourseCurveRateDegPerSec(
                fwd, Quaternion.Euler(-10f, 0f, 0f) * fwd, up, dt);
            if (Mathf.Abs(climbRate.y - 20f) > 0.1f)
            {
                throw new Exception($"上り坂角速度異常: {climbRate.y:F3}");
            }

            Vector2 diveRate = RacerAnimationController.ComputeCourseCurveRateDegPerSec(
                fwd, Quaternion.Euler(10f, 0f, 0f) * fwd, up, dt);
            if (Mathf.Abs(diveRate.y - (-20f)) > 0.1f)
            {
                throw new Exception($"下り坂角速度異常: {diveRate.y:F3}");
            }

            Vector2 spikeRate = RacerAnimationController.ComputeCourseCurveRateDegPerSec(
                fwd, Quaternion.Euler(0f, 90f, 0f) * fwd, up, dt);
            if (spikeRate != Vector2.zero)
            {
                throw new Exception($"ワープ時スパイクが除外されませんでした: {spikeRate}");
            }

            return $"Right=+{rightRate.x:F1}, Left={leftRate.x:F1}, Up=+{climbRate.y:F1}, Down={diveRate.y:F1} deg/s, Spike=0";
        }

        private static string TestCombineFlightMotionModes()
        {
            const float inputW = 1.0f;
            const float centerW = 0.65f;
            const float centerRef = 8.0f;
            const float centerMax = 0.70f;
            const float curveW = 0.70f;
            const float curveRef = 35.0f;
            const float curveMax = 0.85f;

            // 1. InputOnly
            Vector2 c1 = RacerAnimationController.CombineFlightMotion(
                FlightMotionSourceMode.InputOnly,
                new Vector2(0.4f, -0.2f),
                new Vector2(-8f, 8f),
                new Vector2(35f, -35f),
                inputW, centerW, centerRef, centerMax, curveW, curveRef, curveMax, true,
                out Vector2 cc1, out Vector2 kc1);
            if (cc1 != Vector2.zero || kc1 != Vector2.zero || Vector2.Distance(c1, new Vector2(0.4f, -0.2f)) > 0.001f)
            {
                throw new Exception($"InputOnly異常: out={c1}, cc={cc1}, kc={kc1}");
            }

            // 2. InputAndCenterReturn（ニュートラル時は -0.65、フル入力 +1.0 時は二重加算防止で cc=0）
            Vector2 c2 = RacerAnimationController.CombineFlightMotion(
                FlightMotionSourceMode.InputAndCenterReturn,
                Vector2.zero,
                new Vector2(-8f, 0f),
                new Vector2(35f, 0f),
                inputW, centerW, centerRef, centerMax, curveW, curveRef, curveMax, true,
                out Vector2 cc2, out Vector2 kc2);
            if (Mathf.Abs(c2.x - (-0.65f)) > 0.001f || kc2 != Vector2.zero)
            {
                throw new Exception($"InputAndCenterReturn(ニュートラル)異常: out={c2}, cc={cc2}, kc={kc2}");
            }

            Vector2 c2Full = RacerAnimationController.CombineFlightMotion(
                FlightMotionSourceMode.InputAndCenterReturn,
                new Vector2(1.0f, 0f),
                new Vector2(16f, 0f),
                Vector2.zero,
                inputW, centerW, centerRef, centerMax, curveW, curveRef, curveMax, true,
                out Vector2 cc2Full, out _);
            if (Mathf.Abs(cc2Full.x) > 0.0001f || Mathf.Abs(c2Full.x - 1.0f) > 0.001f)
            {
                throw new Exception($"二重加算防止失敗: fullOut={c2Full}, cc={cc2Full}");
            }

            // 3. InputAndCourseCurve
            Vector2 c3 = RacerAnimationController.CombineFlightMotion(
                FlightMotionSourceMode.InputAndCourseCurve,
                Vector2.zero,
                new Vector2(-8f, 0f),
                new Vector2(35f, 0f),
                inputW, centerW, centerRef, centerMax, curveW, curveRef, curveMax, true,
                out Vector2 cc3, out Vector2 kc3);
            if (Mathf.Abs(c3.x - 0.70f) > 0.001f || cc3 != Vector2.zero || Mathf.Abs(kc3.x - 0.70f) > 0.001f)
            {
                throw new Exception($"InputAndCourseCurve異常: out={c3}, cc={cc3}, kc={kc3}");
            }

            // 4. FullCombined + 上限クランプ検証 + 上下OFF検証
            Vector2 c4 = RacerAnimationController.CombineFlightMotion(
                FlightMotionSourceMode.FullCombined,
                Vector2.zero,
                new Vector2(100f, 100f),
                new Vector2(500f, 500f),
                inputW, centerW, centerRef, centerMax, curveW, curveRef, curveMax, false,
                out Vector2 cc4, out Vector2 kc4);
            if (Mathf.Abs(cc4.x - centerMax) > 0.001f || Mathf.Abs(kc4.x - curveMax) > 0.001f ||
                Mathf.Abs(c4.x - 1.0f) > 0.001f || Mathf.Abs(cc4.y) > 0.0001f || Mathf.Abs(kc4.y) > 0.0001f)
            {
                throw new Exception($"FullCombined/クランプ/縦OFF検証失敗: out={c4}, cc={cc4}, kc={kc4}");
            }

            return "全4モード・二重加算防止・上限クランプ・縦無効化 正常";
        }

        private static Dictionary<string, Transform> BuildNameMap(Transform root)
        {
            Dictionary<string, Transform> map = new Dictionary<string, Transform>(128, StringComparer.Ordinal);
            Transform[] all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!map.ContainsKey(all[i].name))
                {
                    map[all[i].name] = all[i];
                }
            }
            return map;
        }

        private static int CountSubAssetBlendTrees(string controllerAssetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(controllerAssetPath);
            int count = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is BlendTree)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
#endif
