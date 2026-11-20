#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation.Editor
{
    /// <summary>
    /// FBXインポート設定診断・最適化、mainRig階層パス自動補正、
    /// および全ボーン厳密X鏡像アニメーション（LeftCurve_Mirrored.anim 等）の自動生成ユーティリティ。
    /// ・TeamResources/Art/Animation 配下のアニメーションFBXのみ最適化対象とし、born model.fbx.meta は変更しません。
    /// ・生成 .anim は既存アセットがあれば EditorUtility.CopySerialized で上書きし、GUID参照切れを防ぎます。
    /// </summary>
    public static class RacerAnimationClipUtility
    {
        public const string DefaultReferenceModelPath = "Assets/TeamResources/Art/Models/born model.fbx";
        public const string DefaultAnimationFolderPath = "Assets/TeamResources/Art/Animation";
        public const string FixedRightCurveFileName = "RightCurve_Fixed.anim";
        public const string MirroredLeftCurveFileName = "LeftCurve_Mirrored.anim";

        private static readonly Regex LeftRightTokenRegex = new Regex("_(L|R)(\\d+)_", RegexOptions.Compiled);

        public struct ClipDiagnostic
        {
            public string SlotName;
            public AnimationClip Clip;
            public string AssetPath;
            public float LengthSeconds;
            public float FrameRate;
            public bool IsLooping;
            public bool NeedsMainRigPrefix;
            public bool IsVeryShortSingleFrame;
        }

        // ============================================================
        // 1. フォルダ自動作成・デフォルト参照補完
        // ============================================================
        public static void EnsureAssetFolderExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }

        public static bool AutoAssignReferenceModelIfMissing(RacerAnimationProfile profile)
        {
            if (profile == null || profile.ReferenceModelPrefab != null)
            {
                return false;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultReferenceModelPath);
            if (model != null)
            {
                profile.ReferenceModelPrefab = model;
                EditorUtility.SetDirty(profile);
                return true;
            }

            return false;
        }

        // ============================================================
        // 2. TeamResources/Art/Animation 配下 FBX の安全最適化＆自動割当
        // ============================================================
        public static string OptimizeAnimationFolderFbxAndAutoAssign(
            RacerAnimationProfile profile,
            string animationFolderPath = DefaultAnimationFolderPath,
            bool applyImporterChanges = true)
        {
            if (profile == null)
            {
                return "Profile が未設定です。";
            }

            AutoAssignReferenceModelIfMissing(profile);

            StringBuilder log = new StringBuilder();
            string normalizedFolder = animationFolderPath.Replace('\\', '/').TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(normalizedFolder))
            {
                return $"アニメーションフォルダが見つかりません: {normalizedFolder}";
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { normalizedFolder });
            int optimizedCount = 0;
            int assignedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 安全ガード：アニメーションフォルダ外（born model.fbx 等）は絶対に変更しない
                if (!path.Replace('\\', '/').StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileNameLower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                if (applyImporterChanges)
                {
                    ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer != null)
                    {
                        bool changed = false;

                        if (importer.animationType != ModelImporterAnimationType.Generic)
                        {
                            importer.animationType = ModelImporterAnimationType.Generic;
                            changed = true;
                        }

                        if (importer.optimizeBones)
                        {
                            importer.optimizeBones = false;
                            changed = true;
                        }

                        if (!importer.importAnimation)
                        {
                            importer.importAnimation = true;
                            changed = true;
                        }

                        bool desiredLoop = DetermineDesiredLoopForFileName(profile, fileNameLower);
                        ModelImporterClipAnimation[] clips = importer.clipAnimations;
                        if (clips == null || clips.Length == 0)
                        {
                            clips = importer.defaultClipAnimations;
                        }

                        if (clips != null && clips.Length > 0)
                        {
                            bool clipChanged = false;
                            for (int c = 0; c < clips.Length; c++)
                            {
                                if (clips[c].loopTime != desiredLoop)
                                {
                                    clips[c].loopTime = desiredLoop;
                                    clipChanged = true;
                                }
                            }

                            if (clipChanged || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
                            {
                                importer.clipAnimations = clips;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            importer.SaveAndReimport();
                            optimizedCount++;
                            log.AppendLine($"[最適化] {Path.GetFileName(path)} (StripBones=OFF, Loop={desiredLoop})");
                        }
                    }
                }

                AnimationClip extractedClip = LoadPrimaryAnimationClipFromAsset(path);
                if (extractedClip == null)
                {
                    log.AppendLine($"[注意] クリップが見つかりません: {Path.GetFileName(path)}");
                    continue;
                }

                if (AssignClipByFileName(profile, fileNameLower, extractedClip))
                {
                    assignedCount++;
                    log.AppendLine($"[自動割当] {Path.GetFileName(path)} → {extractedClip.name} ({extractedClip.length:F3}s)");
                }

                if (extractedClip.length <= 0.035f)
                {
                    log.AppendLine($"[確認推奨] {Path.GetFileName(path)} の取込長が {extractedClip.length:F3}s（約1フレーム）です。静止ポーズ想定ならそのまま Loop=ON で問題ありません。動きがある想定なら FBX の Animation タブで End フレームを確認してください。");
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            log.Insert(0, $"FBX最適化: {optimizedCount}件 / スロット自動割当: {assignedCount}件\n");
            return log.ToString();
        }

        private static bool DetermineDesiredLoopForFileName(RacerAnimationProfile profile, string fileNameLower)
        {
            if (fileNameLower.Contains("fly"))
            {
                return profile.LoopFlyClip;
            }

            if (fileNameLower.Contains("set up") || fileNameLower.Contains("setup"))
            {
                return profile.LoopSetUpClip;
            }

            if (fileNameLower.Contains("take off") || fileNameLower.Contains("takeoff"))
            {
                return false;
            }

            if (fileNameLower.Contains("curve"))
            {
                return profile.LoopCurveClips;
            }

            if (fileNameLower.Contains("fire") || fileNameLower.Contains("shoot") || fileNameLower.Contains("shot"))
            {
                return false;
            }

            return true;
        }

        private static bool AssignClipByFileName(RacerAnimationProfile profile, string fileNameLower, AnimationClip clip)
        {
            if (fileNameLower.Contains("set up") || fileNameLower.Contains("setup"))
            {
                profile.SetUpClip = clip;
                return true;
            }

            if (fileNameLower.Contains("take off") || fileNameLower.Contains("takeoff"))
            {
                profile.TakeOffClip = clip;
                return true;
            }

            if (fileNameLower == "fly" || fileNameLower.Contains("fly"))
            {
                profile.FlyClip = clip;
                return true;
            }

            if (fileNameLower.Contains("right") && fileNameLower.Contains("curve"))
            {
                profile.RawRightCurveSourceClip = clip;
                if (profile.RightCurveClip == null)
                {
                    profile.RightCurveClip = clip;
                }
                return true;
            }

            if (fileNameLower.Contains("left") && fileNameLower.Contains("curve"))
            {
                profile.LeftCurveClip = clip;
                return true;
            }

            if (fileNameLower.Contains("stun"))
            {
                profile.StunClip = clip;
                return true;
            }

            if (fileNameLower.Contains("fire") || fileNameLower.Contains("shoot") || fileNameLower.Contains("shot") || fileNameLower.Contains("jammer"))
            {
                profile.FireClip = clip;
                return true;
            }

            if (fileNameLower == "up" || fileNameLower.Contains("moveup") || fileNameLower.Contains("move_up") || fileNameLower.Contains("move up") || fileNameLower.Contains("climb") || fileNameLower.Contains("ascend"))
            {
                profile.MoveUpClip = clip;
                return true;
            }

            if (fileNameLower == "down" || fileNameLower.Contains("movedown") || fileNameLower.Contains("move_down") || fileNameLower.Contains("move down") || fileNameLower.Contains("dive") || fileNameLower.Contains("descend"))
            {
                profile.MoveDownClip = clip;
                return true;
            }

            if (fileNameLower.Contains("boost"))
            {
                profile.BoostClip = clip;
                return true;
            }

            if (fileNameLower.Contains("shield"))
            {
                profile.ShieldClip = clip;
                return true;
            }

            return false;
        }

        /// <summary>
        /// FBX 内の実 AnimationClip を取得します（Unity内部の __preview__ クリップを除外）。
        /// </summary>
        public static AnimationClip LoadPrimaryAnimationClipFromAsset(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            return null;
        }

        // ============================================================
        // 3. mainRig 階層ズレ診断＆パス補正 .anim 生成
        // ============================================================
        public static bool DoesReferenceModelUseMainRig(GameObject referenceModelPrefab)
        {
            if (referenceModelPrefab == null)
            {
                return true;
            }

            Transform mainRig = referenceModelPrefab.transform.Find("mainRig");
            return mainRig != null && mainRig.Find("jnt_org") != null;
        }

        public static bool DoesClipNeedMainRigPrefix(AnimationClip clip, GameObject referenceModelPrefab)
        {
            if (clip == null || !DoesReferenceModelUseMainRig(referenceModelPrefab))
            {
                return false;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            bool hasBareJntOrg = false;
            bool hasPrefixedMainRig = false;

            for (int i = 0; i < bindings.Length; i++)
            {
                string path = bindings[i].path;
                if (path == "mainRig/jnt_org" || path.StartsWith("mainRig/jnt_org/", StringComparison.Ordinal))
                {
                    hasPrefixedMainRig = true;
                }
                else if (path == "jnt_org" || path.StartsWith("jnt_org/", StringComparison.Ordinal))
                {
                    hasBareJntOrg = true;
                }
            }

            return hasBareJntOrg && !hasPrefixedMainRig;
        }

        public static AnimationClip BuildPathNormalizedClipInMemory(
            AnimationClip sourceClip,
            bool addMainRigPrefix,
            bool loopTime,
            string clipName)
        {
            AnimationClip newClip = new AnimationClip
            {
                name = clipName,
                frameRate = sourceClip.frameRate > 0f ? sourceClip.frameRate : 60f
            };

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding srcBinding = bindings[i];
                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, srcBinding);
                if (curve == null)
                {
                    continue;
                }

                EditorCurveBinding dstBinding = srcBinding;
                if (addMainRigPrefix &&
                    (dstBinding.path == "jnt_org" || dstBinding.path.StartsWith("jnt_org/", StringComparison.Ordinal)))
                {
                    dstBinding.path = "mainRig/" + dstBinding.path;
                }

                AnimationUtility.SetEditorCurve(newClip, dstBinding, new AnimationCurve(curve.keys));
            }

            newClip.EnsureQuaternionContinuity();
            ApplyLoopSettings(newClip, loopTime);
            return newClip;
        }

        public static AnimationClip SaveOrUpdateAnimationClipAsset(AnimationClip transientClip, string assetPath)
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            string folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            EnsureAssetFolderExists(folder);

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(normalizedPath);
            if (existing != null)
            {
                string existingName = Path.GetFileNameWithoutExtension(normalizedPath);
                transientClip.name = existingName;
                EditorUtility.CopySerialized(transientClip, existing);
                existing.name = existingName;
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(transientClip);
                AssetDatabase.SaveAssets();
                return existing;
            }

            transientClip.name = Path.GetFileNameWithoutExtension(normalizedPath);
            AssetDatabase.CreateAsset(transientClip, normalizedPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(normalizedPath);
        }

        // ============================================================
        // 4. RightCurve 階層補正 ＆ 数学的完全鏡像 LeftCurve 自動生成
        // ============================================================

        /// <summary>
        /// Right Curve.fbx から：
        /// ① mainRig/ パス補正済みの RightCurve_Fixed.anim
        /// ② 全ボーン厳密X鏡像（モデル空間デルタ回転反転＋左右ボーン対称転写）の LeftCurve_Mirrored.anim
        /// を一括生成し、Profile へ自動反映します。
        /// </summary>
        public static string GenerateFixedRightAndMirroredLeftCurves(RacerAnimationProfile profile)
        {
            if (profile == null)
            {
                return "エラー: Profile が未設定です。";
            }

            AutoAssignReferenceModelIfMissing(profile);
            if (profile.ReferenceModelPrefab == null)
            {
                return $"エラー: ReferenceModelPrefab（born model.fbx）が未設定です。";
            }

            AnimationClip rawRight = profile.RawRightCurveSourceClip != null
                ? profile.RawRightCurveSourceClip
                : profile.RightCurveClip;

            if (rawRight == null)
            {
                return "エラー: 変換元の Right Curve クリップが未設定です。先に FBX 自動割当を実行してください。";
            }

            string folder = profile.GeneratedAssetFolder;
            EnsureAssetFolderExists(folder);

            bool needsPrefix = DoesClipNeedMainRigPrefix(rawRight, profile.ReferenceModelPrefab);

            // 1. RightCurve_Fixed.anim を生成（mainRig/ が欠けていれば自動補完、Loop設定も同期）
            string fixedRightPath = $"{folder}/{FixedRightCurveFileName}";
            AnimationClip fixedRightTransient = BuildPathNormalizedClipInMemory(
                rawRight,
                addMainRigPrefix: needsPrefix,
                loopTime: profile.LoopCurveClips,
                clipName: Path.GetFileNameWithoutExtension(FixedRightCurveFileName));
            AnimationClip fixedRightAsset = SaveOrUpdateAnimationClipAsset(fixedRightTransient, fixedRightPath);

            // 2. LeftCurve_Mirrored.anim を厳密鏡像生成
            string mirroredLeftPath = $"{folder}/{MirroredLeftCurveFileName}";
            AnimationClip mirroredLeftAsset = GenerateMirroredClipAsset(
                fixedRightAsset,
                profile.ReferenceModelPrefab,
                mirroredLeftPath,
                profile.LoopCurveClips,
                profile.MirrorBakeFrameRate,
                out string mirrorSummary);

            if (mirroredLeftAsset == null)
            {
                return $"エラー: 鏡像クリップの生成に失敗しました。\n{mirrorSummary}";
            }

            profile.RawRightCurveSourceClip = rawRight;
            profile.RightCurveClip = fixedRightAsset;
            profile.LeftCurveClip = mirroredLeftAsset;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string prefixNote = needsPrefix ? "mainRig/ 階層欠落を検出→自動補正済み" : "mainRig/ 階層正常（そのまま正規化コピー）";
            return $"✅ 生成完了\n" +
                   $"・右旋回: {fixedRightPath} ({prefixNote}, {fixedRightAsset.length:F3}s)\n" +
                   $"・左旋回(鏡像): {mirroredLeftPath} ({mirrorSummary})";
        }

        /// <summary>
        /// 任意の Generic アニメーションクリップを、リファレンスモデルのX軸（YZ平面）で数学的に完全鏡像化した .anim アセットを生成します。
        /// </summary>
        public static AnimationClip GenerateMirroredClipAsset(
            AnimationClip sourceClip,
            GameObject referenceModelPrefab,
            string outputAssetPath,
            bool loopTime,
            float bakeFrameRate,
            out string summary)
        {
            summary = string.Empty;

            if (sourceClip == null || referenceModelPrefab == null)
            {
                summary = "sourceClip または referenceModelPrefab が null です。";
                return null;
            }

            AnimationClip samplingClip = sourceClip;
            AnimationClip tempNormalizedClip = null;

            GameObject sourceInstance = null;
            GameObject mirrorInstance = null;

            try
            {
                // 階層欠落がある生クリップが渡された場合でも安全にサンプリングできるよう事前補完
                if (DoesClipNeedMainRigPrefix(sourceClip, referenceModelPrefab))
                {
                    tempNormalizedClip = BuildPathNormalizedClipInMemory(sourceClip, true, loopTime, "__temp_norm__");
                    samplingClip = tempNormalizedClip;
                }

                // 2つの一時モデルインスタンスを生成（Unity自身のTransform行列演算でローカル回転・位置を厳密逆算）
                sourceInstance = UnityEngine.Object.Instantiate(referenceModelPrefab, Vector3.zero, Quaternion.identity);
                sourceInstance.name = "__TempMirrorSource__";
                sourceInstance.hideFlags = HideFlags.HideAndDontSave;
                sourceInstance.SetActive(true);

                mirrorInstance = UnityEngine.Object.Instantiate(referenceModelPrefab, Vector3.zero, Quaternion.identity);
                mirrorInstance.name = "__TempMirrorTarget__";
                mirrorInstance.hideFlags = HideFlags.HideAndDontSave;
                mirrorInstance.SetActive(true);

                DisableAllAnimators(sourceInstance);
                DisableAllAnimators(mirrorInstance);

                List<Transform> srcBones = new List<Transform>(128);
                List<Transform> dstBones = new List<Transform>(128);
                List<string> paths = new List<string>(128);
                Dictionary<string, int> pathToIndex = new Dictionary<string, int>(128, StringComparer.Ordinal);
                Dictionary<string, int> nameToIndex = new Dictionary<string, int>(128, StringComparer.Ordinal);

                CollectHierarchyPreOrder(sourceInstance.transform, mirrorInstance.transform, string.Empty, srcBones, dstBones, paths);

                int boneCount = srcBones.Count;
                for (int i = 0; i < boneCount; i++)
                {
                    pathToIndex[paths[i]] = i;
                    string boneName = srcBones[i].name;
                    if (!nameToIndex.ContainsKey(boneName))
                    {
                        nameToIndex[boneName] = i;
                    }
                }

                // Rest Pose（初期バインドポーズ）を記録
                Transform srcRoot = sourceInstance.transform;
                Transform dstRoot = mirrorInstance.transform;
                Quaternion invSrcRootRot = Quaternion.Inverse(srcRoot.rotation);

                Vector3[] restLocalPos = new Vector3[boneCount];
                Quaternion[] restLocalRot = new Quaternion[boneCount];
                Vector3[] restLocalScale = new Vector3[boneCount];
                Quaternion[] restModelRot = new Quaternion[boneCount];
                Vector3[] restModelPos = new Vector3[boneCount];

                for (int i = 0; i < boneCount; i++)
                {
                    Transform t = srcBones[i];
                    restLocalPos[i] = t.localPosition;
                    restLocalRot[i] = t.localRotation;
                    restLocalScale[i] = t.localScale;
                    restModelRot[i] = invSrcRootRot * t.rotation;
                    restModelPos[i] = srcRoot.InverseTransformPoint(t.position);
                }

                // 左右対称ボーンマップを作成（_L0〜L3_ ⇄ _R0〜R3_、中央ボーンは自身）
                int[] mirrorIndex = new int[boneCount];
                int pairedCount = 0;
                int centerCount = 0;

                for (int i = 0; i < boneCount; i++)
                {
                    string boneName = srcBones[i].name;
                    string swappedName = SwapLeftRightBoneName(boneName);
                    if (!string.Equals(swappedName, boneName, StringComparison.Ordinal) &&
                        nameToIndex.TryGetValue(swappedName, out int partnerIdx))
                    {
                        mirrorIndex[i] = partnerIdx;
                        pairedCount++;
                    }
                    else
                    {
                        mirrorIndex[i] = i;
                        centerCount++;
                    }
                }

                // ソースクリップに存在するバインディングを調べ、対象ボーン（とその対称ペア）をマーク
                bool[] writeRot = new bool[boneCount];
                bool[] writePos = new bool[boneCount];
                bool[] writeScale = new bool[boneCount];

                EditorCurveBinding[] srcBindings = AnimationUtility.GetCurveBindings(samplingClip);
                for (int i = 0; i < srcBindings.Length; i++)
                {
                    EditorCurveBinding b = srcBindings[i];
                    if (b.type != typeof(Transform))
                    {
                        continue;
                    }

                    if (!pathToIndex.TryGetValue(b.path, out int idx))
                    {
                        continue;
                    }

                    int partner = mirrorIndex[idx];
                    if (b.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal) ||
                        b.propertyName.StartsWith("localEulerAngles", StringComparison.Ordinal))
                    {
                        writeRot[idx] = true;
                        writeRot[partner] = true;
                    }
                    else if (b.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal))
                    {
                        writePos[idx] = true;
                        writePos[partner] = true;
                    }
                    else if (b.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal))
                    {
                        writeScale[idx] = true;
                        writeScale[partner] = true;
                    }
                }

                // サンプリング設定
                float fps = bakeFrameRate > 0f
                    ? bakeFrameRate
                    : (samplingClip.frameRate > 0f ? samplingClip.frameRate : 60f);
                float length = Mathf.Max(samplingClip.length, 1f / fps);
                int sampleCount = Mathf.Max(2, Mathf.RoundToInt(length * fps) + 1);

                Keyframe[][] rx = new Keyframe[boneCount][];
                Keyframe[][] ry = new Keyframe[boneCount][];
                Keyframe[][] rz = new Keyframe[boneCount][];
                Keyframe[][] rw = new Keyframe[boneCount][];

                Keyframe[][] px = new Keyframe[boneCount][];
                Keyframe[][] py = new Keyframe[boneCount][];
                Keyframe[][] pz = new Keyframe[boneCount][];

                Keyframe[][] sx = new Keyframe[boneCount][];
                Keyframe[][] sy = new Keyframe[boneCount][];
                Keyframe[][] sz = new Keyframe[boneCount][];

                for (int i = 0; i < boneCount; i++)
                {
                    if (writeRot[i])
                    {
                        rx[i] = new Keyframe[sampleCount];
                        ry[i] = new Keyframe[sampleCount];
                        rz[i] = new Keyframe[sampleCount];
                        rw[i] = new Keyframe[sampleCount];
                    }

                    if (writePos[i])
                    {
                        px[i] = new Keyframe[sampleCount];
                        py[i] = new Keyframe[sampleCount];
                        pz[i] = new Keyframe[sampleCount];
                    }

                    if (writeScale[i])
                    {
                        sx[i] = new Keyframe[sampleCount];
                        sy[i] = new Keyframe[sampleCount];
                        sz[i] = new Keyframe[sampleCount];
                    }
                }

                Quaternion[] animModelRot = new Quaternion[boneCount];
                Vector3[] animModelPos = new Vector3[boneCount];
                Vector3[] animLocalScale = new Vector3[boneCount];
                Quaternion[] prevLocalRot = new Quaternion[boneCount];

                for (int frame = 0; frame < sampleCount; frame++)
                {
                    float t = (frame == sampleCount - 1) ? length : (frame / (float)(sampleCount - 1)) * length;

                    // 1. ソース側を Rest Pose に戻してから該当時刻をサンプリング
                    for (int i = 0; i < boneCount; i++)
                    {
                        srcBones[i].localPosition = restLocalPos[i];
                        srcBones[i].localRotation = restLocalRot[i];
                        srcBones[i].localScale = restLocalScale[i];
                    }

                    samplingClip.SampleAnimation(sourceInstance, t);

                    for (int i = 0; i < boneCount; i++)
                    {
                        Transform st = srcBones[i];
                        animModelRot[i] = invSrcRootRot * st.rotation;
                        animModelPos[i] = srcRoot.InverseTransformPoint(st.position);
                        animLocalScale[i] = st.localScale;
                    }

                    // 2. ミラー側を Rest Pose に戻し、親→子順でモデル空間鏡像姿勢を書き込んでローカル値を逆算
                    for (int i = 0; i < boneCount; i++)
                    {
                        dstBones[i].localPosition = restLocalPos[i];
                        dstBones[i].localRotation = restLocalRot[i];
                        dstBones[i].localScale = restLocalScale[i];
                    }

                    Quaternion dstRootRot = dstRoot.rotation;

                    for (int i = 0; i < boneCount; i++)
                    {
                        int src = mirrorIndex[i];
                        Transform dt = dstBones[i];

                        // 回転のX軸鏡像: MirrorX(q) = (q.x, -q.y, -q.z, q.w)
                        Quaternion deltaRot = animModelRot[src] * Quaternion.Inverse(restModelRot[src]);
                        Quaternion mirroredDelta = MirrorQuaternionX(deltaRot);
                        Quaternion targetModelRot = mirroredDelta * restModelRot[i];
                        dt.rotation = dstRootRot * targetModelRot;

                        if (writePos[i])
                        {
                            Vector3 sp = animModelPos[src];
                            Vector3 mirroredPos = new Vector3(-sp.x, sp.y, sp.z);
                            dt.position = dstRoot.TransformPoint(mirroredPos);
                        }

                        if (writeScale[i])
                        {
                            dt.localScale = animLocalScale[src];
                        }

                        if (writeRot[i])
                        {
                            Quaternion localRot = dt.localRotation;
                            if (frame > 0 && Quaternion.Dot(prevLocalRot[i], localRot) < 0f)
                            {
                                localRot = new Quaternion(-localRot.x, -localRot.y, -localRot.z, -localRot.w);
                            }
                            prevLocalRot[i] = localRot;

                            rx[i][frame] = new Keyframe(t, localRot.x);
                            ry[i][frame] = new Keyframe(t, localRot.y);
                            rz[i][frame] = new Keyframe(t, localRot.z);
                            rw[i][frame] = new Keyframe(t, localRot.w);
                        }

                        if (writePos[i])
                        {
                            Vector3 lp = dt.localPosition;
                            px[i][frame] = new Keyframe(t, lp.x);
                            py[i][frame] = new Keyframe(t, lp.y);
                            pz[i][frame] = new Keyframe(t, lp.z);
                        }

                        if (writeScale[i])
                        {
                            Vector3 ls = dt.localScale;
                            sx[i][frame] = new Keyframe(t, ls.x);
                            sy[i][frame] = new Keyframe(t, ls.y);
                            sz[i][frame] = new Keyframe(t, ls.z);
                        }
                    }
                }

                // 出力 AnimationClip を組み立て
                AnimationClip mirroredClip = new AnimationClip
                {
                    name = Path.GetFileNameWithoutExtension(outputAssetPath),
                    frameRate = fps
                };

                int writtenRotCurves = 0;
                int writtenPosCurves = 0;

                for (int i = 0; i < boneCount; i++)
                {
                    string path = paths[i];
                    if (writeRot[i])
                    {
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", CreateSmoothLinearCurve(rx[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", CreateSmoothLinearCurve(ry[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", CreateSmoothLinearCurve(rz[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", CreateSmoothLinearCurve(rw[i]));
                        writtenRotCurves++;
                    }

                    if (writePos[i])
                    {
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", CreateSmoothLinearCurve(px[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", CreateSmoothLinearCurve(py[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", CreateSmoothLinearCurve(pz[i]));
                        writtenPosCurves++;
                    }

                    if (writeScale[i])
                    {
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalScale.x", CreateSmoothLinearCurve(sx[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalScale.y", CreateSmoothLinearCurve(sy[i]));
                        mirroredClip.SetCurve(path, typeof(Transform), "m_LocalScale.z", CreateSmoothLinearCurve(sz[i]));
                    }
                }

                mirroredClip.EnsureQuaternionContinuity();
                ApplyLoopSettings(mirroredClip, loopTime);

                AnimationClip savedAsset = SaveOrUpdateAnimationClipAsset(mirroredClip, outputAssetPath);
                summary = $"長さ={length:F3}s, {sampleCount}サンプル@{fps:F0}fps, 対称ペア={pairedCount}本 / 中央={centerCount}本, 回転書出={writtenRotCurves}ボーン / 位置書出={writtenPosCurves}ボーン";
                return savedAsset;
            }
            finally
            {
                if (tempNormalizedClip != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempNormalizedClip);
                }

                if (sourceInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceInstance);
                }

                if (mirrorInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(mirrorInstance);
                }
            }
        }

        /// <summary>
        /// クォータニオンの YZ 平面（X軸）鏡面反転。
        /// Pitch (X回り) は符号維持、Yaw (Y回り) と Roll (Z回り) は符号反転します。
        /// </summary>
        public static Quaternion MirrorQuaternionX(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        /// <summary>
        /// mGear 命名規則（_L0〜L9_ ⇄ _R0〜R9_）で対称ボーン名を返します。
        /// </summary>
        public static string SwapLeftRightBoneName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName))
            {
                return boneName;
            }

            return LeftRightTokenRegex.Replace(boneName, match =>
            {
                string side = match.Groups[1].Value;
                string index = match.Groups[2].Value;
                return side == "L" ? $"_R{index}_" : $"_L{index}_";
            });
        }

        // ============================================================
        // 5. 診断一覧（Inspector 表示用）
        // ============================================================
        public static List<ClipDiagnostic> DiagnoseProfileClips(RacerAnimationProfile profile)
        {
            List<ClipDiagnostic> list = new List<ClipDiagnostic>(11);
            if (profile == null)
            {
                return list;
            }

            GameObject refModel = profile.ReferenceModelPrefab;
            AddDiag(list, "SetUp (離陸＆ホバリング)", profile.SetUpClip, refModel);
            AddDiag(list, "TakeOff (飛行補間)", profile.TakeOffClip, refModel);
            AddDiag(list, "Fly (直線飛行)", profile.FlyClip, refModel);
            AddDiag(list, "RightCurve (右旋回)", profile.RightCurveClip, refModel);
            AddDiag(list, "LeftCurve (左旋回)", profile.LeftCurveClip, refModel);
            AddDiag(list, "Stun (スタン)", profile.StunClip, refModel);
            AddDiag(list, "Fire (弾発射)", profile.FireClip, refModel);
            AddDiag(list, "[拡張] MoveUp (上昇)", profile.MoveUpClip, refModel);
            AddDiag(list, "[拡張] MoveDown (下降)", profile.MoveDownClip, refModel);
            AddDiag(list, "[拡張] Boost", profile.BoostClip, refModel);
            AddDiag(list, "[拡張] Shield", profile.ShieldClip, refModel);
            return list;
        }

        private static void AddDiag(List<ClipDiagnostic> list, string slotName, AnimationClip clip, GameObject refModel)
        {
            if (clip == null)
            {
                list.Add(new ClipDiagnostic { SlotName = slotName, Clip = null });
                return;
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            list.Add(new ClipDiagnostic
            {
                SlotName = slotName,
                Clip = clip,
                AssetPath = AssetDatabase.GetAssetPath(clip),
                LengthSeconds = clip.length,
                FrameRate = clip.frameRate,
                IsLooping = settings.loopTime,
                NeedsMainRigPrefix = DoesClipNeedMainRigPrefix(clip, refModel),
                IsVeryShortSingleFrame = clip.length <= 0.035f
            });
        }

        // ============================================================
        // 内部ヘルパー
        // ============================================================
        private static void CollectHierarchyPreOrder(
            Transform srcParent,
            Transform dstParent,
            string parentPath,
            List<Transform> srcBones,
            List<Transform> dstBones,
            List<string> paths)
        {
            int count = Mathf.Min(srcParent.childCount, dstParent.childCount);
            for (int i = 0; i < count; i++)
            {
                Transform sc = srcParent.GetChild(i);
                Transform dc = dstParent.GetChild(i);
                string path = string.IsNullOrEmpty(parentPath) ? sc.name : parentPath + "/" + sc.name;

                srcBones.Add(sc);
                dstBones.Add(dc);
                paths.Add(path);

                CollectHierarchyPreOrder(sc, dc, path, srcBones, dstBones, paths);
            }
        }

        private static void DisableAllAnimators(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(includeInactive: true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].enabled = false;
            }
        }

        private static AnimationCurve CreateSmoothLinearCurve(Keyframe[] keys)
        {
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            return curve;
        }

        private static void ApplyLoopSettings(AnimationClip clip, bool loopTime)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
    }
}
#endif
