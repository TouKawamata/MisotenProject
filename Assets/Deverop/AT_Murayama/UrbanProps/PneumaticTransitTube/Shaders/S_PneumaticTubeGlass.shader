Shader "Custom/UrbanProps/S_PneumaticTubeGlass"
{
    Properties
    {
        [Header(Glass Base)]
        _BaseColor ("Glass Tint & Opacity", Color) = (0.85, 0.95, 1.0, 0.25)
        _Smoothness ("Glass Smoothness", Range(0.0, 1.0)) = 0.92
        _Metallic ("Glass Metallic", Range(0.0, 1.0)) = 0.1

        [Header(Fresnel Reflection)]
        _FresnelColor ("Fresnel Edge Color", Color) = (0.7, 0.92, 1.0, 0.8)
        _FresnelPower ("Fresnel Power", Range(1.0, 8.0)) = 3.5

        [Header(Procedural Tube Detailing)]
        [Toggle] _EnableGuideRails ("Enable Procedural Guide Rails", Float) = 1.0
        _RailCount ("Guide Rail Count Around Tube", Float) = 4.0
        _RailColor ("Rail Glow Color", Color) = (0.4, 0.85, 1.0, 0.6)
        _PressureRingDensity ("Vacuum Pressure Ring Density", Range(0.0, 50.0)) = 12.0
        _PressureRingGlow ("Pressure Ring Intensity", Range(0.0, 2.0)) = 0.35

        [Header(Specular Sheen)]
        _SpecularSheen ("Sunlight Specular Sheen", Range(0.0, 2.0)) = 1.2

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 2
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardGlass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

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
                float4 _FresnelColor;
                float4 _RailColor;
                float _Smoothness;
                float _Metallic;
                float _FresnelPower;
                float _EnableGuideRails;
                float _RailCount;
                float _PressureRingDensity;
                float _PressureRingGlow;
                float _SpecularSheen;
                float _Cull;
                float _ZWrite;
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

                // 1. Fresnel Rim Reflection (Thick glass effect)
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // 2. Sunlight Specular Sheen
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float spec = pow(NdotH, _Smoothness * 128.0 + 8.0) * _SpecularSheen;

                // 3. Procedural Guide Rails (Vertical lines around cylinder)
                float railAlpha = 0.0;
                if (_EnableGuideRails > 0.5)
                {
                    float uRepeat = frac(input.uv.x * _RailCount);
                    float distToRail = abs(uRepeat - 0.5);
                    // Sharp guide rail line
                    float railMask = smoothstep(0.06, 0.01, distToRail);
                    railAlpha = railMask;
                }

                // 4. Procedural Pressure Rings (Horizontal bands along height)
                float ringMask = 0.0;
                if (_PressureRingDensity > 0.01)
                {
                    float vRepeat = frac(input.uv.y * _PressureRingDensity);
                    ringMask = smoothstep(0.08, 0.02, abs(vRepeat - 0.5)) * _PressureRingGlow;
                }

                // 5. Combine Color & Opacity
                half3 glassColor = _BaseColor.rgb + mainLight.color * spec;
                glassColor = lerp(glassColor, _FresnelColor.rgb, fresnel * _FresnelColor.a);
                glassColor = lerp(glassColor, _RailColor.rgb, railAlpha * _RailColor.a);
                glassColor += _FresnelColor.rgb * ringMask;

                half alpha = saturate(_BaseColor.a + fresnel * 0.65 + railAlpha * 0.4 + ringMask * 0.3);

                return half4(glassColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
