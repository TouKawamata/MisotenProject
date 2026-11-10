Shader "Custom/GlitchEffects"
{
    Properties
    {
        // ‡@ Base Scan Line
        _BaseScanLineIntensity ("Base Scan Line Intensity", Range(0, 1)) = 0
        _BaseScanLineStrength ("Base Scan Line Strength", Range(0, 1)) = 0.25

        // ‡A Scan Line Up
        _ScanLineUpIntensity ("Scan Line Up Intensity", Range(0, 1)) = 0
        _ScanPosition ("Scan Position", Range(0, 1)) = 0
        _ScanLineUpWidth ("Scan Line Up Width", Range(0.001, 0.2)) = 0.03

        // ‡B Waveform Warp
        _WaveformWarpIntensity ("Waveform Warp Intensity", Range(0, 1)) = 0
        _WaveTime ("Wave Time", Float) = 0
        _WaveFrequency ("Wave Frequency", Range(1, 100)) = 20
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.2)) = 0.08

        // ‡C Deform Line
        _DeformLineIntensity ("Deform Line Intensity", Range(0, 1)) = 0
        _DeformLinePosition ("Deform Line Position", Range(0, 1)) = 0.5
        _DeformLineWidth ("Deform Line Width", Range(0.001, 0.2)) = 0.04
        _DeformLineOffset ("Deform Line Offset", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _BaseScanLineIntensity;
            float _BaseScanLineStrength;

            float _ScanLineUpIntensity;
            float _ScanPosition;
            float _ScanLineUpWidth;

            float _WaveformWarpIntensity;
            float _WaveTime;
            float _WaveFrequency;
            float _WaveAmplitude;

            float _DeformLineIntensity;
            float _DeformLinePosition;
            float _DeformLineWidth;
            float _DeformLineOffset;
        CBUFFER_END

        ENDHLSL

        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "GlitchEffectsPass"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // ‡B Waveform Warp
                float wave = sin(
                    uv.y * _WaveFrequency + _WaveTime * 3.0
                );

                float waveOffset = wave
                    * _WaveAmplitude
                    * _WaveformWarpIntensity;

                uv.x += waveOffset;

                // ‡C Deform Line
                float deformDistance = abs(
                    uv.y - _DeformLinePosition
                );

                float deformLine = 1.0 - smoothstep(
                    _DeformLineWidth * 0.5,
                    _DeformLineWidth,
                    deformDistance
                );

                float deformOffset = deformLine
                    * _DeformLineOffset
                    * _DeformLineIntensity;

                uv.x += deformOffset;

                // ‰æ–Ê‚ÌŽæ“¾
                half4 color = SAMPLE_TEXTURE2D(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                );

                // ‡@ Base Scan Line
                float baseScan = sin(uv.y * _ScreenParams.y * 1.5);
                baseScan = smoothstep(0.2, 0.8, baseScan);

                float baseEffect = baseScan
                    * _BaseScanLineStrength
                    * _BaseScanLineIntensity;

                color.rgb *= (1.0 - baseEffect);

                // ‡A Scan Line Up
                float distanceFromLine = abs(
                    uv.y - _ScanPosition
                );

                float scanLine = 1.0 - smoothstep(
                    _ScanLineUpWidth * 0.5,
                    _ScanLineUpWidth,
                    distanceFromLine
                );

                float movingEffect = scanLine
                    * _ScanLineUpIntensity;

                color.rgb *= (1.0 - movingEffect * 0.8);

                return color;
            }

            ENDHLSL
        }
    }
}