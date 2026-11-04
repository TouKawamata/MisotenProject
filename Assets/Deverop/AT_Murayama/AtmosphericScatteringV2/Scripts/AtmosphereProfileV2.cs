using System;
using UnityEngine;

namespace AT_Murayama.AtmosphericScatteringV2
{
    /// <summary>
    /// 大気散乱V2のパラメータ束（構造体）。
    /// 昼・夕・夜それぞれのプロファイルとして保存・シリアライズされ、Time of Day 補間の対象となります。
    /// </summary>
    [Serializable]
    public struct AtmosphereProfileV2
    {
        [SerializeField, HideInInspector]
        public bool isInitialized;

        // -------------------------------------------------------------
        // 1. 空気の厚み（Optical Depth）
        // -------------------------------------------------------------
        [Tooltip("空気のチリによる霞み・散乱が始まる距離（メートル）")]
        [Min(0f)]
        public float startDistance;

        [Tooltip("空気の厚みが最大（100%）に達する距離（メートル）")]
        [Min(10f)]
        public float maxDistance;

        [Tooltip("距離に応じた空気の厚みの増え方カーブ（小さいほど手前から緩やかに立ち上がる）")]
        [Range(0.2f, 3.0f)]
        public float densityCurve;

        [Tooltip("空（Skybox）自体に大気のベース色をどの程度乗せるか")]
        [Range(0f, 1f)]
        public float skyboxInfluence;

        // -------------------------------------------------------------
        // 2. 2段距離カーブ（Dual-Range 2000〜3000mスケール対応）
        // -------------------------------------------------------------
        [Tooltip("近景帯（コース視認性保護帯）の終端距離（メートル）")]
        [Min(10f)]
        public float nearEndDistance;

        [Tooltip("遠景帯（メガロポリス空気遠近帯）の終端距離（メートル）")]
        [Min(50f)]
        public float farEndDistance;

        [Tooltip("近景帯の終端で立ち上がるAirMassの上限割合（0.35なら近景帯では最大35%までしか濃くならない）")]
        [Range(0.05f, 0.95f)]
        public float nearMaxAirMass;

        [Tooltip("遠景帯（NearEndDistance〜FarEndDistance）での密度立ち上がりカーブ")]
        [Range(0.2f, 3.0f)]
        public float farDensityCurve;

        // -------------------------------------------------------------
        // 3. 遠景のチリによる像のボケ（Atmospheric Blur）
        // -------------------------------------------------------------
        [Tooltip("遠景オブジェクト自体の輪郭やディテールが空気のチリでぼやける半径")]
        [Range(0f, 8.0f)]
        public float blurRadius;

        [Tooltip("手前のオブジェクト（コースやモノレールや自機）の輪郭に奥のボケが食い込むのを防ぐ保護強度")]
        [Range(0f, 1.0f)]
        public float depthEdgeSharpness;

        // -------------------------------------------------------------
        // 4. 発光・ライトの空気チリ散乱（Light In-Scattering）
        // -------------------------------------------------------------
        [Tooltip("遠景にある窓・ネオン・街灯などの光が周囲の空気のチリに散乱して明るく滲む強さ")]
        [Range(0f, 4.0f)]
        public float scatterIntensity;

        [Tooltip("光が周囲の空気に散乱して広がる半径")]
        [Range(0.5f, 16.0f)]
        public float scatterRadius;

        [Tooltip("どのくらいの明るさ（輝度）以上のピクセルを『光源（散乱元）』として扱うか")]
        [Range(0f, 3.0f)]
        public float scatterThreshold;

        [Tooltip("しきい値付近の滑らかさ")]
        [Range(0.01f, 1.0f)]
        public float scatterSoftKnee;

        [Tooltip("空気のチリが光を散乱するときの色味")]
        [ColorUsage(false, true)]
        public Color scatterTint;

        [Tooltip("散乱光に元の光源の色（赤い看板なら赤、黄色いライトなら黄色）をどれだけ残すか")]
        [Range(0f, 1.0f)]
        public float lightColorPreservation;

        // -------------------------------------------------------------
        // 5. 遠景の発光チラつき防止（Anti-Flicker）
        // -------------------------------------------------------------
        [Tooltip("散乱計算に入力する最大輝度の上限クランプ")]
        [Range(1.0f, 32.0f)]
        public float maxScatterLuminance;

        [Tooltip("カメラ移動時に遠景の1ピクセルHDR光が暴れるのを抑えるKaris Average重み強度")]
        [Range(0f, 1.0f)]
        public float antiFlickerStrength;

