Shader "Custom/SkyAquaCascade/S_AquaSpoutStructure"
{
    Properties
    {
        [Header(Architectural Aqueduct Spout)]
        _BaseColor ("Architectural White Base Color", Color) = (0.93, 0.95, 0.98, 1.0)
        _TrimColor ("Dark Alloy Trim Color", Color) = (0.18, 0.22, 0.28, 1.0)
        _ChannelTint ("Wet Aqueduct Channel Tint", Color) = (0.32, 0.78, 0.96, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.28
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.74

        [Header(Hydro Guide Emission Accent)]
        [HDR] _AccentGlowColor ("Hydro Accent Strip Color (HDR)", Color) = (0.15, 0.88, 1.0, 1.0)
        _AccentGlowIntensity ("Accent Glow Intensity", Range(0.0, 5.0)) = 1.6

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 2
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
        ZWrite On
        ZTest LEqual

        // 1. UniversalForward Lit Pass
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                float4 _TrimColor;
                float4 _ChannelTint;
                float4 _AccentGlowColor;
                float  _Metallic;
                float  _Smoothness;
                float  _AccentGlowIntensity;
                float  _Cull;
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

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                float2 uv = input.uv;

                // UV.y > 1.5 は内部導水路（Wet Channel）、UV.y < 0.15 は壁面フランジトリム
                float isWetChannel = step(1.5, uv.y);
                float isTrimRib = smoothstep(0.06, 0.02, abs(frac(uv.x * 4.0) - 0.5)) * (1.0 - isWetChannel);
                float isGlowStrip = smoothstep(0.035, 0.008, abs(uv.y - 0.82)) * (1.0 - isWetChannel);

                float3 albedo = lerp(_BaseColor.rgb, _TrimColor.rgb, isTrimRib * 0.65);
                albedo = lerp(albedo, _ChannelTint.rgb, isWetChannel * 0.75);

                float smoothness = lerp(_Smoothness, 0.92, isWetChannel);
                float metallic   = lerp(_Metallic, 0.15, isWetChannel);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = normalize(mainLight.direction);
                float NdotL = saturate(dot(N, L));
                float shadow = mainLight.shadowAttenuation;

                float3 sh = SampleSH(N);
                float3 diffuse = albedo * (mainLight.color * (NdotL * shadow * 0.85 + 0.15) + sh * 0.55);

                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specPower = exp2(4.0 + smoothness * 6.5);
                float3 specular = mainLight.color * (pow(NdotH, specPower) * (0.25 + 0.75 * metallic) * shadow * smoothness);

                float3 emission = _AccentGlowColor.rgb * (_AccentGlowIntensity * isGlowStrip);

                return half4(diffuse + specular + emission, 1.0);
            }
            ENDHLSL
        }

        // 2. ShadowCaster Pass (Unity 6 / URP 17 Strict Compliance)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TrimColor;
                float4 _ChannelTint;
                float4 _AccentGlowColor;
                float  _Metallic;
                float  _Smoothness;
                float  _AccentGlowIntensity;
                float  _Cull;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - posWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // 3. DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TrimColor;
                float4 _ChannelTint;
                float4 _AccentGlowColor;
                float  _Metallic;
                float  _Smoothness;
                float  _AccentGlowIntensity;
                float  _Cull;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // 4. DepthNormals Pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TrimColor;
                float4 _ChannelTint;
                float4 _AccentGlowColor;
                float  _Metallic;
                float  _Smoothness;
                float  _AccentGlowIntensity;
                float  _Cull;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
