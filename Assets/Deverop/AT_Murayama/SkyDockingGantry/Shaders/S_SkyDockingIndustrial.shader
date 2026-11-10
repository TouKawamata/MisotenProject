Shader "Custom/SkyDockingGantry/S_SkyDockingIndustrial"
{
    Properties
    {
        [Header(Base Industrial Material)]
        _BaseColor ("Base Color", Color) = (0.92, 0.94, 0.96, 1.0)
        _DarkAccentColor ("Dark Accent Color", Color) = (0.15, 0.18, 0.22, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.35
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.65

        [Header(Procedural Hazard Stripes)]
        [Toggle] _HazardEnable ("Enable Hazard Stripes", Float) = 1.0
        _HazardWidth ("Hazard Border Width (UV 0..0.5)", Range(0.0, 0.35)) = 0.08
        _HazardTiling ("Hazard Stripe Density", Range(4.0, 60.0)) = 24.0
        _HazardColorA ("Stripe Color A (Yellow/White)", Color) = (0.98, 0.82, 0.05, 1.0)
        _HazardColorB ("Stripe Color B (Black/Navy/Red)", Color) = (0.12, 0.12, 0.14, 1.0)

        [Header(Procedural Grating Floor)]
        [Toggle] _GratingEnable ("Enable Walkway Grating Cutout", Float) = 0.0
        _GratingTiling ("Grating Tiling", Range(4.0, 64.0)) = 24.0
        _GratingBarThickness ("Grating Bar Thickness", Range(0.05, 0.45)) = 0.18
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Header(Procedural Anti Slip Tread)]
        [Toggle] _TreadEnable ("Enable Center Diamond Tread", Float) = 1.0
        _TreadTiling ("Diamond Tread Tiling", Range(10.0, 80.0)) = 36.0

        [Header(Deck Aspect Correction)]
        _Aspect ("Deck Aspect Ratio (Depth / Width)", Float) = 0.75

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 2
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest LEqual

        // ─────────────────────────────────────────────────────────────
        // 1. UniversalForward Lit Pass (URP Forward+ Compatible)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DarkAccentColor;
                float4 _HazardColorA;
                float4 _HazardColorB;
                float _Metallic;
                float _Smoothness;
                float _HazardEnable;
                float _HazardWidth;
                float _HazardTiling;
                float _GratingEnable;
                float _GratingTiling;
                float _GratingBarThickness;
                float _Cutoff;
                float _TreadEnable;
                float _TreadTiling;
                float _Aspect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float3 normalWS = normalize(input.normalWS);
                float3 posWS = input.positionWS;

                // 1. 基本マテリアル色
                float3 baseCol = _BaseColor.rgb;
                float metallic = _Metallic;
                float smoothness = _Smoothness;

                // 水平床面判定（上面および底面のみハザード帯・グレーチングを適用、側面は堅牢な金属リムフレーム）
                float isTopOrBottom = step(0.65, abs(normalWS.y));

                // アスペクト補正（奥行き/幅 比率による厳密な45度ストライプ・正方形金網・均等マージン）
                float aspect = (_Aspect > 0.001) ? _Aspect : 1.0;
                float2 uvAspect = float2(uv.x, uv.y * aspect);

                bool isWalkingDeck = (isTopOrBottom > 0.5) && (uv.x >= 0.0) && (uv.y >= 0.0);

                if (isWalkingDeck)
                {
                    // 2. デッキ外周マージン計算（Perimeter Hazard Border: 4辺均等物理幅）
                    float distEdge = min(min(uv.x, 1.0 - uv.x), min(uv.y * aspect, (1.0 - uv.y) * aspect));
                    float isBorder = step(distEdge, _HazardWidth);

                    // 3. プロシージャル 45度警戒ハザード縞（黄色/黒、白/赤等）
                    if (_HazardEnable > 0.5)
                    {
                        float diag = (uvAspect.x + uvAspect.y) * _HazardTiling;
                        float stripe = step(0.5, frac(diag));
                        float3 hazardCol = lerp(_HazardColorA.rgb, _HazardColorB.rgb, stripe);

                        baseCol = lerp(baseCol, hazardCol, isBorder);
                        smoothness = lerp(smoothness, 0.45, isBorder);
                        metallic = lerp(metallic, 0.08, isBorder);
                    }

                    // 4. デッキ中央領域の質感（グレーチング金網 または 滑り止めダイヤ目地）
                    if (isBorder < 0.5)
                    {
                        if (_GratingEnable > 0.5)
                        {
                            // 工業用グレーチング床：格子状にアルファクリップで下界が見通せる（正方形グリッド）
                            float2 gUV = frac(uvAspect * _GratingTiling) - 0.5;
                            float bar = max(abs(gUV.x), abs(gUV.y));
                            float isBar = step(0.5 - _GratingBarThickness, bar);
                            clip(isBar - _Cutoff);

                            baseCol = _DarkAccentColor.rgb;
                            metallic = 0.85;
                            smoothness = 0.55;
                        }
                        else if (_TreadEnable > 0.5)
                        {
                            // 滑り止めチェッカープレート（正方形ダイヤ目）
                            float2 tUV = frac(uvAspect * _TreadTiling) - 0.5;
                            float d1 = abs(tUV.x + tUV.y);
                            float d2 = abs(tUV.x - tUV.y);
                            float diamond = saturate(1.0 - max(d1, d2) * 2.8);
                            baseCol = lerp(baseCol, baseCol * 1.15 + float3(0.05, 0.05, 0.05), diamond);
                        }
                    }
                }
                else
                {
                    // 垂直側面・リム補強フレーム：穴あきなしの重厚な工業金属フレーム
                    baseCol = _DarkAccentColor.rgb;
                    metallic = 0.60;
                    smoothness = 0.55;
                }

                // 5. URP Forward+ 物理ベースライティング（太陽光＋影）
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 diffuse = baseCol * mainLight.color * (NdotL * shadowAtten);

                // 鏡面光沢（MainLight Specular Sheen）
                float3 viewDirWS = normalize(GetCameraPositionWS() - posWS);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(smoothness * 9.0 + 1.0);
                float spec = pow(NdotH, specPower) * (metallic * 0.75 + 0.15);
                float3 specular = mainLight.color * (spec * shadowAtten * NdotL);

                // 環境光（Spherical Harmonics GI）
                float3 ambient = SampleSH(normalWS) * baseCol;

                float3 finalRGB = diffuse + specular + ambient;

                // 追加ライト（Forward+ 点光源・スポットライト等）
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, posWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    float3 addLightCol = addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation;
                    finalRGB += baseCol * addLightCol * addNdotL;
                }
                #endif

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 2. ShadowCaster Pass (太陽光による立体トラス＆金網の影投影)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DarkAccentColor;
                float4 _HazardColorA;
                float4 _HazardColorB;
                float _Metallic;
                float _Smoothness;
                float _HazardEnable;
                float _HazardWidth;
                float _HazardTiling;
                float _GratingEnable;
                float _GratingTiling;
                float _GratingBarThickness;
                float _Cutoff;
                float _TreadEnable;
                float _TreadTiling;
                float _Aspect;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = input.uv;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.normalWS   = normalWS;

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // グレーチング床の場合は影も格子状にくり抜く（水平床面かつ有効UVのみ）
                float aspect = (_Aspect > 0.001) ? _Aspect : 1.0;
                if (_GratingEnable > 0.5 && abs(input.normalWS.y) > 0.65 && input.uv.x >= 0.0 && input.uv.y >= 0.0)
                {
                    float distEdge = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y * aspect, (1.0 - input.uv.y) * aspect));
                    if (distEdge > _HazardWidth)
                    {
                        float2 uvAspect = float2(input.uv.x, input.uv.y * aspect);
                        float2 gUV = frac(uvAspect * _GratingTiling) - 0.5;
                        float bar = max(abs(gUV.x), abs(gUV.y));
                        float isBar = step(0.5 - _GratingBarThickness, bar);
                        clip(isBar - _Cutoff);
                    }
                }
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 3. DepthOnly Pass
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DarkAccentColor;
                float4 _HazardColorA;
                float4 _HazardColorB;
                float _Metallic;
                float _Smoothness;
                float _HazardEnable;
                float _HazardWidth;
                float _HazardTiling;
                float _GratingEnable;
                float _GratingTiling;
                float _GratingBarThickness;
                float _Cutoff;
                float _TreadEnable;
                float _TreadTiling;
                float _Aspect;
            CBUFFER_END

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                return output;
            }

            half4 fragDepth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float aspect = (_Aspect > 0.001) ? _Aspect : 1.0;
                if (_GratingEnable > 0.5 && abs(input.normalWS.y) > 0.65 && input.uv.x >= 0.0 && input.uv.y >= 0.0)
                {
                    float distEdge = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y * aspect, (1.0 - input.uv.y) * aspect));
                    if (distEdge > _HazardWidth)
                    {
                        float2 uvAspect = float2(input.uv.x, input.uv.y * aspect);
                        float2 gUV = frac(uvAspect * _GratingTiling) - 0.5;
                        float bar = max(abs(gUV.x), abs(gUV.y));
                        float isBar = step(0.5 - _GratingBarThickness, bar);
                        clip(isBar - _Cutoff);
                    }
                }
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 4. DepthNormals Pass (SSAO / Forward+ 法線バッファ)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepthNormals
            #pragma fragment fragDepthNormals
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _DarkAccentColor;
                float4 _HazardColorA;
                float4 _HazardColorB;
                float _Metallic;
                float _Smoothness;
                float _HazardEnable;
                float _HazardWidth;
                float _HazardTiling;
                float _GratingEnable;
                float _GratingTiling;
                float _GratingBarThickness;
                float _Cutoff;
                float _TreadEnable;
                float _TreadTiling;
                float _Aspect;
            CBUFFER_END

            Varyings vertDepthNormals(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                return output;
            }

            half4 fragDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float aspect = (_Aspect > 0.001) ? _Aspect : 1.0;
                if (_GratingEnable > 0.5 && abs(input.normalWS.y) > 0.65 && input.uv.x >= 0.0 && input.uv.y >= 0.0)
                {
                    float distEdge = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y * aspect, (1.0 - input.uv.y) * aspect));
                    if (distEdge > _HazardWidth)
                    {
                        float2 uvAspect = float2(input.uv.x, input.uv.y * aspect);
                        float2 gUV = frac(uvAspect * _GratingTiling) - 0.5;
                        float bar = max(abs(gUV.x), abs(gUV.y));
                        float isBar = step(0.5 - _GratingBarThickness, bar);
                        clip(isBar - _Cutoff);
                    }
                }
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
