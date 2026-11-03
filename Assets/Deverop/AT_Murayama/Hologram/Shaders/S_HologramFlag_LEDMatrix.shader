Shader "Custom/Hologram/HologramFlag_LEDMatrix"
{
    Properties
    {
        [Header(Flag Texture and Builtin Pattern)]
        _MainTex ("Flag Texture (Auto-converted to LED Dots)", 2D) = "white" {}
        [Toggle] _PixelateTexToDots ("Quantize Texture per LED Dot (Pixel Art Style)", Float) = 1.0
        [Toggle] _UseTexColor ("Keep Texture Original RGB Color", Float) = 1.0
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 2
        _PatternScale ("Built-in Pattern Repeat Scale", Vector) = (5, 2, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed", Float) = 1.8

        [Header(LED Dot Matrix Settings)]
        _DotGridCounts ("LED Dot Count (X: Columns, Y: Rows)", Vector) = (48, 20, 0, 0)
        _DotRadius ("LED Dot Radius (0.1 to 0.5)", Range(0.1, 0.5)) = 0.34
        _DotSoftness ("LED Dot Edge Softness", Range(0.01, 0.3)) = 0.08
        _BackgroundAlpha ("Between-Dots Hologram Alpha", Range(0.0, 0.6)) = 0.12

        [Header(Hologram Colors)]
        [HDR] _MainColor ("Bright LED Color (HDR)", Color) = (0.0, 3.2, 1.8, 0.95)
        [HDR] _SubColor ("Dim LED Color (HDR)", Color) = (0.0, 0.45, 0.8, 0.25)
        [HDR] _BorderColor ("Laser Frame Color (HDR)", Color) = (0.2, 4.5, 2.5, 1.0)

        [Header(Wind Wave Animation)]
        [KeywordEnum(Left_UVx0, Right_UVx1, Top_UVy1, Bottom_UVy0)] _PoleSide ("Flag Pole Pin Side", Float) = 0
        _WaveAmplitude ("Wind Wave Amplitude (m)", Range(0.0, 2.0)) = 0.35
        _WaveFrequency ("Wind Wave Frequency", Float) = 8.0
        _WaveSpeed ("Wind Wave Speed", Float) = 6.5
        _FlutterAmplitude ("High-Freq Flutter Amplitude", Range(0.0, 0.5)) = 0.08

        [Header(Tip Particle Dissolve and Border)]
        [KeywordEnum(FullMesh, Swallowtail_VCut, TrianglePennant, AngledSlash)] _ShapeClip ("Procedural Shape Cutout (Optional)", Float) = 0
        _BorderWidth ("Top/Bottom Laser Edge Width", Range(0.0, 0.15)) = 0.035
        _DissolveStart ("Tip Particle Dissolve Start (0 to 1)", Range(0.3, 1.0)) = 0.68

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One/Additive, 10=OneMinusSrcAlpha)", Float) = 1
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
            Name "HologramFlagLEDMatrixPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
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
                float2 uv         : TEXCOORD0;
                float2 rawUV      : TEXCOORD1;
                float  pinFactor  : TEXCOORD2;
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
                float4 _BorderColor;
                float _PixelateTexToDots;
                float _UseTexColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _DotRadius;
                float _DotSoftness;
                float _BackgroundAlpha;
                float _PoleSide;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _FlutterAmplitude;
                float _ShapeClip;
                float _BorderWidth;
                float _DissolveStart;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float GetPoleCoord(float2 uv, out float crossCoord)
            {
                if (_PoleSide < 0.5)      { crossCoord = uv.y; return uv.x; }
                else if (_PoleSide < 1.5) { crossCoord = uv.y; return 1.0 - uv.x; }
                else if (_PoleSide < 2.5) { crossCoord = uv.x; return 1.0 - uv.y; }
                else                      { crossCoord = uv.x; return uv.y; }
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float crossCoord;
                float poleDist = GetPoleCoord(input.uv, crossCoord);
                float pinMask = pow(saturate(poleDist), 1.2);

                float phaseMain = poleDist * _WaveFrequency - _Time.y * _WaveSpeed + crossCoord * 1.5;
                float phaseFlutter = poleDist * (_WaveFrequency * 2.7) - _Time.y * (_WaveSpeed * 1.9) - crossCoord * 3.2;
                float waveOffset = (sin(phaseMain) * _WaveAmplitude + sin(phaseFlutter) * _FlutterAmplitude) * pinMask;

                float3 displaceDir = (length(input.normalOS) > 0.01) ? normalize(input.normalOS) : float3(0, 0, 1);
                float3 posOS = input.positionOS.xyz + displaceDir * waveOffset;

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUV      = input.uv;
                output.pinFactor  = poleDist;
                return output;
            }

            float EvaluateProceduralPattern(float2 uv)
            {
                if (_PatternMode < 0.5) return 1.0;

                float2 p = uv * _PatternScale.xy;
                if (_PatternMode < 1.5)
                {
                    float2 cell = floor(p);
                    return fmod(cell.x + cell.y, 2.0);
                }
                else if (_PatternMode < 2.5)
                {
                    float centeredY = abs(uv.y - 0.5) * 2.0;
                    float arrowPhase = frac(p.x - _Time.y * _PatternScrollSpeed - centeredY * 0.85);
                    return smoothstep(0.35, 0.42, arrowPhase) * (1.0 - smoothstep(0.75, 0.82, arrowPhase));
                }
                else
                {
                    float stripePhase = frac(p.x + p.y * 0.6 - _Time.y * _PatternScrollSpeed * 0.5);
                    return step(0.4, stripePhase);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (_ShapeClip > 0.5)
                {
                    if (_ShapeClip < 1.5 && input.pinFactor > (0.68 + abs(input.rawUV.y - 0.5) * 0.64)) discard;
                    else if (_ShapeClip >= 1.5 && _ShapeClip < 2.5 && abs(input.rawUV.y - 0.5) > (1.0 - input.pinFactor * 0.92) * 0.5) discard;
                    else if (_ShapeClip >= 2.5 && input.pinFactor > (0.65 + input.rawUV.y * 0.35)) discard;
                }

                float2 gridCount = max(float2(2.0, 2.0), _DotGridCounts.xy);
                float2 gridUV = input.rawUV * gridCount;
                float2 cellIndex = floor(gridUV);
                float2 cellLocal = frac(gridUV) - 0.5;

                // --------------------------------------------------------
                // 1. 各LEDドットの円形マスク生成
                // --------------------------------------------------------
                float distFromDotCenter = length(cellLocal);
                float dotMask = 1.0 - smoothstep(_DotRadius - _DotSoftness, _DotRadius + _DotSoftness, distFromDotCenter);

                // --------------------------------------------------------
                // 2. ドット単位の量子化UV（テクスチャをドット絵LED化するオプション）
                // --------------------------------------------------------
                float2 quantizedRawUV = (cellIndex + 0.5) / gridCount;
                float2 patternUV = lerp(input.rawUV, quantizedRawUV, _PixelateTexToDots);
                float2 texSampleUV = lerp(input.uv, TRANSFORM_TEX(quantizedRawUV, _MainTex), _PixelateTexToDots);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texSampleUV);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float patternMask = EvaluateProceduralPattern(patternUV);
                half4 ledTargetColor = lerp(_SubColor, _MainColor, patternMask);

                // --------------------------------------------------------
                // 3. 旗の先端に向かってドット単位でランダムに消滅するデジタルディゾルブ
                // --------------------------------------------------------
                float cellRand = Hash21(cellIndex + floor(_Time.y * 6.0) * 0.13);
                float dissolveProgress = smoothstep(_DissolveStart, 1.0, input.pinFactor);
                float cellAlive = step(dissolveProgress, cellRand * 0.85 + 0.15);

                // --------------------------------------------------------
                // 4. 上下端のレーザーフレーム
                // --------------------------------------------------------
                float crossDistToEdge = min(input.rawUV.y, 1.0 - input.rawUV.y);
                float borderMask = 1.0 - smoothstep(0.0, max(0.001, _BorderWidth), crossDistToEdge);

                // --------------------------------------------------------
                // 最終合成
                // --------------------------------------------------------
                half3 dotRGB = ledTargetColor.rgb * texTint * lerp(0.25, 1.25, dotMask);
                half3 finalRGB = lerp(dotRGB, _BorderColor.rgb, borderMask);

                half dotAlpha = lerp(_BackgroundAlpha, ledTargetColor.a, dotMask) * texCol.a * cellAlive;
                half finalAlpha = saturate(lerp(dotAlpha, _BorderColor.a * (1.0 - dissolveProgress * 0.7), borderMask));

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

