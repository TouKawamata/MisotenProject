Shader "Custom/Hologram/ModelHologram_LEDMatrix"
{
    Properties
    {
        [Header(Base Texture and Builtin Pattern)]
        _MainTex ("Base Texture (Auto-quantized to LEDs)", 2D) = "white" {}
        [Toggle] _PixelateTexToDots ("Quantize Texture per LED (Pixel Art)", Float) = 1.0
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 1.0
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 2
        _PatternScale ("Pattern Repeat Scale", Vector) = (8, 4, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed", Float) = 1.5

        [Header(LED Dot Matrix Settings)]
        _DotGridCounts ("LED Dot Count (X: Columns, Y: Rows)", Vector) = (48, 32, 0, 0)
        _DotRadius ("LED Dot Radius", Range(0.1, 0.5)) = 0.35
        _DotSoftness ("LED Dot Edge Softness", Range(0.01, 0.3)) = 0.08
        _BackgroundAlpha ("Between-Dots Fill Alpha", Range(0.0, 0.5)) = 0.08

        [Header(Colors and Fresnel Rim)]
        [HDR] _MainColor ("Bright LED Dot Color (HDR)", Color) = (0.0, 3.2, 1.8, 0.95)
        [HDR] _SubColor ("Dim LED Dot Color (HDR)", Color) = (0.0, 0.45, 0.8, 0.25)
        [HDR] _RimColor ("Fresnel Rim Glow Color (HDR)", Color) = (0.3, 4.0, 3.5, 0.95)
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.2

        [Header(Vertical Dissolve Sparkle)]
        _DissolveStart ("Height Dissolve Start (World Y)", Float) = 1000.0
        _DissolveRange ("Height Dissolve Range (m)", Float) = 2.0
        _SparkleSpeed ("LED Sparkle Blink Speed", Float) = 4.0

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
            Name "ModelHologramLEDMatrixPass"
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
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PatternScale;
                float4 _DotGridCounts;
                float4 _MainColor;
                float4 _SubColor;
                float4 _RimColor;
                float _PixelateTexToDots;
                float _UseTexColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _DotRadius;
                float _DotSoftness;
                float _BackgroundAlpha;
                float _FresnelPower;
                float _DissolveStart;
                float _DissolveRange;
                float _SparkleSpeed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
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

                float2 gridCount = max(float2(2.0, 2.0), _DotGridCounts.xy);
                float2 gridUV = input.uv * gridCount;
                float2 cellIndex = floor(gridUV);
                float2 cellLocal = frac(gridUV) - 0.5;

                // 1. 各LEDドットの円形マスク
                float distFromDotCenter = length(cellLocal);
                float dotMask = 1.0 - smoothstep(_DotRadius - _DotSoftness, _DotRadius + _DotSoftness, distFromDotCenter);

                // 2. ドット単位の量子化UVサンプリング（テクスチャのドット絵化）
                float2 quantizedUV = (cellIndex + 0.5) / gridCount;
                float2 sampleUV = lerp(input.uv, quantizedUV, _PixelateTexToDots);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float patternMask = EvaluateProceduralPattern(sampleUV);
                half4 ledTargetColor = lerp(_SubColor, _MainColor, patternMask);

                // 3. フレネル輪郭発光（3D曲面のエッジを綺麗に浮き上がらせる）
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 4. ランダムドットスパークル（きらめき）
                float cellRand = Hash21(cellIndex + floor(_Time.y * _SparkleSpeed) * 0.17);
                float sparkle = lerp(0.85, 1.25, cellRand);

                // 5. 高さディゾルブ（オプション）
                float dissolve = 1.0;
                if (_DissolveStart < 900.0)
                {
                    dissolve = 1.0 - smoothstep(_DissolveStart, _DissolveStart + max(0.1, _DissolveRange), input.positionWS.y);
                }

                // 6. カラー・アルファ合成
                half3 dotRGB = ledTargetColor.rgb * texTint * sparkle * lerp(0.2, 1.3, dotMask);
                half3 finalRGB = lerp(dotRGB, _RimColor.rgb, fresnel * 0.7);

                half baseAlpha = lerp(_BackgroundAlpha, ledTargetColor.a, dotMask) * texCol.a;
                half finalAlpha = saturate(lerp(baseAlpha, _RimColor.a, fresnel) * dissolve);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

