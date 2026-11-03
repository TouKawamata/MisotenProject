Shader "Custom/Hologram/HologramFlag_Scanline"
{
    Properties
    {
        [Header(Flag Texture and Builtin Pattern)]
        _MainTex ("Flag Texture (Logo / Design)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB Color", Float) = 1.0
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 1
        _PatternScale ("Built-in Pattern Repeat Scale", Vector) = (6, 3, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed (For Arrows/Stripes)", Float) = 1.5

        [Header(Hologram Colors)]
        [HDR] _MainColor ("Primary Hologram Color (HDR)", Color) = (0.0, 2.2, 3.0, 0.75)
        [HDR] _SubColor ("Secondary Pattern Color (HDR)", Color) = (0.0, 0.4, 1.2, 0.25)
        [HDR] _BorderColor ("Top/Bottom Laser Edge Color (HDR)", Color) = (0.6, 3.8, 4.2, 1.0)

        [Header(Wind Wave Animation)]
        [KeywordEnum(Left_UVx0, Right_UVx1, Top_UVy1, Bottom_UVy0)] _PoleSide ("Flag Pole Pin Side (Non-moving edge)", Float) = 0
        _WaveAmplitude ("Wind Wave Amplitude (m)", Range(0.0, 2.0)) = 0.35
        _WaveFrequency ("Wind Wave Frequency", Float) = 8.0
        _WaveSpeed ("Wind Wave Speed", Float) = 6.5
        _FlutterAmplitude ("High-Freq Flutter Amplitude", Range(0.0, 0.5)) = 0.08
        _WaveShading ("Wave Fold Shading Contrast", Range(0.0, 1.0)) = 0.35

        [Header(Space Scanlines and Borders)]
        [KeywordEnum(FullMesh, Swallowtail_VCut, TrianglePennant, AngledSlash)] _ShapeClip ("Procedural Shape Cutout (Optional)", Float) = 0
        [Toggle] _UseWorldSpaceScan ("Use World Space Scanlines (OFF = UV Space)", Float) = 1.0
        _ScanlineDensity ("Scanline Density", Float) = 25.0
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 2.0
        _ScanlineIntensity ("Scanline Contrast", Range(0.0, 1.0)) = 0.55
        _BorderWidth ("Top/Bottom Edge Glow Width", Range(0.0, 0.2)) = 0.04
        _TipFadeStart ("Flag Tip Dissolve Start (0 to 1)", Range(0.2, 1.0)) = 0.75

        [Header(UV Glitch)]
        _GlitchIntensity ("Texture UV Horizontal Glitch", Range(0.0, 0.3)) = 0.04
        _GlitchFrequency ("Glitch Trigger Rate", Range(0.0, 1.0)) = 0.2

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
            Name "HologramFlagScanlinePass"
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
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float2 rawUV      : TEXCOORD2;
                float  pinFactor  : TEXCOORD3;
                float  waveSlope  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainColor;
                float4 _SubColor;
                float4 _BorderColor;
                float4 _PatternScale;
                float _UseTexColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _PoleSide;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _FlutterAmplitude;
                float _WaveShading;
                float _ShapeClip;
                float _UseWorldSpaceScan;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineIntensity;
                float _BorderWidth;
                float _TipFadeStart;
                float _GlitchIntensity;
                float _GlitchFrequency;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // ポール固定軸に応じた 0(根元)〜1(先端) の距離比率を返す
            float GetPoleCoord(float2 uv, out float crossCoord)
            {
                if (_PoleSide < 0.5)      { crossCoord = uv.y; return uv.x; }         // Left (UV.x = 0 が根元)
                else if (_PoleSide < 1.5) { crossCoord = uv.y; return 1.0 - uv.x; }   // Right (UV.x = 1 が根元)
                else if (_PoleSide < 2.5) { crossCoord = uv.x; return 1.0 - uv.y; }   // Top (UV.y = 1 が根元)
                else                      { crossCoord = uv.x; return uv.y; }         // Bottom (UV.y = 0 が根元)
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float crossCoord;
                float poleDist = GetPoleCoord(input.uv, crossCoord);

                // 根元(0)は完全に固定し、先端(1)に行くほど大きく揺れるマスク
                float pinMask = pow(saturate(poleDist), 1.2);

                // メインの風うねり波 ＋ 細かいはためき波
                float phaseMain = poleDist * _WaveFrequency - _Time.y * _WaveSpeed + crossCoord * 1.5;
                float phaseFlutter = poleDist * (_WaveFrequency * 2.7) - _Time.y * (_WaveSpeed * 1.9) - crossCoord * 3.2;

                float waveOffset = (sin(phaseMain) * _WaveAmplitude + sin(phaseFlutter) * _FlutterAmplitude) * pinMask;
                float waveDerivative = cos(phaseMain) * pinMask;

                // 法線方向（法線が0ならZ軸方向）へ頂点を変位させて旗をはためかせる
                float3 displaceDir = (length(input.normalOS) > 0.01) ? normalize(input.normalOS) : float3(0, 0, 1);
                float3 posOS = input.positionOS.xyz + displaceDir * waveOffset;

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUV      = input.uv;
                output.pinFactor  = poleDist;
                output.waveSlope  = waveDerivative;
                return output;
            }

            // テクスチャ未割当時でもすぐ試せる内蔵プロシージャル旗模様
            float EvaluateProceduralPattern(float2 uv)
            {
                if (_PatternMode < 0.5) return 1.0; // TextureOnly

                float2 p = uv * _PatternScale.xy;
                if (_PatternMode < 1.5)
                {
                    // 1: Checker Flag (チェッカーフラッグ)
                    float2 cell = floor(p);
                    return fmod(cell.x + cell.y, 2.0);
                }
                else if (_PatternMode < 2.5)
                {
                    // 2: Cyber Arrows (流れるシェブロン矢印)
                    float centeredY = abs(uv.y - 0.5) * 2.0;
                    float arrowPhase = frac(p.x - _Time.y * _PatternScrollSpeed - centeredY * 0.85);
                    return smoothstep(0.35, 0.42, arrowPhase) * (1.0 - smoothstep(0.75, 0.82, arrowPhase));
                }
                else
                {
                    // 3: Racing Stripes (斜めレーシングストライプ)
                    float stripePhase = frac(p.x + p.y * 0.6 - _Time.y * _PatternScrollSpeed * 0.5);
                    return step(0.4, stripePhase);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 sampleUV = input.uv;
                float2 rawUV = input.rawUV;

                // シェーダー側での形状切り抜きオプション（標準QuadやPlaneでも燕尾・三角に抜ける補助機能）
                if (_ShapeClip > 0.5)
                {
                    if (_ShapeClip < 1.5 && input.pinFactor > (0.68 + abs(rawUV.y - 0.5) * 0.64)) discard;
                    else if (_ShapeClip >= 1.5 && _ShapeClip < 2.5 && abs(rawUV.y - 0.5) > (1.0 - input.pinFactor * 0.92) * 0.5) discard;
                    else if (_ShapeClip >= 2.5 && input.pinFactor > (0.65 + rawUV.y * 0.35)) discard;
                }

                // --------------------------------------------------------
                // 1. ホログラム特有の UV 横ズレグリッチ
                // --------------------------------------------------------
                if (_GlitchIntensity > 0.0001 && _GlitchFrequency > 0.001)
                {
                    float timeStep = floor(_Time.y * 16.0);
                    float trigger = Hash11(timeStep * 2.13);
                    if (trigger < _GlitchFrequency)
                    {
                        float slice = floor(rawUV.y * 14.0 + timeStep);
                        float shift = (Hash11(slice * 5.7 + timeStep) * 2.0 - 1.0);
                        float mask = step(0.6, abs(shift));
                        sampleUV.x += shift * mask * _GlitchIntensity;
                        rawUV.x += shift * mask * _GlitchIntensity;
                    }
                }

                // --------------------------------------------------------
                // 2. テクスチャ ＆ 内蔵模様の合成（揺れる頂点のUVにそのまま追従！）
                // --------------------------------------------------------
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float patternMask = EvaluateProceduralPattern(rawUV);
                half4 patternColor = lerp(_SubColor, _MainColor, patternMask);

                // --------------------------------------------------------
                // 3. 走査線（ワールド空間固定 or UV追従を選択可）
                // --------------------------------------------------------
                float scanCoord = (_UseWorldSpaceScan > 0.5) ? input.positionWS.y : (rawUV.y * 3.0);
                float scanWave = sin(scanCoord * _ScanlineDensity + _Time.y * _ScanlineSpeed * 6.28318) * 0.5 + 0.5;
                float scanFactor = lerp(1.0, scanWave, _ScanlineIntensity);

                // --------------------------------------------------------
                // 4. 旗の上下端レーザーエッジ ＆ 波の陰影 ＆ 先端ディゾルブ消滅
                // --------------------------------------------------------
                float crossDistToEdge = min(rawUV.y, 1.0 - rawUV.y);
                float borderMask = 1.0 - smoothstep(0.0, max(0.001, _BorderWidth), crossDistToEdge);

                // 旗のひだ（波の傾き）による立体ハイライト
                float foldLight = 1.0 + input.waveSlope * _WaveShading;

                // 先端（poleDist -> 1.0）に向かってホログラムが薄く消えていくフェード
                float tipFade = 1.0 - smoothstep(_TipFadeStart, 1.0, input.pinFactor);

                // --------------------------------------------------------
                // 最終合成
                // --------------------------------------------------------
                half3 finalRGB = patternColor.rgb * texTint * foldLight * scanFactor;
                finalRGB = lerp(finalRGB, _BorderColor.rgb, borderMask);

                half baseAlpha = lerp(patternColor.a * texCol.a, _BorderColor.a, borderMask);
                half finalAlpha = saturate(baseAlpha * scanFactor * tipFade);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

