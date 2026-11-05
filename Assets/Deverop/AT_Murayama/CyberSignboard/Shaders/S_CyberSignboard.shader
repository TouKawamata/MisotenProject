Shader "Custom/CyberSignboard/S_CyberSignboard"
{
    Properties
    {
        [Header(Base Texture and Tint)]
        _MainTex ("Signboard Texture (Optional)", 2D) = "white" {}
        [Toggle] _HasCustomTex ("Has Custom Texture (1=Yes, 0=Procedural)", Float) = 0.0
        _AspectRatio ("Aspect Ratio (Width / Height)", Float) = 1.7777
        [HDR] _BaseColor ("Base Color (HDR)", Color) = (0.0, 2.5, 3.8, 0.9)
        _EmissionMultiplier ("Emission Multiplier", Range(0.5, 10.0)) = 2.0
        _CyberOverlayIntensity ("Cyber UI Overlay Intensity", Range(0.0, 1.0)) = 1.0

        [Header(Scanline Effect)]
        _ScanlineSpeed ("Scanline Scroll Speed", Range(0.0, 10.0)) = 2.5
        _ScanlineCount ("Scanline Density", Range(10.0, 300.0)) = 80.0
        _ScanlineIntensity ("Scanline Contrast", Range(0.0, 1.0)) = 0.35

        [Header(Glitch and Chromatic Aberration)]
        _GlitchIntensity ("Glitch Intensity", Range(0.0, 1.0)) = 0.4
        _GlitchFrequency ("Glitch Occurrence Frequency", Range(0.0, 1.0)) = 0.35
        _ChromaticSplit ("RGB Chromatic Separation", Range(0.0, 0.08)) = 0.015

        [Header(Voltage Micro Flicker)]
        _FlickerSpeed ("Flicker Speed", Range(0.0, 30.0)) = 18.0
        _FlickerIntensity ("Flicker Intensity", Range(0.0, 0.5)) = 0.08

        [Header(Procedural Pattern Layout)]
        _GridDensity ("Cyber Grid Density (X, Y)", Vector) = (24.0, 14.0, 0.0, 0.0)
        _CautionBandHeight ("Caution Stripe Height", Range(0.0, 0.25)) = 0.10
        _CautionBandSpeed ("Caution Stripe Speed", Range(-5.0, 5.0)) = 1.2
        _CautionBandDensity ("Caution Stripe Density", Range(5.0, 40.0)) = 20.0
        _CornerBracketSize ("Corner Bracket Size", Range(0.02, 0.25)) = 0.12
        _CornerBracketWidth ("Corner Bracket Width", Range(0.002, 0.03)) = 0.008

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (10=OneMinusSrcAlpha, 1=One/Additive)", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off/Both, 2=Back)", Float) = 0
        [Toggle] _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (4=LEqual, 8=Always)", Float) = 4
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
            Name "CyberSignboardPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
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
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float  glitchActive : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _GridDensity;
                float  _HasCustomTex;
                float  _AspectRatio;
                float  _EmissionMultiplier;
                float  _CyberOverlayIntensity;
                float  _ScanlineSpeed;
                float  _ScanlineCount;
                float  _ScanlineIntensity;
                float  _GlitchIntensity;
                float  _GlitchFrequency;
                float  _ChromaticSplit;
                float  _FlickerSpeed;
                float  _FlickerIntensity;
                float  _CautionBandHeight;
                float  _CautionBandSpeed;
                float  _CautionBandDensity;
                float  _CornerBracketSize;
                float  _CornerBracketWidth;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // プロシージャル・サイバーUIパターンの評価
            // 返り値: float4(RGBパターン強度, アルファ)
            float4 EvalProceduralCyberUI(float2 uv)
            {
                float patternRGB = 0.0;
                float patternAlpha = 0.12; // 基本の薄いホログラム背景光
                float2 safeUV = clamp(uv, 0.0, 1.0);

                // 1. サイバーグリッド
                float2 gridCoord = safeUV * _GridDensity.xy;
                float2 g = abs(frac(gridCoord - 0.5) - 0.5);
                float gridLine = 1.0 - smoothstep(0.0, 0.07, min(g.x, g.y));
                patternRGB += gridLine * 0.55;
                patternAlpha += gridLine * 0.35;

                // 2. 上下の警告ハザードストライプ帯
                float isCautionTop = step(1.0 - _CautionBandHeight, safeUV.y);
                float isCautionBot = step(safeUV.y, _CautionBandHeight);
                float isCaution = saturate(isCautionTop + isCautionBot);
                if (isCaution > 0.001)
                {
                    float stripe = frac((safeUV.x + safeUV.y) * _CautionBandDensity + _Time.y * _CautionBandSpeed);
                    float stripePattern = step(0.5, stripe);
                    patternRGB = lerp(patternRGB, stripePattern * 1.5, isCaution * 0.85);
                    patternAlpha = lerp(patternAlpha, 0.9, isCaution * stripePattern);

                    // 境界の仕切りネオンライン
                    float divTop = smoothstep(0.006, 0.0, abs(safeUV.y - (1.0 - _CautionBandHeight)));
                    float divBot = smoothstep(0.006, 0.0, abs(safeUV.y - _CautionBandHeight));
                    float divider = (divTop + divBot) * 1.4;
                    patternRGB += divider;
                    patternAlpha = max(patternAlpha, divider);
                }

                // 3. 四隅のサイバーL字ブラケット
                float2 cDist = min(safeUV, 1.0 - safeUV);
                float isCorner = step(cDist.x, _CornerBracketSize) * step(cDist.y, _CornerBracketSize);
                if (isCorner > 0.5)
                {
                    float edgeDist = min(cDist.x, cDist.y);
                    float bracketLine = smoothstep(_CornerBracketWidth, 0.0, edgeDist);
                    patternRGB += bracketLine * 1.6;
                    patternAlpha = max(patternAlpha, bracketLine * 0.95);
                }

                // 4. 中央のHUDターゲットサークル＆ステータスライン
                float aspect = max(0.01, _AspectRatio);
                float2 cUV = (safeUV - 0.5) * float2(aspect, 1.0);
                float distCenter = length(cUV);
                float circle = smoothstep(0.006, 0.0, abs(distCenter - 0.22)) * 0.65;
                float centerDot = smoothstep(0.015, 0.0, distCenter) * (0.8 + 0.2 * sin(_Time.y * 5.0));
                float hLine = smoothstep(0.003, 0.0, abs(safeUV.y - 0.5)) * step(0.12, abs(safeUV.x - 0.5)) * step(abs(safeUV.x - 0.5), 0.42) * 0.5;
                float tick = smoothstep(0.004, 0.0, abs(safeUV.x - 0.5)) * step(abs(safeUV.y - 0.5), 0.035) * 0.7;

                patternRGB += circle + centerDot + hLine + tick;
                patternAlpha = saturate(patternAlpha + circle * 0.5 + centerDot + hLine * 0.4);

                return float4(patternRGB, patternRGB, patternRGB, patternAlpha);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float glitch = 0.0;

                // 頂点段差グリッチ（低頻度の瞬間横揺れ）
                if (_GlitchIntensity > 0.001)
                {
                    float timeStep = floor(_Time.y * 14.0);
                    float trigger = Hash11(timeStep * 3.71);
                    if (trigger < _GlitchFrequency)
                    {
                        float slice = floor(posOS.y * 6.0 + timeStep);
                        float shift = Hash11(slice * 9.17) * 2.0 - 1.0;
                        posOS.x += shift * _GlitchIntensity * 0.04;
                        glitch = abs(shift);
                    }
                }

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS   = TransformWorldToHClip(posWS);
                output.positionWS   = posWS;
                output.uv           = TRANSFORM_TEX(input.uv, _MainTex);
                output.glitchActive = glitch;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // --- 1. デジタルスライスクラック＆UVグリッチ ---
                float timeBlock = floor(_Time.y * 18.0);
                float slice = floor(input.uv.y * 22.0 + timeBlock * 0.4);
                float glitchTrigger = Hash11(timeBlock * 7.13 + 3.41);
                float glitchActive = step(1.0 - _GlitchFrequency, glitchTrigger) * _GlitchIntensity;

                float uvShift = 0.0;
                if (glitchActive > 0.001)
                {
                    float sliceShift = (Hash11(slice * 11.23 + timeBlock) * 2.0 - 1.0);
                    uvShift = sliceShift * glitchActive * 0.05;
                }

                float2 baseUV = input.uv;
                baseUV.x += uvShift;

                // --- 2. RGB色収差（Chromatic Aberration）サンプリング ---
                float split = _ChromaticSplit * (1.0 + (glitchActive + input.glitchActive) * 2.5);
                float2 uvR = baseUV + float2(split, 0.0);
                float2 uvG = baseUV;
                float2 uvB = baseUV - float2(split, 0.0);

                half4 texR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR);
                half4 texG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG);
                half4 texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB);
                half4 sampledTex = half4(texR.r, texG.g, texB.b, texG.a);

                // プロシージャルパターンのRGB色ズレ
                float4 procR = EvalProceduralCyberUI(uvR);
                float4 procG = EvalProceduralCyberUI(uvG);
                float4 procB = EvalProceduralCyberUI(uvB);
                float4 procUI = float4(procR.r, procG.g, procB.b, procG.a);

                // --- 3. 画像テクスチャとプロシージャルHUDの合成 ---
                half3 finalPattern;
                half baseAlpha;

                if (_HasCustomTex > 0.5)
                {
                    // 画像テクスチャ指定時: テクスチャの全色・白文字・ロゴを高品位に維持しつつ、サイバーHUDを重畳
                    finalPattern = sampledTex.rgb + procUI.rgb * _CyberOverlayIntensity;
                    baseAlpha = saturate(sampledTex.a + procUI.a * _CyberOverlayIntensity);
                }
                else
                {
                    // テクスチャ未指定時: プロシージャル・サイバーHUDを100%描画
                    finalPattern = procUI.rgb;
                    baseAlpha = procUI.a;
                }

                // --- 4. 動的走査線（スキャンライン） ---
                float scanline = sin(baseUV.y * _ScanlineCount + _Time.y * _ScanlineSpeed);
                float scanFactor = 1.0 - (_ScanlineIntensity * 0.5 * (scanline * 0.5 + 0.5));

                // --- 5. 電圧微細明滅（マイクロフリッカー） ---
                float flickerNoise = Hash11(floor(_Time.y * _FlickerSpeed) * 13.37);
                float flicker = 1.0 - _FlickerIntensity * flickerNoise;

                // --- 6. グリッチスパイク時のカラーバースト ---
                half totalGlitch = saturate(glitchActive + input.glitchActive);
                half3 glitchSpike = half3(1.0, 1.0, 1.2) * (totalGlitch * 0.7);

                // --- 7. HDRカラー ＆ 発光強度 統合出力 ---
                half3 finalRGB = (finalPattern * _BaseColor.rgb + glitchSpike) * (_EmissionMultiplier * scanFactor * flicker);
                half finalAlpha = saturate(baseAlpha * _BaseColor.a * (1.0 + totalGlitch * 0.3));

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
