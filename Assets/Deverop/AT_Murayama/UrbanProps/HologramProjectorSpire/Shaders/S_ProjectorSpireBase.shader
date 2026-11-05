Shader "Custom/UrbanProps/S_ProjectorSpireBase"
{
    Properties
    {
        [Header(Heavy Machinery Base)]
        _BaseColor ("Base Alloy Color", Color) = (0.20, 0.22, 0.28, 1.0)
        _AccentColor ("Trim Accent Color", Color) = (0.85, 0.88, 0.92, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.85
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.65

        [Header(Cooling Vent and Heat Sink Glow)]
        _VentColor ("Vent Heat Sink Glow", Color) = (0.0, 0.85, 1.0, 1.0)
        _VentIntensity ("Vent Emission Multiplier", Range(0.0, 6.0)) = 2.5
        _PulseFrequency ("Heat Pulse Speed", Range(0.0, 8.0)) = 2.5

        [Header(Status LEDs and Emitter Ring)]
        _LedColor ("Emitter Lens Glow", Color) = (0.0, 1.0, 0.9, 1.0)
        _LedIntensity ("Lens Glow Multiplier", Range(0.0, 8.0)) = 4.0

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
                float4 _AccentColor;
                float4 _VentColor;
                float4 _LedColor;
                float _Metallic;
                float _Smoothness;
                float _VentIntensity;
                float _PulseFrequency;
                float _LedIntensity;
                float _Cull;
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

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = normalize(mainLight.direction);
                float NdotL = saturate(dot(N, L));
                float shadow = mainLight.shadowAttenuation;

                // Base alloy color with bevel accents
                float seam = smoothstep(0.03, 0.06, abs(frac(input.uv.y * 3.0) - 0.5));
                half3 surfaceColor = lerp(_AccentColor.rgb, _BaseColor.rgb, seam);

                half3 diffuse = surfaceColor * (mainLight.color * (NdotL * shadow) + SampleSH(N) * 0.35);

                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float spec = pow(NdotH, _Smoothness * 64.0 + 8.0) * _Metallic * shadow;
                half3 specular = mainLight.color * spec;

                // Cooling vent grill emission (slotted heat sink grills on side)
                float isVentZone = smoothstep(0.1, 0.3, input.uv.y) * smoothstep(0.8, 0.6, input.uv.y);
                float ventGrill = smoothstep(0.2, 0.08, abs(frac(input.uv.x * 24.0) - 0.5));
                float heatPulse = 0.85 + 0.15 * sin(_Time.y * _PulseFrequency);
                half3 ventEmission = _VentColor.rgb * (_VentIntensity * isVentZone * ventGrill * heatPulse);

                // Emitter lens glow at high Y or top face
                float isLens = smoothstep(0.85, 0.95, input.uv.y) * step(0.6, N.y);
                half3 lensEmission = _LedColor.rgb * (_LedIntensity * isLens);

                return half4(diffuse + specular + ventEmission + lensEmission, 1.0);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
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
            };

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

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

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthNormals Pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest LEqual

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                return half4(normalWS * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
