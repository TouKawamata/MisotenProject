Shader "Custom/CharacterElectricity/S_CharacterElectricity"
{
    Properties
    {
        [Header(Base Armor PBR)]
        [MainTexture] _BaseMap ("Base Map (Albedo)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.3

        [Header(Electricity Pattern Mode)]
        [Enum(AT_Murayama.CharacterElectricity.CharacterElectricityMode)]
        _PatternMode ("Pattern Preset", Float) = 0

        [Header(Electricity Master Settings)]
        [HDR] _ElectricityColor ("Electricity Color", Color) = (0.25, 0.85, 2.5, 1.0)
        _ElectricityIntensity ("Master Intensity", Range(0.0, 5.0)) = 1.8
        _ElectricitySpeed ("Discharge Speed", Range(0.1, 10.0)) = 2.5
        _ElectricityScale ("Lightning Scale / Density", Range(1.0, 40.0)) = 14.0
        _ElectricityBranching ("Branch Sharpness", Range(0.5, 4.0)) = 1.8

        [Header(Fresnel Clinging Rim)]
        _RimPower ("Fresnel Rim Power", Range(0.5, 6.0)) = 2.2
        _RimBoost ("Rim Intensity Boost", Range(0.0, 5.0)) = 2.5

        [Header(Pulse and Overcharge Dynamics)]
        _PulseFrequency ("Pulse Frequency (Pattern 2)", Range(0.2, 5.0)) = 1.8
        _GlowSpread ("Thermal Glow Spread", Range(0.0, 2.0)) = 0.6
        _JitterIntensity ("Electric Flicker / Jitter", Range(0.0, 1.0)) = 0.45

        [Header(Spatial Bounds and Impact Vector)]
        _MeshBoundsMin ("Mesh Bounds Min (Auto)", Vector) = (-0.8, 0.0, -0.5, 0.0)
        _MeshBoundsInvSize ("Mesh Bounds InvSize (Auto)", Vector) = (0.625, 0.5, 1.0, 0.0)
        _ImpactConfig ("Impact Center UV (XY) & Dir (ZW)", Vector) = (0.5, 0.55, 0.28, -0.18)

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull [_Cull]
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                float3 positionOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            #include "CharacterElectricityInput.hlsl"

            // 高速プロシージャルノイズ関数群
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float FBM(float2 uv, int octaves)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                float2x2 rot = float2x2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
                for (int i = 0; i < octaves; ++i)
                {
                    v += a * ValueNoise(uv);
                    uv = mul(rot, uv) * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // 2D空間における点pと線分(a -> b)の最短距離
            float DistToSeg2D(float2 p, float2 a, float2 b)
            {
                float2 ba = b - a;
                float2 pa = p - a;
                float h = saturate(dot(pa, ba) / max(0.0001, dot(ba, ba)));
                return length(pa - ba * h);
            }

            // 3D空間における点pと線分(a -> b)の最短距離
            float DistToSeg3D(float3 p, float3 a, float3 b)
            {
                float3 ba = b - a;
                float3 pa = p - a;
                float h = saturate(dot(pa, ba) / max(0.0001, dot(ba, ba)));
                return length(pa - ba * h);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 基本装甲カラー（元のテクスチャサンプリング）
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                // URP Standard Lighting (Forward+)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = albedo.rgb * mainLight.color * (NdotL * mainLight.shadowAttenuation + 0.2);

                // 【非スタン時・強度0の超高速パス】放電強度が0なら追加計算を完全スキップして通常装甲のみ描画
                if (_ElectricityIntensity <= 0.001)
                {
                    return half4(diffuse, albedo.a);
                }

                // フレネル（視線に対する輪郭抽出）
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, max(0.1, _RimPower));
                float rimFactor = 1.0 + fresnel * _RimBoost;

                // 2系統の動的ノイズスクロール（逆方向移動で稲妻の交差・分岐を生成）
                float time = _Time.y * _ElectricitySpeed;
                float2 scroll1 = float2(time, time * 0.6);
                float2 scroll2 = float2(-time * 0.75, time * 0.85);

                // 放電特有のランダムな明滅・瞬き（Jitter）
                float frameJitter = Hash21(float2(floor(time * 24.0), floor(uv.y * 6.0)));
                float jitter = lerp(1.0, step(0.25, frameJitter) * (0.6 + 0.8 * frameJitter), _JitterIntensity);

                float3 electricityEmission = 0.0;
                int mode = (int)round(_PatternMode);

                if (mode == 0)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン1: 高密度アーク放電 (Surface Arc Discharge)
                    // 装甲表面を血管や基板のように鋭く走る細い稲妻
                    // ─────────────────────────────────────────────────────────
                    float n1 = FBM(uv * _ElectricityScale + scroll1, 3);
                    float n2 = FBM(uv * _ElectricityScale * 1.25 + scroll2, 3);
                    float diff = abs(n1 - n2);
                    float lineArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.045 * _ElectricityBranching, diff)), 3.5);

                    float arc = lineArc * jitter * rimFactor;
                    float subArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.12 * _ElectricityBranching, diff)), 5.0) * 0.4;
                    electricityEmission = (arc + subArc) * _ElectricityColor.rgb;
                }
                else if (mode == 1)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン2: 脈動オーバードライブ (Pulsing Overcharge)
                    // 体を縦断する周期的な高圧サージウェーブ ＋ 装甲の励起グロー
                    // ─────────────────────────────────────────────────────────
                    float n1 = FBM(uv * _ElectricityScale + scroll1, 3);
                    float n2 = FBM(uv * _ElectricityScale * 1.25 + scroll2, 3);
                    float diff = abs(n1 - n2);
                    float lineArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.045 * _ElectricityBranching, diff)), 3.5);

                    float wavePhase = sin(uv.y * 18.0 - _Time.y * _PulseFrequency * 6.28) * 0.5 + 0.5;
                    float wavePulse = pow(wavePhase, 3.5);

                    // 放電ライン
                    float arc = lineArc * (0.4 + 1.2 * wavePulse) * jitter * rimFactor;
                    // 装甲自体の励起グロー（熱/エネルギー余韻）
                    float armorGlow = n1 * wavePulse * _GlowSpread * (0.6 + 0.4 * fresnel);

                    electricityEmission = (arc + armorGlow) * _ElectricityColor.rgb;
                }
                else if (mode == 2)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン3: 纏わりプラズマ膜＆局所ショート (Plasma Cling & Sparks)
                    // 輪郭に沿った電離プラズマの皮膜 ＋ 弾け飛ぶ放電ノード
                    // ─────────────────────────────────────────────────────────
                    float n1 = FBM(uv * _ElectricityScale + scroll1, 3);
                    float n2 = FBM(uv * _ElectricityScale * 1.25 + scroll2, 3);
                    float diff = abs(n1 - n2);
                    float lineArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.045 * _ElectricityBranching, diff)), 3.5);

                    float plasmaBase = fresnel * (ValueNoise(uv * 6.0 + scroll1 * 0.5) * 0.4 + 0.6);
                    float sparkNodes = pow(ValueNoise(uv * _ElectricityScale * 2.5 + scroll2 * 2.0), 6.5) * 8.0;
                    float plasmaArc = lineArc * 0.8 * fresnel;

                    float totalPlasma = (plasmaBase * 1.5 + sparkNodes * jitter + plasmaArc) * rimFactor;
                    electricityEmission = totalPlasma * _ElectricityColor.rgb;
                }
                else if (mode == 3)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン4: 超帯電ゴールド (Golden Overload)
                    // 白熱する芯を持つ高エネルギー金色の稲妻
                    // ─────────────────────────────────────────────────────────
                    float n1 = FBM(uv * _ElectricityScale + scroll1, 3);
                    float n2 = FBM(uv * _ElectricityScale * 1.25 + scroll2, 3);
                    float diff = abs(n1 - n2);
                    float lineArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.045 * _ElectricityBranching, diff)), 3.5);

                    float hotCore = pow(max(0.0, 1.0 - smoothstep(0.0, 0.02 * _ElectricityBranching, diff)), 6.0);
                    float arc = lineArc * jitter * rimFactor;
                    float3 outerColor = _ElectricityColor.rgb;
                    float3 coreColor = float3(1.0, 0.95, 0.85) * 3.0; // 白熱コア

                    electricityEmission = arc * outerColor + hotCore * coreColor * jitter * 1.5;
                }
                else if (mode == 4)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン5: クリーン・低密度ストリーク (Clean Sparse Streak)
                    // 密度を落とし、太く明瞭な数本の稲妻が走る、うるさくない洗練されたパターン
                    // ─────────────────────────────────────────────────────────
                    float sparseScale = _ElectricityScale * 0.45;
                    float sN1 = FBM(uv * sparseScale + scroll1 * 0.75, 2);
                    float sN2 = FBM(uv * sparseScale * 1.15 + scroll2 * 0.75, 2);
                    float sDiff = abs(sN1 - sN2);

                    // 太くコントラストの高い主放電ライン
                    float cleanLine = 1.0 - smoothstep(0.0, 0.055 * _ElectricityBranching, sDiff);
                    cleanLine = pow(max(0.0, cleanLine), 3.0);

                    // 白熱するシャープな中心芯（視認性の向上）
                    float cleanCore = pow(max(0.0, 1.0 - smoothstep(0.0, 0.018 * _ElectricityBranching, sDiff)), 5.0);

                    // 柔らかなオーラハロ
                    float cleanHalo = pow(max(0.0, 1.0 - smoothstep(0.0, 0.14 * _ElectricityBranching, sDiff)), 2.5) * 0.35;

                    // 控えめで上品な明滅（画面を騒がしくさせない）
                    float cleanJitter = lerp(1.0, jitter, _JitterIntensity * 0.35);

                    float cleanArc = (cleanLine + cleanHalo) * cleanJitter * (1.0 + fresnel * _RimBoost * 0.5);
                    electricityEmission = cleanArc * _ElectricityColor.rgb + cleanCore * float3(1.0, 1.0, 1.0) * cleanJitter * 2.5;
                }
                else if (mode == 5)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン6: フレネル・エッジ放電 (Fresnel Rim Discharge)
                    // 輪郭部分のみに電気が纏わりつき、正面のキャラディテールや顔を隠さない
                    // ─────────────────────────────────────────────────────────
                    float rimMask = smoothstep(0.35, 0.85, fresnel);
                    if (rimMask > 0.001)
                    {
                        float n1 = FBM(uv * _ElectricityScale + scroll1, 2);
                        float n2 = FBM(uv * _ElectricityScale * 1.2 + scroll2, 2);
                        float diff = abs(n1 - n2);
                        float lineArc = pow(max(0.0, 1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, diff)), 3.0);

                        float rimArc = lineArc * rimMask * (1.0 + fresnel * _RimBoost * 1.5);
                        float edgeNoise = ValueNoise(float2(fresnel * 12.0, time * 2.0) + uv * _ElectricityScale * 0.6);
                        float rimAura = pow(fresnel, max(0.5, _RimPower)) * _RimBoost * (0.6 + 0.4 * edgeNoise) * rimMask * jitter;

                        electricityEmission = (rimArc * 1.8 + rimAura) * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 6)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン7: 間欠的ショートスパーク (Intermittent Short Sparks)
                    // バチッ…バチッ…と不規則に激しい火花が局所的に弾け飛ぶ故障・麻痺感
                    // ─────────────────────────────────────────────────────────
                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 2.2);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    // サイクルごとの乱数判定（約65%の頻度でバースト、残りは静止）
                    float burstRand = Hash21(float2(burstCycle, 42.17));
                    float isBurst = step(0.35, burstRand);

                    // 鋭い瞬間アタックと急峻な減衰
                    float burstEnv = isBurst * exp(-burstPhase * 10.0) * step(burstPhase, 0.40);

                    if (burstEnv > 0.001)
                    {
                        // バースト時のみの50Hz高速激震フリッカー
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 50.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.0, _JitterIntensity);

                        // ショート発生起点の局所化
                        float2 sparkCenter = float2(Hash21(float2(burstCycle, 11.23)), Hash21(float2(burstCycle, 73.41)));
                        float distToCenter = length(uv - sparkCenter);
                        float sparkRadius = 0.28;
                        float sparkArea = saturate(1.0 - distToCenter / sparkRadius);
                        sparkArea = sparkArea * sparkArea;

                        // 起点から走る激しい放電クラックル
                        float spN1 = ValueNoise((uv + scroll1 * 1.5) * (_ElectricityScale * 1.8));
                        float spN2 = ValueNoise((uv + scroll2 * 1.5) * (_ElectricityScale * 2.2));
                        float spDiff = abs(spN1 - spN2);
                        float sparkCrackle = pow(max(0.0, 1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, spDiff)), 4.0);

                        // 起点中心部の白熱スパークコア（ピンポイントの超高エネルギー）
                        float sparkCore = pow(saturate(1.0 - distToCenter / 0.05), 3.0) * 4.5;

                        float sparkLines = sparkCrackle * sparkArea * 3.5 * sparkJitter * rimFactor;
                        float sparkHotspot = pow(sparkArea, 2.0) * 0.8 * sparkJitter;

                        float totalSparks = (sparkLines + sparkHotspot) * burstEnv;
                        float sparkWhite = sparkCore * burstEnv * sparkJitter;

                        electricityEmission = totalSparks * _ElectricityColor.rgb + sparkWhite * float3(1.0, 0.98, 0.92);
                    }
                    else
                    {
                        // 静止時のごく微弱な漏電リーク
                        float residualLeak = pow(fresnel, 4.0) * 0.04 * step(0.6, Hash21(float2(floor(_Time.y * 12.0), 9.0)));
                        electricityEmission = residualLeak * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 7)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン8: 電子サーキット・サイバーグリッド (Cyber Circuit Surge)
                    // メッシュに沿って電子基板の直角ラインとデータサージが走る幾何学・SFパターン
                    // ─────────────────────────────────────────────────────────
                    float2 gridCoord = uv * _ElectricityScale;

                    // マンハッタン配線：端子ノード間で完全接続される直交トレース
                    // 水平トレース：(x, y) と (x+1, y) を結ぶ
                    float hEdgeHash = Hash21(float2(floor(gridCoord.x), round(gridCoord.y) * 17.13));
                    float wireH = (1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, abs(gridCoord.y - round(gridCoord.y)))) * step(0.32, hEdgeHash);

                    // 垂直トレース：(x, y) と (x, y+1) を結ぶ
                    float vEdgeHash = Hash21(float2(round(gridCoord.x) * 13.37, floor(gridCoord.y)));
                    float wireV = (1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, abs(gridCoord.x - round(gridCoord.x)))) * step(0.32, vEdgeHash);

                    float circuitTraces = max(wireH, wireV);

                    // 回路端子・ノードパッド（交差点の端子）
                    float2 distToNode = gridCoord - round(gridCoord);
                    float nodeDist = length(distToNode);
                    float nodeDot = pow(max(0.0, 1.0 - smoothstep(0.0, 0.16, nodeDist)), 2.5) * 2.2;
                    float nodeRing = smoothstep(0.06, 0.09, nodeDist) * (1.0 - smoothstep(0.14, 0.18, nodeDist)) * 1.5;

                    // 回路を駆け巡るエネルギーサージパケット
                    float packetTime = _Time.y * _ElectricitySpeed * 2.2;
                    float packetH = pow(max(0.0, sin(gridCoord.x * 3.14159 - packetTime + hEdgeHash * 6.28)), 8.0) * wireH * 2.5;
                    float packetV = pow(max(0.0, sin(gridCoord.y * 3.14159 - packetTime + vEdgeHash * 6.28)), 8.0) * wireV * 2.5;
                    float dataPacket = packetH + packetV;

                    // サイバースキャンラインウェーブ
                    float scanline = pow(sin(uv.y * 36.0 - _Time.y * _PulseFrequency * 4.0) * 0.5 + 0.5, 4.0);

                    float circuitSignal = (circuitTraces * 0.8 + dataPacket + (nodeDot + nodeRing) * 0.7) * (0.6 + 0.7 * scanline) * jitter * rimFactor;
                    float cyberGlow = pow(fresnel, 2.5) * (circuitTraces * 0.6 + 0.15) * _RimBoost;

                    electricityEmission = (circuitSignal + cyberGlow) * _ElectricityColor.rgb + (dataPacket + nodeDot * 0.6) * float3(0.9, 1.0, 1.0) * 1.5;
                }
                else if (mode == 8)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン9: 多点連鎖クラスタースパーク (Multi-Point Chain Sparks)
                    // 2〜3箇所の別々の部位から時間差で「バチバチッ！…バチッ！」と連鎖的に飛び交う激しい過負荷ショート
                    // ─────────────────────────────────────────────────────────
                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 2.0);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float burstRand = Hash21(float2(burstCycle, 77.31));
                    float isBurst = step(0.28, burstRand); // 約72%でバースト発生

                    // 時間差エンベロープ（Node 0 -> Node 1 -> Node 2 と連鎖発火）
                    float p0 = burstPhase;
                    float p1 = burstPhase - 0.085;
                    float p2 = burstPhase - 0.170;

                    float env0 = isBurst * exp(-max(0.0, p0) * 14.0) * step(0.0, p0) * step(p0, 0.28);
                    float env1 = isBurst * exp(-max(0.0, p1) * 14.0) * step(0.0, p1) * step(p1, 0.28);
                    float env2 = isBurst * exp(-max(0.0, p2) * 14.0) * step(0.0, p2) * step(p2, 0.28);
                    float maxEnv = max(env0, max(env1, env2));

                    if (maxEnv > 0.001)
                    {
                        // 60Hz高速激震フリッカー
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        // 3箇所の独立した放電ノード中心を計算
                        float2 c0 = float2(Hash21(float2(burstCycle, 13.71)), Hash21(float2(burstCycle, 53.19)));
                        float2 c1 = float2(Hash21(float2(burstCycle, 41.25)), Hash21(float2(burstCycle, 89.63)));
                        float2 c2 = float2(Hash21(float2(burstCycle, 67.89)), Hash21(float2(burstCycle, 31.47)));

                        // 各ノードの距離と影響範囲
                        float d0 = length(uv - c0);
                        float d1 = length(uv - c1);
                        float d2 = length(uv - c2);

                        float nodeRadius = 0.24;
                        float a0 = pow(saturate(1.0 - d0 / nodeRadius), 2.0) * env0;
                        float a1 = pow(saturate(1.0 - d1 / nodeRadius), 2.0) * env1;
                        float a2 = pow(saturate(1.0 - d2 / nodeRadius), 2.0) * env2;

                        // 起点ごとの高周波クラックル放電
                        float nVal1 = ValueNoise((uv + scroll1 * 1.5) * (_ElectricityScale * 2.0));
                        float nVal2 = ValueNoise((uv + scroll2 * 1.5) * (_ElectricityScale * 2.3));
                        float crackle = pow(max(0.0, 1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, abs(nVal1 - nVal2))), 3.8);

                        // 各ノードの白熱コア（ピンポイント白光）
                        float core0 = pow(saturate(1.0 - d0 / 0.04), 3.0) * env0;
                        float core1 = pow(saturate(1.0 - d1 / 0.04), 3.0) * env1;
                        float core2 = pow(saturate(1.0 - d2 / 0.04), 3.0) * env2;
                        float totalCores = (core0 + core1 + core2) * 4.5 * sparkJitter;

                        // 1. ノード間を渡る連鎖放電ブリッジアーク (c0 と c1)
                        float2 ba01 = c1 - c0;
                        float2 pa01 = uv - c0;
                        float h01 = saturate(dot(pa01, ba01) / max(0.0001, dot(ba01, ba01)));
                        float dBridge01 = length(pa01 - ba01 * h01);

                        float bridgeNoise01 = ValueNoise(uv * (_ElectricityScale * 1.6) + scroll1 * 2.0);
                        float bridgeArc01 = pow(max(0.0, 1.0 - smoothstep(0.0, 0.035 * _ElectricityBranching, abs(dBridge01 - (bridgeNoise01 - 0.5) * 0.035 * _ElectricityBranching))), 4.0);
                        float bridgeEnv01 = max(env0, env1) * step(length(ba01), 0.75);

                        // 2. ノード間を渡る連鎖放電ブリッジアーク (c1 と c2: 連鎖ジャンプ)
                        float2 ba12 = c2 - c1;
                        float2 pa12 = uv - c1;
                        float h12 = saturate(dot(pa12, ba12) / max(0.0001, dot(ba12, ba12)));
                        float dBridge12 = length(pa12 - ba12 * h12);

                        float bridgeNoise12 = ValueNoise(uv * (_ElectricityScale * 1.6) + scroll2 * 2.0);
                        float bridgeArc12 = pow(max(0.0, 1.0 - smoothstep(0.0, 0.035 * _ElectricityBranching, abs(dBridge12 - (bridgeNoise12 - 0.5) * 0.035 * _ElectricityBranching))), 4.0);
                        float bridgeEnv12 = max(env1, env2) * step(length(ba12), 0.75);

                        float totalNodeArea = (a0 + a1 + a2);
                        float bridgeArcs = (bridgeArc01 * bridgeEnv01 + bridgeArc12 * bridgeEnv12) * 2.2;
                        float sparkLines = (crackle * totalNodeArea * 3.5 + bridgeArcs) * sparkJitter * rimFactor;
                        float sparkHotspots = (a0 * a0 + a1 * a1 + a2 * a2) * 1.2 * sparkJitter;

                        float3 sparkCol = (sparkLines + sparkHotspots) * _ElectricityColor.rgb;
                        float3 whiteCoreCol = totalCores * float3(1.0, 0.98, 0.95);

                        electricityEmission = sparkCol + whiteCoreCol;
                    }
                    else
                    {
                        // 待機時の微弱リーク
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 10.0), 3.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 9)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン10: 微小・高頻度チリチリリーク (Micro Spark Jitter / Crackle)
                    // 大爆発ではなく、小型の鋭い火花が全身各所で「チリッ…チリチリッ…」と不規則に弾ける繊細な感電麻痺
                    // ─────────────────────────────────────────────────────────
                    float2 microGrid = uv * (_ElectricityScale * 0.9);
                    float2 cellId = floor(microGrid);
                    float2 cellUv = frac(microGrid) - 0.5;

                    float cellRand = Hash21(cellId);
                    float cellRate = _Time.y * (max(1.5, _ElectricitySpeed * 2.8) + cellRand * 3.5);
                    float cellCycle = floor(cellRate);
                    float cellPhase = frac(cellRate);

                    float cellSparkRand = Hash21(cellId + float2(cellCycle, 54.12));
                    float cellActive = step(0.55, cellSparkRand); // 約45%のセルが不規則に発火

                    // セル内の火花中心点（[-0.30, +0.30]の安全マージン）
                    float2 sparkOffset = (float2(Hash21(cellId * 3.17 + cellCycle), Hash21(cellId * 7.73 + cellCycle)) - 0.5) * 0.6;
                    float distToMicro = length(cellUv - sparkOffset);

                    // 針のように鋭い超高速減衰
                    float microEnv = cellActive * exp(-cellPhase * 16.0) * step(cellPhase, 0.25);

                    // 火花周辺の微細クラックル（セル境界の矩形シーム防止：中心点からの滑らかな円形フェード）
                    float mN1 = ValueNoise((microGrid + scroll1 * 2.0) * 2.2);
                    float mN2 = ValueNoise((microGrid + scroll2 * 2.0) * 2.6);
                    float rawCrackle = pow(max(0.0, 1.0 - smoothstep(0.0, 0.07 * _ElectricityBranching, abs(mN1 - mN2))), 3.5);
                    float sparkFalloff = pow(saturate(1.0 - distToMicro / 0.32), 2.0);
                    float microCrackle = rawCrackle * sparkFalloff;

                    // セル内火花コア
                    float microCore = pow(saturate(1.0 - distToMicro / 0.12), 3.5) * 3.0;

                    // 45Hzフリッカー
                    float microJitter = Hash21(float2(floor(_Time.y * 45.0), cellCycle));
                    float jitterFactor = lerp(1.0, microJitter * 2.0, _JitterIntensity);

                    float microEmission = (microCrackle * 2.5 + microCore) * microEnv * jitterFactor * rimFactor;

                    // 装甲表面を這うごく微細な静電気チリチリノイズ（視界を阻害しない）
                    float staticTickle = pow(ValueNoise(uv * (_ElectricityScale * 1.8) + scroll1 * 0.8), 4.5) * 0.25 * (fresnel + 0.2);

                    electricityEmission = (microEmission + staticTickle) * _ElectricityColor.rgb + (microCore * microEnv * jitterFactor * 1.8) * float3(1.0, 1.0, 1.0);
                }
                else if (mode == 10)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン11: 高圧アーク放電バースト (Heavy High-Voltage Arc Burst)
                    // 予兆の小さな火花から一瞬だけ「ドカン！バチィン！」と太く強烈な高電圧アークが奔流するインパクト重視の放電
                    // ─────────────────────────────────────────────────────────
                    float heavyRate = _Time.y * max(0.3, _PulseFrequency * 0.85);
                    float heavyCycle = floor(heavyRate);
                    float heavyPhase = frac(heavyRate);

                    // フェーズ構成（境界での突然のポップを解消する滑らかな減衰配置）:
                    // 0.00 〜 0.35: 静止・帯電蓄積 (Charge buildup)
                    // 0.35 〜 0.50: 予兆小スパーク (Premonition crackles)
                    // 0.50 〜 0.62: 激烈アーク爆発 (Heavy Strike)
                    // 0.52 〜 0.90: イオン化プラズマ余熱減衰 (Ion Decay)
                    // 0.90 〜 1.00: 冷却・静止 (Cooling silence before next cycle)
                    float premonition = smoothstep(0.35, 0.50, heavyPhase) * step(heavyPhase, 0.50);
                    float strikeTau = heavyPhase - 0.50;
                    float isStrike = step(0.0, strikeTau);
                    float strikeEnv = isStrike * exp(-strikeTau * 32.0) * step(strikeTau, 0.12);
                    float ionDecay = isStrike * exp(-strikeTau * 6.0) * (1.0 - smoothstep(0.30, 0.42, strikeTau)) * (1.0 - strikeEnv);

                    // 予兆時の高周波チック火花
                    float preFlicker = step(0.45, Hash21(float2(floor(_Time.y * 35.0), heavyCycle)));
                    float preSparks = pow(ValueNoise(uv * (_ElectricityScale * 2.5) + scroll1 * 3.0), 5.0) * premonition * preFlicker * 2.0;

                    // 激烈アーク放電本体（太くうねる高電圧ストリーク）
                    float arcWidth = 0.08 * _ElectricityBranching;
                    float hN1 = FBM(uv * (_ElectricityScale * 0.55) + scroll1 * 2.5, 2);
                    float hN2 = FBM(uv * (_ElectricityScale * 0.70) + scroll2 * 2.5, 2);
                    float hDiff = abs(hN1 - hN2);

                    float heavyTrunk = pow(max(0.0, 1.0 - smoothstep(0.0, arcWidth, hDiff)), 2.5);
                    float heavyCore = pow(max(0.0, 1.0 - smoothstep(0.0, arcWidth * 0.25, hDiff)), 4.5);
                    float heavyHalo = pow(max(0.0, 1.0 - smoothstep(0.0, arcWidth * 2.2, hDiff)), 1.8) * 0.5;

                    // 爆発時フリッカー
                    float strikeFlicker = Hash21(float2(floor(_Time.y * 70.0), heavyCycle));
                    float strikeJitter = lerp(1.0, strikeFlicker * 2.5, _JitterIntensity);

                    // 爆発本体の合成
                    float strikeArc = (heavyTrunk * 3.5 + heavyHalo) * strikeEnv * strikeJitter * rimFactor;
                    float strikeBlindingCore = heavyCore * strikeEnv * strikeJitter * 6.0;

                    // 放電直後のイオン化プラズマ余熱（Ion Trail Glow）
                    float ionGlow = (heavyTrunk * 1.2 + heavyHalo * 0.8) * ionDecay * (0.8 + 0.4 * fresnel);

                    // 待機時の微弱静電気
                    float residualStatic = pow(fresnel, 4.0) * 0.03 * (1.0 - premonition) * (1.0 - isStrike);

                    float totalArc = (strikeArc + ionGlow + preSparks * 1.5 + residualStatic);
                    electricityEmission = totalArc * _ElectricityColor.rgb + strikeBlindingCore * float3(1.1, 1.1, 1.2);
                }
                else if (mode == 11)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン12: 余熱グロー付きスパーク (Thermal Afterglow Sparks / Molten Ember)
                    // 火花がバチッと弾けた後、その起点の装甲が赤熱・余熱（Molten Ember）でボワッとオレンジに残りながらゆっくり冷めていく
                    // ─────────────────────────────────────────────────────────
                    float emberRate = _Time.y * max(0.4, _PulseFrequency * 1.5);
                    float emberCycle = floor(emberRate);
                    float emberPhase = frac(emberRate);

                    float emberRand = Hash21(float2(emberCycle, 39.41));
                    float isBurst = step(0.25, emberRand); // 約75%でバースト発生

                    // 1. 高速放電スパーク（青白・急峻減衰）
                    float sparkEnv = isBurst * exp(-emberPhase * 14.0) * step(emberPhase, 0.22);

                    // 2. 装甲の赤熱・余熱グロー（Molten Ember: 緩やかな冷却カーブ）
                    // 火花直後から emberPhase = 0.85 にかけてゆっくり冷える
                    float heatEnv = isBurst * exp(-emberPhase * 2.8) * (1.0 - smoothstep(0.70, 0.90, emberPhase));

                    if (sparkEnv > 0.001 || heatEnv > 0.001)
                    {
                        // 火花起点中心
                        float2 emberCenter = float2(Hash21(float2(emberCycle, 15.73)), Hash21(float2(emberCycle, 81.39)));
                        float distToEmber = length(uv - emberCenter);

                        // 放電スパーク計算
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 55.0), emberCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        float spN1 = ValueNoise((uv + scroll1 * 1.5) * (_ElectricityScale * 1.9));
                        float spN2 = ValueNoise((uv + scroll2 * 1.5) * (_ElectricityScale * 2.3));
                        float sparkCrackle = pow(max(0.0, 1.0 - smoothstep(0.0, 0.05 * _ElectricityBranching, abs(spN1 - spN2))), 4.0);

                        float sparkRadius = 0.25;
                        float sparkArea = saturate(1.0 - distToEmber / sparkRadius);
                        sparkArea = sparkArea * sparkArea;
                        float sparkCore = pow(saturate(1.0 - distToEmber / 0.045), 3.0) * 4.0;

                        float3 sparkEmission = (sparkCrackle * sparkArea * 3.5 * sparkJitter * rimFactor) * _ElectricityColor.rgb * sparkEnv
                                             + (sparkCore * sparkEnv * sparkJitter) * float3(1.0, 0.98, 0.95);

                        // 熱伝導で少し広がりながら冷める
                        float heatRadius = 0.18 + emberPhase * 0.06;
                        float heatMask = smoothstep(0.0, 1.0, saturate(1.0 - distToEmber / heatRadius));

                        // 冷却固化するスラグ・溶融金属テクスチャ
                        float slagNoise = ValueNoise(uv * (_ElectricityScale * 2.2) + emberCenter * 10.0);
                        float emberPattern = pow(heatMask, 2.0) * (0.65 + 0.35 * slagNoise);
                        float temp = saturate(heatEnv * emberPattern);

                        // プランクの黒体放射・熱放射カラー遷移（赤熱 -> 溶融オレンジ -> 白熱）
                        float3 emberColor = lerp(float3(1.2, 0.10, 0.02), float3(2.8, 0.95, 0.08), smoothstep(0.18, 0.58, temp));
                        emberColor = lerp(emberColor, float3(1.4, 1.35, 1.0) * 2.2, smoothstep(0.58, 0.92, temp));
                        float3 thermalEmission = emberColor * temp * _GlowSpread * 2.6;

                        electricityEmission = sparkEmission + thermalEmission;
                    }
                    else
                    {
                        // 待機時または冷却完了時の微弱漏電
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 10.0), 9.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 12)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン13: 3D骨格階層・解剖学的接地アーク (Anatomical Skeletal Grounding) [次世代案1]
                    // UVの切れ目を超越し、胸/肩の高電位コアから腰・脚部のアース接地へ3D最短経路で貫通する連鎖放電
                    // ─────────────────────────────────────────────────────────
                    float3 invSize = (dot(_MeshBoundsInvSize.xyz, _MeshBoundsInvSize.xyz) > 0.0001)
                        ? _MeshBoundsInvSize.xyz
                        : float3(0.625, 0.5, 1.0);
                    float3 bMin = (dot(_MeshBoundsInvSize.xyz, _MeshBoundsInvSize.xyz) > 0.0001)
                        ? _MeshBoundsMin.xyz
                        : float3(-0.8, 0.0, -0.5);
                    float3 normPos3D = saturate((input.positionOS - bMin) * invSize);

                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 1.9);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float isBurst = step(0.26, Hash21(float2(burstCycle, 61.13)));
                    float p0 = burstPhase;
                    float p1 = burstPhase - 0.075;
                    float p2 = burstPhase - 0.150;

                    float env0 = isBurst * exp(-max(0.0, p0) * 14.0) * step(0.0, p0) * step(p0, 0.28);
                    float env1 = isBurst * exp(-max(0.0, p1) * 14.0) * step(0.0, p1) * step(p1, 0.28);
                    float env2 = isBurst * exp(-max(0.0, p2) * 14.0) * step(0.0, p2) * step(p2, 0.28);
                    float maxEnv = max(env0, max(env1, env2));

                    if (maxEnv > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        // 3D骨格上の3点：胸部アノード(高電位) -> 腰部中継ノード -> 脚部アース接地(低電位)
                        float3 nodeAnode  = float3(0.25 + 0.5 * Hash21(float2(burstCycle, 11.1)), 0.72 + 0.16 * Hash21(float2(burstCycle, 22.2)), 0.35 + 0.3 * Hash21(float2(burstCycle, 33.3)));
                        float3 nodeMid    = float3(0.20 + 0.6 * Hash21(float2(burstCycle, 44.4)), 0.42 + 0.16 * Hash21(float2(burstCycle, 55.5)), 0.30 + 0.4 * Hash21(float2(burstCycle, 66.6)));
                        float3 nodeGround = float3(0.25 + 0.5 * Hash21(float2(burstCycle, 77.7)), 0.10 + 0.18 * Hash21(float2(burstCycle, 88.8)), 0.35 + 0.3 * Hash21(float2(burstCycle, 99.9)));

                        float dNode0 = length(normPos3D - nodeAnode);
                        float dNode1 = length(normPos3D - nodeMid);
                        float dNode2 = length(normPos3D - nodeGround);

                        // 3D空間での歪みオフセット（UVシームに一切依存しない3D座標ノイズ）
                        float3 warp3D = float3(
                            ValueNoise(normPos3D.yz * _ElectricityScale + scroll1) - 0.5,
                            ValueNoise(normPos3D.xz * _ElectricityScale + scroll2) - 0.5,
                            ValueNoise(normPos3D.xy * _ElectricityScale - scroll1) - 0.5
                        ) * (0.045 * _ElectricityBranching);

                        float dSeg01 = DistToSeg3D(normPos3D + warp3D, nodeAnode, nodeMid);
                        float dSeg12 = DistToSeg3D(normPos3D + warp3D * 1.15, nodeMid, nodeGround);

                        float arcWidth = 0.032 * _ElectricityBranching;
                        float trunk01 = pow(saturate(1.0 - dSeg01 / arcWidth), 3.5) * max(env0, env1);
                        float trunk12 = pow(saturate(1.0 - dSeg12 / arcWidth), 3.5) * max(env1, env2);

                        float core01 = pow(saturate(1.0 - dSeg01 / (arcWidth * 0.35)), 5.0) * max(env0, env1);
                        float core12 = pow(saturate(1.0 - dSeg12 / (arcWidth * 0.35)), 5.0) * max(env1, env2);

                        float nodeFlash = (pow(saturate(1.0 - dNode0 / 0.18), 2.5) * env0
                                         + pow(saturate(1.0 - dNode1 / 0.18), 2.5) * env1
                                         + pow(saturate(1.0 - dNode2 / 0.18), 2.5) * env2) * 1.6;

                        float nodePinpoint = (pow(saturate(1.0 - dNode0 / 0.05), 3.5) * env0
                                            + pow(saturate(1.0 - dNode1 / 0.05), 3.5) * env1
                                            + pow(saturate(1.0 - dNode2 / 0.05), 3.5) * env2) * 4.5;

                        float totalArc = ((trunk01 + trunk12) * 3.2 + nodeFlash) * sparkJitter * rimFactor;
                        float totalWhite = ((core01 + core12) * 3.5 + nodePinpoint) * sparkJitter;

                        electricityEmission = totalArc * _ElectricityColor.rgb + totalWhite * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 4.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 13)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン14: リーヒテンベルク樹状分岐カスケード (Dendritic Lichtenberg Branching) [次世代案2]
                    // 太い主幹アークが走った直後（+18ms）、斜め前方へ鋭い小枝と孫枝がフラクタルに枝分かれして弾ける
                    // ─────────────────────────────────────────────────────────
                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 1.9);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float isBurst = step(0.28, Hash21(float2(burstCycle, 37.91)));

                    // 主幹(0ms) -> 第1枝分かれ(+18ms) -> 孫枝・先端スパーク(+36ms)
                    float envTrunk  = isBurst * exp(-burstPhase * 13.0) * step(burstPhase, 0.30);
                    float pBranch   = burstPhase - 0.060;
                    float envBranch = isBurst * exp(-max(0.0, pBranch) * 16.0) * step(0.0, pBranch) * step(pBranch, 0.25);
                    float pTwig     = burstPhase - 0.115;
                    float envTwig   = isBurst * exp(-max(0.0, pTwig) * 20.0) * step(0.0, pTwig) * step(pTwig, 0.20);

                    if (max(envTrunk, max(envBranch, envTwig)) > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        // 主幹の始点 c0 と終点 c1
                        float2 c0 = float2(0.22 + 0.56 * Hash21(float2(burstCycle, 14.3)), 0.22 + 0.56 * Hash21(float2(burstCycle, 29.7)));
                        float angle = Hash21(float2(burstCycle, 51.2)) * 6.28318;
                        float2 trunkDir = float2(cos(angle), sin(angle));
                        float2 c1 = clamp(c0 + trunkDir * 0.36, 0.08, 0.92);
                        trunkDir = normalize(c1 - c0 + 0.0001);

                        // 空間ワープ（高周波の鋭い稲妻折れ曲がり）
                        float n1 = ValueNoise((uv + scroll1 * 1.4) * (_ElectricityScale * 1.7)) - 0.5;
                        float n2 = ValueNoise((uv + scroll2 * 1.4) * (_ElectricityScale * 2.1)) - 0.5;
                        float2 uvWarped = uv + float2(n1, n2) * (0.042 * _ElectricityBranching);

                        // 主幹距離
                        float dTrunk = DistToSeg2D(uvWarped, c0, c1);
                        float trunkWidth = 0.024 * _ElectricityBranching;
                        float trunkLine = pow(saturate(1.0 - dTrunk / trunkWidth), 3.5) * envTrunk;
                        float trunkCore = pow(saturate(1.0 - dTrunk / (trunkWidth * 0.32)), 5.0) * envTrunk;

                        // 分岐点1 (40%地点) と 分岐点2 (75%地点) から斜め前方(±35度、±50度)へ伸びる4本の枝
                        float2 bOrigin1 = lerp(c0, c1, 0.40);
                        float2 bOrigin2 = lerp(c0, c1, 0.75);
                        float2 ortho = float2(-trunkDir.y, trunkDir.x);

                        float2 bEnd1A = bOrigin1 + (trunkDir * 0.82 + ortho * 0.57) * 0.19;
                        float2 bEnd1B = bOrigin1 + (trunkDir * 0.76 - ortho * 0.65) * 0.16;
                        float2 bEnd2A = bOrigin2 + (trunkDir * 0.78 - ortho * 0.62) * 0.18;
                        float2 bEnd2B = bOrigin2 + (trunkDir * 0.65 + ortho * 0.76) * 0.15;

                        float dBranch1 = min(DistToSeg2D(uvWarped, bOrigin1, bEnd1A), DistToSeg2D(uvWarped, bOrigin1, bEnd1B));
                        float dBranch2 = min(DistToSeg2D(uvWarped, bOrigin2, bEnd2A), DistToSeg2D(uvWarped, bOrigin2, bEnd2B));
                        float dBranch  = min(dBranch1, dBranch2);

                        float branchWidth = 0.015 * _ElectricityBranching;
                        float branchLine = pow(saturate(1.0 - dBranch / branchWidth), 3.5) * envBranch;
                        float branchCore = pow(saturate(1.0 - dBranch / (branchWidth * 0.35)), 5.0) * envBranch;

                        // 孫枝（先端からさらに細かく弾けるクラックル）
                        float dTwigEnd = min(min(length(uv - bEnd1A), length(uv - bEnd2A)), min(length(uv - bEnd1B), length(uv - c1)));
                        float twigArea = pow(saturate(1.0 - dTwigEnd / 0.14), 2.0) * envTwig;
                        float twigCrackle = pow(saturate(1.0 - abs(n1 - n2) / (0.045 * _ElectricityBranching)), 4.0) * twigArea * 3.5;

                        // 起点白熱コア
                        float rootCore = pow(saturate(1.0 - length(uv - c0) / 0.045), 3.5) * envTrunk * 4.5;

                        float totalColored = (trunkLine * 3.2 + branchLine * 2.6 + twigCrackle) * sparkJitter * rimFactor;
                        float totalWhite   = (trunkCore * 3.8 + branchCore * 2.5 + rootCore) * sparkJitter;

                        electricityEmission = totalColored * _ElectricityColor.rgb + totalWhite * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 5.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 14)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン15: 絶縁破壊ステップトリーダー・スタッカート (Stepped Leader Staccato) [次世代案3]
                    // 「チッ(25%)…パチッ(60%)…」と前駆放電が小刻みに前進した直後、「バチィィン！(100%)」と極太リターンストロークが炸裂
                    // ─────────────────────────────────────────────────────────
                    float leaderRate = _Time.y * max(0.4, _PulseFrequency * 1.45);
                    float leaderCycle = floor(leaderRate);
                    float leaderPhase = frac(leaderRate);

                    float isBurst = step(0.22, Hash21(float2(leaderCycle, 83.19)));

                    // 3段ジグザグ経路の頂点 S0 -> S1 -> S2 -> S3
                    float2 s0 = float2(0.20 + 0.60 * Hash21(float2(leaderCycle, 11.7)), 0.72 + 0.18 * Hash21(float2(leaderCycle, 23.4)));
                    float2 s3 = float2(0.20 + 0.60 * Hash21(float2(leaderCycle, 35.1)), 0.14 + 0.20 * Hash21(float2(leaderCycle, 47.8)));
                    float2 mainVec = s3 - s0;
                    float2 perpVec = float2(-mainVec.y, mainVec.x);

                    float2 s1 = lerp(s0, s3, 0.33) + perpVec * ((Hash21(float2(leaderCycle, 59.3)) - 0.5) * 0.45);
                    float2 s2 = lerp(s0, s3, 0.67) + perpVec * ((Hash21(float2(leaderCycle, 71.6)) - 0.5) * 0.45);

                    // Step 1 (0.00〜0.09): チッ！ (S0 -> S1)
                    float step1Env = isBurst * step(0.00, leaderPhase) * step(leaderPhase, 0.10) * exp(-leaderPhase * 12.0);
                    // Step 2 (0.10〜0.19): パチッ！ (S0 -> S1 -> S2)
                    float step2Env = isBurst * step(0.10, leaderPhase) * step(leaderPhase, 0.20) * exp(-(leaderPhase - 0.10) * 12.0);
                    // Return Stroke (0.20〜0.48): バチィィン！！ (全線開通・超高輝度リターンストローク)
                    float returnTau = leaderPhase - 0.20;
                    float returnEnv = isBurst * step(0.0, returnTau) * step(returnTau, 0.32) * exp(-returnTau * 14.0);

                    if (max(step1Env, max(step2Env, returnEnv)) > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 65.0), leaderCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.3, _JitterIntensity);

                        float n1 = ValueNoise((uv + scroll1 * 1.6) * (_ElectricityScale * 1.8)) - 0.5;
                        float n2 = ValueNoise((uv + scroll2 * 1.6) * (_ElectricityScale * 2.2)) - 0.5;
                        float2 uvW = uv + float2(n1, n2) * (0.038 * _ElectricityBranching);

                        float d01 = DistToSeg2D(uvW, s0, s1);
                        float d12 = DistToSeg2D(uvW, s1, s2);
                        float d23 = DistToSeg2D(uvW, s2, s3);

                        // 前駆放電（細く鋭いリーダーライン ＋ 先端の絶縁破壊スパーク点）
                        float leaderW = 0.014 * _ElectricityBranching;
                        float l01 = pow(saturate(1.0 - d01 / leaderW), 3.5) * (step1Env + step2Env * 0.6);
                        float l12 = pow(saturate(1.0 - d12 / leaderW), 3.5) * step2Env;
                        float tip1 = pow(saturate(1.0 - length(uv - s1) / 0.045), 3.0) * step1Env * 3.5;
                        float tip2 = pow(saturate(1.0 - length(uv - s2) / 0.045), 3.0) * step2Env * 4.0;

                        // リターンストローク本線（全セグメントが一気に極太白熱化）
                        float dAll = min(d01, min(d12, d23));
                        float returnW = 0.032 * _ElectricityBranching;
                        float returnTrunk = pow(saturate(1.0 - dAll / returnW), 3.0) * returnEnv * 4.2;
                        float returnCore  = pow(saturate(1.0 - dAll / (returnW * 0.35)), 5.0) * returnEnv * 6.0;
                        float impactEnds  = (pow(saturate(1.0 - length(uv - s0) / 0.07), 3.0)
                                           + pow(saturate(1.0 - length(uv - s3) / 0.07), 3.0)) * returnEnv * 4.5;

                        float totalColored = (l01 * 1.8 + l12 * 2.2 + returnTrunk) * sparkJitter * rimFactor;
                        float totalWhite   = (tip1 + tip2 + returnCore + impactEnds) * sparkJitter;

                        electricityEmission = totalColored * _ElectricityColor.rgb + totalWhite * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 6.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 15)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン16: 被弾点・タックル衝突ベクトル飛散放電 (Impact Dynamic Localization) [次世代案4]
                    // 被弾した衝撃点（Impact Center）を起点に、気流・吹っ飛び方向（Impact Dir）へ扇状に電撃と火花が飛散する
                    // ─────────────────────────────────────────────────────────
                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 2.1);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float isBurst = step(0.24, Hash21(float2(burstCycle, 91.47)));

                    // 着弾点(0ms) -> 第1飛散波(+20ms) -> 第2飛散波(+45ms)
                    float envImpact = isBurst * exp(-burstPhase * 15.0) * step(burstPhase, 0.25);
                    float pWave1    = burstPhase - 0.055;
                    float envWave1  = isBurst * exp(-max(0.0, pWave1) * 14.0) * step(0.0, pWave1) * step(pWave1, 0.28);
                    float pWave2    = burstPhase - 0.115;
                    float envWave2  = isBurst * exp(-max(0.0, pWave2) * 14.0) * step(0.0, pWave2) * step(pWave2, 0.28);

                    if (max(envImpact, max(envWave1, envWave2)) > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        // 被弾中心と吹っ飛びベクトル（_ImpactConfig から取得 ＋ サイクル毎の微小揺らぎ）
                        float2 baseImpact = _ImpactConfig.xy;
                        float2 baseDir    = (dot(_ImpactConfig.zw, _ImpactConfig.zw) > 0.001) ? _ImpactConfig.zw : float2(0.28, -0.18);
                        float2 jitterPos  = (float2(Hash21(float2(burstCycle, 12.3)), Hash21(float2(burstCycle, 45.6))) - 0.5) * 0.10;
                        float2 cImpact    = clamp(baseImpact + jitterPos, 0.12, 0.88);

                        float2 flowDir  = normalize(baseDir);
                        float2 flowPerp = float2(-flowDir.y, flowDir.x);
                        float flowLen   = clamp(length(baseDir), 0.18, 0.42);

                        // 扇状（0度、+28度、-28度）に後方へ飛散するストリーマー終点
                        float2 cMid1 = cImpact + (flowDir * 0.50 + flowPerp * 0.18) * flowLen;
                        float2 cMid2 = cImpact + (flowDir * 0.50 - flowPerp * 0.18) * flowLen;
                        float2 cTail0 = cImpact + flowDir * flowLen * 1.15;
                        float2 cTail1 = cImpact + (flowDir * 0.85 + flowPerp * 0.42) * flowLen * 1.10;
                        float2 cTail2 = cImpact + (flowDir * 0.85 - flowPerp * 0.42) * flowLen * 1.10;

                        float n1 = ValueNoise((uv + scroll1 * 1.8) * (_ElectricityScale * 1.8)) - 0.5;
                        float n2 = ValueNoise((uv + scroll2 * 1.8) * (_ElectricityScale * 2.2)) - 0.5;
                        float2 uvW = uv + float2(n1, n2) * (0.04 * _ElectricityBranching);

                        // 着弾点の爆発クラックル
                        float dImpact = length(uv - cImpact);
                        float impactArea = pow(saturate(1.0 - dImpact / 0.24), 2.0) * envImpact;
                        float impactCore = pow(saturate(1.0 - dImpact / 0.055), 3.0) * envImpact * 5.0;
                        float crackle = pow(saturate(1.0 - abs(n1 - n2) / (0.05 * _ElectricityBranching)), 3.8);

                        // 後方へ吹き飛ぶ3本のカスケードストリーマー
                        float dStream1 = min(DistToSeg2D(uvW, cImpact, cMid1), DistToSeg2D(uvW, cImpact, cMid2));
                        float dStream2 = min(DistToSeg2D(uvW, cImpact, cTail0), min(DistToSeg2D(uvW, cMid1, cTail1), DistToSeg2D(uvW, cMid2, cTail2)));

                        float streamW = 0.022 * _ElectricityBranching;
                        float streamArc1 = pow(saturate(1.0 - dStream1 / streamW), 3.5) * envWave1;
                        float streamArc2 = pow(saturate(1.0 - dStream2 / (streamW * 0.85)), 3.5) * envWave2;

                        // 後方へちぎれて飛ぶ火花粒
                        float dSparks = min(length(uv - cTail0), min(length(uv - cTail1), length(uv - cTail2)));
                        float tailSparks = pow(saturate(1.0 - dSparks / 0.04), 3.0) * envWave2 * 4.0;

                        float totalColored = (crackle * impactArea * 3.8 + (streamArc1 + streamArc2) * 3.2) * sparkJitter * rimFactor;
                        float totalWhite   = (impactCore + tailSparks + pow(streamArc1, 2.0) * 2.5) * sparkJitter;

                        electricityEmission = totalColored * _ElectricityColor.rgb + totalWhite * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 7.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 16)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン17: 衝撃波法線ディストーション＆爆圧スパーク (Kinetic Shockwave Refraction) [次世代案5]
                    // スパークが炸裂した瞬間、同心円状の衝撃波リングで周囲の装甲テクスチャと法線が物理的にグニャッと歪む
                    // ─────────────────────────────────────────────────────────
                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 1.85);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float isBurst = step(0.25, Hash21(float2(burstCycle, 19.83)));
                    float burstEnv = isBurst * exp(-burstPhase * 11.0) * step(burstPhase, 0.36);

                    if (burstEnv > 0.001)
                    {
                        float2 sparkCenter = float2(0.20 + 0.60 * Hash21(float2(burstCycle, 31.7)), 0.20 + 0.60 * Hash21(float2(burstCycle, 67.3)));
                        float2 toPixel = uv - sparkCenter;
                        float dist = length(toPixel);
                        float2 radialDir = toPixel / max(0.0001, dist);

                        // 膨張する音速衝撃波リング（半径 0.02 -> 0.38 へ急膨張）
                        float shockRadius = 0.02 + burstPhase * 1.15;
                        float ringWidth = 0.045;
                        float ringDelta = dist - shockRadius;
                        float shockRing = exp(-(ringDelta * ringDelta) / (ringWidth * ringWidth)) * smoothstep(0.35, 0.0, burstPhase) * isBurst;

                        // 衝撃波による装甲UV屈折（テクスチャのモールドや模様が爆圧で揺れる）
                        float2 refractedUV = uv - radialDir * (shockRing * 0.032);
                        half4 refractedAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, refractedUV) * _BaseColor;

                        // 衝撃波による法線の波紋（鏡面ハイライトとリム光がリング状に煌めく）
                        float3 perturbedNormal = normalize(normalWS + float3(radialDir.x, radialDir.y, 0.0) * (sin(ringDelta * 90.0) * shockRing * 0.85));
                        float pertNdotL = saturate(dot(perturbedNormal, lightDir));
                        diffuse = refractedAlbedo.rgb * mainLight.color * (pertNdotL * mainLight.shadowAttenuation + 0.2);

                        // 衝撃波リング自体の空気電離発光
                        float3 shockRimGlow = shockRing * 1.8 * _ElectricityColor.rgb;

                        // 爆心地の激しい短絡スパーク
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 55.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        float spN1 = ValueNoise((refractedUV + scroll1 * 1.6) * (_ElectricityScale * 1.9));
                        float spN2 = ValueNoise((refractedUV + scroll2 * 1.6) * (_ElectricityScale * 2.3));
                        float sparkCrackle = pow(saturate(1.0 - abs(spN1 - spN2) / (0.05 * _ElectricityBranching)), 4.0);

                        float sparkArea = pow(saturate(1.0 - dist / 0.26), 2.0);
                        float sparkCore = pow(saturate(1.0 - dist / 0.055), 3.2) * 5.0;

                        float3 centerSparks = (sparkCrackle * sparkArea * 3.8 * sparkJitter * rimFactor) * _ElectricityColor.rgb * burstEnv
                                            + (sparkCore * burstEnv * sparkJitter) * float3(1.0, 0.98, 0.95);

                        electricityEmission = centerSparks + shockRimGlow;
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 8.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 17)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン18: 双極マグネティック・アークウィップ (Magnetic Arc Whip) [独自追加案1]
                    // 2つの電極間でアークが磁場(ローレンツ力)で弓なりにしなり(Whip)、限界でパチン！と千切れて火花を散らす
                    // ─────────────────────────────────────────────────────────
                    float whipRate = _Time.y * max(0.5, _PulseFrequency * 2.0);
                    float whipCycle = floor(whipRate);
                    float whipPhase = frac(whipRate);

                    float isBurst = step(0.22, Hash21(float2(whipCycle, 49.17)));

                    // 時間差で2組の双極アークウィップがしなる
                    float pA = whipPhase;
                    float pB = whipPhase - 0.14;
                    float envA = isBurst * step(0.0, pA) * step(pA, 0.34) * (1.0 - smoothstep(0.24, 0.34, pA));
                    float envB = isBurst * step(0.0, pB) * step(pB, 0.34) * (1.0 - smoothstep(0.24, 0.34, pB));

                    if (max(envA, envB) > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), whipCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        float n1 = ValueNoise((uv + scroll1 * 1.5) * (_ElectricityScale * 1.7)) - 0.5;
                        float n2 = ValueNoise((uv + scroll2 * 1.5) * (_ElectricityScale * 2.1)) - 0.5;

                        // --- 第1ウィップ電極ペア ---
                        float2 a0 = float2(0.22 + 0.55 * Hash21(float2(whipCycle, 13.1)), 0.25 + 0.50 * Hash21(float2(whipCycle, 27.3)));
                        float angA = Hash21(float2(whipCycle, 39.5)) * 6.28318;
                        float2 dirA = float2(cos(angA), sin(angA));
                        float2 perpA = float2(-dirA.y, dirA.x);
                        float lenA = 0.26;
                        float2 a1 = a0 + dirA * lenA;

                        float2 relA = uv - a0;
                        float uParaA = saturate(dot(relA, dirA) / lenA);
                        float uPerpA = dot(relA, perpA);
                        // 放物線アーチ 4 * u * (1 - u) が時間経過 pA とともに外へ膨らんでしなる
                        float bowA = 4.0 * uParaA * (1.0 - uParaA) * (0.02 + pow(max(0.0, pA * 3.0), 1.4) * 0.14);
                        float dCurveA = length(float2((dot(relA, dirA) - uParaA * lenA), uPerpA - bowA + n1 * 0.032 * _ElectricityBranching));
                        // 断裂寸前(pA > 0.22)にアークが千切れてビーズ状の火花になる
                        float snapMaskA = lerp(1.0, step(0.35, frac(uParaA * 7.0 + n2)), smoothstep(0.20, 0.32, pA));
                        float whipArcA = pow(saturate(1.0 - dCurveA / (0.022 * _ElectricityBranching)), 3.5) * snapMaskA * envA;
                        float whipCoreA = pow(saturate(1.0 - dCurveA / (0.008 * _ElectricityBranching)), 5.0) * snapMaskA * envA;
                        float polesA = (pow(saturate(1.0 - length(uv - a0) / 0.045), 3.0) + pow(saturate(1.0 - length(uv - a1) / 0.045), 3.0)) * envA * 3.5;

                        // --- 第2ウィップ電極ペア (時間差連鎖) ---
                        float2 b0 = float2(0.22 + 0.55 * Hash21(float2(whipCycle, 61.7)), 0.25 + 0.50 * Hash21(float2(whipCycle, 74.9)));
                        float angB = Hash21(float2(whipCycle, 88.1)) * 6.28318;
                        float2 dirB = float2(cos(angB), sin(angB));
                        float2 perpB = float2(-dirB.y, dirB.x);
                        float lenB = 0.26;
                        float2 b1 = b0 + dirB * lenB;

                        float2 relB = uv - b0;
                        float uParaB = saturate(dot(relB, dirB) / lenB);
                        float uPerpB = dot(relB, perpB);
                        float bowB = 4.0 * uParaB * (1.0 - uParaB) * (0.02 + pow(max(0.0, max(0.0, pB) * 3.0), 1.4) * 0.14);
                        float dCurveB = length(float2((dot(relB, dirB) - uParaB * lenB), uPerpB - bowB + n2 * 0.032 * _ElectricityBranching));
                        float snapMaskB = lerp(1.0, step(0.35, frac(uParaB * 7.0 + n1)), smoothstep(0.20, 0.32, max(0.0, pB)));
                        float whipArcB = pow(saturate(1.0 - dCurveB / (0.022 * _ElectricityBranching)), 3.5) * snapMaskB * envB;
                        float whipCoreB = pow(saturate(1.0 - dCurveB / (0.008 * _ElectricityBranching)), 5.0) * snapMaskB * envB;
                        float polesB = (pow(saturate(1.0 - length(uv - b0) / 0.045), 3.0) + pow(saturate(1.0 - length(uv - b1) / 0.045), 3.0)) * envB * 3.5;

                        float totalColored = (whipArcA + whipArcB) * 3.5 * sparkJitter * rimFactor;
                        float totalWhite   = ((whipCoreA + whipCoreB) * 4.2 + polesA + polesB) * sparkJitter;

                        electricityEmission = totalColored * _ElectricityColor.rgb + totalWhite * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 10.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 18)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン19: 装甲シーム（継ぎ目・エッジ）沿い短絡奔流 (Armor Seam & Crease Short) [独自追加案2]
                    // テクスチャの塗り分け境界・モールドやポリゴン法線の角（エッジ）を自動検出し、パーツの隙間から火花が噴き出す
                    // ─────────────────────────────────────────────────────────
                    // 1. 装甲のテクスチャ境界（色差微分）と 3Dポリゴン角（法線微分）のリアルタイム抽出
                    float3 dAlbedo = fwidth(albedo.rgb);
                    float3 dNorm   = fwidth(normalWS);
                    float seamStrength = saturate(dot(dAlbedo, float3(2.5, 2.5, 2.5)) + length(dNorm) * 1.8);

                    float burstRate = _Time.y * max(0.5, _PulseFrequency * 2.1);
                    float burstCycle = floor(burstRate);
                    float burstPhase = frac(burstRate);

                    float isBurst = step(0.24, Hash21(float2(burstCycle, 57.29)));
                    float env0 = isBurst * exp(-burstPhase * 13.0) * step(burstPhase, 0.30);
                    float p1   = burstPhase - 0.08;
                    float env1 = isBurst * exp(-max(0.0, p1) * 13.0) * step(0.0, p1) * step(p1, 0.30);

                    if (max(env0, env1) > 0.001)
                    {
                        float sparkFlicker = Hash21(float2(floor(_Time.y * 60.0), burstCycle));
                        float sparkJitter = lerp(1.0, sparkFlicker * 2.2, _JitterIntensity);

                        float2 c0 = float2(0.18 + 0.64 * Hash21(float2(burstCycle, 17.3)), 0.18 + 0.64 * Hash21(float2(burstCycle, 43.9)));
                        float2 c1 = float2(0.18 + 0.64 * Hash21(float2(burstCycle, 69.1)), 0.18 + 0.64 * Hash21(float2(burstCycle, 85.7)));

                        float d0 = length(uv - c0);
                        float d1 = length(uv - c1);

                        float area0 = pow(saturate(1.0 - d0 / 0.28), 1.8) * env0;
                        float area1 = pow(saturate(1.0 - d1 / 0.28), 1.8) * env1;
                        float activeZone = area0 + area1;

                        float n1 = ValueNoise((uv + scroll1 * 1.6) * (_ElectricityScale * 1.9));
                        float n2 = ValueNoise((uv + scroll2 * 1.6) * (_ElectricityScale * 2.3));
                        float crackle = pow(saturate(1.0 - abs(n1 - n2) / (0.055 * _ElectricityBranching)), 3.8);

                        // 装甲のシーム（継ぎ目・角）上で電撃が3.5倍に集中・激化する
                        float seamArc = (crackle * (1.0 + seamStrength * 3.8) + seamStrength * 2.2) * activeZone;
                        float core0 = pow(saturate(1.0 - d0 / 0.048), 3.0) * env0 * 4.5;
                        float core1 = pow(saturate(1.0 - d1 / 0.048), 3.0) * env1 * 4.5;
                        float seamWhiteHot = seamStrength * crackle * activeZone * 4.0;

                        electricityEmission = (seamArc * 3.0 * sparkJitter * rimFactor) * _ElectricityColor.rgb
                                            + ((core0 + core1 + seamWhiteHot) * sparkJitter) * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        // 待機時も装甲の継ぎ目・エッジに微弱な漏電がチリッと走る
                        float residual = (pow(fresnel, 3.5) + seamStrength * 0.5) * 0.045 * step(0.62, Hash21(float2(floor(_Time.y * 12.0), 11.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else if (mode == 19)
                {
                    // ─────────────────────────────────────────────────────────
                    // パターン20: 臨界メルトダウン・ランダムストロボ連鎖 (Critical Strobe Cascade) [P7×P9究極進化・独自案3]
                    // P7の「単発極小高輝度スパーク」とP9の「3連跳躍ブリッジ」が非周期リズムでランダムに切り替わる集大成パターン
                    // ─────────────────────────────────────────────────────────
                    // 2つの非整数比リズムを合成して周期的な「慣れ」を完全に排除
                    float baseRate = _Time.y * max(0.5, _PulseFrequency * 2.15);
                    float cycleId  = floor(baseRate);
                    float phase    = frac(baseRate);

                    float modeSelector = Hash21(float2(cycleId, 97.31));
                    float isActive = step(0.20, modeSelector); // 80%稼働、20%完全静寂の溜め

                    float sparkFlicker = Hash21(float2(floor(_Time.y * 65.0), cycleId));
                    float sparkJitter = lerp(1.0, sparkFlicker * 2.3, _JitterIntensity);

                    float n1 = ValueNoise((uv + scroll1 * 1.6) * (_ElectricityScale * 1.9)) - 0.5;
                    float n2 = ValueNoise((uv + scroll2 * 1.6) * (_ElectricityScale * 2.3)) - 0.5;
                    float crackle = pow(saturate(1.0 - abs(n1 - n2) / (0.048 * _ElectricityBranching)), 4.0);

                    if (isActive > 0.5 && modeSelector < 0.56)
                    {
                        // 【サブモードA (45%): P7強化型・超鋭利ピンポイント2段ダブルタップ短絡】
                        // 「バチッ・バチッ！」と同一箇所付近で2回連続スパークが炸裂
                        float tap1 = exp(-phase * 18.0) * step(phase, 0.14);
                        float pTap2 = phase - 0.11;
                        float tap2 = exp(-max(0.0, pTap2) * 15.0) * step(0.0, pTap2) * step(pTap2, 0.24) * 1.35;
                        float envTap = tap1 + tap2;

                        float2 center = float2(0.18 + 0.64 * Hash21(float2(cycleId, 19.1)), 0.18 + 0.64 * Hash21(float2(cycleId, 73.7)));
                        float dist = length(uv - center);
                        float area = pow(saturate(1.0 - dist / 0.26), 2.0);
                        float core = pow(saturate(1.0 - dist / 0.052), 3.2) * 5.2;

                        electricityEmission = (crackle * area * 3.8 * sparkJitter * rimFactor * envTap) * _ElectricityColor.rgb
                                            + (core * envTap * sparkJitter) * float3(1.0, 0.98, 0.95);
                    }
                    else if (isActive > 0.5)
                    {
                        // 【サブモードB (55%): P9強化型・3点高速跳躍カスケード＋クロスブリッジ】
                        // 「バチ・バチ・バチィン！」と3点を高速ジャンプして最後に強烈な架橋アークを残す
                        float p0 = phase;
                        float p1 = phase - 0.075;
                        float p2 = phase - 0.150;

                        float env0 = exp(-max(0.0, p0) * 15.0) * step(0.0, p0) * step(p0, 0.26);
                        float env1 = exp(-max(0.0, p1) * 15.0) * step(0.0, p1) * step(p1, 0.26);
                        float env2 = exp(-max(0.0, p2) * 13.0) * step(0.0, p2) * step(p2, 0.30) * 1.25;

                        float2 c0 = float2(0.18 + 0.64 * Hash21(float2(cycleId, 13.7)), 0.18 + 0.64 * Hash21(float2(cycleId, 53.1)));
                        float2 c1 = float2(0.18 + 0.64 * Hash21(float2(cycleId, 41.2)), 0.18 + 0.64 * Hash21(float2(cycleId, 89.6)));
                        float2 c2 = float2(0.18 + 0.64 * Hash21(float2(cycleId, 67.8)), 0.18 + 0.64 * Hash21(float2(cycleId, 31.4)));

                        float d0 = length(uv - c0);
                        float d1 = length(uv - c1);
                        float d2 = length(uv - c2);

                        float a0 = pow(saturate(1.0 - d0 / 0.22), 2.0) * env0;
                        float a1 = pow(saturate(1.0 - d1 / 0.22), 2.0) * env1;
                        float a2 = pow(saturate(1.0 - d2 / 0.25), 2.0) * env2;

                        float2 uvW = uv + float2(n1, n2) * (0.036 * _ElectricityBranching);
                        float dB01 = DistToSeg2D(uvW, c0, c1);
                        float dB12 = DistToSeg2D(uvW, c1, c2);
                        float brW  = 0.022 * _ElectricityBranching;

                        float br01 = pow(saturate(1.0 - dB01 / brW), 3.8) * max(env0, env1) * step(length(c1 - c0), 0.75);
                        float br12 = pow(saturate(1.0 - dB12 / brW), 3.8) * max(env1, env2) * step(length(c2 - c1), 0.75);

                        float cores = (pow(saturate(1.0 - d0 / 0.045), 3.2) * env0
                                     + pow(saturate(1.0 - d1 / 0.045), 3.2) * env1
                                     + pow(saturate(1.0 - d2 / 0.052), 3.2) * env2) * 4.8;
                        float brCore = (pow(br01, 2.0) + pow(br12, 2.0)) * 2.5;

                        float totalArc = (crackle * (a0 + a1 + a2) * 3.6 + (br01 + br12) * 2.8) * sparkJitter * rimFactor;
                        electricityEmission = totalArc * _ElectricityColor.rgb + ((cores + brCore) * sparkJitter) * float3(1.0, 0.98, 0.95);
                    }
                    else
                    {
                        float residual = pow(fresnel, 4.0) * 0.04 * step(0.65, Hash21(float2(floor(_Time.y * 12.0), 12.0)));
                        electricityEmission = residual * _ElectricityColor.rgb;
                    }
                }
                else
                {
                    electricityEmission = 0.0;
                }

                electricityEmission *= _ElectricityIntensity;

                // 最終合成：装甲拡散色 ＋ 電流Emission加算
                float3 finalColor = diffuse + electricityEmission;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "CharacterElectricityInput.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CharacterElectricityInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
}

