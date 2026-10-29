Shader "Hidden/Custom/GrainNoise"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "GrainNoise"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _GrainNoiseIntensity;
            float _GrainNoiseScale;
            float _GrainNoiseSeed;

            float Hash12(float2 position)
            {
                float3 value = frac(
                    float3(position.xyx) * 0.1031
                );

                value += dot(
                    value,
                    value.yzx + 33.33
                );

                return frac(
                    (value.x + value.y) * value.z
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                half4 sourceColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                );

                float grainScale = max(
                    _GrainNoiseScale,
                    1.0
                );

                float2 pixelPosition = floor(
                    uv * _ScreenParams.xy / grainScale
                );

                float randomValue = Hash12(
                    pixelPosition
                    + float2(
                        _GrainNoiseSeed,
                        _GrainNoiseSeed * 1.6180339
                    )
                );

                float grain =
                    randomValue * 2.0 - 1.0;

                float luminance = dot(
                    sourceColor.rgb,
                    float3(
                        0.2126,
                        0.7152,
                        0.0722
                    )
                );

                float luminanceMask =
                    1.0
                    - abs(luminance * 2.0 - 1.0) * 0.35;

                float noiseAmount =
                    grain
                    * _GrainNoiseIntensity
                    * luminanceMask;

                sourceColor.rgb += noiseAmount;

                return sourceColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}