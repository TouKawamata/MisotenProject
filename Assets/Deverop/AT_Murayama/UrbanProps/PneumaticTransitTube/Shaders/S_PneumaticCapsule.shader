Shader "Custom/UrbanProps/S_PneumaticCapsule"
{
    Properties
    {
        [Header(Capsule Fuselage)]
        _BaseColor ("Hull Base Color", Color) = (0.95, 0.96, 0.98, 1.0)
        _AccentColor ("Trim Accent Color", Color) = (0.10, 0.14, 0.20, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.8
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.85

        [Header(Cockpit Window Visor)]
        _VisorColor ("Visor Glass Tint", Color) = (0.05, 0.15, 0.25, 1.0)
        _VisorFresnel ("Visor Reflection Sheen", Range(1.0, 5.0)) = 3.0

        [Header(Engine Thruster Glow)]
        _ThrusterColor ("Thruster Ring Glow", Color) = (0.0, 0.85, 1.0, 1.0)
        _ThrusterIntensity ("Thruster Multiplier", Range(0.0, 8.0)) = 3.5
        _PulseSpeed ("Thruster Pulse Frequency", Range(0.0, 10.0)) = 4.0

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
                float4 _VisorColor;
                float4 _ThrusterColor;
                float _Metallic;
                float _Smoothness;
                float _VisorFresnel;
                float _ThrusterIntensity;
                float _PulseSpeed;
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

                // Lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = normalize(mainLight.direction);
                float NdotL = saturate(dot(N, L));
                float shadow = mainLight.shadowAttenuation;

                // Pod UV Layout mapping:
                // uv.y: 0.0 at tail nozzle, 0.1-0.2 engine rings, 0.4-0.7 passenger hull, 0.7-0.85 visor window, 0.85-1.0 nose cone.
                float y = input.uv.y;

                // Cockpit Visor Band
                float isVisor = smoothstep(0.68, 0.71, y) * smoothstep(0.85, 0.82, y);

                // Engine Thruster Ring Band
                float isThruster = smoothstep(0.20, 0.17, y);

                // Trim Accents
                float isTrim = smoothstep(0.04, 0.01, abs(y - 0.35)) + smoothstep(0.04, 0.01, abs(y - 0.88));

                half3 albedo = lerp(_BaseColor.rgb, _AccentColor.rgb, saturate(isTrim));
                albedo = lerp(albedo, _VisorColor.rgb, isVisor);

                half3 diffuse = albedo * (mainLight.color * (NdotL * shadow) + SampleSH(N) * 0.35);

                // High specular sheen
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float specPower = lerp(_Smoothness * 64.0 + 8.0, 128.0, isVisor);
                float spec = pow(NdotH, specPower) * _Metallic * shadow;
                half3 specular = mainLight.color * spec;

                // Engine glow
                float pulse = 0.85 + 0.15 * sin(_Time.y * _PulseSpeed);
                half3 emission = _ThrusterColor.rgb * (isThruster * _ThrusterIntensity * pulse);

                // Visor reflection rim
                float NdotV = saturate(dot(N, V));
                float visorFresnel = pow(1.0 - NdotV, _VisorFresnel) * isVisor * 0.8;
                specular += half3(0.7, 0.9, 1.0) * visorFresnel;

                return half4(diffuse + specular + emission, 1.0);
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
