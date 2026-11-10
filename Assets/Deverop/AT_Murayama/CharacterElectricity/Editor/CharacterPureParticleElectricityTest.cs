#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AT_Murayama.CharacterElectricity.Editor
{
    /// <summary>
    /// CharacterPureParticleElectricity の動作検証・リグレッション防止用自動テストスクリプト
    /// （Toolsメニューは追加せず、batchmode またはインスペクターから呼び出し可能）
    /// 
    /// 【安全対策】
    /// 1. 全テストオブジェクトに HideFlags.DontSave を付与し、シーン汚染を完全防止。
    /// 2. 全テストの破棄処理を try ... finally で厳密に保護し、例外発生時も残存させない。
    /// 3. テスト開始時・終了時に CleanupOrphanedTestObjects() を実行し、ヒエラルキーのクリーン状態を維持。
    /// 4. 元モデルのマテリアルが不変であることを厳格に検証。
    /// </summary>
    public static class CharacterPureParticleElectricityTest
    {
        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            string triggerPath = Path.Combine(Application.dataPath, "../Temp/RunPureElectricityTests.trigger");
            if (File.Exists(triggerPath))
            {
                try { File.Delete(triggerPath); } catch {}
                EditorApplication.delayCall += () => RunTests();
            }
        }

        public static void CleanupOrphanedTestObjects()
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    GameObject[] roots = activeScene.GetRootGameObjects();
                    for (int i = roots.Length - 1; i >= 0; i--)
                    {
                        GameObject go = roots[i];
                        if (go == null) continue;
                        string n = go.name;
                        if (n.StartsWith("TestPureChara") || n.Contains("TestPureChara_"))
                        {
                            GameObject.DestroyImmediate(go);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterPureParticleElectricityTest] クリーンアップ警告: {ex.Message}");
            }
        }

        public static void RunTests()
        {
            CleanupOrphanedTestObjects();

            var logSb = new StringBuilder();
            Action<string> logPass = msg => { Debug.Log(msg); logSb.AppendLine(msg); };
            Action<string> logErr = msg => { Debug.LogError(msg); logSb.AppendLine(msg); };

            logPass("==================================================");
            logPass("[CharacterPureParticleElectricityTest] テスト開始");
            logPass("==================================================");

            int passCount = 0;
            int failCount = 0;

            // テスト1: アセットおよび純粋パーティクル用マテリアルのパス解決テスト
            try
            {
                GameObject bornModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                if (bornModel == null) throw new Exception("Assets/TeamResources/Art/Models/born model.fbx のロードに失敗しました。");

                Material shiroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
                Material aoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/ao.mat");
                Material kuroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/kuro.mat");
                if (shiroMat == null || aoMat == null || kuroMat == null)
                    throw new Exception("カラーバリエーションマテリアル (shiro/ao/kuro) のロードに失敗しました。");

                string matDir = "Assets/Deverop/AT_Murayama/CharacterElectricity/Materials";
                string[] pureMats = new[]
                {
                    "M_Electricity_Pure_Trail.mat",
                    "M_Electricity_Pure_Needle.mat",
                    "M_Electricity_Pure_Plasma.mat",
                    "M_Electricity_Pure_Fork.mat",
                    "M_Electricity_Pure_Crackle.mat"
                };

                for (int i = 0; i < pureMats.Length; i++)
                {
                    Material m = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/{pureMats[i]}");
                    if (m == null) throw new Exception($"純粋パーティクル用マテリアル {pureMats[i]} のロードに失敗しました。");
                }

                logPass("✅ [PASS] テスト1: アセットおよび純粋パーティクル用マテリアルのロード確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト1: {ex.Message}");
                failCount++;
            }

            // テスト2: SkinnedMeshRenderer 自動バインドと全6エミッター階層生成
            GameObject testChara2 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara2 = GameObject.Instantiate(prefab);
                testChara2.hideFlags = HideFlags.DontSave;
                testChara2.name = "TestPureChara_Setup";

                CharacterPureParticleElectricity controller = testChara2.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                if (controller.RibbonEmitter == null || controller.NeedleEmitter == null ||
                    controller.AuraEmitter == null || controller.ForkEmitter == null ||
                    controller.CrackleEmitter == null || controller.BurstEmitter == null)
                {
                    throw new Exception("全6エミッターの一部が未生成またはバインドされていません。");
                }

                var smr = testChara2.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null) throw new Exception("born model の SkinnedMeshRenderer が検出できませんでした。");

                if (controller.RibbonEmitter.shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer ||
                    controller.RibbonEmitter.shape.skinnedMeshRenderer != smr)
                {
                    throw new Exception("RibbonEmitter の Shape が SkinnedMeshRenderer に正常にバインドされていません。");
                }

                logPass("✅ [PASS] テスト2: SkinnedMeshRenderer自動バインドと全6エミッター階層生成確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト2: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara2 != null) GameObject.DestroyImmediate(testChara2);
            }

            // テスト3: 全8パターンの切り替えと放出レート整合性検証
            GameObject testChara3 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara3 = GameObject.Instantiate(prefab);
                testChara3.hideFlags = HideFlags.DontSave;
                testChara3.name = "TestPureChara_8Patterns";

                CharacterPureParticleElectricity controller = testChara3.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();
                controller.SetStun(true, immediate: true);

                var patterns = (PureParticlePattern[])Enum.GetValues(typeof(PureParticlePattern));
                foreach (var pat in patterns)
                {
                    controller.CurrentPattern = pat;
                    controller.ApplyPatternImmediate();

                    if (pat == PureParticlePattern.Off)
                    {
                        float rate = controller.RibbonEmitter.emission.rateOverTime.constant;
                        if (rate > 0.001f)
                            throw new Exception($"Off パターン時に放出レートが 0 になっていません: {rate}");
                    }
                    else
                    {
                        // 各パターンに応じたエミッターのいずれかが活性化していること
                        float totalRate = controller.RibbonEmitter.emission.rateOverTime.constant +
                                          controller.NeedleEmitter.emission.rateOverTime.constant +
                                          controller.AuraEmitter.emission.rateOverTime.constant +
                                          controller.ForkEmitter.emission.rateOverTime.constant +
                                          controller.CrackleEmitter.emission.rateOverTime.constant;
                        if (totalRate <= 0.001f)
                            throw new Exception($"パターン {pat} でパーティクル放出レートが 0 です。");
                    }
                }

                logPass("✅ [PASS] テスト3: 全8パターンの切り替えと放出レート整合性確認（Off含む全9状態）");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト3: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara3 != null) GameObject.DestroyImmediate(testChara3);
            }

            // テスト4: リボンエミッター Trails モジュールの設定確認
            GameObject testChara4 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara4 = GameObject.Instantiate(prefab);
                testChara4.hideFlags = HideFlags.DontSave;
                testChara4.name = "TestPureChara_Trails";

                CharacterPureParticleElectricity controller = testChara4.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                var trails = controller.RibbonEmitter.trails;
                if (!trails.enabled)
                    throw new Exception("RibbonEmitter の trails モジュールが有効化されていません。");

                ParticleSystemRenderer psr = controller.RibbonEmitter.GetComponent<ParticleSystemRenderer>();
                if (psr.trailMaterial == null)
                    throw new Exception("RibbonEmitter の trailMaterial が設定されていません。");

                logPass("✅ [PASS] テスト4: リボンエミッター Trails（3D稲妻軌跡）モジュール設定確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト4: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara4 != null) GameObject.DestroyImmediate(testChara4);
            }

            // テスト5: スタン状態連動APIとスムーズな放出停止・クリーンアップ検証
            GameObject testChara5 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara5 = GameObject.Instantiate(prefab);
                testChara5.hideFlags = HideFlags.DontSave;
                testChara5.name = "TestPureChara_Stun";

                CharacterPureParticleElectricity controller = testChara5.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();
                controller.CurrentPattern = PureParticlePattern.Pattern1_ArcRibbons;

                // 1. SetStun(true, immediate: true)
                controller.SetStun(true, immediate: true);
                if (!controller.IsStunned || controller.CurrentStunWeight < 0.99f)
                    throw new Exception("SetStun(true, immediate: true) の反映に失敗しました。");

                // 2. SetStun(false, immediate: true)
                controller.SetStun(false, immediate: true);
                if (controller.IsStunned || controller.CurrentStunWeight > 0.01f)
                    throw new Exception("SetStun(false, immediate: true) の反映に失敗しました。");

                float rate = controller.RibbonEmitter.emission.rateOverTime.constant;
                if (rate > 0.001f)
                    throw new Exception($"スタンOFF時の放出停止が機能していません: rate = {rate}");

                // 3. SetStunNormalizedTime(0.5f)
                controller.SetStunNormalizedTime(0.5f);
                if (Mathf.Abs(controller.CurrentStunWeight - 0.5f) > 0.01f)
                    throw new Exception($"SetStunNormalizedTime の反映に失敗: {controller.CurrentStunWeight}");

                logPass("✅ [PASS] テスト5: スタン連動API（SetStun, SetStunNormalizedTime）と放出停止動作確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト5: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara5 != null) GameObject.DestroyImmediate(testChara5);
            }

            // テスト6: バースト射出APIと被弾ベクトル後の VelocityOverLifetime 解除検証（残留ステート汚染防止）
            GameObject testChara6 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara6 = GameObject.Instantiate(prefab);
                testChara6.hideFlags = HideFlags.DontSave;
                testChara6.name = "TestPureChara_Burst";

                CharacterPureParticleElectricity controller = testChara6.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                // 1. 被弾ベクトル付きバースト
                controller.TriggerBurst(25, 1.0f, Color.white, Vector3.forward);
                if (!controller.BurstEmitter.velocityOverLifetime.enabled)
                    throw new Exception("被弾ベクトル指定時に velocityOverLifetime が有効化されていません。");

                // 2. 通常バースト（ベクトル未指定）
                controller.TriggerBurst(25, 1.0f, Color.white, null);
                if (controller.BurstEmitter.velocityOverLifetime.enabled)
                    throw new Exception("ベクトルなしバースト呼び出し時に velocityOverLifetime が解除されていません（残留汚染バグ）。");

                logPass("✅ [PASS] テスト6: 被弾バーストAPIと VelocityOverLifetime 確実な解除確認（残留ステート汚染防止）");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト6: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara6 != null) GameObject.DestroyImmediate(testChara6);
            }

            // テスト7: 純粋パーティクル制御の毎フレーム完全ゼロGC確認 (0 bytes)
            GameObject testChara7 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara7 = GameObject.Instantiate(prefab);
                testChara7.hideFlags = HideFlags.DontSave;
                testChara7.name = "TestPureChara_GC";

                CharacterPureParticleElectricity controller = testChara7.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();
                controller.CurrentPattern = PureParticlePattern.Pattern1_ArcRibbons;
                controller.SetStun(true, immediate: true);

                // ウォームアップ
                for (int i = 0; i < 5; i++)
                {
                    controller.ApplyPatternImmediate();
                }

                long allocBefore = GC.GetAllocatedBytesForCurrentThread();
                const int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    controller.ApplyPatternImmediate();
                }
                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long totalAlloc = allocAfter - allocBefore;

                logPass($"[GC Alloc] {iterations} 回の ApplyPatternImmediate: {totalAlloc} bytes");

                if (totalAlloc > 0)
                {
                    throw new Exception($"パーティクル制御ルーチンで GC アロケーションが検出されました: {totalAlloc} bytes / {iterations} 回");
                }

                logPass("✅ [PASS] テスト7: 純粋パーティクル制御の毎フレーム完全ゼロGC確認 (0 bytes)");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト7: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara7 != null) GameObject.DestroyImmediate(testChara7);
            }

            // テスト8: キャラクター本来のマテリアル（shiro/ao/kuro）が完全に保護・不変であることの検証
            GameObject testChara8 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                Material aoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/ao.mat");
                Material kuroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/kuro.mat");

                testChara8 = GameObject.Instantiate(prefab);
                testChara8.hideFlags = HideFlags.DontSave;
                testChara8.name = "TestPureChara_MaterialSafety";

                var smr = testChara8.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.sharedMaterials = new Material[] { aoMat };

                CharacterPureParticleElectricity controller = testChara8.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                // 全パターンを次々に適用
                foreach (PureParticlePattern p in Enum.GetValues(typeof(PureParticlePattern)))
                {
                    controller.CurrentPattern = p;
                    controller.ApplyPatternImmediate();

                    // マテリアルが一切書き換わっていないことを厳格に検証
                    if (smr.sharedMaterials[0] != aoMat)
                    {
                        throw new Exception($"パターン {p} 適用時にキャラクターのマテリアルが書き換えられました！ 現: {smr.sharedMaterials[0]?.name}");
                    }
                }

                // kuro.mat でも検証
                smr.sharedMaterials = new Material[] { kuroMat };
                controller.CurrentPattern = PureParticlePattern.Pattern6_OverdriveMeltdown;
                controller.ApplyPatternImmediate();
                if (smr.sharedMaterials[0] != kuroMat)
                {
                    throw new Exception($"Overdrive 適用時に kuro.mat が書き換えられました！ 現: {smr.sharedMaterials[0]?.name}");
                }

                logPass("✅ [PASS] テスト8: キャラクター本来のマテリアル（shiro/ao/kuro）完全不変・保護確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト8: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara8 != null) GameObject.DestroyImmediate(testChara8);
            }

            // テスト9: SetupParticlesHierarchy の冪等性（重複生成・リーク防止）
            GameObject testChara9 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara9 = GameObject.Instantiate(prefab);
                testChara9.hideFlags = HideFlags.DontSave;
                testChara9.name = "TestPureChara_Idempotency";

                CharacterPureParticleElectricity controller = testChara9.AddComponent<CharacterPureParticleElectricity>();

                // 3回連続セットアップ
                controller.SetupParticlesHierarchy();
                controller.SetupParticlesHierarchy();
                controller.SetupParticlesHierarchy();

                // 子オブジェクトの数を数える（6つだけであるべき）
                int childCount = 0;
                foreach (Transform child in testChara9.transform)
                {
                    if (child.name.StartsWith("PS_Pure_")) childCount++;
                }

                if (childCount != 6)
                {
                    throw new Exception($"重複セットアップによりエミッターが多重生成されました。子オブジェクト数: {childCount} (期待値: 6)");
                }

                logPass("✅ [PASS] テスト9: SetupParticlesHierarchy の冪等性（多重生成防止・子オブジェクト数 6）確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト9: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara9 != null) GameObject.DestroyImmediate(testChara9);
            }

            // テスト10: 親階層アタッチ時の SkinnedMeshRenderer 自動検出テスト
            GameObject rootObj10 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                rootObj10 = new GameObject("TestPureChara_RootParent");
                rootObj10.hideFlags = HideFlags.DontSave;

                GameObject childModel = GameObject.Instantiate(prefab, rootObj10.transform);
                childModel.name = "ModelChild";

                CharacterPureParticleElectricity controller = rootObj10.AddComponent<CharacterPureParticleElectricity>();
                controller.EnsureTargetRenderer();

                if (controller.TargetRenderer == null || !(controller.TargetRenderer is SkinnedMeshRenderer))
                {
                    throw new Exception("親階層からの子 SkinnedMeshRenderer 自動検出に失敗しました。");
                }

                logPass("✅ [PASS] テスト10: 親階層アタッチ時の子 SkinnedMeshRenderer 自動検出確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト10: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (rootObj10 != null) GameObject.DestroyImmediate(rootObj10);
            }

            // テスト11: RibbonWorldSpaceトグル反映とClearAndStopの完全停止安全性検証
            GameObject testChara11 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara11 = GameObject.Instantiate(prefab);
                testChara11.hideFlags = HideFlags.DontSave;
                testChara11.name = "TestPureChara_Safety";

                CharacterPureParticleElectricity controller = testChara11.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();
                controller.SetStun(true, immediate: true);

                // WorldSpaceトグル検証
                controller.RibbonWorldSpace = true;
                if (!controller.RibbonEmitter.trails.worldSpace)
                    throw new Exception("RibbonWorldSpace = true の反映に失敗しました。");

                controller.RibbonWorldSpace = false;
                if (controller.RibbonEmitter.trails.worldSpace)
                    throw new Exception("RibbonWorldSpace = false の反映に失敗しました。");

                // ClearAndStop の完全停止検証（再点火防止）
                controller.ClearAndStop();
                if (controller.IsStunned)
                    throw new Exception("ClearAndStop 後に IsStunned が true のままです。");
                if (controller.CurrentStunWeight > 0.001f)
                    throw new Exception("ClearAndStop 後に CurrentStunWeight が 0 になっていません。");

                logPass("✅ [PASS] テスト11: RibbonWorldSpaceトグル反映とClearAndStop完全停止安全性確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト11: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara11 != null) GameObject.DestroyImmediate(testChara11);
            }

            // テスト12: エミッターのマテリアル整合性・レンダーモード検証（バーストStretch・リボンTrail等）
            GameObject testChara12 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara12 = GameObject.Instantiate(prefab);
                testChara12.hideFlags = HideFlags.DontSave;
                testChara12.name = "TestPureChara_Materials";

                CharacterPureParticleElectricity controller = testChara12.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                var burstPsr = controller.BurstEmitter.GetComponent<ParticleSystemRenderer>();
                if (burstPsr.renderMode != ParticleSystemRenderMode.Stretch)
                    throw new Exception("BurstEmitter の renderMode が Stretch ではありません。");

                var needleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Electricity_Pure_Needle.mat");
                if (burstPsr.sharedMaterial != needleMat)
                    throw new Exception("BurstEmitter のマテリアルが M_Electricity_Pure_Needle ではありません。");

                var ribbonPsr = controller.RibbonEmitter.GetComponent<ParticleSystemRenderer>();
                var trailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Electricity_Pure_Trail.mat");
                if (ribbonPsr.trailMaterial != trailMat)
                    throw new Exception("RibbonEmitter の trailMaterial が M_Electricity_Pure_Trail ではありません。");

                // パターン5（這い回りプラズマ）でリボン・ニードル放出がゼロであることを確認
                CharacterPureParticleElectricity.GetPatternParameters(PureParticlePattern.Pattern5_CrawlingPlasmaCreep,
                    out float ribbonRate, out float needleRate, out float auraRate,
                    out float forkRate, out float crackleRate,
                    out float baseSpeed, out float baseSize,
                    out Color defaultColor,
                    out bool hasPeriodicBurst, out float burstInterval,
                    out bool hasVortex, out bool isStaccato);

                if (ribbonRate > 0.001f || needleRate > 0.001f)
                    throw new Exception("パターン5（這い回り）でリボンまたはニードルの放出レートが 0 ではありません（飛散火花混入バグ）。");
                if (crackleRate < 50.0f)
                    throw new Exception("パターン5（這い回り）でチリチリ放電レートが十分に高くありません。");

                logPass("✅ [PASS] テスト12: エミッターのマテリアル整合性・レンダーモード及びパターン5這い回り純粋性確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト12: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara12 != null) GameObject.DestroyImmediate(testChara12);
            }

            // テスト13: 全6系統パーティクルエミッターの安全完全クリーンアップ（CleanupAllParticles）及び階層保護検証
            GameObject testChara13 = null;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara13 = GameObject.Instantiate(prefab);
                testChara13.hideFlags = HideFlags.DontSave;
                testChara13.name = "TestPureChara_Cleanup_Hierarchy";

                // 元のキャラクター階層（ボーン・メッシュ構造）を記録
                int initialChildCount = testChara13.transform.childCount;
                string[] initialChildNames = new string[initialChildCount];
                for (int i = 0; i < initialChildCount; i++)
                {
                    initialChildNames[i] = testChara13.transform.GetChild(i).name;
                }

                CharacterPureParticleElectricity controller = testChara13.AddComponent<CharacterPureParticleElectricity>();
                controller.SetupParticlesHierarchy();

                // 全6系統のエミッターが生成されていることを確認
                if (testChara13.transform.childCount != initialChildCount + 6)
                {
                    throw new Exception($"全6エミッター生成後の子オブジェクト数が不正です: 期待={initialChildCount + 6}, 実際={testChara13.transform.childCount}");
                }

                controller.SetStun(true, immediate: true);
                controller.CurrentPattern = PureParticlePattern.Pattern1_ArcRibbons;

                // 安全クリーンアップ（全エミッター削除・初期化）を実行
                controller.CleanupAllParticles(recordUndo: false);

                // 参照が null に初期化されていることを確認
                if (controller.RibbonEmitter != null || controller.NeedleEmitter != null ||
                    controller.AuraEmitter != null || controller.ForkEmitter != null ||
                    controller.CrackleEmitter != null || controller.BurstEmitter != null)
                {
                    throw new Exception("CleanupAllParticles 後にエミッタープロパティ参照が null にクリアされていません。");
                }

                if (controller.CurrentPattern != PureParticlePattern.Off)
                {
                    throw new Exception("CleanupAllParticles 後に CurrentPattern が Off に戻っていません。");
                }
                if (controller.IsStunned)
                {
                    throw new Exception("CleanupAllParticles 後に IsStunned が false に戻っていません。");
                }
                if (controller.ParticlesEnabled)
                {
                    throw new Exception("CleanupAllParticles 後に ParticlesEnabled が false になっていません。");
                }

                // PS_Pure_* の子オブジェクトがすべて破棄されていることを確認
                string[] pureNames = new string[]
                {
                    "PS_Pure_RibbonArcs", "PS_Pure_NeedleSparks", "PS_Pure_PlasmaAura",
                    "PS_Pure_ForkArcs", "PS_Pure_MicroCrackles", "PS_Pure_ShockBurst"
                };
                foreach (var pName in pureNames)
                {
                    if (testChara13.transform.Find(pName) != null)
                    {
                        throw new Exception($"CleanupAllParticles 後に {pName} が残存しています。");
                    }
                }

                // キャラクター本来のボーン・メッシュ階層が100%保護されていることを確認
                if (testChara13.transform.childCount != initialChildCount)
                {
                    throw new Exception($"キャラクターの子オブジェクト数が変化しています（階層破壊の恐れ）: 初期={initialChildCount}, 現在={testChara13.transform.childCount}");
                }
                for (int i = 0; i < initialChildCount; i++)
                {
                    if (testChara13.transform.GetChild(i).name != initialChildNames[i])
                    {
                        throw new Exception($"キャラクターの子オブジェクト構成が破壊されています: {testChara13.transform.GetChild(i).name} != {initialChildNames[i]}");
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // フェーズ2: エミッター再構築後、直接 Remove Component (OnDestroy) の自動消去を検証
                // ─────────────────────────────────────────────────────────────
                controller.SetupParticlesHierarchy();
                if (testChara13.transform.childCount != initialChildCount + 6)
                {
                    throw new Exception("エミッター再セットアップ後の子オブジェクト数が不正です。");
                }

                // ユーザーによるインスペクター Remove Component 操作の完全模擬（事前のクリーンアップボタン押し忘れケース）
                GameObject.DestroyImmediate(controller);

                // OnDestroy により全6系統のエミッターが自動消去され、残存ゼロになっていることを確認
                foreach (var pName in pureNames)
                {
                    if (testChara13.transform.Find(pName) != null)
                    {
                        throw new Exception($"Remove Component (OnDestroy) 後に {pName} が孤立残存しています。");
                    }
                }

                // 完全な素体ボーン・メッシュ階層に戻っていることを確認
                if (testChara13.transform.childCount != initialChildCount)
                {
                    throw new Exception($"Remove Component 後に子オブジェクト数が初期状態と一致しません: 初期={initialChildCount}, 現在={testChara13.transform.childCount}");
                }
                for (int i = 0; i < initialChildCount; i++)
                {
                    if (testChara13.transform.GetChild(i).name != initialChildNames[i])
                    {
                        throw new Exception($"Remove Component 後に階層構成が破壊されています: {testChara13.transform.GetChild(i).name} != {initialChildNames[i]}");
                    }
                }

                logPass("✅ [PASS] テスト13: 全6系統パーティクルエミッターの手動クリーンアップ（CleanupAllParticles）及び直接 Remove Component 時の OnDestroy 安全完全消去・階層保護確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト13: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara13 != null) GameObject.DestroyImmediate(testChara13);
            }

            // 終了クリーンアップ
            CleanupOrphanedTestObjects();

            logPass("==================================================");
            logPass($"[CharacterPureParticleElectricityTest] テスト完了: 合格 {passCount} / 失敗 {failCount}");
            logPass("==================================================");

            try
            {
                string dir = Path.Combine(Application.dataPath, "Deverop/AT_Murayama/CharacterElectricity");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string resultPath = Path.Combine(dir, "test_results_pure_electricity.txt");
                File.WriteAllText(resultPath, logSb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterPureParticleElectricityTest] 結果ファイル保存に失敗: {ex.Message}");
            }

            if (failCount > 0 && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
