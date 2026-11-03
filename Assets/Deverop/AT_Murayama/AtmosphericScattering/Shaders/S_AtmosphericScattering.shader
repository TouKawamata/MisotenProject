Shader "Hidden/AT_Murayama/AtmosphericScattering"
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

        [Header(Atmospheric Blur Settings)]
        _BlurRadius ("Far Blur Radius", Range(0.0, 8.0)) = 2.2
        _DepthEdgeSharpness ("Foreground Edge Protection", Range(0.0, 1.0)) = 0.85

        [Header(Light In Scattering Settings)]
        _ScatterIntensity ("Light Scatter Intensity", Range(0.0, 4.0)) = 1.55
        _ScatterRadius ("Light Scatter Radius", Range(0.5, 16.0)) = 7.0
        _ScatterThreshold ("Light Threshold", Range(0.0, 2.0)) = 0.35
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
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    // Half-Res モード時に Pass 2 の結果を受け取るグローバルテクスチャ
    TEXTURE2D_X(_HalfResScatteringTex);
    float4 _HalfResScatteringTex_TexelSize;

    float _OverallStrength;

    float _StartDistance;
    float _MaxDistance;
    float _DensityCurve;
    float _SkyboxInfluence;

    float _BlurRadius;
    float _DepthEdgeSharpness;

    float _ScatterIntensity;
    float _ScatterRadius;
    float _ScatterThreshold;
    float _ScatterSoftKnee;
    float4 _ScatterTint;
    float _LightColorPreservation;

    float _MaxScatterLuminance;
    float _AntiFlickerStrength;
    float _NoiseJitter;

    float4 _AtmosphereColor;
    float _AtmosphereDensity;
    float _ContrastReduction;

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

    // カメラからの距離（メートル）から「空気の厚み（Optical Depth: 0.0〜1.0）」を算出
    float ComputeAirMass(float linearDepth, bool isSkybox)
    {
        float maxDist = max(_StartDistance + 1.0f, _MaxDistance);
        float normalizedDist = saturate((linearDepth - _StartDistance) / (maxDist - _StartDistance));
        float airMass = pow(normalizedDist, max(0.1f, _DensityCurve));

        if (isSkybox)
        {
            return 1.0f;
        }
        return airMass;
    }

    // 明るい光（発光・ライト）を滑らかに抽出しつつ、遠景の1ピクセルHDR暴れ（ファイアフライ点滅）を抑える関数
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
        // Far Clip が非常に大きいシーンでも破綻しない実距離取得
        float farClip = max(_MaxDistance, _ProjectionParams.z);
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);
        float centerAirMass = ComputeAirMass(centerLinearDepth, centerIsSkybox);

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
                float sampleAirMass = ComputeAirMass(sampleLinearDepth, sampleIsSkybox);

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
            float scAirMass = ComputeAirMass(scLinearDepth, scIsSkybox);

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

        // 大気によるコントラスト減衰＆環境散乱（Aerial Perspective）
        float effectiveAirMass = centerIsSkybox ? (centerAirMass * _SkyboxInfluence) : centerAirMass;
        float extinction = effectiveAirMass * _ContrastReduction;
        float atmosphereBlend = effectiveAirMass * _AtmosphereDensity;

        float3 aerialColor = baseResult * (1.0f - extinction) + _AtmosphereColor.rgb * atmosphereBlend;

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
    // フル解像度の周囲16ピクセル領域（5タップ・バイリニア）を深度＆Karis重みで滑らかに縮小し、
    // 1ピクセル未満の窓やネオンがサンプル点の隙間に落ちてチラつく現象を根本から防ぐ。
    // ※近景やSkybox深度では不要な縮小ボケを起こさないよう、距離（centerAirMass）に応じてフィルタ広がりを制御する。
    // =========================================================================
    half4 FragHalfResPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 texel = _BlitTexture_TexelSize.xy;

        float centerRawDepth = SampleSceneDepth(uv);
        bool centerIsSkybox = IsSkyboxDepth(centerRawDepth);
        float farClip = max(_MaxDistance, _ProjectionParams.z);
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);
        float centerAirMass = ComputeAirMass(centerLinearDepth, centerIsSkybox);
        float depthTol = max(3.0f, centerLinearDepth * 0.15f);

        float3 centerCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        float w0 = ComputeKarisWeight(centerCol) * 2.0f;
        float3 sumCol = centerCol * w0;
        float sumW = w0;

        // 近景（centerAirMassが小さい領域）やSkyboxでは縮小時の5タップ拡散を抑え、近景ディテールの滲みを防ぐ
        float prefilterSpread = centerIsSkybox ? 0.0f : smoothstep(0.05f, 0.40f, centerAirMass);

        // 4隅の半テクセルオフセット（バイリニアサンプラーにより周囲4x4=16ピクセルを効率的にカバー）
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

                // 手前と奥の境界を跨いで色を混ぜない深度重み付け
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
        // プリフィルタ済みの滑らかな1/2解像度テクスチャから散乱とボケを計算
        float4 scattered = EvaluateAtmosphericScatteringCore(uv, 1.05f);
        return half4(scattered);
    }

    // =========================================================================
    // Pass 3: Full-Res Bilateral Upsample & Composite (深度保護アップサンプル合成)
    // 手前のモノレールやビル、ホログラム旗、自機の輪郭をフル解像度（1ピクセル単位）でパキッと保護しながら、
    // 1/2解像度の滑らかな大気散乱＆遠景ボケを合成する。
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
        float farClip = max(_MaxDistance, _ProjectionParams.z);
        float centerLinearDepth = centerIsSkybox ? farClip : LinearEyeDepth(centerRawDepth, _ZBufferParams);
        float centerAirMass = ComputeAirMass(centerLinearDepth, centerIsSkybox);

        // 完全に手前の領域（StartDistance付近〜手前）なら、100%フル解像度の元画像をそのまま返して輪郭劣化ゼロにする
        if (!centerIsSkybox && centerAirMass <= 0.002f)
        {
            return fullResColor;
        }

        // 1/2解像度テクスチャの近傍4点をバイラテラル（深度差）重み付けでアップサンプル
        // → 手前のビルや旗のエッジに奥の1/2解像度ボケが滲む「ハロー現象」を完全に防ぐ
        float2 halfTexel = _HalfResScatteringTex_TexelSize.xy;
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
            // Skyboxと建物の境界では同じ種別同士を優先
            if (centerIsSkybox != hIsSkybox)
            {
                w *= 0.05f;
            }
            w = max(0.001f, w);

            float3 hScatterCol = SAMPLE_TEXTURE2D_X(_HalfResScatteringTex, sampler_LinearClamp, hUV).rgb;
            upsampledSum += hScatterCol * w;
            weightSum += w;
        }

        float3 halfResScattered = upsampledSum / max(0.0001f, weightSum);

        // Full-Resパス(Pass 0)と挙動を統一：
        // ・Skybox深度のピクセルはHalf-Resボケ画像に置換しない（blurBlend = 0.0f）
        // ・+0.35fの固定バイアスを廃止し、近景（centerAirMass <= 0.08）では100%フル解像度（blurBlend = 0.0f）を維持する
        float blurDistanceFactor = centerIsSkybox ? 0.0f : smoothstep(0.08f, 0.85f, centerAirMass);
        float blurBlend = saturate(blurDistanceFactor * saturate(_BlurRadius * 0.4f));

        // 元のフル解像度に大気散乱だけを乗せた色（シャープな輪郭・芯）
        float effectiveAirMass = centerIsSkybox ? (centerAirMass * _SkyboxInfluence) : centerAirMass;
        float extinction = effectiveAirMass * _ContrastReduction;
        float atmosphereBlend = effectiveAirMass * _AtmosphereDensity;
        float3 sharpAerial = fullResColor.rgb * (1.0f - extinction) + _AtmosphereColor.rgb * atmosphereBlend;

        // Half-Res側で増えた光散乱（In-Scattering）成分を取り出す際、
        // 単なる「1/2解像度化による縮小ボケ差分（HalfResBase - FullResBase）」が近景の走査線や輪郭に滲まないよう距離マスクを掛ける
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
            Name "AtmosphericScattering_FullRes"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFullResSingle
            ENDHLSL
        }

        // Pass 1: Half-Res Downsample & Anti-Flicker Prefilter
        Pass
        {
            Name "AtmosphericScattering_HalfResPrefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHalfResPrefilter
            ENDHLSL
        }

        // Pass 2: Half-Res Main Scattering & Blur
        Pass
        {
            Name "AtmosphericScattering_HalfResMain"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHalfResScattering
            ENDHLSL
        }

        // Pass 3: Full-Res Bilateral Upsample & Composite
        Pass
        {
            Name "AtmosphericScattering_FullResComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFullResComposite
            ENDHLSL
        }
    }
}
