Shader "Custom/SkyAquaCascade/S_AquaCascadeWaterfall"
{
    Properties
    {
        [Header(Fluid Textures and Flow Distortion)]
        _FluidNoiseTex ("Fluid Noise (R:Stream G:Foam B:Mist A:Sparkle)", 2D) = "white" {}
        _FlowDistortionTex ("Flow Curl Distortion (RG:Vector B:Turb A:Ripple)", 2D) = "bump" {}

        [Header(Layer and Cascade Dynamics)]
        _LayerMode ("Layer Mode (0=Core Liquid, 1=Outer Spray Veil)", Float) = 0.0
        _FlowSpeed ("Waterfall Flow Speed", Range(0.1, 5.0)) = 1.6
        _Turbulence ("Wind and Spray Turbulence", Range(0.0, 2.0)) = 0.45
        _StreamTilingX ("Horizontal Stream Tiling", Range(0.5, 12.0)) = 3.0
        _StreamTilingY ("Vertical Stream Tiling", Range(0.5, 16.0)) = 5.0

        [Header(Optical Refraction and Mist Blur)]
        _RefractionStrength ("Background Refraction Strength", Range(0.0, 0.12)) = 0.035
        _MistBlurRadius ("Lower Mist Frosted Blur Radius", Range(0.0, 0.04)) = 0.012
        _ShallowAquaColor ("Clear Upper Aqua Tint", Color) = (0.55, 0.90, 1.0, 0.65)
        _DeepCascadeColor ("Deep Core Cyan Tint", Color) = (0.18, 0.68, 0.92, 0.82)
        [HDR] _FoamColor ("Aerated Whitewater Foam (HDR)", Color) = (1.15, 1.22, 1.28, 0.95)
        _SpecularIntensity ("Sunlight Water Specular", Range(0.0, 5.0)) = 2.4
        _SpecularSmoothness ("Specular Sharpness", Range(8.0, 128.0)) = 48.0

        [Header(Evaporation and Soft Intersection)]
        _EvaporationStart ("Evaporation Fade Start (UV.y 0=Top, 1=Bottom)", Range(0.2, 0.95)) = 0.65
        _EdgeFadePower ("Grazing and Side Edge Softness", Range(0.5, 6.0)) = 2.0
        _SoftDepthDistance ("Wall Intersection Soft Fade (m)", Range(0.1, 8.0)) = 2.2
        _CameraFadeStart ("Camera Fly-Through Near Fade Start (m)", Range(0.5, 5.0)) = 1.2
        _CameraFadeEnd ("Camera Fly-Through Near Fade End (m)", Range(1.5, 12.0)) = 4.5

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (10=OneMinusSrcAlpha, 1=One)", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 0
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (4=LEqual, 8=Always)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                float4 vertColor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_FluidNoiseTex);
            SAMPLER(sampler_FluidNoiseTex);
            TEXTURE2D(_FlowDistortionTex);
            SAMPLER(sampler_FlowDistortionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FluidNoiseTex_ST;
                float4 _FlowDistortionTex_ST;
                float4 _ShallowAquaColor;
                float4 _DeepCascadeColor;
                float4 _FoamColor;
                float  _LayerMode;
                float  _FlowSpeed;
                float  _Turbulence;
                float  _StreamTilingX;
                float  _StreamTilingY;
                float  _RefractionStrength;
                float  _MistBlurRadius;
                float  _SpecularIntensity;
                float  _SpecularSmoothness;
                float  _EvaporationStart;
                float  _EdgeFadePower;
                float  _SoftDepthDistance;
                float  _CameraFadeStart;
                float  _CameraFadeEnd;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            // 5タップ高速水煙ブラー＆微小光学分散サンプリング
            float3 SampleRefractedMistScene(float2 screenUV, float2 refractOffset, float blurRadius)
            {
                const float2 kernel[5] = {
                    float2( 0.000,  0.000),
                    float2( 0.650,  0.250),
                    float2(-0.650, -0.250),
                    float2(-0.250,  0.650),
                    float2( 0.250, -0.650)
                };
                const float weights[5] = { 0.36, 0.16, 0.16, 0.16, 0.16 };

                float2 baseUV = clamp(screenUV + refractOffset, 0.002, 0.998);
                if (blurRadius <= 0.0005)
                {
                    return SampleSceneColor(baseUV);
                }

                float3 accum = float3(0.0, 0.0, 0.0);
                for (int i = 0; i < 5; i++)
                {
                    float2 off = kernel[i] * blurRadius;
                    float2 sampleUV = clamp(baseUV + off, 0.002, 0.998);
                    accum += SampleSceneColor(sampleUV) * weights[i];
                }
                return accum;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posOS = input.positionOS.xyz;
                float3 normOS = input.normalOS;
                float2 uv = input.uv;

                // 落下進行度 (0=放水口, 1=下端霧散)
                float fallProgress = saturate(uv.y);

                // 外側飛沫ベール層 (_LayerMode > 0.5) および下端での風揺らぎ・拡散ディスプレイスメント
                float isVeil = step(0.5, _LayerMode);
                float waveTime = _Time.y * (_FlowSpeed * 1.8);
                float windSwayX = sin(waveTime * 0.7 + posOS.y * 0.08 + uv.x * 4.0) * cos(waveTime * 0.45 - posOS.y * 0.05);
                float windSwayZ = cos(waveTime * 0.9 - posOS.y * 0.11 + uv.x * 3.14159);

                float turbAmp = _Turbulence * (0.15 + 0.85 * fallProgress) * (1.0 + isVeil * 1.35);
                posOS.x += windSwayX * turbAmp * 0.85;
                posOS.z += windSwayZ * turbAmp * 0.65 + normOS.z * (isVeil * fallProgress * _Turbulence * 0.8);

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.normalWS   = TransformObjectToWorldNormal(normOS);
                output.uv         = uv;
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.vertColor  = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float fallT = saturate(uv.y);
                float isVeil = step(0.5, _LayerMode);

                // 1. スクリーン座標・深度フェード（壁面交差消去 ＆ カメラ近接突き抜け防止）
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float surfaceEyeDepth = input.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float depthDiff = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                float softIntersection = saturate(depthDiff / max(0.05, _SoftDepthDistance));
                float cameraNearFade = smoothstep(_CameraFadeStart, max(_CameraFadeStart + 0.2, _CameraFadeEnd), surfaceEyeDepth);

                // 2. カールノイズFlow Mapによる多重流体スクロールUV
                float timeFlow = _Time.y * _FlowSpeed;
                float2 tiling = float2(_StreamTilingX, _StreamTilingY);

                // 下流に向かうほど重力加速でストレッチするUV効果
                float gravityStretch = 1.0 - 0.28 * fallT;
                float2 flowMapUV1 = uv * float2(tiling.x * 0.65, tiling.y * 0.45 * gravityStretch) + float2(0.0, -timeFlow * 0.55);
                float2 flowMapUV2 = uv * float2(tiling.x * 1.15, tiling.y * 0.80 * gravityStretch) + float2(0.37, -timeFlow * 0.82);

                float4 flowSample1 = SAMPLE_TEXTURE2D(_FlowDistortionTex, sampler_FlowDistortionTex, flowMapUV1);
                float4 flowSample2 = SAMPLE_TEXTURE2D(_FlowDistortionTex, sampler_FlowDistortionTex, flowMapUV2);

                float2 curlVec = ((flowSample1.rg - 0.5) + (flowSample2.rg - 0.5)) * (0.12 + 0.14 * _Turbulence);

                // 2層の速度差を持つメイン流体ノイズサンプリング
                float2 streamUV1 = uv * float2(tiling.x, tiling.y * gravityStretch) + float2(0.0, -timeFlow * 1.15) + curlVec;
                float2 streamUV2 = uv * float2(tiling.x * 1.7, tiling.y * 1.35 * gravityStretch) + float2(0.51, -timeFlow * 1.55) - curlVec * 0.75;

                float4 fluid1 = SAMPLE_TEXTURE2D(_FluidNoiseTex, sampler_FluidNoiseTex, streamUV1);
                float4 fluid2 = SAMPLE_TEXTURE2D(_FluidNoiseTex, sampler_FluidNoiseTex, streamUV2);

                // R: 水流筋フィラメント, G: 激しい泡・飛沫, B: ソフト水煙クラウド, A: 水滴スパークル
                float streamFilament = lerp(fluid1.r, fluid2.r, 0.45);
                float cellularFoam   = saturate(fluid1.g * 0.65 + fluid2.g * 0.65);
                float mistCloud      = lerp(fluid1.b, fluid2.b, 0.5);
                float sparklePoints  = fluid1.a * fluid2.a * 3.2;

                // 3. 放物線クレスト（上部飛び出し部）の激しい白泡と、下部蒸発ゾーンの水煙遷移
                float crestZone = smoothstep(0.0, 0.08, fallT) * (1.0 - smoothstep(0.08, 0.35, fallT));
                float evapStart = clamp(_EvaporationStart, 0.15, 0.95);
                float evapProgress = smoothstep(evapStart, 1.0, fallT);

                // 下端の空中完全霧散（Evaporation Fade-Out）: ノイズで有機的に千切れながら100%ゼロへ溶け込む
                float organicBreakup = lerp(streamFilament * 0.5 + mistCloud * 0.5, mistCloud, evapProgress);
                float evaporationMask = smoothstep(1.0, evapStart, fallT + (1.0 - organicBreakup) * 0.24 * evapProgress);
                evaporationMask *= smoothstep(1.0, 0.93, fallT); // 最下端で確実に0へ収束

                // 上端スパウト接続部のソフトイン
                float topSpoutBlend = smoothstep(0.0, 0.025, fallT);

                // 4. 横方向エッジフェード ＆ 視線Grazing Angleフェード（板ポリ断面感を完全消去）
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                float NdotV = abs(dot(N, V));

                float sideArc = sin(saturate(uv.x) * 3.14159265);
                float sideFade = pow(saturate(sideArc), lerp(0.55, 1.15, isVeil));
                float grazingFade = pow(saturate(NdotV + 0.25), lerp(0.65, _EdgeFadePower, isVeil));

                // 5. ライティング（太陽光スペキュラ反射・ミー散乱透過光・環境SH）
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = normalize(mainLight.direction);

                // 流体ノイズから擬似法線摂動を生成し、水面の細かな煌めきを作る
                float3 perturbedN = normalize(N + float3(curlVec.x * 2.5, (streamFilament - 0.5) * 0.8, curlVec.y * 2.5));
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(perturbedN, H));
                float NdotL = saturate(dot(perturbedN, L)) * 0.65 + 0.35;

                float specPower = max(8.0, _SpecularSmoothness);
                float sunSpec = pow(NdotH, specPower) * _SpecularIntensity * (1.0 - isVeil * 0.45);
                sunSpec += sparklePoints * pow(NdotH, 16.0) * _SpecularIntensity * 1.4;

                // 逆光時の水煙前方散乱（Forward Subsurface Scattering）
                float backLight = pow(saturate(dot(V, -L)), 3.0) * 0.65;

                // 6. 背景リアルタイム屈折 ＆ 水煙フロストブラー (_CameraOpaqueTexture)
                float3 viewNormal = TransformWorldToViewDir(perturbedN);
                float2 refractOffset = (viewNormal.xy * 0.45 + curlVec * 2.2) * _RefractionStrength * sideFade * (1.0 - evapProgress * 0.6);
                float blurAmount = _MistBlurRadius * (0.25 + 0.75 * max(evapProgress, isVeil * 0.7));
                float3 sceneBg = SampleRefractedMistScene(screenUV, refractOffset, blurAmount);

                // 7. コア水流層 vs 外側飛沫ベール層のカラー＆アルファ合成
                float foamMask = saturate(
                    pow(streamFilament, 1.6) * 0.75 +
                    cellularFoam * (0.55 + 0.55 * crestZone) +
                    evapProgress * mistCloud * 0.45
                );

                float3 waterTint = lerp(_DeepCascadeColor.rgb, _ShallowAquaColor.rgb, saturate(streamFilament + crestZone * 0.4));
                float3 litWater = waterTint * (mainLight.color * NdotL + SampleSH(N) * 0.55 + mainLight.color * backLight);
                float3 litFoam  = _FoamColor.rgb * (mainLight.color * (0.75 + 0.25 * NdotL) + SampleSH(N) * 0.45);

                // 背景屈折と水体カラーのブレンド
                float3 coreColor = lerp(sceneBg * (0.88 + 0.18 * waterTint), litWater, _DeepCascadeColor.a * 0.65);
                coreColor = lerp(coreColor, litFoam, saturate(foamMask * _FoamColor.a));
                coreColor += mainLight.color * sunSpec;

                // ベール層はよりエアリーな純白飛沫＆ミスト
                float3 veilColor = lerp(sceneBg, litFoam, saturate(foamMask * 0.85 + mistCloud * 0.35));
                veilColor += mainLight.color * (sunSpec * 0.65 + backLight * 0.45);

                float3 finalRGB = lerp(coreColor, veilColor, isVeil);

                // アルファ計算：コア層は中心が濃く下端で霧散、ベール層は飛沫粒と水煙で柔らかく包む
                float baseDensity = lerp(
                    saturate(0.55 + foamMask * 0.45),
                    saturate(cellularFoam * 0.65 + mistCloud * 0.50) * 0.72,
                    isVeil
                );

                float finalAlpha = baseDensity
                    * evaporationMask
                    * topSpoutBlend
                    * sideFade
                    * grazingFade
                    * softIntersection
                    * cameraNearFade
                    * input.vertColor.a;

                return half4(finalRGB, saturate(finalAlpha));
            }
            ENDHLSL
        }
    }
}
