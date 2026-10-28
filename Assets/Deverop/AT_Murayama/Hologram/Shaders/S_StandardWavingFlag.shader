Shader "Custom/Hologram/StandardWavingFlag"
{
    Properties
    {
        [Header(Flag Texture and Builtin Pattern)]
        _MainTex ("Flag Texture (RGB + Alpha)", 2D) = "white" {}
        _AlphaCutoff ("Texture Alpha Cutoff", Range(0.0, 1.0)) = 0.1
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 1
        _PatternScale ("Built-in Pattern Repeat Scale", Vector) = (6, 3, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed (0 = Static Cloth)", Float) = 0.0

        [Header(Cloth Colors and Shading)]
        _MainColor ("Primary Cloth Color", Color) = (0.95, 0.95, 0.98, 1.0)
        _SubColor ("Secondary Cloth Color (For Pattern)", Color) = (0.12, 0.14, 0.18, 1.0)
        _BorderColor ("Edge Trim / Frame Color", Color) = (1.0, 0.75, 0.15, 1.0)
        _BorderWidth ("Top/Bottom Edge Trim Width", Range(0.0, 0.15)) = 0.035
        _WaveShading ("Cloth Wave Fold Shadow/Highlight", Range(0.0, 1.0)) = 0.45
        _AmbientMin ("Minimum Shadow Brightness", Range(0.1, 1.0)) = 0.55

        [Header(Shape Clipping Option)]
        [KeywordEnum(FullMesh, Swallowtail_VCut, TrianglePennant, AngledSlash)] _ShapeClip ("Procedural Shape Cutout (Optional)", Float) = 0

        [Header(Wind Wave Animation)]
        [KeywordEnum(Left_UVx0, Right_UVx1, Top_UVy1, Bottom_UVy0)] _PoleSide ("Flag Pole Pin Side (Non-moving edge)", Float) = 0
        _WaveAmplitude ("Wind Wave Amplitude (m)", Range(0.0, 2.0)) = 0.35
        _WaveFrequency ("Wind Wave Frequency", Float) = 8.0
        _WaveSpeed ("Wind Wave Speed", Float) = 6.5
        _FlutterAmplitude ("High-Freq Flutter Amplitude", Range(0.0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _PatternScale;
            float4 _MainColor;
            float4 _SubColor;
            float4 _BorderColor;
            float _AlphaCutoff;
            float _PatternMode;
            float _PatternScrollSpeed;
            float _BorderWidth;
            float _WaveShading;
            float _AmbientMin;
            float _ShapeClip;
            float _PoleSide;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _FlutterAmplitude;
        CBUFFER_END

        float GetPoleCoord(float2 uv, out float crossCoord)
        {
            if (_PoleSide < 0.5)      { crossCoord = uv.y; return uv.x; }         // Left (UV.x = 0 が根元)
            else if (_PoleSide < 1.5) { crossCoord = uv.y; return 1.0 - uv.x; }   // Right (UV.x = 1 が根元)
            else if (_PoleSide < 2.5) { crossCoord = uv.x; return 1.0 - uv.y; }   // Top (UV.y = 1 が根元)
            else                      { crossCoord = uv.x; return uv.y; }         // Bottom (UV.y = 0 が根元)
        }

        float3 ComputeWavingPositionOS(float3 positionOS, float3 normalOS, float2 uv, out float poleDist, out float crossCoord, out float waveDerivative)
        {
            poleDist = GetPoleCoord(uv, crossCoord);
            float pinMask = pow(saturate(poleDist), 1.2);

            float phaseMain = poleDist * _WaveFrequency - _Time.y * _WaveSpeed + crossCoord * 1.5;
            float phaseFlutter = poleDist * (_WaveFrequency * 2.7) - _Time.y * (_WaveSpeed * 1.9) - crossCoord * 3.2;

            float waveOffset = (sin(phaseMain) * _WaveAmplitude + sin(phaseFlutter) * _FlutterAmplitude) * pinMask;
            waveDerivative = cos(phaseMain) * pinMask;

            float3 displaceDir = (length(normalOS) > 0.01) ? normalize(normalOS) : float3(0, 0, 1);
            return positionOS + displaceDir * waveOffset;
        }

        // 四角形メッシュに対してもシェーダー側で燕尾・三角・斜めカットを適用できる補助マスク
        bool IsClippedByShape(float poleDist, float crossCoord)
        {
            if (_ShapeClip < 0.5) return false; // 0: FullMesh

            if (_ShapeClip < 1.5)
            {
                // 1: Swallowtail (先端中央がV字に切れ込む燕尾形)
                float centerDist = abs(crossCoord - 0.5) * 2.0;
                float cutStart = 0.68 + centerDist * 0.32;
                return (poleDist > cutStart);
            }
            else if (_ShapeClip < 2.5)
            {
                // 2: TrianglePennant (先端に向かって細くなる三角ペナント)
                float halfWidth = (1.0 - poleDist * 0.92) * 0.5;
                return (abs(crossCoord - 0.5) > halfWidth);
            }
            else
            {
                // 3: AngledSlash (先端が斜めにスパッと切れた形状)
                float maxLen = 0.65 + crossCoord * 0.35;
                return (poleDist > maxLen);
            }
        }
        ENDHLSL

        Pass
        {
            Name "StandardWavingFlagPass"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float2 rawUV      : TEXCOORD3;
                float  pinFactor  : TEXCOORD4;
                float  crossCoord : TEXCOORD5;
                float  waveSlope  : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float poleDist, crossCoord, waveDerivative;
                float3 posOS = ComputeWavingPositionOS(input.positionOS.xyz, input.normalOS, input.uv, poleDist, crossCoord, waveDerivative);

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUV      = input.uv;
                output.pinFactor  = poleDist;
                output.crossCoord = crossCoord;
                output.waveSlope  = waveDerivative;
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

                if (IsClippedByShape(input.pinFactor, input.crossCoord))
                {
                    discard;
                }

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (texCol.a < _AlphaCutoff)
                {
                    discard;
                }

                float patternMask = EvaluateProceduralPattern(input.rawUV);
                half3 clothBase = lerp(_SubColor.rgb, _MainColor.rgb, patternMask) * texCol.rgb;

                // 上下端の縁取り（トリム）
                float crossDistToEdge = min(input.crossCoord, 1.0 - input.crossCoord);
                float borderMask = (_BorderWidth > 0.001)
                    ? (1.0 - smoothstep(0.0, _BorderWidth, crossDistToEdge))
                    : 0.0;
                clothBase = lerp(clothBase, _BorderColor.rgb, borderMask);

                // URP メインライト ＋ 布の波ひだ（waveSlope）による立体陰影
                Light mainLight = GetMainLight();
                float3 nWS = normalize(input.normalWS);
                float ndotl = abs(dot(nWS, mainLight.direction)) * 0.5 + 0.5;
                float lightFactor = lerp(_AmbientMin, 1.05, ndotl);

                // 波の凹凸による布の明暗ハイライト
                float foldShade = 1.0 + input.waveSlope * _WaveShading;

                half3 finalRGB = clothBase * mainLight.color * lightFactor * foldShade;
                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // DepthOnly パス：_CameraDepthTexture に揺れる旗の正確な近景深度を書き込み、大気散乱等で背景の空と誤判定されるのを防ぐ
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  pinFactor  : TEXCOORD1;
                float  crossCoord : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDepth vertDepth(AttributesDepth input)
            {
                VaryingsDepth output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float poleDist, crossCoord, waveDerivative;
                float3 posOS = ComputeWavingPositionOS(input.positionOS.xyz, input.normalOS, input.uv, poleDist, crossCoord, waveDerivative);

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.pinFactor  = poleDist;
                output.crossCoord = crossCoord;
                return output;
            }

            half4 fragDepth(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (IsClippedByShape(input.pinFactor, input.crossCoord))
                {
                    discard;
                }

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                if (alpha < _AlphaCutoff)
                {
                    discard;
                }

                return 0;
            }
            ENDHLSL
        }

        // DepthNormals パス：SSAO有効時などDepthNormals Prepassが使われる環境でも正確な深度・法線を書き込む
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertDepthNormals
            #pragma fragment fragDepthNormals
            #pragma multi_compile_instancing

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float  pinFactor  : TEXCOORD2;
                float  crossCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsDN vertDepthNormals(AttributesDN input)
            {
                VaryingsDN output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float poleDist, crossCoord, waveDerivative;
                float3 posOS = ComputeWavingPositionOS(input.positionOS.xyz, input.normalOS, input.uv, poleDist, crossCoord, waveDerivative);

                output.positionCS = TransformObjectToHClip(posOS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.pinFactor  = poleDist;
                output.crossCoord = crossCoord;
                return output;
            }

            half4 fragDepthNormals(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if (IsClippedByShape(input.pinFactor, input.crossCoord))
                {
                    discard;
                }

                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                if (alpha < _AlphaCutoff)
                {
                    discard;
                }

                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }
}

