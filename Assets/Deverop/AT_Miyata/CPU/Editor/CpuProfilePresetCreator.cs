using UnityEditor;
using UnityEngine;

// 難易度3つ（Easy／Normal／Hard）と個性5つの初期値のアセットを作る。
// 既に同じ名前のアセットがあれば、上書きせずに飛ばす（調整した値を消さないように）。
public static class CpuProfilePresetCreator
{
    private const string ParentFolder = "Assets/Deverop/AT_Miyata/CPU";
    private const string ProfileFolder = ParentFolder + "/Profiles";

    [MenuItem("Tools/CPU/Create Default Profiles")]
    private static void CreateDefaultProfiles()
    {
        if (!AssetDatabase.IsValidFolder(ProfileFolder))
        {
            AssetDatabase.CreateFolder(ParentFolder, "Profiles");
        }

        // 考え直す間隔, 反応時間, 操作の強さ, 操作の打ち消し, ぶれ, ぶれの速さ, 攻撃のそろい, カーブの限界, カーブを見る距離, ゲージの下限, シールド成功率, ショートカット成功率, 追い上げの強さ, 追い上げの距離
        CreateDifficulty("CpuDifficulty_Easy", 0.25f, 0.6f, 0.35f, 0.08f, 0.25f, 0.5f, 2.5f, 90f, 60f, 0f, 0.3f, 0.4f, 1f, 150f);
        CreateDifficulty("CpuDifficulty_Normal", 0.15f, 0.35f, 0.5f, 0.12f, 0.12f, 0.7f, 1.5f, 35f, 60f, 0.05f, 0.6f, 0.7f, 0.6f, 150f);
        CreateDifficulty("CpuDifficulty_Hard", 0.08f, 0.15f, 0.7f, 0.15f, 0.04f, 1f, 0.8f, 25f, 70f, 0.1f, 0.9f, 1f, 0.3f, 150f);

        // 表示名, 表示の色, 攻撃性, 慎重さ, ブーストの使い方, 走行ライン, ライン変更の頻度, 執念, ショートカット志向
        CreatePersonality("CpuPersonality_Attacker", "アタッカー", new Color(1f, 0.35f, 0.3f), 0.9f, 0.2f, 0.8f, 0.3f, 0.6f, 0.6f, 0.5f);
        CreatePersonality("CpuPersonality_Guardian", "ガーディアン", new Color(0.35f, 0.6f, 1f), 0.25f, 0.9f, 0.3f, 0.5f, 0.3f, 0.3f, 0.5f);
        CreatePersonality("CpuPersonality_Speedster", "スピードスター", new Color(1f, 0.85f, 0.2f), 0.05f, 0.3f, 0.9f, 0f, 0.1f, 0f, 0.6f);
        CreatePersonality("CpuPersonality_Trickster", "トリックスター", new Color(0.8f, 0.45f, 1f), 0.5f, 0.4f, 0.5f, 0.8f, 0.9f, 1f, 1f);
        CreatePersonality("CpuPersonality_Balance", "バランス", new Color(0.4f, 0.9f, 0.45f), 0.5f, 0.5f, 0.5f, 0.4f, 0.5f, 0.4f, 0.6f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CpuProfilePresetCreator: {ProfileFolder} に初期値のアセットを作りました");
    }

    private static void CreateDifficulty(string assetName, float thinkInterval, float reactionTime, float steeringGain, float steeringDamping,
        float steeringNoise, float steeringNoiseFrequency, float attackAlignTolerance, float boostCurveLimit, float curveCheckDistance,
        float boostReserve, float shieldSuccessRate, float shortcutSuccessRate, float catchUpStrength, float catchUpDistance)
    {
        SerializedObject serialized = CreateAsset<CpuDifficultyProfile>(assetName);
        if (serialized == null)
        {
            return;
        }

        serialized.FindProperty("_thinkInterval").floatValue = thinkInterval;
        serialized.FindProperty("_reactionTime").floatValue = reactionTime;
        serialized.FindProperty("_steeringGain").floatValue = steeringGain;
        serialized.FindProperty("_steeringDamping").floatValue = steeringDamping;
        serialized.FindProperty("_steeringNoise").floatValue = steeringNoise;
        serialized.FindProperty("_steeringNoiseFrequency").floatValue = steeringNoiseFrequency;
        serialized.FindProperty("_attackAlignTolerance").floatValue = attackAlignTolerance;
        serialized.FindProperty("_boostCurveLimit").floatValue = boostCurveLimit;
        serialized.FindProperty("_curveCheckDistance").floatValue = curveCheckDistance;
        serialized.FindProperty("_boostReserve").floatValue = boostReserve;
        serialized.FindProperty("_shieldSuccessRate").floatValue = shieldSuccessRate;
        serialized.FindProperty("_shortcutSuccessRate").floatValue = shortcutSuccessRate;
        serialized.FindProperty("_catchUpStrength").floatValue = catchUpStrength;
        serialized.FindProperty("_catchUpDistance").floatValue = catchUpDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 既にあるアセットは、調整した好みの値は変えず、表示名が空のときだけ表示名・色を入れる（表示の項目を後から足したため）。
    private static void CreatePersonality(string assetName, string displayName, Color labelColor, float aggression, float caution, float boostEagerness,
        float preferredLine, float lineChangeFrequency, float vengefulness, float shortcutPreference)
    {
        CpuPersonalityProfile existing = AssetDatabase.LoadAssetAtPath<CpuPersonalityProfile>($"{ProfileFolder}/{assetName}.asset");
        if (existing != null)
        {
            SerializedObject existingSerialized = new SerializedObject(existing);
            if (string.IsNullOrEmpty(existingSerialized.FindProperty("_displayName").stringValue))
            {
                SetLabel(existingSerialized, displayName, labelColor);
                existingSerialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"CpuProfilePresetCreator: {assetName} に表示名・色を入れました");
            }

            return;
        }

        SerializedObject serialized = CreateAsset<CpuPersonalityProfile>(assetName);
        if (serialized == null)
        {
            return;
        }

        SetLabel(serialized, displayName, labelColor);
        serialized.FindProperty("_aggression").floatValue = aggression;
        serialized.FindProperty("_caution").floatValue = caution;
        serialized.FindProperty("_boostEagerness").floatValue = boostEagerness;
        serialized.FindProperty("_preferredLine").floatValue = preferredLine;
        serialized.FindProperty("_lineChangeFrequency").floatValue = lineChangeFrequency;
        serialized.FindProperty("_vengefulness").floatValue = vengefulness;
        serialized.FindProperty("_shortcutPreference").floatValue = shortcutPreference;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLabel(SerializedObject serialized, string displayName, Color labelColor)
    {
        serialized.FindProperty("_displayName").stringValue = displayName;
        serialized.FindProperty("_labelColor").colorValue = labelColor;
    }

    // 既にあればnull。
    private static SerializedObject CreateAsset<T>(string assetName) where T : ScriptableObject
    {
        string path = $"{ProfileFolder}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            Debug.Log($"CpuProfilePresetCreator: {path} は既にあるので飛ばします");
            return null;
        }

        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return new SerializedObject(asset);
    }
}