        [Tooltip("サンプル回転のランダムジッター量（0で時間的に完全安定）")]
        [Range(0f, 1.0f)]
        public float noiseJitter;

        // -------------------------------------------------------------
        // 6. 大気の環境散乱・暗部シルエット化（Aerial Perspective）
        // -------------------------------------------------------------
        [Tooltip("遠景のオブジェクトの影の部分が浮き上がるベース大気色")]
        [ColorUsage(false, true)]
        public Color atmosphereColor;

        [Tooltip("遠景の暗部を大気色で持ち上げてシルエット化する濃度")]
        [Range(0f, 1.0f)]
        public float atmosphereDensity;

        [Tooltip("遠景に行くほど本来のコントラストを減衰させる割合")]
        [Range(0f, 1.0f)]
        public float contrastReduction;

        // -------------------------------------------------------------
        // 7. 高度フォグ（Height Fog）
        // -------------------------------------------------------------
        [Tooltip("高度によるフォグ密度の適用強度（0で高度依存なし）")]
        [Range(0f, 1.0f)]
        public float heightFogStrength;

        [Tooltip("フォグが最も濃い基準高度Y（メートル）")]
        public float fogBaseHeight;

        [Tooltip("上空に向かってフォグ密度が薄まる減衰率（大きいほど低層に溜まる）")]
        [Range(0.0005f, 0.05f)]
        public float heightFalloff;

        [Tooltip("地平線・空（Skybox）の高さフォグと最遠方ビル屋上のフォグ連続性マッチ係数（1.0推奨）")]
        [Range(0.1f, 2.0f)]
        public float skyHeightClear;

        // -------------------------------------------------------------
        // 8. 太陽方向 Mie 散乱（前方散乱 / サンシャフト感）
        // -------------------------------------------------------------
        [Tooltip("太陽方向を向いた時の大気散乱の強調強度")]
        [Range(0f, 5.0f)]
        public float mieIntensity;

        [Tooltip("Mie散乱の前方集中度（大きいほど太陽の周囲が鋭く輝く、上限0.92）")]
        [Range(0f, 0.92f)]
        public float mieAnisotropy;

        [Tooltip("空（Skybox）の太陽周囲に乗るMie散乱の影響度")]
        [Range(0f, 1.0f)]
        public float skyboxMieInfluence;

        [Tooltip("Mie散乱が白飛び（色相破綻）しないための輝度ソフトクランプ")]
        [Range(0.5f, 20.0f)]
        public float mieMaxLuminance;

        [Tooltip("太陽色に掛ける色味調整（デフォルト白）")]
        [ColorUsage(false, true)]
        public Color mieTint;

        /// <summary>
        /// V1 の CyberpunkNight_Reference と100%同一の初期値（HeightFog=0, Mie=0）。
        /// 回帰検証の基準プロファイル。
        /// </summary>
        public static AtmosphereProfileV2 CreateDefaultV1CompatibleNight()
        {
            return new AtmosphereProfileV2
            {
                isInitialized = true,

                startDistance = 35f,
                maxDistance = 400f,
                densityCurve = 0.85f,
                skyboxInfluence = 0.08f,

                nearEndDistance = 400f,
                farEndDistance = 2000f,
                nearMaxAirMass = 0.35f,
                farDensityCurve = 1.0f,

                blurRadius = 2.4f,
                depthEdgeSharpness = 0.85f,

                scatterIntensity = 1.65f,
                scatterRadius = 7.5f,
                scatterThreshold = 0.32f,
                scatterSoftKnee = 0.5f,
                scatterTint = new Color(0.35f, 0.82f, 1.18f, 1.0f),
                lightColorPreservation = 0.55f,

                maxScatterLuminance = 8.0f,
                antiFlickerStrength = 0.85f,
                noiseJitter = 0.0f,

                atmosphereColor = new Color(0.08f, 0.24f, 0.39f, 1.0f),
                atmosphereDensity = 0.58f,
                contrastReduction = 0.45f,

                heightFogStrength = 0.0f,
                fogBaseHeight = 0.0f,
                heightFalloff = 0.008f,
                skyHeightClear = 1.0f,

                mieIntensity = 0.0f,
                mieAnisotropy = 0.76f,
                skyboxMieInfluence = 0.5f,
                mieMaxLuminance = 4.0f,
                mieTint = Color.white
            };
        }

