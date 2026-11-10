Shader "Hidden/Custom/ChromaticAberration"
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
            Name "ChromaticAberration"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ChromaticAberrationIntensity;
            float2 _ChromaticAberrationDirection;
            float2 _ChromaticAberrationCenter;
            float _ChromaticAberrationRadial;
            float _ChromaticAberrationEdgePower;
            float _ChromaticAberrationPreserveCenter;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                float2 centerToPixel =
                    uv - _ChromaticAberrationCenter;

                float distanceFromCenter =
                    length(centerToPixel);

                float2 radialDirection =
                    distanceFromCenter > 0.0001
                        ? centerToPixel / distanceFromCenter
                        : float2(0.0, 0.0);

                float2 linearDirection =
                    normalize(
                        _ChromaticAberrationDirection
                        + float2(0.0001, 0.0001)
                    );

                // Radialが1なら中心から外側、
                // 0なら指定された一定方向に分離する。
                float2 separationDirection = lerp(
                    linearDirection,
                    radialDirection,
                    saturate(_ChromaticAberrationRadial)
                );

                float preserveCenter =
                    saturate(
                        _ChromaticAberrationPreserveCenter
                    );

                float centerMask = smoothstep(
                    preserveCenter,
                    1.0,
                    saturate(distanceFromCenter * 2.0)
                );

                float edgeMask = pow(
                    max(centerMask, 0.0001),
                    max(
                        _ChromaticAberrationEdgePower,
                        0.1
                    )
                );

                // Linearモードでは全画面に同じ強さで適用する。
                float intensityMask = lerp(
                    1.0,
                    edgeMask,
                    saturate(_ChromaticAberrationRadial)
                );

                float2 offset =
                    separationDirection
                    * _ChromaticAberrationIntensity
                    * intensityMask;

                // 画面外を参照しないようにUVを制限する。
                float2 redUV = saturate(
                    uv + offset
                );

                float2 blueUV = saturate(
                    uv - offset
                );

                half red = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    redUV
                ).r;

                half2 greenAlpha = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                ).ga;

                half blue = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    blueUV
                ).b;

                return half4(
                    red,
                    greenAlpha.x,
                    blue,
                    greenAlpha.y
                );
            }

            ENDHLSL
        }
    }

    Fallback Off
}
