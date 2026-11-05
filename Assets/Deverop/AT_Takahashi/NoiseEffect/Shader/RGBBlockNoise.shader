Shader "Hidden/Custom/RGBBlockNoise"
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
        Blend Off

        Pass
        {
            Name "RGBBlockNoise"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _RGBBlockNoiseIntensity;
            float2 _RGBBlockNoiseBlockCount;
            float _RGBBlockNoiseDensity;
            float _RGBBlockNoiseDisplacement;
            float _RGBBlockNoiseColorSeparation;
            float _RGBBlockNoiseSeed;
            float _RGBBlockNoiseVerticalInfluence;

            // 2次元座標から0～1の疑似乱数を生成する。
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

            // 別パターンの疑似乱数を生成する。
            float2 Hash22(float2 position)
            {
                float3 value = frac(
                    float3(position.xyx)
                    * float3(0.1031, 0.1030, 0.0973)
                );

                value += dot(
                    value,
                    value.yzx + 33.33
                );

                return frac(
                    (value.xx + value.yz) * value.zy
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

                float intensity = saturate(
                    _RGBBlockNoiseIntensity
                );

                // 強度0の場合は元映像を返す。
                if (intensity <= 0.0001)
                {
                    return sourceColor;
                }

                float2 blockCount = max(
                    _RGBBlockNoiseBlockCount,
                    float2(1.0, 1.0)
                );

                // 現在のピクセルが所属するブロック番号。
                float2 blockId = floor(
                    uv * blockCount
                );

                float2 seededBlockId =
                    blockId
                    + float2(
                        _RGBBlockNoiseSeed * 7.13,
                        _RGBBlockNoiseSeed * 13.71
                    );

                float blockRandom = Hash12(
                    seededBlockId
                );

                // Density以上の乱数を持つブロックでは
                // エフェクトを発生させない。
                float activeBlock = step(
                    1.0 - saturate(_RGBBlockNoiseDensity),
                    blockRandom
                );

                float2 randomDirection =
                    Hash22(
                        seededBlockId + 19.17
                    ) * 2.0 - 1.0;

                // 横方向を主体にし、Vertical Influenceで
                // 縦方向の移動量を制限する。
                randomDirection.y *=
                    saturate(_RGBBlockNoiseVerticalInfluence);

                float randomStrength = Hash12(
                    seededBlockId + 41.73
                );

                float2 blockOffset =
                    randomDirection
                    * _RGBBlockNoiseDisplacement
                    * randomStrength
                    * activeBlock
                    * intensity;

                float separationSign =
                    Hash12(seededBlockId + 73.51) > 0.5
                        ? 1.0
                        : -1.0;

                float2 colorDirection = float2(
                    separationSign,
                    separationSign
                    * randomDirection.y
                );

                float2 colorOffset =
                    colorDirection
                    * _RGBBlockNoiseColorSeparation
                    * activeBlock
                    * intensity;

                float2 baseUV = saturate(
                    uv + blockOffset
                );

                float2 redUV = saturate(
                    baseUV + colorOffset
                );

                float2 blueUV = saturate(
                    baseUV - colorOffset
                );

                half red = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    redUV
                ).r;

                half green = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    baseUV
                ).g;

                half blue = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    blueUV
                ).b;

                half3 glitchColor = half3(
                    red,
                    green,
                    blue
                );

                // 非アクティブなブロックは元映像を維持する。
                half3 finalColor = lerp(
                    sourceColor.rgb,
                    glitchColor,
                    activeBlock * intensity
                );

                return half4(
                    finalColor,
                    sourceColor.a
                );
            }

            ENDHLSL
        }
    }

    Fallback Off
}