        /// <summary>
        /// 快晴の昼（白飛び防止・Aerial Perspective 主役・2000m Dual-Range・低層スモッグ・Mie前方散乱）。
        /// </summary>
        public static AtmosphereProfileV2 CreateDefaultDay()
        {
            return new AtmosphereProfileV2
            {
                isInitialized = true,

                startDistance = 50f,
                maxDistance = 450f,
                densityCurve = 0.85f,
                skyboxInfluence = 0.25f,

                nearEndDistance = 400f,
                farEndDistance = 2000f,
                nearMaxAirMass = 0.35f,
                farDensityCurve = 1.1f,

                blurRadius = 1.5f,
                depthEdgeSharpness = 0.85f,

                // 昼は空や白壁が光源化して白飛びしないようしきい値を高く、散乱強度を抑える
                scatterIntensity = 0.35f,
                scatterRadius = 6.0f,
                scatterThreshold = 1.60f,
                scatterSoftKnee = 0.5f,
                scatterTint = new Color(1.0f, 0.98f, 0.95f, 1.0f),
                lightColorPreservation = 0.7f,

                maxScatterLuminance = 12.0f,
                antiFlickerStrength = 0.85f,
                noiseJitter = 0.0f,

                // 昼は空気遠近法（Aerial Perspective）が主役
                atmosphereColor = new Color(0.55f, 0.72f, 0.92f, 1.0f),
                atmosphereDensity = 0.45f,
                contrastReduction = 0.50f,

                heightFogStrength = 0.45f,
                fogBaseHeight = 0.0f,
                heightFalloff = 0.006f,
                skyHeightClear = 1.0f,

                mieIntensity = 0.65f,
                mieAnisotropy = 0.82f,
                skyboxMieInfluence = 0.7f,
                mieMaxLuminance = 5.0f,
                mieTint = new Color(1.0f, 0.96f, 0.88f, 1.0f)
            };
        }

        /// <summary>
        /// 夕暮れ・ゴールデンアワー（暖色大気・強いMie前方散乱・低層スモッグ）。
        /// </summary>
        public static AtmosphereProfileV2 CreateDefaultSunset()
        {
            return new AtmosphereProfileV2
            {
                isInitialized = true,

                startDistance = 40f,
                maxDistance = 420f,
                densityCurve = 0.85f,
                skyboxInfluence = 0.35f,

                nearEndDistance = 350f,
                farEndDistance = 2000f,
                nearMaxAirMass = 0.38f,
                farDensityCurve = 1.0f,

                blurRadius = 2.2f,
                depthEdgeSharpness = 0.85f,

                scatterIntensity = 1.20f,
                scatterRadius = 8.0f,
                scatterThreshold = 0.80f,
                scatterSoftKnee = 0.5f,
                scatterTint = new Color(1.20f, 0.75f, 0.45f, 1.0f),
                lightColorPreservation = 0.60f,

                maxScatterLuminance = 10.0f,
                antiFlickerStrength = 0.85f,
                noiseJitter = 0.0f,

                atmosphereColor = new Color(0.48f, 0.22f, 0.20f, 1.0f),
                atmosphereDensity = 0.62f,
                contrastReduction = 0.55f,

                heightFogStrength = 0.65f,
                fogBaseHeight = 0.0f,
                heightFalloff = 0.010f, // 低層に溜まるスモッグ
                skyHeightClear = 1.0f,

                mieIntensity = 1.85f,   // 強いMie前方散乱
                mieAnisotropy = 0.86f,
                skyboxMieInfluence = 0.85f,
                mieMaxLuminance = 8.0f,
                mieTint = new Color(1.30f, 0.65f, 0.35f, 1.0f)
            };
        }

        /// <summary>
        /// 2000mスケール対応サイバー夜景（シアンブルー散乱＋高度フォグ＋Dual-Range）。
        /// </summary>
        public static AtmosphereProfileV2 CreateDefaultNight()
        {
            return new AtmosphereProfileV2
            {
                isInitialized = true,

                startDistance = 35f,
                maxDistance = 400f,
                densityCurve = 0.85f,
                skyboxInfluence = 0.08f,

                nearEndDistance = 400f,
                farEndDistance = 2000f,
                nearMaxAirMass = 0.35f,
                farDensityCurve = 1.0f,

                blurRadius = 2.4f,
                depthEdgeSharpness = 0.85f,

                scatterIntensity = 1.65f,
                scatterRadius = 7.5f,
                scatterThreshold = 0.32f,
                scatterSoftKnee = 0.5f,
                scatterTint = new Color(0.35f, 0.82f, 1.18f, 1.0f),
                lightColorPreservation = 0.55f,

                maxScatterLuminance = 8.0f,
                antiFlickerStrength = 0.85f,
                noiseJitter = 0.0f,

                atmosphereColor = new Color(0.08f, 0.24f, 0.39f, 1.0f),
                atmosphereDensity = 0.58f,
                contrastReduction = 0.45f,

                heightFogStrength = 0.30f,
                fogBaseHeight = 0.0f,
                heightFalloff = 0.008f,
                skyHeightClear = 1.0f,

                mieIntensity = 0.25f,
                mieAnisotropy = 0.75f,
                skyboxMieInfluence = 0.4f,
                mieMaxLuminance = 4.0f,
                mieTint = new Color(0.6f, 0.85f, 1.1f, 1.0f)
            };
        }

