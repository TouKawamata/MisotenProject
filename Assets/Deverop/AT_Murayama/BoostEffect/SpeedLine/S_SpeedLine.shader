Shader "Hidden/Custom/SpeedLine"
{
    Properties
    {
        [Header(Base Settings)]
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)

        // 強さは _SpeedLineStrength をグローバル変数として外から渡す（SpeedLineEffect が設定）

        [Header(Line Settings)]
        _LineCount ("Line Count", Range(8, 200)) = 80
        _LineThickness ("Line Thickness", Range(0.0, 1.0)) = 0.35
        _ThicknessRandomness ("Thickness Randomness", Range(0.0, 1.0)) = 0.7
        _LineSoftness ("Line Edge Softness", Range(0.001, 0.5)) = 0.1
        _FlickerSpeed ("Flicker Speed", Range(0, 60)) = 20

        [Header(Mask Settings)]
        _CenterRadius ("Center Clear Radius", Range(0.0, 1.0)) = 0.35
        _FullRadius ("Full Intensity Radius", Range(0.1, 1.5)) = 0.8
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float2 _Center;
    half4 _LineColor;

    // 外部（SpeedLineEffect）からセットされるグローバル変数
    float _SpeedLineStrength;

    float _LineCount;
    float _LineThickness;
    float _ThicknessRandomness;
    float _LineSoftness;
    float _FlickerSpeed;

    float _CenterRadius;
    float _FullRadius;

    float Hash(float n)
    {
        return frac(sin(n * 12.9898) * 43758.5453);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // 画面の縦横比で歪まないように、中心からの向きを補正する
        float2 dir = uv - _Center.xy;
        dir.x *= _ScreenParams.x / _ScreenParams.y;
        float dist = length(dir);

        // 角度を 0〜1 にして、線の本数ぶんの区画に分ける（継ぎ目が出ないよう本数は整数に丸める）
        float angle01 = atan2(dir.y, dir.x) / TWO_PI + 0.5;
        float count = max(1.0, round(_LineCount));
        float slice = angle01 * count;
        float id = floor(slice);
        float local = frac(slice);

        // 区画ごとの乱数。一定時間ごとに変えて線をちらつかせる
        float flicker = floor(_Time.y * _FlickerSpeed);
        float rand = Hash(id + flicker * 17.0);

        // 区画の中心からの距離（0=中心, 1=端）と、区画ごとの線の太さ
        float fromCenter = abs(local - 0.5) * 2.0;
        float width = _LineThickness * lerp(1.0, rand, _ThicknessRandomness);
        float lineMask = 1.0 - smoothstep(width - _LineSoftness, width, fromCenter);

        // 画面の中心は線を出さず、外側ほど濃くする
        float mask = smoothstep(_CenterRadius, _FullRadius, dist);

        float alpha = saturate(lineMask * mask * _SpeedLineStrength * _LineColor.a);
        color.rgb = lerp(color.rgb, _LineColor.rgb, alpha);
        return color;
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
            Name "SpeedLinePass"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
