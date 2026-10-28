Shader "Custom/Hologram/HologramFlag_CyberGlitch"
{
    Properties
    {
        [Header(Flag Texture and Builtin Pattern)]
        _MainTex ("Flag Texture (With RGB Chromatic Glitch)", 2D) = "white" {}
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 1
        _PatternScale ("Built-in Pattern Repeat Scale", Vector) = (6, 3, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed", Float) = 1.5

        [Header(Hologram Projector Colors)]
        [HDR] _MainColor ("Hologram Core Color (HDR)", Color) = (0.1, 2.4, 3.5, 0.85)
        [HDR] _EmitterColor ("Pole Emitter Glow Color (HDR)", Color) = (1.5, 4.0, 4.5, 1.0)
        _ChromaticSplit ("RGB Chromatic Aberration Amount", Range(0.0, 0.08)) = 0.018

        [Header(Wind Wave Animation)]
        [KeywordEnum(Left_UVx0, Right_UVx1, Top_UVy1, Bottom_UVy0)] _PoleSide ("Flag Pole Pin Side", Float) = 0
        _WaveAmplitude ("Wind Wave Amplitude (m)", Range(0.0, 2.0)) = 0.22
        _WaveFrequency ("Wind Wave Frequency", Float) = 7.0
        _WaveSpeed ("Wind Wave Speed", Float) = 5.5

        [Header(Projector Beams and Noise Dissolve)]
        [KeywordEnum(FullMesh, Swallowtail_VCut, TrianglePennant, AngledSlash)] _ShapeClip ("Procedural Shape Cutout (Optional)", Float) = 0
        _BeamDensity ("Horizontal Projector Beam Density", Float) = 32.0
        _BeamSpeed ("Beam Noise Speed", Float) = 4.0
        _EmitterGlowWidth ("Pole Emitter Glow Width", Range(0.02, 0.4)) = 0.12
        _NoiseDissolveStart ("Tip Noise Dissolve Start", Range(0.2, 1.0)) = 0.6

        [Header(Glitch Settings)]
        _GlitchStrength ("Block Glitch Strength", Range(0.0, 0.4)) = 0.08
        _GlitchRate ("Block Glitch Rate", Range(0.0, 1.0)) = 0.35

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
            Name "HologramFlagCyberGlitchPass"
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
                float  crossCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _PatternScale;
                float4 _MainColor;
                float4 _EmitterColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _ChromaticSplit;
                float _PoleSide;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _ShapeClip;
                float _BeamDensity;
                float _BeamSpeed;
                float _EmitterGlowWidth;
                float _NoiseDissolveStart;
                float _GlitchStrength;
                float _GlitchRate;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

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

                float phase = poleDist * _WaveFrequency - _Time.y * _WaveSpeed + crossCoord * 1.2;
                float waveOffset = sin(phase) * _WaveAmplitude * pinMask;

                float3 displaceDir = (length(input.normalOS) > 0.01) ? normalize(input.normalOS) : float3(0, 0, 1);
                float3 posOS = input.positionOS.xyz + displaceDir * waveOffset;

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUV      = input.uv;
                output.pinFactor  = poleDist;
                output.crossCoord = crossCoord;
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
                    if (_ShapeClip < 1.5 && input.pinFactor > (0.68 + abs(input.crossCoord - 0.5) * 0.64)) discard;
                    else if (_ShapeClip >= 1.5 && _ShapeClip < 2.5 && abs(input.crossCoord - 0.5) > (1.0 - input.pinFactor * 0.92) * 0.5) discard;
                    else if (_ShapeClip >= 2.5 && input.pinFactor > (0.65 + input.crossCoord * 0.35)) discard;
                }

                // --------------------------------------------------------
                // 1. ランダム水平ブロックグリッチ計算
                // --------------------------------------------------------
                float timeStep = floor(_Time.y * 20.0);
                float sliceId = floor(input.crossCoord * 16.0 + timeStep * 0.25);
                float trigger = step(1.0 - _GlitchRate, Hash11(timeStep * 1.71));
                float sliceShift = (Hash11(sliceId * 3.91 + timeStep) * 2.0 - 1.0) * trigger * _GlitchStrength;

                // --------------------------------------------------------
                // 2. RGB色収差（Chromatic Aberration）付きテクスチャ＆模様サンプリング
                // --------------------------------------------------------
                float rgbSplit = _ChromaticSplit * (0.4 + trigger * 1.8);
                float2 uvR = input.uv + float2(sliceShift + rgbSplit, 0.0);
                float2 uvG = input.uv + float2(sliceShift, 0.0);
                float2 uvB = input.uv + float2(sliceShift - rgbSplit, 0.0);

                half rTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                half4 gSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG);
                half bTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                half3 chromaTex = half3(rTex, gSample.g, bTex);

                float patR = EvaluateProceduralPattern(input.rawUV + float2(sliceShift + rgbSplit, 0.0));
                float patG = EvaluateProceduralPattern(input.rawUV + float2(sliceShift, 0.0));
                float patB = EvaluateProceduralPattern(input.rawUV + float2(sliceShift - rgbSplit, 0.0));
                half3 chromaPat = lerp(half3(0.18, 0.18, 0.25), half3(1.0, 1.0, 1.0), half3(patR, patG, patB));

                // --------------------------------------------------------
                // 3. プロジェクター照射ビームライン ＆ 根元エミッター発光
                // --------------------------------------------------------
                float beamLineId = floor(input.crossCoord * _BeamDensity);
                float beamNoise = Hash11(beamLineId + floor(_Time.y * _BeamSpeed));
                float beamMask = lerp(0.55, 1.25, beamNoise);

                // ポール根元（pinFactor = 0）が強く光る照射グラデーション
                float emitterGlow = 1.0 - smoothstep(0.0, max(0.01, _EmitterGlowWidth), input.pinFactor);

                // --------------------------------------------------------
                // 4. 先端に向かって走査線単位でちぎれて消えるノイズディゾルブ
                // --------------------------------------------------------
                float dissolveNoise = Hash21(float2(floor(input.pinFactor * 40.0), beamLineId) + floor(_Time.y * 12.0));
                float dissolveFactor = smoothstep(_NoiseDissolveStart, 1.0, input.pinFactor);
                float tipMask = step(dissolveFactor, dissolveNoise * 0.9 + 0.1) * (1.0 - input.pinFactor * 0.35);

                // --------------------------------------------------------
                // 最終合成
                // --------------------------------------------------------
                half3 finalRGB = chromaTex * chromaPat * _MainColor.rgb * beamMask;
                finalRGB += _EmitterColor.rgb * emitterGlow;

                half finalAlpha = saturate((_MainColor.a * gSample.a * tipMask * beamMask) + emitterGlow);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

