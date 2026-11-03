Shader "Custom/Hologram/ModelHologram_PatternBanner"
{
    Properties
    {
        [Header(Base Texture and Builtin Pattern)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 0.0
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 2
        _PatternScale ("Pattern Repeat Scale (X, Y)", Vector) = (6, 3, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed", Float) = 1.8

        [Header(Hologram Colors)]
        [HDR] _MainColor ("Primary Hologram Color (HDR)", Color) = (0.0, 2.2, 3.2, 0.75)
        [HDR] _SubColor ("Secondary Pattern Color (HDR)", Color) = (0.0, 0.4, 1.2, 0.25)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.5, 3.8, 4.5, 0.95)
        [HDR] _EdgeFrameColor ("UV Frame Edge Color (HDR)", Color) = (0.6, 4.0, 4.2, 1.0)
        _EdgeFrameWidth ("UV Frame Edge Width", Range(0.0, 0.2)) = 0.04

        [Header(Fresnel Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.4
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.2

        [Header(Scanlines)]
        [Toggle] _UseWorldSpaceScan ("Use World Space Scanlines (OFF = Object Space)", Float) = 1.0
        _ScanlineDensity ("Scanline Density", Float) = 26.0
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 2.0
        _ScanlineIntensity ("Scanline Contrast", Range(0.0, 1.0)) = 0.5

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One/Additive, 10=OneMinusSrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (2=Back, 0=Off/Both)", Float) = 2
        [Toggle] _ZWrite ("ZWrite (ON for solid silhouette)", Float) = 0
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
            Name "ModelHologramPatternBannerPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

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
                float3 positionOS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PatternScale;
                float4 _MainColor;
                float4 _SubColor;
                float4 _RimColor;
                float4 _EdgeFrameColor;
                float _UseTexColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _EdgeFrameWidth;
                float _FresnelPower;
                float _CenterAlpha;
                float _UseWorldSpaceScan;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float EvaluateProceduralPattern(float2 uv)
            {
                if (_PatternMode < 0.5) return 1.0;

                float2 p = uv * _PatternScale.xy;
                if (_PatternMode < 1.5)
                {
                    // 1: Checker
                    float2 cell = floor(p);
                    return fmod(cell.x + cell.y, 2.0);
                }
                else if (_PatternMode < 2.5)
                {
                    // 2: Cyber Arrows
                    float centeredY = abs(frac(uv.y) - 0.5) * 2.0;
                    float arrowPhase = frac(p.x - _Time.y * _PatternScrollSpeed - centeredY * 0.85);
                    return smoothstep(0.35, 0.42, arrowPhase) * (1.0 - smoothstep(0.75, 0.82, arrowPhase));
                }
                else
                {
                    // 3: Racing Stripes
                    float stripePhase = frac(p.x + p.y * 0.6 - _Time.y * _PatternScrollSpeed * 0.5);
                    return step(0.4, stripePhase);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float patternMask = EvaluateProceduralPattern(input.uv);
                half4 patternColor = lerp(_SubColor, _MainColor, patternMask);

                // 1. フレネル輪郭
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 2. 走査線
                float scanCoord = (_UseWorldSpaceScan > 0.5) ? input.positionWS.y : input.positionOS.y;
                float scanWave = sin(scanCoord * _ScanlineDensity + _Time.y * _ScanlineSpeed * 6.28318) * 0.5 + 0.5;
                float scanFactor = lerp(1.0, scanWave, _ScanlineIntensity);

                // 3. UVフレーム縁取り（オプション）
                float distToUVEdge = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y, 1.0 - input.uv.y));
                float frameMask = (_EdgeFrameWidth > 0.001)
                    ? (1.0 - smoothstep(0.0, _EdgeFrameWidth, distToUVEdge))
                    : 0.0;

                // 4. カラー・アルファ合成
                half3 baseRGB = patternColor.rgb * texTint * scanFactor;
                half3 finalRGB = lerp(baseRGB, _RimColor.rgb, fresnel * 0.75);
                finalRGB = lerp(finalRGB, _EdgeFrameColor.rgb, frameMask);

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * patternColor.a * texCol.a;
                half finalAlpha = saturate(lerp(baseAlpha * scanFactor, _EdgeFrameColor.a, frameMask));

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

