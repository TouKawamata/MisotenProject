Shader "Hidden/AT_Murayama/AtmosphericScatteringV2"
{
    Properties
    {
        [Header(Master Control)]
        _OverallStrength ("Overall Strength", Range(0.0, 1.0)) = 1.0

        [Header(Optical Depth Settings)]
        _StartDistance ("Start Distance (m)", Float) = 40.0
        _MaxDistance ("Max Distance (m)", Float) = 450.0
        _DensityCurve ("Distance Curve", Range(0.2, 3.0)) = 0.85
        _SkyboxInfluence ("Skybox Base Fog", Range(0.0, 1.0)) = 0.08

        [Header(Dual Range Settings (V2))]
        _ASV2_UseDualRange ("Use Dual-Range Curve", Float) = 0.0
        _ASV2_NearEndDistance ("Near End Distance (m)", Float) = 400.0
        _ASV2_FarEndDistance ("Far End Distance (m)", Float) = 2000.0
        _ASV2_NearMaxAirMass ("Near Band Max AirMass", Range(0.05, 0.95)) = 0.35
        _ASV2_FarDensityCurve ("Far Density Curve", Range(0.2, 3.0)) = 1.0

        [Header(Atmospheric Blur Settings)]
        _BlurRadius ("Far Blur Radius", Range(0.0, 8.0)) = 2.2
        _DepthEdgeSharpness ("Foreground Edge Protection", Range(0.0, 1.0)) = 0.85

        [Header(Light In Scattering Settings)]
        _ScatterIntensity ("Light Scatter Intensity", Range(0.0, 4.0)) = 1.55
        _ScatterRadius ("Light Scatter Radius", Range(0.5, 16.0)) = 7.0
        _ScatterThreshold ("Light Threshold", Range(0.0, 3.0)) = 0.35
        _ScatterSoftKnee ("Threshold Softness", Range(0.01, 1.0)) = 0.5
        [HDR] _ScatterTint ("Dust Scatter Tint", Color) = (0.35, 0.82, 1.15, 1.0)
        _LightColorPreservation ("Preserve Original Light Color", Range(0.0, 1.0)) = 0.55

        [Header(Anti Flicker Settings)]
        _MaxScatterLuminance ("Max Scatter Luminance Clamp", Range(1.0, 32.0)) = 8.0
        _AntiFlickerStrength ("Anti Flicker Karis Weight", Range(0.0, 1.0)) = 0.85
        _NoiseJitter ("Sample Noise Jitter", Range(0.0, 1.0)) = 0.0

        [Header(Aerial Perspective Settings)]
        [HDR] _AtmosphereColor ("Atmosphere Base Color", Color) = (0.08, 0.23, 0.38, 1.0)
        _AtmosphereDensity ("Atmosphere Density", Range(0.0, 1.0)) = 0.58
        _ContrastReduction ("Far Contrast Reduction", Range(0.0, 1.0)) = 0.45

        [Header(Height Fog Settings (V2))]
        _ASV2_HeightFogStrength ("Height Fog Strength", Range(0.0, 1.0)) = 0.0
        _ASV2_FogBaseHeight ("Fog Base Height Y (m)", Float) = 0.0
        _ASV2_HeightFalloff ("Height Falloff Rate", Range(0.0005, 0.05)) = 0.008
        _ASV2_SkyHeightClear ("Sky Horizon Height Match", Range(0.1, 2.0)) = 1.0

        [Header(Mie Scattering Settings (V2))]
        _ASV2_MieIntensity ("Mie Scatter Intensity", Range(0.0, 5.0)) = 0.0
        _ASV2_MieAnisotropy ("Mie Forward Anisotropy g", Range(0.0, 0.92)) = 0.78
        _ASV2_SkyboxMieInfluence ("Skybox Mie Influence", Range(0.0, 1.0)) = 0.5
        _ASV2_MieMaxLuminance ("Mie Max Luminance Soft Knee", Range(0.5, 20.0)) = 5.0
        [HDR] _ASV2_MieTint ("Mie Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _ASV2_SunDirection ("Sun Direction WS", Vector) = (0.0, 1.0, 0.0, 0.0)
        [HDR] _ASV2_SunColor ("Sun Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    // V2 専用グローバルテクスチャ（V1 との競合を完全遮断）
    TEXTURE2D_X(_ASV2_HalfResScatteringTex);
    float4 _ASV2_HalfResScatteringTex_TexelSize;

    float _OverallStrength;

    // Optical Depth
    float _StartDistance;
    float _MaxDistance;
    float _DensityCurve;
    float _SkyboxInfluence;

    // Dual-Range (P1)
    float _ASV2_UseDualRange;
    float _ASV2_NearEndDistance;
    float _ASV2_FarEndDistance;
    float _ASV2_NearMaxAirMass;
    float _ASV2_FarDensityCurve;

    // Blur
    float _BlurRadius;
    float _DepthEdgeSharpness;

    // Light In-Scattering
    float _ScatterIntensity;
    float _ScatterRadius;
    float _ScatterThreshold;
    float _ScatterSoftKnee;
    float4 _ScatterTint;
    float _LightColorPreservation;

    // Anti Flicker
    float _MaxScatterLuminance;
    float _AntiFlickerStrength;
    float _NoiseJitter;

    // Aerial Perspective
    float4 _AtmosphereColor;
    float _AtmosphereDensity;
    float _ContrastReduction;

    // Height Fog (P2)
    float _ASV2_HeightFogStrength;
    float _ASV2_FogBaseHeight;
    float _ASV2_HeightFalloff;
    float _ASV2_SkyHeightClear;

    // Mie Scattering (P4)
    float _ASV2_MieIntensity;
    float _ASV2_MieAnisotropy;
    float _ASV2_SkyboxMieInfluence;
    float _ASV2_MieMaxLuminance;
    float4 _ASV2_MieTint;
    float4 _ASV2_SunDirection;
    float4 _ASV2_SunColor;

    static const int SAMPLE_COUNT = 16;
    static const float GOLDEN_ANGLE = 2.39996323f;

    float InterleavedGradientNoise(float2 pixelCoord)
    {
        float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);
        return frac(magic.z * frac(dot(pixelCoord, magic.xy)));
    }

    // Far Clip ギリギリ（99.99%の距離）にあるオブジェクトでも空（Skybox）と誤判定しない厳密判定
    bool IsSkyboxDepth(float rawDepth)
    {
        #if UNITY_REVERSED_Z
            return rawDepth <= 1e-7f;
        #else
            return rawDepth >= (1.0f - 1e-7f);
        #endif
    }

    // Dual-Range 有効時を考慮した実効最遠クリップ距離
    float ASV2_GetEffectiveFarClip()
    {
        float effectiveMax = (_ASV2_UseDualRange > 0.5f)
            ? max(_MaxDistance, _ASV2_FarEndDistance)
            : _MaxDistance;
        return max(effectiveMax, _ProjectionParams.z);
    }

    // Henyey-Greenstein 位相関数（ピーク抑制付き標準形）
    float ASV2_ComputeMiePhase(float cosTheta, float g)
    {
        g = clamp(g, 0.0f, 0.92f); // 0.95以上は分母が極小になりHDRスパイクを起こすため上限0.92
        float g2 = g * g;
        float denom = max(0.001f, 1.0f + g2 - 2.0f * g * cosTheta);
        return (1.0f - g2) / (4.0f * 3.14159265f * pow(denom, 1.5f));
    }

    // 太陽方向 Mie 散乱（色相維持ソフトクランプ付き）
    float3 ASV2_ComputeMieScatter(float3 viewDirWS, float centerAirMass, bool isSkybox)
    {
        if (_ASV2_MieIntensity <= 0.0001f) return 0.0f;
        float cosTheta = saturate(dot(viewDirWS, _ASV2_SunDirection.xyz));
        float phase = ASV2_ComputeMiePhase(cosTheta, _ASV2_MieAnisotropy);
        float mask = isSkybox ? (centerAirMass * _ASV2_SkyboxMieInfluence) : centerAirMass;
        // 太陽が地平線より大きく下に沈んでいる夜間の裏面発光をフェードアウト
        float horizonFade = saturate(_ASV2_SunDirection.y * 5.0f + 0.5f);
        float3 sunCol = _ASV2_SunColor.rgb * _ASV2_MieTint.rgb;
        float3 mie = sunCol * (phase * _ASV2_MieIntensity * mask * horizonFade);

        // 【重要】RGB各チャンネル個別の min() は色相が壊れる（オレンジの夕日が真っ黄色にクリップする）ため、
        // 必ず最大成分（または輝度）ベースのソフトニー圧縮で色相（RGB比）を100%維持する！
        float peak = max(mie.r, max(mie.g, mie.b));
        if (peak > 0.0001f)
        {
            float maxLum = max(0.1f, _ASV2_MieMaxLuminance);
            float compressed = maxLum * (peak / (peak + maxLum));
            mie *= (compressed / peak);
        }
        return mie;
    }

    // 指数オーバーフロー完全防止・高度フォグ係数算出式
    float ASV2_ComputeHeightFogFactor(float camY, float pixY)
    {
        if (_ASV2_HeightFogStrength <= 0.0001f) return 1.0f;

        float k = max(0.0001f, _ASV2_HeightFalloff);
        // y0 以下の領域は常に密度 1.0（h = 0）にクランプ。これで hCam >= 0, hPix >= 0 が保証される
        float hCam = max(0.0f, camY - _ASV2_FogBaseHeight);
        float hPix = max(0.0f, pixY - _ASV2_FogBaseHeight);

        // hCam >= 0 かつ hPix >= 0 なので、exp の引数は常に 0 以下となりオーバーフローは数学的に絶対発生しない
        float rhoCam = exp(-min(80.0f, k * hCam));
        float rhoPix = exp(-min(80.0f, k * hPix));

        float dh = k * (hPix - hCam);
        float avgRho;
        if (abs(dh) < 1e-4f)
        {
            // 水平視線（hPix ≈ hCam）でのゼロ割り回避（極限値）
            avgRho = 0.5f * (rhoCam + rhoPix);
        }
        else
        {
            // 平均値の定理により、必ず 0.0〜1.0 の範囲に収まる
            avgRho = (rhoCam - rhoPix) / dh;
        }

        return lerp(1.0f, saturate(avgRho), saturate(_ASV2_HeightFogStrength));
    }

    // 1. 距離のみの AirMass（16サンプルループ内の重み計算で高速に呼ぶ用）
    float ASV2_ComputeDistanceAirMass(float linearDepth, bool isSkybox)
    {
        if (isSkybox) return 1.0f;
        if (_ASV2_UseDualRange > 0.5f)
        {
            float nearEnd = max(_StartDistance + 1.0f, _ASV2_NearEndDistance);
            float farEnd  = max(nearEnd + 1.0f, _ASV2_FarEndDistance);
            float nearRatio = saturate(_ASV2_NearMaxAirMass);
            float tNear = saturate((linearDepth - _StartDistance) / (nearEnd - _StartDistance));
            float tFar  = saturate((linearDepth - nearEnd) / (farEnd - nearEnd));
            return saturate(nearRatio * pow(tNear, max(0.1f, _DensityCurve))
                          + (1.0f - nearRatio) * pow(tFar, max(0.1f, _ASV2_FarDensityCurve)));
        }
        else
        {
            float maxDist = max(_StartDistance + 1.0f, _MaxDistance);
            float normalizedDist = saturate((linearDepth - _StartDistance) / (maxDist - _StartDistance));
            return pow(normalizedDist, max(0.1f, _DensityCurve));
        }
    }

    // 2. 中心ピクセル用：ワールド座標・視線方向・高度フォグ込みの AirMass（Pass 0, 1, 2, 3 すべてで共通使用する）
    float ASV2_ComputeCenterAirMass(float2 uv, float rawDepth, float linearDepth, bool isSkybox, out float3 viewDirWS)
    {
        float distAirMass = ASV2_ComputeDistanceAirMass(linearDepth, isSkybox);
        float maxRange = (_ASV2_UseDualRange > 0.5f) ? max(_MaxDistance, _ASV2_FarEndDistance) : _MaxDistance;

        // 高度フォグもMie散乱も無効なら行列計算自体をスキップ（V1と完全同一コスト＆同一結果）
        if (_ASV2_HeightFogStrength <= 0.0001f && _ASV2_MieIntensity <= 0.0001f)
        {
            viewDirWS = float3(0.0f, 0.0f, 1.0f);
            return distAirMass;
        }

        float3 camPosWS = _WorldSpaceCameraPos.xyz;
        float pixY;
        if (isSkybox)
        {
            // Skybox時は深度 0.5 の近傍点を再構成して正確な視線方向ベクトルを得る（ゼロ割り・無限遠誤差を完全回避）
            #if UNITY_REVERSED_Z
                float safeDepth = 0.5f;
            #else
                float safeDepth = 0.0f;
            #endif
            float3 nearWS = ComputeWorldSpacePosition(uv, safeDepth, UNITY_MATRIX_I_VP);
            viewDirWS = SafeNormalize(nearWS - camPosWS);
            pixY = camPosWS.y + viewDirWS.y * maxRange * max(0.1f, _ASV2_SkyHeightClear);
        }
        else
        {
            #if UNITY_REVERSED_Z
                float devDepth = rawDepth;
            #else
                float devDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, rawDepth);
            #endif
            float3 worldPos = ComputeWorldSpacePosition(uv, devDepth, UNITY_MATRIX_I_VP);
            viewDirWS = SafeNormalize(worldPos - camPosWS);
            pixY = worldPos.y;
        }

        float heightFactor = ASV2_ComputeHeightFogFactor(camPosWS.y, pixY);
        return distAirMass * heightFactor;
    }

    // 3. Pass 2 のベース色と Pass 3 の sharpAerial で 100% 同じ式を使う共通関数（差分逆算の破綻防止）
    float3 ASV2_ApplyAerialAndMie(float3 baseColor, float centerAirMass, bool isSkybox, float3 viewDirWS)
    {
        float effectiveAirMass = isSkybox ? (centerAirMass * _SkyboxInfluence) : centerAirMass;
        float extinction = effectiveAirMass * _ContrastReduction;
        float atmosphereBlend = effectiveAirMass * _AtmosphereDensity;

        float3 aerial = baseColor * (1.0f - extinction) + _AtmosphereColor.rgb * atmosphereBlend;
        float3 mie = ASV2_ComputeMieScatter(viewDirWS, centerAirMass, isSkybox);
        return aerial + mie;
    }

    // 明るい光（発光・ライト）を滑らかに抽出しつつ、遠景の1ピクセルHDR暴れを抑える関数
    float3 ExtractBrightLight(float3 color, float threshold, float knee)
    {
        color = max(0.0f, min(color, 64.0f));
        float brightness = max(color.r, max(color.g, color.b));

        // 輝度上限ソフトクランプ（カメラ移動時のサブピクセルHDRスパイクを抑える）
        float maxLum = max(1.0f, _MaxScatterLuminance);
        if (brightness > 0.0001f)
        {
            float clampedBrightness = maxLum * (brightness / (brightness + maxLum * 0.5f));
            color *= lerp(1.0f, clampedBrightness / brightness, _AntiFlickerStrength);
            brightness = max(color.r, max(color.g, color.b));
        }

        float soft = brightness - threshold + knee;
        soft = clamp(soft, 0.0f, 2.0f * knee);
        soft = (soft * soft) / (4.0f * knee + 0.0001f);
        float contribution = max(soft, brightness - threshold) / max(brightness, 0.0001f);
        return color * saturate(contribution);
    }

    // Karis Average 重み（1ピクセルだけ極端に明るい輝点が平均を支配してチラつくのを防ぐ）
    float ComputeKarisWeight(float3 col)
    {
        float luma = Luminance(max(0.0f, col));
        return 1.0f / (1.0f + luma * _AntiFlickerStrength * 0.5f);
    }

    // -------------------------------------------------------------------------
    // 共通コア：大気散乱＆遠景ボケ計算（Pass 0 の Full-Res と Pass 2 の Half-Res で共有）
    // -------------------------------------------------------------------------
    float4 EvaluateAtmosphericScatteringCore(float2 uv, float radiusScale)
    {
        half4 centerColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        float centerRawDepth = SampleSceneDepth(uv);
        bool centerIsSkybox = IsSkyboxDepth(centerRawDepth);
        float farClip = ASV2_GetEffectiveFarClip();
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);

        float3 viewDirWS;
        float centerAirMass = ASV2_ComputeCenterAirMass(uv, centerRawDepth, centerLinearDepth, centerIsSkybox, viewDirWS);

        // アスペクト比補正付きのテクセルスケール
        float2 aspectScale = float2(_ScreenParams.y / max(1.0f, _ScreenParams.x), 1.0f) * 0.0015f * radiusScale;

        // カメラ移動時のザラつきを防ぐため、デフォルト(_NoiseJitter = 0)では固定角で時間的に完全安定させる
        float noise = InterleavedGradientNoise(uv * _ScreenParams.xy);
        float baseAngle = noise * 6.2831853f * _NoiseJitter;

        float centerKaris = ComputeKarisWeight(centerColor.rgb);
        float3 blurredColorSum = centerColor.rgb * centerKaris;
        float blurWeightSum = centerKaris;

        float3 centerBright = ExtractBrightLight(centerColor.rgb, _ScatterThreshold, _ScatterSoftKnee);
        float3 scatteredLightSum = centerIsSkybox ? 0.0f : (centerBright * centerAirMass);
        float scatterWeightSum = 1.0f;

        float blurRadiusUV = _BlurRadius * centerAirMass;
        float scatterRadiusUV = _ScatterRadius;

        UNITY_UNROLL
        for (int i = 0; i < SAMPLE_COUNT; i++)
        {
            float fi = (float)i + 0.5f;
            float r = sqrt(fi / (float)SAMPLE_COUNT);
            float theta = fi * GOLDEN_ANGLE + baseAngle;
            float s, c;
            sincos(theta, s, c);
            float2 diskDir = float2(c, s) * r * aspectScale;

            // --- (A) 遠景の像のボケ（Atmospheric Blur） ---
            if (blurRadiusUV > 0.01f && !centerIsSkybox)
            {
                float2 blurUV = clamp(uv + diskDir * blurRadiusUV, 0.001f, 0.999f);
                float sampleRawDepth = SampleSceneDepth(blurUV);
                bool sampleIsSkybox = IsSkyboxDepth(sampleRawDepth);
                float sampleLinearDepth = sampleIsSkybox ? farClip : LinearEyeDepth(sampleRawDepth, _ZBufferParams);
                float sampleAirMass = ASV2_ComputeDistanceAirMass(sampleLinearDepth, sampleIsSkybox);

                // 手前・奥の両方向に対する輪郭保護（バイラテラル重み付け）
                float depthDiff = abs(centerLinearDepth - sampleLinearDepth);
                float depthTolerance = max(4.0f, centerLinearDepth * 0.22f);
                float bilateralWeight = saturate(1.0f - (depthDiff / depthTolerance) * _DepthEdgeSharpness);

                float3 sampleCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, blurUV).rgb;
                float karisW = ComputeKarisWeight(sampleCol);
                float wBlur = bilateralWeight * (sampleIsSkybox ? 0.0f : 1.0f) * sampleAirMass * karisW;

                blurredColorSum += sampleCol * wBlur;
                blurWeightSum += wBlur;
            }

            // --- (B) 発光・ライトの空気チリ散乱（Light In-Scattering） ---
            float2 scatterUV = clamp(uv + diskDir * scatterRadiusUV, 0.001f, 0.999f);
            float scRawDepth = SampleSceneDepth(scatterUV);
            bool scIsSkybox = IsSkyboxDepth(scRawDepth);
            float scLinearDepth = scIsSkybox ? farClip : LinearEyeDepth(scRawDepth, _ZBufferParams);
            float scAirMass = ASV2_ComputeDistanceAirMass(scLinearDepth, scIsSkybox);

            if (!scIsSkybox && scAirMass > 0.01f)
            {
                float3 scCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, scatterUV).rgb;
                float3 brightLight = ExtractBrightLight(scCol, _ScatterThreshold, _ScatterSoftKnee);

                float radialFalloff = exp(-r * r * 2.0f);
                float scatterWeight = radialFalloff * scAirMass * ComputeKarisWeight(brightLight);

                // 光源が自分（中心の物体）の背後にある場合、手前の物体を光が突き抜けないように遮蔽
                if (!centerIsSkybox && scLinearDepth > centerLinearDepth + 8.0f)
                {
                    float occlude = saturate(1.0f - (scLinearDepth - centerLinearDepth) / max(10.0f, centerLinearDepth * 0.35f));
                    scatterWeight *= occlude;
                }

                scatteredLightSum += brightLight * scatterWeight;
            }
            scatterWeightSum += exp(-r * r * 2.0f);
        }

        // 遠景ボケを適用したベース画像
        float3 baseResult = blurredColorSum / max(0.0001f, blurWeightSum);

        // 大気によるコントラスト減衰＆環境散乱＋Mie散乱（共通関数）
        float3 aerialColor = ASV2_ApplyAerialAndMie(baseResult, centerAirMass, centerIsSkybox, viewDirWS);

        // そこに存在する発光・ライトのチリ散乱光（In-Scattering）の合成
        float3 avgScatteredLight = scatteredLightSum / max(0.0001f, scatterWeightSum);
        float scatterLuma = Luminance(avgScatteredLight);
        float3 tintedScatter = scatterLuma * _ScatterTint.rgb;
        float3 finalScatteredLight = lerp(tintedScatter, avgScatteredLight * _ScatterTint.rgb, _LightColorPreservation);

        float receiverAirMask = centerIsSkybox ? saturate(centerAirMass * _SkyboxInfluence * 2.0f) : centerAirMass;
        float3 resultColor = aerialColor + finalScatteredLight * (_ScatterIntensity * receiverAirMask);

        return float4(resultColor, centerAirMass);
    }

    // =========================================================================
    // Pass 0: Full-Res Single Pass (フル解像度1パス直接実行モード)
    // =========================================================================
    half4 FragFullResSingle(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;

        half4 centerColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        if (_OverallStrength <= 0.001f)
        {
            return centerColor;
        }

        float4 scattered = EvaluateAtmosphericScatteringCore(uv, 1.0f);
        float3 finalRGB = lerp(centerColor.rgb, scattered.rgb, saturate(_OverallStrength));
        return half4(finalRGB, centerColor.a);
    }

    // =========================================================================
    // Pass 1: Half-Res Downsample & Anti-Flicker Prefilter (1/2解像度プリフィルタ)
    // =========================================================================
    half4 FragHalfResPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 texel = _BlitTexture_TexelSize.xy;

        float centerRawDepth = SampleSceneDepth(uv);
        bool centerIsSkybox = IsSkyboxDepth(centerRawDepth);
        float farClip = ASV2_GetEffectiveFarClip();
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);

        float3 dummyViewDir;
        float centerAirMass = ASV2_ComputeCenterAirMass(uv, centerRawDepth, centerLinearDepth, centerIsSkybox, dummyViewDir);
        float depthTol = max(3.0f, centerLinearDepth * 0.15f);

        float3 centerCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        float w0 = ComputeKarisWeight(centerCol) * 2.0f;
        float3 sumCol = centerCol * w0;
        float sumW = w0;

        // 近景（centerAirMassが小さい領域）やSkyboxでは縮小時の5タップ拡散を抑え、近景ディテールの滲みを防ぐ
        float prefilterSpread = centerIsSkybox ? 0.0f : smoothstep(0.05f, 0.40f, centerAirMass);

        // 4隅の半テクセルオフセット
        static const float2 offsets[4] =
        {
            float2(-0.95f, -0.95f),
            float2( 0.95f, -0.95f),
            float2(-0.95f,  0.95f),
            float2( 0.95f,  0.95f)
        };

        if (prefilterSpread > 0.001f)
        {
            UNITY_UNROLL
            for (int i = 0; i < 4; i++)
            {
                float2 sUV = clamp(uv + offsets[i] * texel * prefilterSpread, 0.001f, 0.999f);
                float sRawDepth = SampleSceneDepth(sUV);
                bool sIsSkybox = IsSkyboxDepth(sRawDepth);
                float sLinearDepth = sIsSkybox ? farClip : LinearEyeDepth(sRawDepth, _ZBufferParams);

                float dDiff = abs(centerLinearDepth - sLinearDepth);
                float bilateral = saturate(1.0f - (dDiff / depthTol) * _DepthEdgeSharpness);

                float3 sCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sUV).rgb;
                float w = max(0.05f, bilateral) * ComputeKarisWeight(sCol) * prefilterSpread;
                sumCol += sCol * w;
                sumW += w;
            }
        }

        float3 prefiltered = sumCol / max(0.0001f, sumW);
        return half4(prefiltered, centerAirMass);
    }

    // =========================================================================
    // Pass 2: Half-Res Scattering & Blur (1/2解像度での高速・滑らか散乱計算)
    // =========================================================================
    half4 FragHalfResScattering(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float4 scattered = EvaluateAtmosphericScatteringCore(uv, 1.05f);
        return half4(scattered);
    }

    // =========================================================================
    // Pass 3: Full-Res Bilateral Upsample & Composite (深度保護アップサンプル合成)
    // =========================================================================
    half4 FragFullResComposite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;

        half4 fullResColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        if (_OverallStrength <= 0.001f)
        {
            return fullResColor;
        }

        float centerRawDepth = SampleSceneDepth(uv);
        bool centerIsSkybox = IsSkyboxDepth(centerRawDepth);
        float farClip = ASV2_GetEffectiveFarClip();
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);

        float3 viewDirWS;
        float centerAirMass = ASV2_ComputeCenterAirMass(uv, centerRawDepth, centerLinearDepth, centerIsSkybox, viewDirWS);

        // 完全に手前の領域（StartDistance付近〜手前）なら、100%フル解像度の元画像をそのまま返して輪郭劣化ゼロにする
        if (!centerIsSkybox && centerAirMass <= 0.002f)
        {
            return fullResColor;
        }

        // 1/2解像度テクスチャの近傍4点をバイラテラル（深度差）重み付けでアップサンプル
        float2 halfTexel = _ASV2_HalfResScatteringTex_TexelSize.xy;
        if (halfTexel.x <= 0.0f)
        {
            halfTexel = (_ScreenParams.zw - 1.0f) * 2.0f;
        }

        // 近景やSkyboxではアップサンプル時の4タップ拡散を抑えて二重ボケを防ぐ
        float upSpread = centerIsSkybox ? 0.0f : smoothstep(0.05f, 0.35f, centerAirMass);

        float depthTol = max(3.5f, centerLinearDepth * 0.18f);
        float3 upsampledSum = 0.0f;
        float weightSum = 0.0f;

        static const float2 upOffsets[4] =
        {
            float2(-0.5f, -0.5f),
            float2( 0.5f, -0.5f),
            float2(-0.5f,  0.5f),
            float2( 0.5f,  0.5f)
        };

        UNITY_UNROLL
        for (int i = 0; i < 4; i++)
        {
            float2 hUV = clamp(uv + upOffsets[i] * halfTexel * upSpread, 0.001f, 0.999f);
            float hRawDepth = SampleSceneDepth(hUV);
            bool hIsSkybox = IsSkyboxDepth(hRawDepth);
            float hLinearDepth = hIsSkybox ? farClip : LinearEyeDepth(hRawDepth, _ZBufferParams);

            float dDiff = abs(centerLinearDepth - hLinearDepth);
            float w = saturate(1.0f - (dDiff / depthTol) * _DepthEdgeSharpness);
            if (centerIsSkybox != hIsSkybox)
            {
                w *= 0.05f;
            }
            w = max(0.001f, w);

            float3 hScatterCol = SAMPLE_TEXTURE2D_X(_ASV2_HalfResScatteringTex, sampler_LinearClamp, hUV).rgb;
            upsampledSum += hScatterCol * w;
            weightSum += w;
        }

        float3 halfResScattered = upsampledSum / max(0.0001f, weightSum);

        float blurDistanceFactor = centerIsSkybox ? 0.0f : smoothstep(0.08f, 0.85f, centerAirMass);
        float blurBlend = saturate(blurDistanceFactor * saturate(_BlurRadius * 0.4f));

        // 元のフル解像度に大気散乱だけを乗せた色（Pass 2 と 100% 同一の ASV2_ApplyAerialAndMie を使用）
        float3 sharpAerial = ASV2_ApplyAerialAndMie(fullResColor.rgb, centerAirMass, centerIsSkybox, viewDirWS);

        // Half-Res側で増えた光散乱（In-Scattering）成分の抽出
        float scatterDistanceMask = centerIsSkybox
            ? saturate(_SkyboxInfluence * 2.0f)
            : smoothstep(0.04f, 0.40f, centerAirMass);
        float3 pureInScattering = max(0.0f, halfResScattered - sharpAerial) * scatterDistanceMask;
        float3 sharpWithScatter = sharpAerial + pureInScattering;

        float3 targetColor = lerp(sharpWithScatter, halfResScattered, blurBlend);
        float3 finalRGB = lerp(fullResColor.rgb, targetColor, saturate(_OverallStrength));

        return half4(finalRGB, fullResColor.a);
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        // Pass 0: Full-Res Single Pass
        Pass
        {
            Name "AtmosphericScatteringV2_FullRes"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFullResSingle
            ENDHLSL
        }

        // Pass 1: Half-Res Downsample & Anti-Flicker Prefilter
        Pass
        {
            Name "AtmosphericScatteringV2_HalfResPrefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHalfResPrefilter
            ENDHLSL
        }

        // Pass 2: Half-Res Main Scattering & Blur
        Pass
        {
            Name "AtmosphericScatteringV2_HalfResMain"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHalfResScattering
            ENDHLSL
        }

        // Pass 3: Full-Res Bilateral Upsample & Composite
        Pass
        {
            Name "AtmosphericScatteringV2_FullResComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFullResComposite
            ENDHLSL
        }
    }
}
