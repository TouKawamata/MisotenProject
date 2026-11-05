Shader "Custom/UrbanProps/S_ProjectorBeamCone"
{
    Properties
    {
        [Header(Beam Color and Brightness)]
        [HDR] _BeamColor ("Beam Core Color (HDR)", Color) = (0.0, 0.85, 1.0, 1.0)
        _BeamIntensity ("Beam Intensity Multiplier", Range(0.1, 5.0)) = 1.8
        _BaseOpacity ("Base Opacity", Range(0.0, 1.0)) = 0.65

        [Header(Height Attenuation)]
        _HeightFadeStart ("Height Fade Start (UV Y)", Range(0.0, 0.5)) = 0.05
        _HeightFadeEnd ("Height Fade End (UV Y)", Range(0.5, 1.0)) = 0.95
        _BottomGlowBoost ("Bottom Nozzle Glow Boost", Range(1.0, 5.0)) = 2.5

        [Header(Scan Waves and Laser Rays)]
        _ScanSpeed ("Vertical Scan Wave Speed", Range(0.0, 10.0)) = 3.5
        _ScanDensity ("Scan Wave Density", Range(1.0, 30.0)) = 8.0
        _RayCount ("Angular Laser Ray Count", Range(4.0, 64.0)) = 24.0
        _RaySharpness ("Ray Sharpness", Range(1.0, 10.0)) = 3.0

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (4=LEqual, 8=Always)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BeamPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One // Pure Additive volumetric light
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _BeamIntensity;
                float _BaseOpacity;
                float _HeightFadeStart;
                float _HeightFadeEnd;
                float _BottomGlowBoost;
                float _ScanSpeed;
                float _ScanDensity;
                float _RayCount;
                float _RaySharpness;
                float _ZTest;
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

                float y = input.uv.y;

                // 1. Height Attenuation (Smooth fade at bottom & top)
                float fadeIn  = smoothstep(0.0, _HeightFadeStart, y);
                float fadeOut = 1.0 - smoothstep(_HeightFadeEnd, 1.0, y);
                float heightAtten = fadeIn * fadeOut;

                // Extra glow near the projector nozzles at the bottom
                float nozzleBoost = lerp(_BottomGlowBoost, 1.0, smoothstep(0.0, 0.25, y));

                // 2. Procedural Angular Laser Rays
                float rayPattern = sin(input.uv.x * _RayCount * 6.28318);
                rayPattern = pow(saturate(rayPattern * 0.5 + 0.5), _RaySharpness);
                float rayWeight = lerp(0.4, 1.0, rayPattern);

                // 3. Upward Moving Scan Waves
                float wave = sin(y * _ScanDensity * 6.28318 - _Time.y * _ScanSpeed);
                float waveWeight = 0.8 + 0.2 * wave;

                // 4. Subtle Micro-flicker
                float flicker = 0.95 + 0.05 * sin(_Time.y * 37.0);

                // 5. Fresnel / Glancing angle volume thickness
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                float edgeGlance = 1.0 - abs(dot(N, V));
                float volumeThick = lerp(0.6, 1.2, edgeGlance);

                // Final Additive Emission
                float totalEnergy = heightAtten * nozzleBoost * rayWeight * waveWeight * flicker * volumeThick * _BaseOpacity * _BeamIntensity;
                half3 beamRgb = _BeamColor.rgb * totalEnergy;

                return half4(beamRgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
