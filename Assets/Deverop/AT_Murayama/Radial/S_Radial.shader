Shader "Hidden/Custom/RadialBlur"
{
    Properties
    {
        [Header(Base Settings)]
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _BlurDistance ("Blur Distance", Range(0, 0.3)) = 0.05
        _Samples ("Samples", Range(2, 20)) = 10
        
        // 強度制御は _RadialBlurStrength というグローバル変数で受け取ります

        [Header(Curve Settings)]
        _CenterRadius ("Center Clear Radius", Range(0.0, 1.0)) = 0.1
        _BlurRadius ("Blur Max Radius", Range(0.1, 1.5)) = 0.7
        _Power ("Curve Power", Range(1.0, 5.0)) = 1.5
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float2 _Center;
    float _BlurDistance;
    float _Samples;
    
    // 競合回避のため固有の変数名に変更
    float _RadialBlurStrength; 

    float _CenterRadius;
    float _BlurRadius;
    float _Power;

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;

        float2 center = _Center.xy;
        float2 direction = uv - center;
        float dist = length(direction);

        float mask = smoothstep(_CenterRadius, _BlurRadius, dist);

        int samples = max(2, (int)_Samples);
        half4 color = 0;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            t = pow(t, _Power); 

            // 固有の変数名を使用してスケールを計算
            float scale = 1.0 - _BlurDistance * _RadialBlurStrength * mask * t;
            float2 sampleUV = center + direction * scale;

            color += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                sampleUV
            );
        }

        return color / samples;
    }

    ENDHLSL

    SubShader
    {
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
            Name "RadialBlurPass"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}