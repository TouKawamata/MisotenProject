Shader "Hidden/Custom/TriTone"
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
            Name "TriTone"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TriToneIntensity;

            float4 _TriToneShadowColor;
            float4 _TriToneMidtoneColor;
            float4 _TriToneHighlightColor;

            float _TriToneShadowThreshold;
            float _TriToneHighlightThreshold;
            float _TriToneBlendSoftness;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                half4 sourceColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                );

                // Rec.709の係数を使って元映像の輝度を計算する。
                float luminance = dot(
                    sourceColor.rgb,
                    float3(
                        0.2126,
                        0.7152,
                        0.0722
                    )
                );

                float softness = max(
                    _TriToneBlendSoftness,
                    0.001
                );

                // シャドウから中間色への補間率。
                float shadowToMid = smoothstep(
                    _TriToneShadowThreshold - softness,
                    _TriToneShadowThreshold + softness,
                    luminance
                );

                // 中間色からハイライトへの補間率。
                float midToHighlight = smoothstep(
                    _TriToneHighlightThreshold - softness,
                    _TriToneHighlightThreshold + softness,
                    luminance
                );

                float3 shadowMidColor = lerp(
                    _TriToneShadowColor.rgb,
                    _TriToneMidtoneColor.rgb,
                    shadowToMid
                );

                float3 triToneColor = lerp(
                    shadowMidColor,
                    _TriToneHighlightColor.rgb,
                    midToHighlight
                );

                // 輝度情報を少し残し、完全なベタ塗りになるのを防ぐ。
                float luminanceVariation = lerp(
                    0.75,
                    1.25,
                    luminance
                );

                triToneColor *= luminanceVariation;

                // Intensityが0なら元映像、
                // 1なら完全なTriTone映像になる。
                float intensity = saturate(
                    _TriToneIntensity
                );

                float3 finalColor = lerp(
                    sourceColor.rgb,
                    triToneColor,
                    intensity
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