        /// <summary>
        /// 2つの大気プロファイルを線形補間します。
        /// </summary>
        public static AtmosphereProfileV2 Lerp(in AtmosphereProfileV2 a, in AtmosphereProfileV2 b, float t)
        {
            t = Mathf.Clamp01(t);
            AtmosphereProfileV2 r = default;
            r.isInitialized = true;

            r.startDistance = Mathf.Lerp(a.startDistance, b.startDistance, t);
            r.maxDistance = Mathf.Lerp(a.maxDistance, b.maxDistance, t);
            r.densityCurve = Mathf.Lerp(a.densityCurve, b.densityCurve, t);
            r.skyboxInfluence = Mathf.Lerp(a.skyboxInfluence, b.skyboxInfluence, t);

            r.nearEndDistance = Mathf.Lerp(a.nearEndDistance, b.nearEndDistance, t);
            r.farEndDistance = Mathf.Lerp(a.farEndDistance, b.farEndDistance, t);
            r.nearMaxAirMass = Mathf.Lerp(a.nearMaxAirMass, b.nearMaxAirMass, t);
            r.farDensityCurve = Mathf.Lerp(a.farDensityCurve, b.farDensityCurve, t);

            r.blurRadius = Mathf.Lerp(a.blurRadius, b.blurRadius, t);
            r.depthEdgeSharpness = Mathf.Lerp(a.depthEdgeSharpness, b.depthEdgeSharpness, t);

            r.scatterIntensity = Mathf.Lerp(a.scatterIntensity, b.scatterIntensity, t);
            r.scatterRadius = Mathf.Lerp(a.scatterRadius, b.scatterRadius, t);
            r.scatterThreshold = Mathf.Lerp(a.scatterThreshold, b.scatterThreshold, t);
            r.scatterSoftKnee = Mathf.Lerp(a.scatterSoftKnee, b.scatterSoftKnee, t);
            r.scatterTint = Color.Lerp(a.scatterTint, b.scatterTint, t);
            r.lightColorPreservation = Mathf.Lerp(a.lightColorPreservation, b.lightColorPreservation, t);

            r.maxScatterLuminance = Mathf.Lerp(a.maxScatterLuminance, b.maxScatterLuminance, t);
            r.antiFlickerStrength = Mathf.Lerp(a.antiFlickerStrength, b.antiFlickerStrength, t);
            r.noiseJitter = Mathf.Lerp(a.noiseJitter, b.noiseJitter, t);

            r.atmosphereColor = Color.Lerp(a.atmosphereColor, b.atmosphereColor, t);
            r.atmosphereDensity = Mathf.Lerp(a.atmosphereDensity, b.atmosphereDensity, t);
            r.contrastReduction = Mathf.Lerp(a.contrastReduction, b.contrastReduction, t);

            r.heightFogStrength = Mathf.Lerp(a.heightFogStrength, b.heightFogStrength, t);
            r.fogBaseHeight = Mathf.Lerp(a.fogBaseHeight, b.fogBaseHeight, t);
            r.heightFalloff = Mathf.Lerp(a.heightFalloff, b.heightFalloff, t);
            r.skyHeightClear = Mathf.Lerp(a.skyHeightClear, b.skyHeightClear, t);

            r.mieIntensity = Mathf.Lerp(a.mieIntensity, b.mieIntensity, t);
            r.mieAnisotropy = Mathf.Lerp(a.mieAnisotropy, b.mieAnisotropy, t);
            r.skyboxMieInfluence = Mathf.Lerp(a.skyboxMieInfluence, b.skyboxMieInfluence, t);
            r.mieMaxLuminance = Mathf.Lerp(a.mieMaxLuminance, b.mieMaxLuminance, t);
            r.mieTint = Color.Lerp(a.mieTint, b.mieTint, t);

            return r;
        }
    }
}
