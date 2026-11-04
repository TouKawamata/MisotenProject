Shader "Custom/SmartSignage/S_FrostedGlassSignboard"
{
    Properties
    {
        [Header(Frosted Glass Physical Transmission)]
        [Toggle] _UseSceneBlur ("Enable Background Frosted Blur", Float) = 1.0
        _BlurRadius ("Frosted Blur Radius", Range(0.0, 0.05)) = 0.015
        _RefractionAmount ("Edge & Bevel Refraction", Range(0.0, 0.04)) = 0.008
        _RoughnessNoise ("Frosted Surface Grain", Range(0.0, 1.0)) = 0.35
        _GlassTint ("Milky Glass Tint (RGB + Alpha)", Color) = (0.92, 0.97, 1.0, 0.22)

        [Header(Glass Specular and Reflection)]
        _SpecularIntensity ("Glass Specular Sheen", Range(0.0, 3.0)) = 1.3
        _SpecularSmoothness ("Specular Shininess", Range(4.0, 128.0)) = 64.0
        _FresnelColor ("Fresnel Edge Glow", Color) = (0.75, 0.95, 1.0, 0.85)
        _FresnelPower ("Fresnel Exponent", Range(1.0, 8.0)) = 3.2
        _EdgeGlowColor ("Bevel Border Glow Tint", Color) = (0.85, 0.97, 1.0, 0.9)
        _EdgeGlowIntensity ("Bevel Border Glow Intensity", Range(0.0, 3.0)) = 1.4

        [Header(Dynamic Glass Light Sweep)]
        _SweepSpeed ("Specular Light Sweep Speed", Range(0.0, 3.0)) = 0.4
        _SweepIntensity ("Specular Light Sweep Intensity", Range(0.0, 1.0)) = 0.35

        [Header(UI Telemetry Overlay)]
        _MainTex ("UI Telemetry Texture (RGBA)", 2D) = "white" {}
        [Toggle] _HasCustomTex ("Has Custom Texture (1=Yes, 0=Procedural)", Float) = 1.0
        _AspectRatio ("Aspect Ratio (Width / Height)", Float) = 1.7777
        [HDR] _UIColor ("UI Accent Tint (HDR)", Color) = (0.85, 0.97, 1.0, 1.0)
        _UIIntensity ("UI Emission / Brightness", Range(0.5, 6.0)) = 2.4
        _UIOpacity ("UI Opacity Multiplier", Range(0.0, 1.0)) = 0.95

        [Header(Dynamic Telemetry Pulse)]
        [Toggle] _DynamicTelemetry ("Enable Telemetry Line Wave", Float) = 1.0
        _GraphWaveSpeed ("Graph Wave Pulse Speed", Range(0.0, 5.0)) = 1.8
        _GraphWaveAmp ("Graph Wave Amplitude", Range(0.0, 0.05)) = 0.008

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (10=OneMinusSrcAlpha, 1=One)", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off/Both, 2=Back)", Float) = 2
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 0
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
            Name "FrostedGlassPass"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
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
                float4 screenPos  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _GlassTint;
                float4 _FresnelColor;
                float4 _EdgeGlowColor;
                float4 _UIColor;
                float  _UseSceneBlur;
                float  _BlurRadius;
                float  _RefractionAmount;
                float  _RoughnessNoise;
                float  _SpecularIntensity;
                float  _SpecularSmoothness;
                float  _FresnelPower;
                float  _EdgeGlowIntensity;
                float  _SweepSpeed;
                float  _SweepIntensity;
                float  _HasCustomTex;
                float  _AspectRatio;
                float  _UIIntensity;
                float  _UIOpacity;
                float  _DynamicTelemetry;
                float  _GraphWaveSpeed;
                float  _GraphWaveAmp;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            // 高速疑似乱数ハッシュ（フロストガラスのサンドブラスト微細粒子感）
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 13タップ ポアソンディスク風フロストガラス多重サンプリング＆微小色収差（RGB分散）
            float3 SampleFrostedScene(float2 screenUV, float blurRadius, float roughness, float2 jitter)
            {
                const float2 offsets[13] = {
                    float2( 0.0000,  0.0000),
                    float2( 0.2800,  0.1500),
                    float2(-0.2100,  0.2600),
                    float2(-0.2900, -0.1700),
                    float2( 0.1600, -0.3100),
                    float2( 0.5800,  0.1200),
                    float2( 0.3800,  0.5100),
                    float2(-0.1900,  0.6200),
                    float2(-0.5500,  0.3100),
                    float2(-0.6200, -0.1800),
                    float2(-0.3500, -0.5600),
                    float2( 0.1800, -0.6300),
                    float2( 0.5200, -0.4200)
                };

                const float weights[13] = {
                    0.20,
                    0.09, 0.09, 0.09, 0.09,
                    0.055, 0.055, 0.055, 0.055, 0.055, 0.055, 0.055, 0.055
                };

                float3 accum = float3(0.0, 0.0, 0.0);
                float radiusWithNoise = blurRadius * (1.0 + roughness * (jitter.x - 0.5) * 0.4);

                for (int i = 0; i < 13; i++)
                {
                    float2 off = offsets[i] * radiusWithNoise;
                    // 赤と青に微小な光学的色分散（Chromatic Dispersion）を付加
                    float3 sR = SampleSceneColor(screenUV + off * 1.02);
                    float3 sG = SampleSceneColor(screenUV + off);
                    float3 sB = SampleSceneColor(screenUV + off * 0.98);

                    accum.r += sR.r * weights[i];
                    accum.g += sG.g * weights[i];
                    accum.b += sB.b * weights[i];
                }

                return accum;
            }

            // プロシージャルUIフォールバック（テクスチャ未指定時に自動描画される精密テレメトリパターン）
            float4 EvalProceduralHUD(float2 uv, float aspect)
            {
                float2 safeUV = clamp(uv, 0.0, 1.0);
                float patternRGB = 0.0;
                float patternAlpha = 0.0;

                // 1. 外周のクリーンなテクニカルフレーム＆コーナーブラケット
                float2 cDist = min(safeUV, 1.0 - safeUV);
                float cornerBox = step(cDist.x, 0.08) * step(cDist.y, 0.08);
                float bracketLine = smoothstep(0.005, 0.0, min(cDist.x, cDist.y)) * cornerBox;
                patternRGB += bracketLine * 1.5;
                patternAlpha = max(patternAlpha, bracketLine * 0.85);

                // 2. 左上・高度インジケーター（ALTITUDEゲージ目盛り）
                float isTopLeft = step(safeUV.x, 0.48) * step(0.65, safeUV.y);
                if (isTopLeft > 0.5)
                {
                    // 水平区切りライン
                    float hLine = smoothstep(0.003, 0.0, abs(safeUV.y - 0.68));
                    // 目盛りチック
                    float tickX = frac(safeUV.x * 20.0);
                    float tick = smoothstep(0.08, 0.0, abs(tickX - 0.5)) * step(abs(safeUV.y - 0.70), 0.012);
                    patternRGB += (hLine + tick) * 1.2;
                    patternAlpha = max(patternAlpha, (hLine + tick) * 0.8);
                }

                // 3. 左下・動的テレメトリ波形グラフ（Telemetry Curve Graph）
                float isBottomLeft = step(safeUV.x, 0.52) * step(safeUV.y, 0.45) * step(0.08, safeUV.x) * step(0.08, safeUV.y);
                if (isBottomLeft > 0.5)
                {
                    float wavePhase = safeUV.x * 28.0 - _Time.y * _GraphWaveSpeed;
                    float wave = sin(wavePhase) * _GraphWaveAmp + cos(wavePhase * 0.5) * (_GraphWaveAmp * 0.5);
                    float graphBaseY = 0.22 + wave;
                    float graphDist = abs(safeUV.y - graphBaseY);
                    float graphLine = smoothstep(0.004, 0.0005, graphDist) * 2.0;

                    // グラフ背景の微細グリッド
                    float2 gCoord = safeUV * float2(32.0, 16.0);
                    float2 gFrac = abs(frac(gCoord) - 0.5);
                    float grid = smoothstep(0.04, 0.0, min(gFrac.x, gFrac.y)) * 0.25;

                    patternRGB += graphLine + grid;
                    patternAlpha = max(patternAlpha, saturate(graphLine + grid * 0.4));
                }

                // 4. 右上・ラップ＆ランク表示枠
                float isTopRight = step(0.55, safeUV.x) * step(0.70, safeUV.y) * step(safeUV.x, 0.92);
                if (isTopRight > 0.5)
                {
                    float boxOutline = smoothstep(0.003, 0.0, abs(safeUV.y - 0.72)) * step(safeUV.x, 0.90);
                    patternRGB += boxOutline * 1.2;
                    patternAlpha = max(patternAlpha, boxOutline * 0.7);
                }

                // 5. ベクター矢印・ステータスドット
                float2 arrowCenter = safeUV - float2(0.38, 0.15);
                float arrowDist = length(arrowCenter * float2(aspect, 1.0));
                float arrowDot = smoothstep(0.012, 0.0, arrowDist) * (0.8 + 0.2 * sin(_Time.y * 6.0));
                patternRGB += arrowDot * 1.8;
                patternAlpha = max(patternAlpha, arrowDot);

                return float4(patternRGB, patternRGB, patternRGB, patternAlpha);
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
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // --- 1. スクリーンUVとフロスト屈折計算 ---
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);

                // ガラスのエッジおよび法線による微小屈折オフセット（ビュー空間法線のXYを使用し任意角度の看板配置に対応）
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 normalScreenOffset = normalVS.xy * _RefractionAmount;
                float2 jitter = float2(Hash12(input.uv * 512.0), Hash12(input.uv * 512.0 + 17.13));

                // --- 2. 背景フロストガラス透過ブラー（URP Opaque Texture） ---
                float3 blurredBg = float3(0.0, 0.0, 0.0);
                if (_UseSceneBlur > 0.5)
                {
                    blurredBg = SampleFrostedScene(screenUV + normalScreenOffset, _BlurRadius, _RoughnessNoise, jitter);

                    // オフスクリーン・未描画時のフォールバック（美しい快晴の空・アンビエントグラデーション）
                    float bgLuma = dot(blurredBg, float3(0.299, 0.587, 0.114));
                    if (bgLuma < 0.001)
                    {
                        float3 skyAmb = lerp(float3(0.72, 0.86, 0.98), float3(0.92, 0.96, 1.0), saturate(normalWS.y * 0.5 + 0.5));
                        blurredBg = skyAmb * 0.85;
                    }
                }
                else
                {
                    float3 skyAmb = lerp(float3(0.72, 0.86, 0.98), float3(0.92, 0.96, 1.0), saturate(normalWS.y * 0.5 + 0.5));
                    blurredBg = skyAmb;
                }

                // --- 3. ガラス表面の鏡面反射（ディレクショナルサンライト Specular Sheen） ---
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir  = normalize(lightDir + viewDirWS);
                float  NdotH    = saturate(dot(normalWS, halfDir));
                float  spec     = pow(NdotH, _SpecularSmoothness) * _SpecularIntensity;
                float3 specCol  = mainLight.color * spec;

                // --- 4. 動的ガラス光沢スイープ（スマートガラスの美しい反射光の流延） ---
                float sweepCoord = (input.uv.x * 0.75 + input.uv.y * 0.25) - frac(_Time.y * _SweepSpeed * 0.25) * 2.2;
                float sweep = smoothstep(0.06, 0.0, abs(sweepCoord)) * _SweepIntensity;
                float3 sweepCol = float3(0.95, 0.98, 1.0) * sweep;

                // --- 5. フレネル・エッジ発光 ＆ 面取り（ベベル）境界の光集約 ---
                float NdotV   = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                float3 rimCol = _FresnelColor.rgb * (fresnel * _FresnelColor.a);

                // 丸角パネル外周のフィレット＆ベベル光沢（丸角に沿って均一に光を集約）
                float aspect = max(0.01, _AspectRatio);
                float2 pBox = abs((input.uv - 0.5) * float2(aspect, 1.0));
                float cornerR = 0.08 * min(aspect, 1.0);
                float2 bBox = float2(0.5 * aspect - cornerR, 0.5 - cornerR);
                float2 dBox = pBox - bBox;
                float distToEdge = cornerR - (length(max(dBox, 0.0)) + min(max(dBox.x, dBox.y), 0.0));
                float bevelGlow = smoothstep(0.025, 0.002, distToEdge) * _EdgeGlowIntensity;
                float3 bevelCol = _EdgeGlowColor.rgb * bevelGlow;

                // --- 6. ガラス本体の合成（乳白フロスト＋鏡面光沢＋フレネル） ---
                float3 glassBody = lerp(blurredBg, _GlassTint.rgb, _GlassTint.a);
                glassBody += specCol + sweepCol + rimCol + bevelCol;

                // --- 7. テレメトリUI（テクスチャ ＆ プロシージャルHUD合成） ---
                half4 uiTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 procHUD = EvalProceduralHUD(input.uv, aspect);

                half3 uiPatternRGB;
                half  uiAlpha;

                if (_HasCustomTex > 0.5)
                {
                    // カスタムテクスチャ指定時: テクスチャの白文字・数字・ベクターを忠実に再現
                    uiPatternRGB = uiTex.rgb;
                    uiAlpha = uiTex.a;

                    // 動的波形パルス（UI文字・グラフが存在するピクセルのみに呼吸感ある微細発光パルスを付加）
                    if (_DynamicTelemetry > 0.5)
                    {
                        float wavePulse = (0.5 + 0.5 * sin(_Time.y * _GraphWaveSpeed * 2.0 + input.uv.x * 12.0)) * 0.25;
                        uiPatternRGB += uiPatternRGB * wavePulse;
                    }
                }
                else
                {
                    // テクスチャ未指定時: プロシージャルHUDをフル描画
                    uiPatternRGB = procHUD.rgb;
                    uiAlpha = procHUD.a;
                }

                half3 uiEmissiveRGB = uiPatternRGB * _UIColor.rgb * _UIIntensity;
                half  effectiveUIAlpha = saturate(uiAlpha * _UIOpacity);

                // --- 8. 最終カラー ＆ アルファ出力 ---
                half3 finalRGB;
                half  finalAlpha;

                if (_UseSceneBlur > 0.5)
                {
                    // 背景透過ブラーが直接サンプリングされている場合
                    // ブルーム用ハロー光はUIが存在する領域（effectiveUIAlpha）にのみ付加
                    float bloomHalo = effectiveUIAlpha * 0.35;
                    finalRGB = lerp(glassBody, uiEmissiveRGB, effectiveUIAlpha) + (uiEmissiveRGB * bloomHalo);
                    finalAlpha = 1.0;
                }
                else
                {
                    // 半透明アルファブレンディングモード
                    finalRGB = glassBody * _GlassTint.a + uiEmissiveRGB;
                    finalAlpha = saturate(_GlassTint.a * 1.5 + effectiveUIAlpha);
                }

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
