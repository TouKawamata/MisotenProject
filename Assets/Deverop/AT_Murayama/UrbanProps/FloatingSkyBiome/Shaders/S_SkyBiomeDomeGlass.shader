Shader "Custom/UrbanProps/S_SkyBiomeDomeGlass"
{
    Properties
    {
        [Header(Geodesic Glass Base)]
        _BaseColor ("Glass Tint & Opacity", Color) = (0.85, 0.96, 1.0, 0.22)
        _Smoothness ("Glass Shininess", Range(0.0, 1.0)) = 0.95
        _Metallic ("Reflectivity", Range(0.0, 1.0)) = 0.15

        [Header(Fresnel and Edge Sheen)]
        _FresnelColor ("Fresnel Rim Color", Color) = (0.75, 0.95, 1.0, 0.85)
        _FresnelPower ("Fresnel Exponent", Range(1.0, 8.0)) = 3.2

        [Header(Procedural Geodesic Strut Lattice)]
        [Toggle] _EnableStrutWire ("Enable Geodesic Strut Wireframe", Float) = 1.0
        _StrutDensity ("Strut Lattice Frequency", Range(10.0, 80.0)) = 36.0
        _StrutColor ("Strut Frame Tint", Color) = (0.2, 0.35, 0.5, 0.7)
        _StrutThickness ("Strut Width", Range(0.01, 0.2)) = 0.05

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
                float4 _StrutColor;
                float _Smoothness;
                float _Metallic;
                float _FresnelPower;
                float _EnableStrutWire;
                float _StrutDensity;
                float _StrutThickness;
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

                // 1. Fresnel Rim Reflection
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // 2. Main Light Specular Sheen
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float NdotH = saturate(dot(N, H));
                float spec = pow(NdotH, _Smoothness * 128.0 + 8.0) * 1.5;

                // 3. Procedural Triangular / Hexagonal Strut Wireframe
                float strutMask = 0.0;
                if (_EnableStrutWire > 0.5)
                {
                    float2 uvGrid = frac(input.uv * _StrutDensity);
                    // Triangular pattern simulation via diagonal and coordinate lines
                    float d1 = abs(uvGrid.x - 0.5);
                    float d2 = abs(uvGrid.y - 0.5);
                    float d3 = abs(uvGrid.x - uvGrid.y);
                    float minDist = min(min(d1, d2), d3);
                    strutMask = smoothstep(_StrutThickness, 0.005, minDist);
                }

                // 4. Color Assembly
                half3 col = _BaseColor.rgb + mainLight.color * spec;
                col = lerp(col, _FresnelColor.rgb, fresnel * _FresnelColor.a);
                col = lerp(col, _StrutColor.rgb, strutMask * _StrutColor.a);

                half alpha = saturate(_BaseColor.a + fresnel * 0.7 + strutMask * 0.6);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
