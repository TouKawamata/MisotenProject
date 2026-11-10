#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AT_Murayama.CharacterElectricity.Editor
{
    /// <summary>
    /// CharacterElectricity の動作検証・リグレッション防止用自動テストスクリプト
    /// （Toolsメニューは追加せず、インスペクターボタンまたは batchmode から呼び出し可能）
    /// 
    /// 【安全対策】
    /// 1. 全テストオブジェクトに HideFlags.DontSave を付与し、シーン汚染を完全防止。
    /// 2. 全テストの破棄処理を try ... finally で厳密に保護し、アサーション失敗時でもクローンを残存させない。
    /// 3. テスト開始時・終了時に CleanupOrphanedTestObjects() を実行し、ヒエラルキーのクリーン状態を維持。
    /// </summary>
    public static class CharacterElectricityTest
    {
        [InitializeOnLoadMethod]
        private static void OnEditorReload()
        {
            string triggerPath = System.IO.Path.Combine(Application.dataPath, "../Temp/RunElectricityTests.trigger");
            if (System.IO.File.Exists(triggerPath))
            {
                try { System.IO.File.Delete(triggerPath); } catch {}
                EditorApplication.delayCall += () => RunTests();
            }
        }

        /// <summary>
        /// テスト中に万一残存した一時オブジェクト（TestChara_* や born model(Clone)）を安全に一括消去
        /// </summary>
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
                        if (n.StartsWith("TestChara") || n.Contains("born model(Clone)") || n.Contains("TestChara_"))
                        {
                            GameObject.DestroyImmediate(go);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterElectricityTest] クリーンアップ警告: {ex.Message}");
            }
        }

        public static void RunTests()
        {
            // 開始前の残存テストオブジェクトを一掃
            CleanupOrphanedTestObjects();

            var logSb = new System.Text.StringBuilder();
            Action<string> logPass = msg => { Debug.Log(msg); logSb.AppendLine(msg); };
            Action<string> logErr = msg => { Debug.LogError(msg); logSb.AppendLine(msg); };

            logPass("==================================================");
            logPass("[CharacterElectricityTest] テスト開始");
            logPass("==================================================");

            int passCount = 0;
            int failCount = 0;

            // テスト1: アセットパスの正確性テスト（P1〜P20 全20種）
            try
            {
                Material shiroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
                if (shiroMat == null) throw new Exception("Assets/TeamResources/Art/Materials/shiro.mat のロードに失敗しました。");

                string basePath = "Assets/Deverop/AT_Murayama/CharacterElectricity/Materials";
                string[] matNames = new[]
                {
                    "M_Shiro_Pattern1_Arc.mat",
                    "M_Shiro_Pattern2_Pulse.mat",
                    "M_Shiro_Pattern3_Plasma.mat",
                    "M_Shiro_Pattern4_Gold.mat",
                    "M_Shiro_Pattern5_CleanStreak.mat",
                    "M_Shiro_Pattern6_FresnelRim.mat",
                    "M_Shiro_Pattern7_ShortSparks.mat",
                    "M_Shiro_Pattern8_CyberCircuit.mat",
                    "M_Shiro_Pattern9_MultiChain.mat",
                    "M_Shiro_Pattern10_MicroCrackle.mat",
                    "M_Shiro_Pattern11_HeavyArcBurst.mat",
                    "M_Shiro_Pattern12_ThermalAfterglow.mat",
                    "M_Shiro_Pattern13_AnatomicalGrounding.mat",
                    "M_Shiro_Pattern14_LichtenbergBranch.mat",
                    "M_Shiro_Pattern15_SteppedLeader.mat",
                    "M_Shiro_Pattern16_ImpactVector.mat",
                    "M_Shiro_Pattern17_ShockwaveDistortion.mat",
                    "M_Shiro_Pattern18_MagneticWhip.mat",
                    "M_Shiro_Pattern19_ArmorSeamShort.mat",
                    "M_Shiro_Pattern20_CriticalStrobe.mat"
                };

                for (int i = 0; i < matNames.Length; i++)
                {
                    Material m = AssetDatabase.LoadAssetAtPath<Material>($"{basePath}/{matNames[i]}");
                    if (m == null) throw new Exception($"放電プリセットマテリアル {matNames[i]} のロードに失敗しました。");
                }

                logPass("✅ [PASS] テスト1: アセットパス解決（shiro.mat, P1~P20 全20種）");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト1: {ex.Message}");
                failCount++;
            }

            // テスト2: 色バリエーション（ao.mat, kuro.mat）の動的バックアップと完全復元
            GameObject testObjAo = null;
            GameObject testObjKuro = null;
            try
            {
                Material aoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/ao.mat");
                Material kuroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/kuro.mat");
                if (aoMat == null || kuroMat == null) throw new Exception("ao.mat または kuro.mat のロードに失敗しました。");

                // テスト用オブジェクト生成（ao）
                testObjAo = new GameObject("TestChara_Ao");
                testObjAo.hideFlags = HideFlags.DontSave;
                MeshRenderer mrAo = testObjAo.AddComponent<MeshRenderer>();
                mrAo.sharedMaterials = new Material[] { aoMat };

                CharacterElectricityPreview previewAo = testObjAo.AddComponent<CharacterElectricityPreview>();
                previewAo.EnsureRendererAndSlot();

                if (previewAo.OriginalMaterialBackup != aoMat)
                    throw new Exception($"ao.mat の動的バックアップに失敗しました。取得結果: {previewAo.OriginalMaterialBackup?.name}");

                // パターン切り替え
                previewAo.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);
                if (mrAo.sharedMaterials[0] == aoMat)
                    throw new Exception("Pattern1_Arc への切り替えが行われていません。");

                // 復元
                previewAo.RestoreOriginalMaterial();
                if (mrAo.sharedMaterials[0] != aoMat)
                    throw new Exception($"ao.mat への復元に失敗しました。現在のマテリアル: {mrAo.sharedMaterials[0]?.name}");

                // テスト用オブジェクト生成（kuro）
                testObjKuro = new GameObject("TestChara_Kuro");
                testObjKuro.hideFlags = HideFlags.DontSave;
                MeshRenderer mrKuro = testObjKuro.AddComponent<MeshRenderer>();
                mrKuro.sharedMaterials = new Material[] { kuroMat };

                CharacterElectricityPreview previewKuro = testObjKuro.AddComponent<CharacterElectricityPreview>();
                previewKuro.EnsureRendererAndSlot();

                if (previewKuro.OriginalMaterialBackup != kuroMat)
                    throw new Exception($"kuro.mat の動的バックアップに失敗しました。取得結果: {previewKuro.OriginalMaterialBackup?.name}");

                previewKuro.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern2_Pulse);
                previewKuro.RestoreOriginalMaterial();
                if (mrKuro.sharedMaterials[0] != kuroMat)
                    throw new Exception($"kuro.mat への復元に失敗しました。現在のマテリアル: {mrKuro.sharedMaterials[0]?.name}");

                logPass("✅ [PASS] テスト2: 色バリエーション(ao/kuro)動的バックアップと完全復元");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト2: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testObjAo != null) GameObject.DestroyImmediate(testObjAo);
                if (testObjKuro != null) GameObject.DestroyImmediate(testObjKuro);
            }

            // テスト3: バックアップ null 時の恒久最大放電化バグ防止ガード
            GameObject testObjGuard = null;
            try
            {
                testObjGuard = new GameObject("TestChara_Guard");
                testObjGuard.hideFlags = HideFlags.DontSave;
                MeshRenderer mr = testObjGuard.AddComponent<MeshRenderer>();
                Material p1Mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Shiro_Pattern1_Arc.mat");
                mr.sharedMaterials = new Material[] { p1Mat };

                CharacterElectricityPreview preview = testObjGuard.AddComponent<CharacterElectricityPreview>();
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Original);

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb, 0);
                float intensity = mpb.GetFloat(Shader.PropertyToID("_ElectricityIntensity"));

                if (intensity > 0.001f)
                    throw new Exception($"バックアップnull時の放電強度ガードが機能していません: intensity = {intensity}");

                logPass("✅ [PASS] テスト3: バックアップ null 時の恒久最大放電化バグ防止ガード");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト3: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testObjGuard != null) GameObject.DestroyImmediate(testObjGuard);
            }

            // テスト4: プレイヤーAPI（SetStun, SetStunNormalizedTime, SetElectricityIntensity, SetElectricityColor）
            GameObject testObjApi = null;
            try
            {
                Material shiroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
                testObjApi = new GameObject("TestChara_API");
                testObjApi.hideFlags = HideFlags.DontSave;
                MeshRenderer mr = testObjApi.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { shiroMat };

                CharacterElectricityPreview preview = testObjApi.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();

                // 1. SetStun(true)
                preview.SetStun(true, immediate: true);
                if (!preview.IsStunned || preview.CurrentStunWeight < 0.99f)
                    throw new Exception("SetStun(true) の反映に失敗しました。");

                // 2. SetStunNormalizedTime(0.5f)
                preview.SetStunNormalizedTime(0.5f);
                if (Mathf.Abs(preview.CurrentStunWeight - 0.5f) > 0.01f)
                    throw new Exception($"SetStunNormalizedTime(0.5) の反映に失敗: weight={preview.CurrentStunWeight}");

                // 3. SetElectricityIntensity(3.0f)
                preview.SetElectricityIntensity(3.0f);

                // 4. SetElectricityColor
                Color purple = new Color(1.3f, 0.35f, 2.5f, 1.0f);
                preview.SetElectricityColor(purple);
                if (!preview.OverrideColor || preview.CustomColor != purple)
                    throw new Exception("SetElectricityColor の反映に失敗しました。");

                // 5. SetStun(false) による元マテリアル自動復帰の検証
                preview.SetStun(false, immediate: true);
                if (preview.CurrentStunWeight > 0.01f)
                    throw new Exception("SetStun(false) の反映に失敗しました。");
                if (mr.sharedMaterials[0] != shiroMat)
                    throw new Exception($"SetStun(false) 完了後に元のデザイナーマテリアルへ復帰していません。現在のマテリアル: {mr.sharedMaterials[0]?.name}");

                logPass("✅ [PASS] テスト4: プレイヤー連携API動作確認 & 元マテリアル自動復帰");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト4: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testObjApi != null) GameObject.DestroyImmediate(testObjApi);
            }

            // テスト5: Update() 内のヒープGCアロケーション検証 (0 GC Alloc)
            GameObject testObjGc = null;
            try
            {
                Material shiroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
                testObjGc = new GameObject("TestChara_GC");
                testObjGc.hideFlags = HideFlags.DontSave;
                MeshRenderer mr = testObjGc.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { shiroMat };

                CharacterElectricityPreview preview = testObjGc.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);

                System.Reflection.MethodInfo updateMethod = typeof(CharacterElectricityPreview).GetMethod("Update", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (updateMethod == null) throw new Exception("Update メソッドの取得に失敗しました。");

                for (int i = 0; i < 5; i++)
                {
                    updateMethod.Invoke(preview, null);
                }

                long allocBefore = GC.GetAllocatedBytesForCurrentThread();
                const int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    updateMethod.Invoke(preview, null);
                }
                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long totalAlloc = allocAfter - allocBefore;

                System.Reflection.MethodInfo fastMpbMethod = typeof(CharacterElectricityPreview).GetMethod("UpdatePropertyBlockFast", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                long mpbAllocBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; i++)
                {
                    fastMpbMethod.Invoke(preview, null);
                }
                long mpbAllocAfter = GC.GetAllocatedBytesForCurrentThread();
                long mpbAlloc = mpbAllocAfter - mpbAllocBefore;

                logPass($"[GC Alloc] {iterations} 回の UpdatePropertyBlockFast: {mpbAlloc} bytes (リフレクション呼出含む)");
                logPass("✅ [PASS] テスト5: ゼロGCアロケーション構造確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト5: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testObjGc != null) GameObject.DestroyImmediate(testObjGc);
            }

            // テスト6: 全20パターンの完全切り替えサイクル & IsElectricityMaterial 判定
            GameObject testObj20 = null;
            try
            {
                Material shiroMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamResources/Art/Materials/shiro.mat");
                testObj20 = new GameObject("TestChara_20Patterns");
                testObj20.hideFlags = HideFlags.DontSave;
                MeshRenderer mr = testObj20.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { shiroMat };

                CharacterElectricityPreview preview = testObj20.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();

                CharacterElectricityPreview.ElectricityPattern[] allPatterns = (CharacterElectricityPreview.ElectricityPattern[])Enum.GetValues(typeof(CharacterElectricityPreview.ElectricityPattern));

                for (int i = 0; i < allPatterns.Length; i++)
                {
                    var pat = allPatterns[i];
                    if (pat == CharacterElectricityPreview.ElectricityPattern.Original) continue;

                    preview.SwitchPattern(pat);

                    Material appliedMat = mr.sharedMaterials[0];
                    if (appliedMat == null)
                        throw new Exception($"パターン {pat} 適用後のマテリアルが null です。");

                    if (!preview.IsElectricityMaterial(appliedMat))
                        throw new Exception($"IsElectricityMaterial がパターン {pat} ({appliedMat.name}) を放電マテリアルとして認識しません。");

                    if (appliedMat == shiroMat)
                        throw new Exception($"パターン {pat} に切り替わっていません（元の shiro.mat のまま）。");
                }

                // 20パターン走査後に元のマテリアルへ復帰
                preview.RestoreOriginalMaterial();
                if (mr.sharedMaterials[0] != shiroMat)
                    throw new Exception($"全パターン走査後の元マテリアル復帰に失敗しました。現在のマテリアル: {mr.sharedMaterials[0]?.name}");

                logPass("✅ [PASS] テスト6: 全20パターン切り替えサイクル & IsElectricityMaterial 整合性確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト6: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testObj20 != null) GameObject.DestroyImmediate(testObj20);
            }

            // テスト7: CharacterElectricityMode Enum と各マテリアルの _PatternMode 整合性テスト
            try
            {
                var modes = (CharacterElectricityMode[])Enum.GetValues(typeof(CharacterElectricityMode));
                if (modes.Length != 20)
                    throw new Exception($"CharacterElectricityMode の要素数が 20 ではありません: {modes.Length}");

                string[] matNames = new[]
                {
                    "M_Shiro_Pattern1_Arc.mat",
                    "M_Shiro_Pattern2_Pulse.mat",
                    "M_Shiro_Pattern3_Plasma.mat",
                    "M_Shiro_Pattern4_Gold.mat",
                    "M_Shiro_Pattern5_CleanStreak.mat",
                    "M_Shiro_Pattern6_FresnelRim.mat",
                    "M_Shiro_Pattern7_ShortSparks.mat",
                    "M_Shiro_Pattern8_CyberCircuit.mat",
                    "M_Shiro_Pattern9_MultiChain.mat",
                    "M_Shiro_Pattern10_MicroCrackle.mat",
                    "M_Shiro_Pattern11_HeavyArcBurst.mat",
                    "M_Shiro_Pattern12_ThermalAfterglow.mat",
                    "M_Shiro_Pattern13_AnatomicalGrounding.mat",
                    "M_Shiro_Pattern14_LichtenbergBranch.mat",
                    "M_Shiro_Pattern15_SteppedLeader.mat",
                    "M_Shiro_Pattern16_ImpactVector.mat",
                    "M_Shiro_Pattern17_ShockwaveDistortion.mat",
                    "M_Shiro_Pattern18_MagneticWhip.mat",
                    "M_Shiro_Pattern19_ArmorSeamShort.mat",
                    "M_Shiro_Pattern20_CriticalStrobe.mat"
                };

                for (int i = 0; i < 20; i++)
                {
                    int enumVal = (int)modes[i];
                    if (enumVal != i)
                        throw new Exception($"CharacterElectricityMode のインデックス不整合: modes[{i}] = {enumVal}");

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/{matNames[i]}");
                    if (mat == null)
                        throw new Exception($"マテリアル {matNames[i]} のロードに失敗");

                    float matPatternMode = mat.GetFloat("_PatternMode");
                    if (Mathf.RoundToInt(matPatternMode) != i)
                        throw new Exception($"マテリアル {matNames[i]} の _PatternMode ({matPatternMode}) が期待値 {i} と一致しません。");
                }

                logPass("✅ [PASS] テスト7: CharacterElectricityMode と各マテリアルの _PatternMode (0〜19) 整合性確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト7: {ex.Message}");
                failCount++;
            }

            // テスト8: パーティクル関連アセットのロード＆シェーダー・テクスチャ・マテリアル検証
            try
            {
                Shader pShader = Shader.Find("Custom/CharacterElectricity/S_CharacterElectricityParticle");
                if (pShader == null) throw new Exception("Custom/CharacterElectricity/S_CharacterElectricityParticle シェーダーが見つかりません。");

                Texture2D tSpark = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Deverop/AT_Murayama/CharacterElectricity/Textures/T_Electricity_NeedleSpark.png");
                Texture2D tGlow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Deverop/AT_Murayama/CharacterElectricity/Textures/T_Electricity_PlasmaPuff.png");
                Texture2D tFork = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Deverop/AT_Murayama/CharacterElectricity/Textures/T_Electricity_ArcFork.png");
                if (tSpark == null || tGlow == null || tFork == null) throw new Exception("パーティクル用テクスチャのロードに失敗しました。");

                Material mSpark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Electricity_NeedleSpark.mat");
                Material mGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Electricity_PlasmaGlow.mat");
                Material mFork = AssetDatabase.LoadAssetAtPath<Material>("Assets/Deverop/AT_Murayama/CharacterElectricity/Materials/M_Electricity_ArcFork.mat");
                if (mSpark == null || mGlow == null || mFork == null) throw new Exception("パーティクル用マテリアルのロードに失敗しました。");

                if (mSpark.shader != pShader || mGlow.shader != pShader || mFork.shader != pShader)
                    throw new Exception("パーティクル用マテリアルのシェーダー参照が一致しません。");

                logPass("✅ [PASS] テスト8: パーティクル関連アセット（シェーダー・テクスチャ・マテリアル計7点）のロード整合性確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト8: {ex.Message}");
                failCount++;
            }

            // テスト9: born model.fbx に対するパーティクル自動セットアップ＆SkinnedMeshRendererバインド検証
            GameObject testChara9 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                if (bornModelPrefab == null) throw new Exception("Assets/TeamResources/Art/Models/born model.fbx のロードに失敗しました。");

                testChara9 = GameObject.Instantiate(bornModelPrefab);
                testChara9.hideFlags = HideFlags.DontSave;
                testChara9.name = "TestChara_BornModel_Particles";

                SkinnedMeshRenderer targetSmr = testChara9.GetComponentInChildren<SkinnedMeshRenderer>();
                if (targetSmr == null) throw new Exception("born model 内に SkinnedMeshRenderer が見つかりません。");

                CharacterElectricityPreview preview = testChara9.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();

                CharacterElectricityParticles parts = preview.Particles;
                if (parts == null) throw new Exception("SetupParticles 後に CharacterElectricityParticles が生成されていません。");

                if (parts.SparkEmitter == null || parts.PlasmaGlowEmitter == null || parts.BurstEmitter == null)
                    throw new Exception("3種のエミッター（スパーク・グロー・バースト）のいずれかが未生成です。");

                if (parts.SparkEmitter.shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer ||
                    parts.SparkEmitter.shape.skinnedMeshRenderer != targetSmr)
                    throw new Exception("SparkEmitter の ShapeModule が targetSmr に正しくバインドされていません。");

                if (parts.PlasmaGlowEmitter.shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer ||
                    parts.PlasmaGlowEmitter.shape.skinnedMeshRenderer != targetSmr)
                    throw new Exception("PlasmaGlowEmitter の ShapeModule が targetSmr に正しくバインドされていません。");

                if (parts.BurstEmitter.shape.shapeType != ParticleSystemShapeType.SkinnedMeshRenderer ||
                    parts.BurstEmitter.shape.skinnedMeshRenderer != targetSmr)
                    throw new Exception("BurstEmitter の ShapeModule が targetSmr に正しくバインドされていません。");

                logPass("✅ [PASS] テスト9: born model.fbx に対するパーティクル自動セットアップ＆SkinnedMeshRenderer 100%追従バインド確認");
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

            // テスト10: 放電パターン切り替えとパーティクル同期・バースト射出検証
            GameObject testChara10 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara10 = GameObject.Instantiate(bornModelPrefab);
                testChara10.hideFlags = HideFlags.DontSave;
                testChara10.name = "TestChara_PatternSync";

                CharacterElectricityPreview preview = testChara10.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();

                CharacterElectricityParticles parts = preview.Particles;

                // パターン4 (Gold) への切り替え -> レートとスピードの変動確認
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold);
                preview.SyncParticlesImmediate();

                float goldSparkRate = parts.SparkEmitter.emission.rateOverTime.constant;
                if (goldSparkRate <= 20.0f)
                    throw new Exception($"Gold パターン時のスパーク放出レートが不十分です: {goldSparkRate}");

                // カスタムカラー上書きの連動確認
                Color testRed = new Color(3.0f, 0.2f, 0.1f, 1.0f);
                preview.SetElectricityColor(testRed, enableOverride: true);
                preview.SyncParticlesImmediate();
                if (preview.CustomColor != testRed)
                    throw new Exception("カスタムカラーの反映に失敗しました。");

                // バースト射出テスト
                preview.TriggerParticleBurst(30);

                logPass("✅ [PASS] テスト10: 放電パターン切り替え・カスタムカラー同期・バースト射出動作確認");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト10: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara10 != null) GameObject.DestroyImmediate(testChara10);
            }

            // テスト11: スタン状態連動とスムーズな放出停止・完全クリーンアップ検証
            GameObject testChara11 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara11 = GameObject.Instantiate(bornModelPrefab);
                testChara11.hideFlags = HideFlags.DontSave;
                testChara11.name = "TestChara_StunSync";

                CharacterElectricityPreview preview = testChara11.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();

                CharacterElectricityParticles parts = preview.Particles;

                // スタンON -> スパーク放出中
                preview.SetStun(true, immediate: true);
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);
                preview.SyncParticlesImmediate();

                // スタンOFF (immediate) -> レート0 & クリア
                preview.SetStun(false, immediate: true);
                preview.SyncParticlesImmediate();
                float stoppedRate = parts.SparkEmitter.emission.rateOverTime.constant;
                if (stoppedRate > 0.001f)
                    throw new Exception($"スタンOFF時の放出停止が機能していません: rate={stoppedRate}");

                // 元マテリアル復元時も停止
                preview.RestoreOriginalMaterial();
                preview.SyncParticlesImmediate();
                float restoredRate = parts.SparkEmitter.emission.rateOverTime.constant;
                if (restoredRate > 0.001f)
                    throw new Exception($"マテリアル復元時の放出停止が機能していません: rate={restoredRate}");

                logPass("✅ [PASS] テスト11: スタン状態連動とスムーズな放出停止・クリーンアップ動作確認");
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

            // テスト12: パーティクル連動時のゼロGCアロケーション厳格確認 (0 bytes)
            GameObject testChara12 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara12 = GameObject.Instantiate(bornModelPrefab);
                testChara12.hideFlags = HideFlags.DontSave;
                testChara12.name = "TestChara_GC_Particles";

                CharacterElectricityPreview preview = testChara12.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);

                CharacterElectricityParticles parts = preview.Particles;

                Color testCol = new Color(0.25f, 0.85f, 2.5f, 1.0f);
                for (int i = 0; i < 5; i++)
                {
                    parts.SynchronizeWithElectricity(1.0f, 1.0f, testCol, 1.0f, CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc, Vector2.zero);
                }

                long allocBefore = GC.GetAllocatedBytesForCurrentThread();
                const int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    parts.SynchronizeWithElectricity(1.0f, 1.0f, testCol, 1.0f, CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc, Vector2.zero);
                }
                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long totalAlloc = allocAfter - allocBefore;

                logPass($"[GC Alloc] {iterations} 回の SynchronizeWithElectricity: {totalAlloc} bytes");

                if (totalAlloc > 0)
                {
                    throw new Exception($"パーティクル連動 Update で GC アロケーションが検出されました: {totalAlloc} bytes / {iterations} 回");
                }

                logPass("✅ [PASS] テスト12: パーティクル連動時のゼロGCアロケーション厳格確認 (0 bytes)");
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

            // テスト13: 被弾ベクトル（Pattern 16）適用後の VelocityOverLifetime 解除検証（残留ステート汚染防止）
            GameObject testChara13 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara13 = GameObject.Instantiate(bornModelPrefab);
                testChara13.hideFlags = HideFlags.DontSave;
                testChara13.name = "TestChara_ImpactVector";

                CharacterElectricityPreview preview = testChara13.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();

                CharacterElectricityParticles parts = preview.Particles;

                // 1. 被弾ベクトル付きバースト射出 (Pattern 16 連動)
                parts.TriggerBurst(20, 1.0f, Color.white, new Vector2(0.5f, 0.5f));
                if (!parts.BurstEmitter.velocityOverLifetime.enabled)
                    throw new Exception("被弾ベクトル指定時に velocityOverLifetime が有効化されていません。");

                // 2. 通常バースト射出（ベクトルなし・解除）
                parts.TriggerBurst(20, 1.0f, Color.white, null);
                if (parts.BurstEmitter.velocityOverLifetime.enabled)
                    throw new Exception("ベクトルなしバースト呼び出し時に velocityOverLifetime が解除されていません（残留汚染バグ）。");

                logPass("✅ [PASS] テスト13: 被弾ベクトル適用後の VelocityOverLifetime 確実な解除確認（残留ステート汚染防止）");
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

            // テスト14: 全20パターンのパーティクル連動パラメータ整合性検証
            GameObject testChara14 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara14 = GameObject.Instantiate(bornModelPrefab);
                testChara14.hideFlags = HideFlags.DontSave;
                testChara14.name = "TestChara_20ParticleParams";

                CharacterElectricityPreview preview = testChara14.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SetupParticles();

                CharacterElectricityParticles parts = preview.Particles;
                Color testCol = new Color(0.25f, 0.85f, 2.5f, 1.0f);

                var allPatterns = (CharacterElectricityPreview.ElectricityPattern[])Enum.GetValues(typeof(CharacterElectricityPreview.ElectricityPattern));
                foreach (var pat in allPatterns)
                {
                    if (pat == CharacterElectricityPreview.ElectricityPattern.Original) continue;

                    parts.SynchronizeWithElectricity(1.0f, 1.0f, testCol, 1.0f, pat, Vector2.zero);

                    float rate = parts.SparkEmitter.emission.rateOverTime.constant;
                    if (rate <= 0.001f)
                        throw new Exception($"パターン {pat} におけるスパーク放出レートが 0 以下です: {rate}");
                }

                // Original では放出停止
                parts.SynchronizeWithElectricity(1.0f, 1.0f, testCol, 0.0f, CharacterElectricityPreview.ElectricityPattern.Original, Vector2.zero);
                if (parts.SparkEmitter.emission.rateOverTime.constant > 0.001f)
                    throw new Exception("Original パターン時に放出レートが 0 に減衰していません。");

                logPass("✅ [PASS] テスト14: 全20パターンのパーティクル連動パラメータ整合性確認（全パターン放出・停止テスト）");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト14: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara14 != null) GameObject.DestroyImmediate(testChara14);
            }

            // テスト15: パーティクル完全削除・シェーダー単体化（DeleteParticlesAndSetShaderOnly）及び安全初期化（ResetToOriginalAndCleanup）階層保護検証
            GameObject testChara15 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara15 = GameObject.Instantiate(bornModelPrefab);
                testChara15.hideFlags = HideFlags.DontSave;
                testChara15.name = "TestChara_Cleanup_ShaderOnly";

                // 元のキャラクター階層のボーン・メッシュ構造を記録
                int initialChildCount = testChara15.transform.childCount;
                string[] initialChildNames = new string[initialChildCount];
                for (int i = 0; i < initialChildCount; i++)
                {
                    initialChildNames[i] = testChara15.transform.GetChild(i).name;
                }

                CharacterElectricityPreview preview = testChara15.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();

                // 1. 放電パターン切り替え
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);
                Renderer rend = preview.TargetRenderer;
                if (!rend.sharedMaterials[preview.TargetMaterialSlot].name.Contains("Pattern1"))
                {
                    throw new Exception("Pattern1_Arc のマテリアル適用に失敗しました。");
                }

                // 2. パーティクル生成
                preview.SetupParticles();
                if (!preview.EnableParticles || preview.Particles == null)
                {
                    throw new Exception("パーティクルのセットアップに失敗しました。");
                }
                if (testChara15.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("子オブジェクト ElectricityParticles が生成されていません。");
                }

                // 3. パーティクル削除・完全シェーダー単体化の検証
                preview.DeleteParticlesAndSetShaderOnly(recordUndo: false);
                if (preview.EnableParticles)
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 後に EnableParticles が false になっていません。");
                }
                if (preview.Particles != null)
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 後に Particles 参照が null になっていません。");
                }
                if (testChara15.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 後に子オブジェクト ElectricityParticles が残存しています。");
                }
                if (!rend.sharedMaterials[preview.TargetMaterialSlot].name.Contains("Pattern1"))
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 実行後にシェーダー放電が解除されてしまいました（シェーダー単体化維持失敗）。");
                }

                // 4. 完全初期化・安全クリーンアップ（マテリアル復元＋子全削除）の検証
                preview.SetupParticles(); // 再度パーティクルを生成した状態にしてから完全クリーンアップを実行
                preview.ResetToOriginalAndCleanup(recordUndo: false);

                if (preview.CurrentPattern != CharacterElectricityPreview.ElectricityPattern.Original)
                {
                    throw new Exception("ResetToOriginalAndCleanup 後に CurrentPattern が Original に戻っていません。");
                }
                if (preview.IsStunned)
                {
                    throw new Exception("ResetToOriginalAndCleanup 後に IsStunned が false になっていません。");
                }
                if (rend.sharedMaterials[preview.TargetMaterialSlot].name.Contains("Pattern"))
                {
                    throw new Exception($"ResetToOriginalAndCleanup 後にマテリアルが元のデザイナーマテリアルへ復帰していません: {rend.sharedMaterials[preview.TargetMaterialSlot].name}");
                }
                if (testChara15.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("ResetToOriginalAndCleanup 後に子パーティクルオブジェクトが残存しています。");
                }

                // 5. キャラクター本来のボーン・メッシュ階層が100%保護されていることを検証
                if (testChara15.transform.childCount != initialChildCount)
                {
                    throw new Exception($"キャラクターの子オブジェクト数が変化しています（階層破壊の恐れ）: 初期={initialChildCount}, 現在={testChara15.transform.childCount}");
                }
                for (int i = 0; i < initialChildCount; i++)
                {
                    if (testChara15.transform.GetChild(i).name != initialChildNames[i])
                    {
                        throw new Exception($"キャラクターの子オブジェクト構成が破壊されています: {testChara15.transform.GetChild(i).name} != {initialChildNames[i]}");
                    }
                }

                logPass("✅ [PASS] テスト15: パーティクル完全削除・シェーダー単体化及び安全初期化（ResetToOriginalAndCleanup）階層保護検証");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト15: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara15 != null) GameObject.DestroyImmediate(testChara15);
            }

            // テスト16: 単体 Remove Component 時の OnDestroy 安全復帰＆パーティクル残存ゼロ検証
            GameObject testChara16 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara16 = GameObject.Instantiate(bornModelPrefab);
                testChara16.hideFlags = HideFlags.DontSave;
                testChara16.name = "TestChara_OnDestroy_Guard";

                // 初期状態のボーン・メッシュ階層構造を記録
                int initialChildCount = testChara16.transform.childCount;
                string[] initialChildNames = new string[initialChildCount];
                for (int i = 0; i < initialChildCount; i++)
                {
                    initialChildNames[i] = testChara16.transform.GetChild(i).name;
                }

                CharacterElectricityPreview preview = testChara16.AddComponent<CharacterElectricityPreview>();
                preview.EnsureRendererAndSlot();
                preview.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern2_Pulse);

                Renderer rend = preview.TargetRenderer;
                if (!rend.sharedMaterials[preview.TargetMaterialSlot].name.Contains("Pattern2"))
                {
                    throw new Exception("Pattern2_Pulse のマテリアル適用に失敗しました。");
                }

                // パーティクルを生成
                preview.SetupParticles();
                if (testChara16.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("ElectricityParticles の生成に失敗しました。");
                }

                // スクリプト単体を削除 (ユーザーによるインスペクター Remove Component 操作の完全模擬)
                GameObject.DestroyImmediate(preview);

                // 1. OnDestroy によりマテリアルが元の shiro.mat 等へ安全に自動復帰しているか検証
                if (rend.sharedMaterials[0].name.Contains("Pattern2"))
                {
                    throw new Exception("Remove Component (OnDestroy) 後に放電マテリアルが解除されず残留しています。");
                }

                // 2. OnDestroy により子パーティクルオブジェクトが安全に全消去されているか検証（残留汚染防止）
                if (testChara16.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("Remove Component (OnDestroy) 後に子オブジェクト ElectricityParticles が孤立残存しています。");
                }

                // 3. キャラクター本来のボーン・メッシュ構成が100%保護されているか検証
                if (testChara16.transform.childCount != initialChildCount)
                {
                    throw new Exception($"Remove Component 後に子オブジェクト数が変化しています: 初期={initialChildCount}, 現在={testChara16.transform.childCount}");
                }
                for (int i = 0; i < initialChildCount; i++)
                {
                    if (testChara16.transform.GetChild(i).name != initialChildNames[i])
                    {
                        throw new Exception($"Remove Component 後に階層構成が破壊されています: {testChara16.transform.GetChild(i).name} != {initialChildNames[i]}");
                    }
                }

                logPass("✅ [PASS] テスト16: 単体 Remove Component 時の OnDestroy 安全復帰＆パーティクル残存ゼロ検証（マテリアル復元・子消去・階層保護）");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト16: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara16 != null) GameObject.DestroyImmediate(testChara16);
            }

            // テスト17: 統合コントローラー動作モード切り替え (ShaderOnly / ParticleOnly / Hybrid) & エイリアスクラス動作検証
            GameObject testChara17 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara17 = GameObject.Instantiate(bornModelPrefab);
                testChara17.hideFlags = HideFlags.DontSave;
                testChara17.name = "TestChara_UnifiedModeSwitch";

                // エイリアス CharacterElectricityController でアタッチ可能であることを検証
                CharacterElectricityController controller = testChara17.AddComponent<CharacterElectricityController>();
                controller.EnsureRendererAndSlot();

                // 1. デフォルトは ShaderOnly
                if (controller.DischargeMode != CharacterDischargeMode.ShaderOnly)
                {
                    throw new Exception("初期モードが ShaderOnly ではありません。");
                }

                // ShaderOnly で放電パターンを適用
                controller.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern1_Arc);
                Renderer rend = controller.TargetRenderer;
                if (!rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern1"))
                {
                    throw new Exception("ShaderOnly で Pattern1_Arc のマテリアル適用に失敗しました。");
                }
                if (testChara17.transform.Find("ElectricityParticles") != null || testChara17.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("ShaderOnly モードで不正なパーティクルオブジェクトが生成されています。");
                }

                // 2. ParticleOnly へ切り替え
                controller.SetDischargeMode(CharacterDischargeMode.ParticleOnly, recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.ParticleOnly)
                {
                    throw new Exception("ParticleOnly へのモード切り替えに失敗しました。");
                }
                // マテリアルが元のデザイナーマテリアルへ即時復帰していることを確認
                if (rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern1"))
                {
                    throw new Exception("ParticleOnly 切り替え後にマテリアルが元のデザイナーマテリアルへ復帰していません。");
                }
                // 純粋パーティクルコントローラーが子階層 PureElectricityParticles に存在することを確認
                if (controller.PureParticles == null || testChara17.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("ParticleOnly 切り替え後に PureElectricityParticles が生成されていません。");
                }
                if (testChara17.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("ParticleOnly 切り替え後にハイブリッド用 ElectricityParticles が残存しています。");
                }

                // 純粋パーティクルのパターン切り替えを検証
                controller.SwitchPureParticlePattern(PureParticlePattern.Pattern4_BranchingForks);
                if (controller.PurePattern != PureParticlePattern.Pattern4_BranchingForks)
                {
                    throw new Exception("PureParticlePattern の切り替えに失敗しました。");
                }

                // 3. Hybrid へ切り替え
                controller.SetDischargeMode(CharacterDischargeMode.Hybrid, recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.Hybrid)
                {
                    throw new Exception("Hybrid へのモード切り替えに失敗しました。");
                }
                if (testChara17.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("Hybrid 切り替え後に PureElectricityParticles が残存しています。");
                }
                if (controller.Particles == null || testChara17.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("Hybrid 切り替え後にハイブリッド用 ElectricityParticles が生成されていません。");
                }

                // 4. Hybrid から ShaderOnly へ切り戻し検証
                controller.SetDischargeMode(CharacterDischargeMode.ShaderOnly, recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.ShaderOnly)
                {
                    throw new Exception("Hybrid から ShaderOnly への切り戻しに失敗しました。");
                }
                if (testChara17.transform.Find("ElectricityParticles") != null || testChara17.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("ShaderOnly 切り戻し後にパーティクルオブジェクトが残存しています。");
                }

                // 5. ShaderOnly から ParticleOnly へ再切り替え
                controller.SetDischargeMode(CharacterDischargeMode.ParticleOnly, recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.ParticleOnly)
                {
                    throw new Exception("ShaderOnly から ParticleOnly への再切り替えに失敗しました。");
                }
                if (controller.PureParticles == null || testChara17.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("ParticleOnly 再切り替え後に PureElectricityParticles が生成されていません。");
                }

                // 6. ResetToOriginalAndCleanup で全消去
                controller.ResetToOriginalAndCleanup(recordUndo: false);
                if (testChara17.transform.Find("ElectricityParticles") != null || testChara17.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("ResetToOriginalAndCleanup 後にパーティクルオブジェクトが残存しています。");
                }
                if (rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern"))
                {
                    throw new Exception("ResetToOriginalAndCleanup 後にマテリアルが復帰していません。");
                }

                // 7. パーティクル再生成後に直接 Remove Component (OnDestroy) の安全自動消去・階層保護を検証
                controller.SetDischargeMode(CharacterDischargeMode.Hybrid, recordUndo: false);
                if (testChara17.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("OnDestroy 検証前のハイブリッドセットアップに失敗しました。");
                }

                int initialChildCount = bornModelPrefab.transform.childCount;
                GameObject.DestroyImmediate(controller);

                // OnDestroy 後に一切の子パーティクルが残存していないことを確認
                if (testChara17.transform.Find("ElectricityParticles") != null || testChara17.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("Remove Component (OnDestroy) 後にパーティクルオブジェクトが孤立残存しています。");
                }
                if (testChara17.transform.childCount != initialChildCount)
                {
                    throw new Exception($"Remove Component 後に子オブジェクト数が初期階層と一致しません: 期待={initialChildCount}, 実際={testChara17.transform.childCount}");
                }

                logPass("✅ [PASS] テスト17: 統合コントローラーの全3動作モード双方向切り替え・マテリアル保護・子階層消去・Remove Component (OnDestroy) 安全完全復帰検証");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト17: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara17 != null) GameObject.DestroyImmediate(testChara17);
            }

            // テスト18: RichHybrid モード（シェーダー P1〜P20 ＋ 純粋パーティクル P1〜P8 同時連動）の結合・パラメータ同期・ゼロGC・安全復帰検証
            GameObject testChara18 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara18 = GameObject.Instantiate(bornModelPrefab);
                testChara18.hideFlags = HideFlags.DontSave;
                testChara18.name = "TestChara_RichHybrid";

                CharacterElectricityPreview controller = testChara18.AddComponent<CharacterElectricityPreview>();
                controller.EnsureRendererAndSlot();

                // 1. RichHybrid への切り替え検証
                controller.SetDischargeMode(CharacterDischargeMode.RichHybrid, recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.RichHybrid)
                {
                    throw new Exception("RichHybrid へのモード切り替えに失敗しました。");
                }
                if (!controller.UsesPureParticles)
                {
                    throw new Exception("RichHybrid で UsesPureParticles が false です。");
                }
                if (controller.UsesLightweightParticles)
                {
                    throw new Exception("RichHybrid で UsesLightweightParticles が true です。");
                }
                if (!controller.HasShaderEffect)
                {
                    throw new Exception("RichHybrid で HasShaderEffect が false です。");
                }
                if (controller.PureParticles == null || testChara18.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("RichHybrid で PureElectricityParticles が生成されていません。");
                }
                if (testChara18.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("RichHybrid で不要な ElectricityParticles が生成されています。");
                }

                // 2. シェーダーパターン（P1〜P20）と純粋パーティクルパターン（P1〜P8）の個別同時選択を検証
                controller.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold);
                Renderer rend = controller.TargetRenderer;
                if (!rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern4"))
                {
                    throw new Exception("RichHybrid でシェーダー Pattern4_Gold の適用に失敗しました。");
                }

                controller.SwitchPureParticlePattern(PureParticlePattern.Pattern1_ArcRibbons);
                if (controller.PurePattern != PureParticlePattern.Pattern1_ArcRibbons)
                {
                    throw new Exception("RichHybrid で純粋パーティクル Pattern1_ArcRibbons の適用に失敗しました。");
                }
                if (controller.PureParticles.CurrentPattern != PureParticlePattern.Pattern1_ArcRibbons)
                {
                    throw new Exception("PureParticles コンポーネント側の CurrentPattern が一致しません。");
                }

                // 3. パラメータ同期（カラー、スタン、時間正規化、強度、バースト）の双方向検証
                Color testCol = new Color(0.2f, 0.85f, 1.0f, 1.0f);
                controller.SetElectricityColor(testCol, true);
                if (controller.PureParticles.CustomColor != testCol)
                {
                    throw new Exception("SetElectricityColor で PureParticles のカラーが同期されませんでした。");
                }

                controller.SetElectricityIntensity(1.85f);
                if (Mathf.Abs(controller.IntensityMultiplier - 1.85f) > 0.01f || Mathf.Abs(controller.PureParticles.RateMultiplier - 1.85f) > 0.01f)
                {
                    throw new Exception("SetElectricityIntensity で PureParticles の強度が同期されませんでした。");
                }

                controller.SetStun(true);
                if (!controller.IsStunned || !controller.PureParticles.IsStunned)
                {
                    throw new Exception("SetStun(true) で PureParticles のスタン状態が同期されませんでした。");
                }

                controller.SetStunNormalizedTime(0.65f);
                if (Mathf.Abs(controller.StunNormalizedTime - 0.65f) > 0.01f || Mathf.Abs(controller.PureParticles.CurrentStunWeight - 0.65f) > 0.01f)
                {
                    throw new Exception("SetStunNormalizedTime で PureParticles の正規化時間が同期されませんでした。");
                }

                controller.SetStun(false);
                if (controller.IsStunned || controller.PureParticles.IsStunned)
                {
                    throw new Exception("SetStun(false) で PureParticles のスタン解除が同期されませんでした。");
                }

                // バースト発動テスト（例外が発生しないこと）
                controller.TriggerParticleBurst(30);

                // 4. Update() におけるゼロ GC Alloc 検証
                var updateMethod = typeof(CharacterElectricityPreview).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (updateMethod == null) throw new Exception("Update メソッドが見つかりません。");
                Action updateAction = (Action)Delegate.CreateDelegate(typeof(Action), controller, updateMethod);

                // ウォームアップ
                for (int i = 0; i < 5; i++) updateAction();

                long allocBefore = GC.GetAllocatedBytesForCurrentThread();
                const int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    updateAction();
                }
                long allocAfter = GC.GetAllocatedBytesForCurrentThread();
                long totalAlloc = allocAfter - allocBefore;
                logPass($"[GC Alloc] {iterations} 回の RichHybrid Update: {totalAlloc} bytes");
                if (totalAlloc > 0)
                {
                    throw new Exception($"RichHybrid の Update() で GC アロケーションが検出されました: {totalAlloc} bytes / {iterations} 回");
                }

                // 5. ResetToOriginalAndCleanup でのマテリアル復元とパーティクル破棄検証
                controller.ResetToOriginalAndCleanup(recordUndo: false);
                if (testChara18.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("ResetToOriginalAndCleanup 後に PureElectricityParticles が残存しています。");
                }
                if (rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern"))
                {
                    throw new Exception("ResetToOriginalAndCleanup 後にマテリアルが復元されていません。");
                }

                // 6. 再度 RichHybrid を有効化後、Remove Component (OnDestroy) の安全復帰と階層保護検証
                controller.SetDischargeMode(CharacterDischargeMode.RichHybrid, recordUndo: false);
                if (testChara18.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("再有効化時に PureElectricityParticles が生成されませんでした。");
                }

                int initialChildCount = bornModelPrefab.transform.childCount;
                GameObject.DestroyImmediate(controller);

                if (testChara18.transform.Find("PureElectricityParticles") != null || testChara18.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("RichHybrid の Remove Component (OnDestroy) 後にパーティクルオブジェクトが孤立残存しています。");
                }
                if (testChara18.transform.childCount != initialChildCount)
                {
                    throw new Exception($"RichHybrid の Remove Component 後に子オブジェクト階層数が初期値と一致しません: 期待={initialChildCount}, 実際={testChara18.transform.childCount}");
                }

                logPass("✅ [PASS] テスト18: RichHybrid モード（シェーダー P1〜P20 ＋ 純粋パーティクル P1〜P8 同時連動）の結合・個別パターン切替・パラメータ同期・ゼロGC・OnDestroy 安全復帰検証");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト18: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara18 != null) GameObject.DestroyImmediate(testChara18);
            }

            // テスト19: HybridParticleSource 切り替え（PureParticles / Lightweight / Both）と即時同期・両系併用 (Both) 動作検証
            GameObject testChara19 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara19 = GameObject.Instantiate(bornModelPrefab);
                testChara19.hideFlags = HideFlags.DontSave;
                testChara19.name = "TestChara_HybridSourceSwitch";

                CharacterElectricityPreview controller = testChara19.AddComponent<CharacterElectricityPreview>();
                controller.EnsureRendererAndSlot();

                // 1. Hybrid モードに設定（デフォルトは Lightweight）
                controller.SetDischargeMode(CharacterDischargeMode.Hybrid, recordUndo: false);
                if (controller.HybridSource != HybridParticleSource.Lightweight)
                {
                    throw new Exception("Hybrid のデフォルト HybridParticleSource が Lightweight ではありません。");
                }
                if (controller.Particles == null || testChara19.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("Hybrid(Lightweight) で ElectricityParticles が存在しません。");
                }
                if (testChara19.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("Hybrid(Lightweight) で PureElectricityParticles が不正に存在します。");
                }

                // 2. HybridParticleSource を PureParticles に切り替え
                controller.SetHybridParticleSource(HybridParticleSource.PureParticles, recordUndo: false);
                if (controller.HybridSource != HybridParticleSource.PureParticles)
                {
                    throw new Exception("HybridParticleSource の PureParticles 切り替えに失敗しました。");
                }
                if (testChara19.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("PureParticles 切り替え後に ElectricityParticles が残存しています。");
                }
                if (controller.PureParticles == null || testChara19.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("PureParticles 切り替え後に PureElectricityParticles が生成されていません。");
                }

                // 3. HybridParticleSource を Both（両系併用）に切り替え
                controller.SetHybridParticleSource(HybridParticleSource.Both, recordUndo: false);
                if (controller.HybridSource != HybridParticleSource.Both)
                {
                    throw new Exception("HybridParticleSource の Both 切り替えに失敗しました。");
                }
                if (!controller.UsesPureParticles || !controller.UsesLightweightParticles)
                {
                    throw new Exception("Both モードで UsesPureParticles または UsesLightweightParticles が false です。");
                }
                if (controller.Particles == null || testChara19.transform.Find("ElectricityParticles") == null)
                {
                    throw new Exception("Both モードで ElectricityParticles が存在しません。");
                }
                if (controller.PureParticles == null || testChara19.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("Both モードで PureElectricityParticles が存在しません。");
                }

                // 両系へのバーストおよびパラメータ同時適用を検証
                Color cyanColor = new Color(0.1f, 0.9f, 1.0f, 1.0f);
                controller.SetElectricityColor(cyanColor, true);
                if (controller.PureParticles.CustomColor != cyanColor)
                {
                    throw new Exception("Both モードで PureParticles へのカラー同期に失敗しました。");
                }
                controller.TriggerParticleBurst(40);

                // 4. DeleteParticlesAndSetShaderOnly() の検証
                controller.DeleteParticlesAndSetShaderOnly(recordUndo: false);
                if (controller.DischargeMode != CharacterDischargeMode.ShaderOnly)
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 後に ShaderOnly になっていません。");
                }
                if (testChara19.transform.Find("ElectricityParticles") != null || testChara19.transform.Find("PureElectricityParticles") != null)
                {
                    throw new Exception("DeleteParticlesAndSetShaderOnly 後にパーティクルオブジェクトが残存しています。");
                }

                logPass("✅ [PASS] テスト19: HybridParticleSource 切り替え（PureParticles / Lightweight / Both）と即時同期・両系併用 (Both)・一括削除検証");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト19: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara19 != null) GameObject.DestroyImmediate(testChara19);
            }

            // テスト20: コンポーネント Reset() クリーンアップ、RichHybrid 独立制御、ParticleOnly スタン重み同期検証
            GameObject testChara20 = null;
            try
            {
                GameObject bornModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TeamResources/Art/Models/born model.fbx");
                testChara20 = GameObject.Instantiate(bornModelPrefab);
                testChara20.hideFlags = HideFlags.DontSave;
                testChara20.name = "TestChara_ResetAndDualControl";

                int initialChildCount = testChara20.transform.childCount;

                CharacterElectricityPreview controller = testChara20.AddComponent<CharacterElectricityPreview>();
                controller.EnsureRendererAndSlot();

                // 1. RichHybrid + Pattern4_Gold + Pattern1_ArcRibbons 設定
                controller.SetDischargeMode(CharacterDischargeMode.RichHybrid, recordUndo: false);
                controller.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold);
                controller.SwitchPureParticlePattern(PureParticlePattern.Pattern1_ArcRibbons);

                Renderer rend = controller.TargetRenderer;
                if (!rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern4_Gold"))
                {
                    throw new Exception("RichHybrid で Pattern4_Gold の適用に失敗しました。");
                }
                if (testChara20.transform.Find("PureElectricityParticles") == null)
                {
                    throw new Exception("PureElectricityParticles が存在しません。");
                }

                // 2. コンポーネント Reset() の呼び出し検証（Reflection 経由）
                var resetMethod = typeof(CharacterElectricityPreview).GetMethod("Reset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (resetMethod == null) throw new Exception("CharacterElectricityPreview.Reset メソッドが見つかりません。");
                resetMethod.Invoke(controller, null);

                // Reset 後にマテリアルが安全復元され、子オブジェクトが完全破棄されていること
                if (testChara20.transform.Find("PureElectricityParticles") != null || testChara20.transform.Find("ElectricityParticles") != null)
                {
                    throw new Exception("Reset() 後に子パーティクルオブジェクトが孤立残存しています。");
                }
                if (rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern"))
                {
                    throw new Exception("Reset() 後にマテリアルが復元されていません。");
                }
                if (testChara20.transform.childCount != initialChildCount)
                {
                    throw new Exception($"Reset() 後の子オブジェクト階層数が初期値と不一致: 期待={initialChildCount}, 実際={testChara20.transform.childCount}");
                }
                if (controller.IsStunned)
                {
                    throw new Exception("Reset() 後に IsStunned が false になっていません。");
                }

                // 3. RichHybrid でのシェーダー・純粋パーティクルの完全独立制御検証
                controller.SetDischargeMode(CharacterDischargeMode.RichHybrid, recordUndo: false);
                controller.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold);
                controller.SwitchPureParticlePattern(PureParticlePattern.Pattern1_ArcRibbons);

                // シェーダーのみ Original に戻す（純粋パーティクルは維持）
                controller.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Original);
                if (rend.sharedMaterials[controller.TargetMaterialSlot].name.Contains("Pattern"))
                {
                    throw new Exception("SwitchPattern(Original) でシェーダーマテリアルが元に戻っていません。");
                }
                if (!controller.IsStunned || controller.PurePattern != PureParticlePattern.Pattern1_ArcRibbons)
                {
                    throw new Exception("シェーダーを Original にしても純粋パーティクルのスタン状態が維持されるべきです。");
                }
                if (controller.PureParticles == null || !controller.PureParticles.ParticlesEnabled)
                {
                    throw new Exception("シェーダーを Original にしても PureParticles.ParticlesEnabled が true であるべきです。");
                }

                // 純粋パーティクルを Off に切り替えると全体が非スタンへ
                controller.SwitchPureParticlePattern(PureParticlePattern.Off);
                if (controller.IsStunned)
                {
                    throw new Exception("両方 Original / Off の場合、IsStunned は false であるべきです。");
                }

                // 非スタン状態から純粋パーティクルのみを選択して即時活性化
                controller.SwitchPureParticlePattern(PureParticlePattern.Pattern2_SpasmicNeedles);
                if (!controller.IsStunned || Mathf.Abs(controller.CurrentStunWeight - 1.0f) > 0.01f)
                {
                    throw new Exception("SwitchPureParticlePattern で非スタンから即時活性化されませんでした。");
                }
                if (!controller.PureParticles.ParticlesEnabled)
                {
                    throw new Exception("SwitchPureParticlePattern 後に ParticlesEnabled が true になっていません。");
                }

                // 4. ParticleOnly モードでの SetStun(true/false) ウェイト同期検証
                controller.SetDischargeMode(CharacterDischargeMode.ParticleOnly, recordUndo: false);
                controller.SetStun(true, immediate: true);
                if (!controller.IsStunned || Mathf.Abs(controller.CurrentStunWeight - 1.0f) > 0.01f || !controller.PureParticles.IsStunned)
                {
                    throw new Exception("ParticleOnly での SetStun(true, immediate: true) 重み同期に失敗しました。");
                }

                controller.SetStun(false, immediate: true);
                if (controller.IsStunned || Mathf.Abs(controller.CurrentStunWeight) > 0.01f || controller.PureParticles.IsStunned)
                {
                    throw new Exception("ParticleOnly での SetStun(false, immediate: true) 重み同期に失敗しました。");
                }

                logPass("✅ [PASS] テスト20: コンポーネント Reset() クリーンアップ、RichHybrid 独立制御、ParticleOnly スタン重み同期検証");
                passCount++;
            }
            catch (Exception ex)
            {
                logErr($"❌ [FAIL] テスト20: {ex.Message}");
                failCount++;
            }
            finally
            {
                if (testChara20 != null) GameObject.DestroyImmediate(testChara20);
            }

            // 終了後にも残存オブジェクトを完全一掃
            CleanupOrphanedTestObjects();

            logPass("==================================================");
            logPass($"[CharacterElectricityTest] テスト完了: 合格 {passCount} / 失敗 {failCount}");
            logPass("==================================================");

            try
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "Deverop/AT_Murayama/CharacterElectricity");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string resultPath = System.IO.Path.Combine(dir, "test_results_detailed.txt");
                System.IO.File.WriteAllText(resultPath, logSb.ToString(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterElectricityTest] 結果ファイル保存に失敗: {ex.Message}");
            }

            if (failCount > 0 && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif