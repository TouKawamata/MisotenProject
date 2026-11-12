Shader "Custom/SmartArrowSignage/S_SmartArrowSignage"
{
    Properties
    {
        [Header(Sign Mode and Rendering)]
        [Enum(FrostedGlass,0,PureHologram,1,MinimalGlass,2,CyberGridHolo,3,DarkAeroVisor,4,EnergyShield,5,AuroraPrism,6)] _SignMode ("Sign Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 2
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4

        [Header(Procedural SDF Arrow Geometry)]
        [Enum(SharpRacing,0,BoldSign,1,DoubleChevron,2,TripleChevron,3,NeonTube,4,QuantumLattice,5,HazardStripes,6,CurvedSpeed,7)] _ArrowPattern ("Arrow Pattern", Float) = 0
        _ArrowAngle ("Arrow Angle (Degrees)", Range(-180.0, 180.0)) = 0.0
        _ArrowScale ("Arrow Scale", Range(0.2, 3.0)) = 1.0
        _ArrowAspect ("Arrow Aspect Ratio (Width / Height)", Range(0.2, 3.0)) = 1.0
        _ArrowRepeat ("Arrow Repeat Count", Range(1.0, 6.0)) = 1.0
        _ArrowSpacing ("Arrow Spacing", Range(0.1, 1.0)) = 0.35
        _ArrowOffset ("Arrow Offset (X, Y)", Vector) = (0, 0, 0, 0)

        [Header(Arrow Animation)]
        [Enum(Flowing,0,Blinking,1,Pulse,2,ScanlineFlow,3,Static,4)] _AnimMode ("Animation Mode", Float) = 0
        _AnimSpeed ("Animation Speed", Range(0.0, 10.0)) = 2.5

        [Header(Arrow Colors and HDR Emission)]
        [HDR] _ArrowColor ("Primary Arrow Color (HDR)", Color) = (0.25, 0.90, 1.0, 1.0)
        [HDR] _SecondaryColor ("Secondary Accent Color (HDR)", Color) = (1.0, 0.45, 0.1, 1.0)
        _EmissionMultiplier ("Emission Brightness (Bloom)", Range(0.5, 10.0)) = 2.8
        [HDR] _CoreColor ("Inner Core Glow Color (HDR)", Color) = (1.0, 1.0, 1.0, 1.0)
        _CoreIntensity ("Core Glow Intensity", Range(0.0, 5.0)) = 2.0
        _GlowWidth ("Outer Soft Glow Radius", Range(0.0, 0.25)) = 0.06
        _GlowIntensity ("Outer Soft Glow Intensity", Range(0.0, 3.0)) = 1.2
        _OutlineWidth ("Outline Width (Neon / Tube)", Range(0.01, 0.15)) = 0.04
        _StripeDensity ("Stripe Density (Hazard)", Range(5.0, 50.0)) = 22.0

        [Header(HUD Tech Decorations)]
        [Toggle] _ShowHUDDecorations ("Show Corner Tech Brackets & Grid", Float) = 1.0
        [HDR] _HUDColor ("HUD Accent Color (HDR)", Color) = (0.75, 0.95, 1.0, 0.7)

        [Header(Frosted Glass Physical Transmission)]
        [Toggle] _UseSceneBlur ("Enable Background Frosted Blur", Float) = 1.0
        _BlurRadius ("Frosted Blur Radius", Range(0.0, 0.05)) = 0.015
        _RefractionAmount ("Edge & Bevel Refraction", Range(0.0, 0.04)) = 0.008
        _RoughnessNoise ("Frosted Surface Grain", Range(0.0, 1.0)) = 0.35
        _GlassTint ("Milky Glass Tint (RGB + Alpha)", Color) = (0.92, 0.97, 1.0, 0.22)
        _SpecularIntensity ("Glass Specular Sheen", Range(0.0, 3.0)) = 1.3
        _SpecularSmoothness ("Specular Shininess", Range(4.0, 128.0)) = 64.0
        _FresnelColor ("Fresnel Edge Glow", Color) = (0.75, 0.95, 1.0, 0.85)
        _FresnelPower ("Fresnel Exponent", Range(1.0, 8.0)) = 3.2
        _EdgeGlowColor ("Bevel Border Glow Tint", Color) = (0.85, 0.97, 1.0, 0.9)
        _EdgeGlowIntensity ("Bevel Border Glow Intensity", Range(0.0, 3.0)) = 1.4
        _SweepSpeed ("Specular Light Sweep Speed", Range(0.0, 3.0)) = 0.4
        _SweepIntensity ("Specular Light Sweep Intensity", Range(0.0, 1.0)) = 0.35
        _SweepPhaseOffset ("Specular Light Sweep Phase Offset", Range(0.0, 1.0)) = 0.0
        _AspectRatio ("Screen Aspect Ratio (SignWidth / SignHeight)", Float) = 1.7777
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
            Name "SmartArrowSignagePass"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassTint;
                float4 _FresnelColor;
                float4 _EdgeGlowColor;
                float4 _ArrowColor;
                float4 _SecondaryColor;
                float4 _CoreColor;
                float4 _HUDColor;
                float4 _ArrowOffset;
                float  _SignMode;
                float  _ArrowPattern;
                float  _ArrowAngle;
                float  _ArrowScale;
                float  _ArrowAspect;
                float  _ArrowRepeat;
                float  _ArrowSpacing;
                float  _AnimMode;
                float  _AnimSpeed;
                float  _EmissionMultiplier;
                float  _CoreIntensity;
                float  _GlowWidth;
                float  _GlowIntensity;
                float  _OutlineWidth;
                float  _StripeDensity;
                float  _ShowHUDDecorations;
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
                float  _SweepPhaseOffset;
                float  _AspectRatio;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            // 高速疑似乱数ハッシュ（フロストガラスサンドブラスト用）
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 13タップ ポアソンディスク風フロストガラス多重サンプリング＆RGB色収差
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
                    float3 sR = SampleSceneColor(screenUV + off * 1.02);
                    float3 sG = SampleSceneColor(screenUV + off);
                    float3 sB = SampleSceneColor(screenUV + off * 0.98);

                    accum.r += sR.r * weights[i];
                    accum.g += sG.g * weights[i];
                    accum.b += sB.b * weights[i];
                }

                return accum;
            }

            // ─────────────────────────────────────────────────────────────
            // 2D SDF 形状計算ライブラリ（プロシージャル・ベクター描画）
            // ─────────────────────────────────────────────────────────────

            // 線分 (a, b) に対する距離
            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // ボックスに対する距離
            float sdBox(float2 p, float2 b)
            {
                float2 d = abs(p) - b;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            // 右向き二等辺三角形 (先端が height, 底辺が x=0, 半幅 halfBase)
            float sdTriangleRight(float2 p, float halfBase, float height)
            {
                float2 q = float2(p.x, abs(p.y));
                float2 e = float2(height, -halfBase);
                float2 w = q - float2(0.0, halfBase);
                float h = saturate(dot(w, e) / dot(e, e));
                float dEdge = length(w - e * h);
                float dBase = length(q - float2(0.0, clamp(q.y, 0.0, halfBase)));
                float d = min(dEdge, dBase);
                float s = (q.x * halfBase + q.y * height - halfBase * height);
                return (s <= 0.0 && q.x >= 0.0) ? -d : d;
            }

            // 円弧 (Arc)
            float sdArc(float2 p, float r, float t, float a0, float a1)
            {
                float ang = atan2(p.y, p.x);
                float dAng = clamp(ang, a0, a1);
                float2 target = float2(cos(dAng), sin(dAng)) * r;
                return length(p - target) - t;
            }

            // 2D 六角形ハニカム距離関数（EnergyShield反重力フォースフィールド用）
            float HexagonDist(float2 p)
            {
                p = abs(p);
                return max(p.x * 0.8660254 + p.y * 0.5, p.y);
            }

            float HexGridPattern(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float2 pA = (frac(p / s) - 0.5) * s;
                float2 pB = (frac((p - s * 0.5) / s) - 0.5) * s;
                float2 pClosest = dot(pA, pA) < dot(pB, pB) ? pA : pB;
                float d = HexagonDist(pClosest);
                return smoothstep(0.43, 0.49, d);
            }

            // ─────────────────────────────────────────────────────────────
            // SDF 矢印パターン評価エンジン
            // ─────────────────────────────────────────────────────────────

            struct ArrowSample
            {
                float dist;        // 主形状のSDF距離 (負=内部, 正=外部)
                float coreDist;    // ホットコアのSDF距離
                float animWeight;  // アニメーション発光係数 (0.0〜1.0)
                float stripeMask;  // ハザードストライプマスク (0 or 1)
            };

            ArrowSample EvaluateArrowPattern(float2 pLocal, int pattern, int animMode, float animSpeed, float idx, float totalCount)
            {
                ArrowSample o;
                o.dist = 100.0;
                o.coreDist = 100.0;
                o.animWeight = 1.0;
                o.stripeMask = 0.0;

                // アニメーション係数の計算
                float time = _Time.y * animSpeed;
                if (animMode == 0) // Flowing (進行方向フロー)
                {
                    float phase = time * 3.5 - idx * 1.35;
                    o.animWeight = 0.35 + 0.65 * saturate(sin(phase) * 1.5);
                }
                else if (animMode == 1) // Blinking (点滅フラッシュ)
                {
                    o.animWeight = step(0.38, frac(time * 0.7));
                }
                else if (animMode == 2) // Pulse (呼吸パルス)
                {
                    o.animWeight = 0.60 + 0.40 * sin(time * 2.2);
                }
                else if (animMode == 3) // ScanlineFlow (スキャンライン)
                {
                    o.animWeight = 0.75 + 0.25 * sin(pLocal.y * 140.0 + time * 12.0);
                }
                else // Static
                {
                    o.animWeight = 1.0;
                }

                // 各矢印パターンのSDF計算
                if (pattern == 0) // ① SharpRacing (鋭角レーシングシェブロン)
                {
                    float dWing = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.28, 0.0), float2(-0.25, 0.38)) - 0.065;
                    o.dist = dWing;
                    o.coreDist = dWing + 0.035;
                }
                else if (pattern == 1) // ② BoldSign (太めサイン / ハイウェイ太矢印)
                {
                    float dHead = sdTriangleRight(pLocal - float2(0.04, 0.0), 0.34, 0.32);
                    float dShaft = sdBox(pLocal - float2(-0.16, 0.0), float2(0.20, 0.14));
                    float dArrow = min(dHead, dShaft) - 0.015;
                    o.dist = dArrow;
                    o.coreDist = dArrow + 0.04;
                }
                else if (pattern == 2) // ③ DoubleChevron (二重シェブロン)
                {
                    float dLead = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.18, 0.0), float2(-0.14, 0.36)) - 0.050;
                    float dTrail = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(-0.10, 0.0), float2(-0.42, 0.36)) - 0.050;

                    // フロー時に二重シェブロン間で進行方向（後方から先端へ）への波のズレを付加
                    if (animMode == 0)
                    {
                        float pTrail = time * 3.5 - idx * 1.35;
                        float pLead = time * 3.5 - idx * 1.35 - 0.55;
                        float wLead = 0.35 + 0.65 * saturate(sin(pLead) * 1.5);
                        float wTrail = 0.35 + 0.65 * saturate(sin(pTrail) * 1.5);
                        o.animWeight = (dLead < dTrail) ? wLead : wTrail;
                    }

                    o.dist = min(dLead, dTrail);
                    o.coreDist = o.dist + 0.030;
                }
                else if (pattern == 3) // ④ TripleChevron (三重連続シェブロン)
                {
                    float d0 = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.28, 0.0), float2(0.05, 0.34)) - 0.042;
                    float d1 = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.02, 0.0), float2(-0.21, 0.34)) - 0.042;
                    float d2 = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(-0.24, 0.0), float2(-0.47, 0.34)) - 0.042;

                    // フロー時に進行方向（後方d2 → 中間d1 → 先端d0）へ光が走るウェーブ
                    if (animMode == 0)
                    {
                        float basePhase = time * 4.0 - idx * 1.5;
                        float p2 = basePhase;
                        float p1 = basePhase - 0.5;
                        float p0 = basePhase - 1.0;
                        float w0 = 0.35 + 0.65 * saturate(sin(p0) * 1.5);
                        float w1 = 0.35 + 0.65 * saturate(sin(p1) * 1.5);
                        float w2 = 0.35 + 0.65 * saturate(sin(p2) * 1.5);
                        if (d0 <= min(d1, d2)) o.animWeight = w0;
                        else if (d1 <= d2) o.animWeight = w1;
                        else o.animWeight = w2;
                    }

                    o.dist = min(d0, min(d1, d2));
                    o.coreDist = o.dist + 0.025;
                }
                else if (pattern == 4) // ⑤ NeonTube (ネオン管中空アウトライン)
                {
                    float baseD = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.26, 0.0), float2(-0.26, 0.36));
                    float tubeCenter = 0.07;
                    float halfThick = max(0.005, _OutlineWidth * 0.5);
                    float tubeD = abs(baseD - tubeCenter) - halfThick;

                    o.dist = tubeD;
                    o.coreDist = abs(baseD - tubeCenter) - (halfThick * 0.3);
                }
                else if (pattern == 5) // ⑥ QuantumLattice (クアンタム・セグメント分割)
                {
                    float baseD = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.26, 0.0), float2(-0.26, 0.36)) - 0.075;
                    float2 cell = abs(frac(pLocal * 18.0) - 0.5);
                    float cellCut = 0.12 - min(cell.x, cell.y);
                    float dQuant = max(baseD, cellCut);

                    o.dist = dQuant;
                    o.coreDist = baseD + 0.04;
                }
                else if (pattern == 6) // ⑦ HazardStripes (ハザード警戒シェブロン)
                {
                    float baseD = sdSegment(float2(pLocal.x, abs(pLocal.y)), float2(0.26, 0.0), float2(-0.26, 0.36)) - 0.090;
                    float animShift = (animMode == 4) ? 0.0 : (time * 0.4);
                    float diag = (pLocal.x * 0.707 + pLocal.y * 0.707) * _StripeDensity + animShift;
                    o.stripeMask = step(0.5, frac(diag));
                    o.dist = baseD;
                    o.coreDist = baseD + 0.045;
                }
                else // ⑧ CurvedSpeed (カーブ案内アーク)
                {
                    float2 arcCenter = float2(0.0, -0.16);
                    float dArc = sdArc(pLocal - arcCenter, 0.33, 0.065, -1.4, 0.75);

                    // 先端の矢じり
                    float2 headPos = arcCenter + float2(cos(0.75), sin(0.75)) * 0.33;
                    float2 toTip = normalize(float2(-sin(0.75), cos(0.75)));
                    float2 normTip = float2(-toTip.y, toTip.x);
                    float2 pHead = pLocal - headPos;
                    float2 pHeadRot = float2(dot(pHead, toTip), dot(pHead, normTip));
                    float dHead = sdTriangleRight(pHeadRot - float2(0.02, 0.0), 0.18, 0.18) - 0.01;

                    o.dist = min(dArc, dHead);
                    o.coreDist = o.dist + 0.035;
                }

                return o;
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
                output.uv         = input.uv;
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // ─────────────────────────────────────────────────────────────
                // 1. プロシージャルSDFベクター矢印の座標変換 & サンプリング
                // ─────────────────────────────────────────────────────────────
                float aspect = max(0.01, _AspectRatio);
                float2 uvCentered = input.uv - 0.5;
                uvCentered.x *= aspect;

                // オフセット適用
                uvCentered -= _ArrowOffset.xy;

                // 角度回転 (θ)
                float rad = radians(_ArrowAngle);
                float cosA = cos(rad);
                float sinA = sin(rad);
                float2 pRot = float2(uvCentered.x * cosA + uvCentered.y * sinA, -uvCentered.x * sinA + uvCentered.y * cosA);

                // スケール適用
                float scale = max(0.01, _ArrowScale);
                pRot /= scale;

                // 矢印の多重リピート計算（セル境界クリッピングのない高精度ループ評価）
                int repeatCount = (int)clamp(_ArrowRepeat, 1.0, 6.0);
                float spacing = _ArrowSpacing;
                float totalSpan = (repeatCount - 1.0) * spacing;

                ArrowSample bestSample;
                bestSample.dist = 1000.0;
                bestSample.coreDist = 1000.0;
                bestSample.animWeight = 1.0;
                bestSample.stripeMask = 0.0;
                float bestLocalX = 0.0;

                for (int i = 0; i < repeatCount; i++)
                {
                    float chevronCenterX = (float)i * spacing - totalSpan * 0.5;
                    float2 pLocal = float2(pRot.x - chevronCenterX, pRot.y / max(0.01, _ArrowAspect));
                    ArrowSample s = EvaluateArrowPattern(pLocal, (int)_ArrowPattern, (int)_AnimMode, _AnimSpeed, (float)i, (float)repeatCount);
                    if (s.dist < bestSample.dist)
                    {
                        bestSample.dist = s.dist;
                        bestSample.animWeight = s.animWeight;
                        bestSample.stripeMask = s.stripeMask;
                        bestLocalX = pLocal.x;
                    }
                    if (s.coreDist < bestSample.coreDist)
                    {
                        bestSample.coreDist = s.coreDist;
                    }
                }

                // サブピクセルAA（アンチエイリアシング）: fwidthによる無限解像度鮮明エッジ（Strict HLSL: 1.0 - smoothstep(-aa, aa, dist)）
                float aa = max(1e-5, fwidth(bestSample.dist));
                float fill = 1.0 - smoothstep(-aa, aa, bestSample.dist);

                // ホットコア（高輝度中心発光）
                float coreAA = max(1e-5, fwidth(bestSample.coreDist));
                float core = 1.0 - smoothstep(-coreAA, coreAA, bestSample.coreDist);

                // アウターソフトグロー（ブルーム拡散）
                float glowRadius = max(0.005, _GlowWidth);
                float glow = exp(-max(bestSample.dist, 0.0) / glowRadius) * _GlowIntensity;

                // カラー合成（ハザードストライプ時はツートン、通常はプライマリ＆セカンダリのグラデーション）
                half3 arrowBaseCol;
                if ((int)_ArrowPattern == 6) // HazardStripes
                {
                    arrowBaseCol = lerp(_ArrowColor.rgb, _SecondaryColor.rgb, bestSample.stripeMask);
                }
                else
                {
                    // 矢印内部の流れるティント（先端が明るいセカンダリ、後方がプライマリ）
                    float tipGrad = saturate(bestLocalX * 1.5 + 0.5);
                    arrowBaseCol = lerp(_ArrowColor.rgb, _SecondaryColor.rgb, tipGrad * 0.35);
                }

                // 発光エミッションRGB
                half3 arrowEmissive = arrowBaseCol * fill + _CoreColor.rgb * (core * _CoreIntensity);
                arrowEmissive += _ArrowColor.rgb * glow;
                arrowEmissive *= (bestSample.animWeight * _EmissionMultiplier);

                // 矢印の有効不透明度
                half arrowAlpha = saturate(fill + glow * 0.7);

                // ─────────────────────────────────────────────────────────────
                // 2. HUD テクニカル装飾マーク（コーナーブラケット＆サイド目盛り）
                // ─────────────────────────────────────────────────────────────
                half3 hudEmissive = half3(0, 0, 0);
                half  hudAlpha = 0.0;

                if (_ShowHUDDecorations > 0.5)
                {
                    // 四隅コーナーブラケット（アスペクト比補正・均等ストローク・角丸インセット）
                    float2 cUV = abs(input.uv - 0.5) * 2.0; // 0=中央, 1=外周縁
                    float2 dCorner = 1.0 - cUV;             // 0=角の頂点

                    // アスペクト比補正したメトリック座標系 (Y基準: 1.0, X基準: aspect)
                    float2 pCornerMetric = float2(dCorner.x * aspect, dCorner.y);
                    float targetInset = 0.040;
                    float2 targetMetric = float2(targetInset, targetInset);
                    float armLength = 0.075;
                    float strokeWidth = 0.0035;

                    float distMetricX = abs(pCornerMetric.x - targetMetric.x);
                    float distMetricY = abs(pCornerMetric.y - targetMetric.y);

                    float armX = step(targetMetric.x, pCornerMetric.x) * step(pCornerMetric.x, targetMetric.x + armLength) * smoothstep(strokeWidth, 0.0, distMetricY);
                    float armY = step(targetMetric.y, pCornerMetric.y) * step(pCornerMetric.y, targetMetric.y + armLength) * smoothstep(strokeWidth, 0.0, distMetricX);
                    float cornerBracket = saturate(armX + armY);

                    // コーナーアクセントドット（完全な円形・アスペクト比補正）
                    float dotDist = length(pCornerMetric - (targetMetric + float2(0.020, 0.020)));
                    float cornerDot = smoothstep(0.006, 0.002, dotDist);

                    // サイド中央のテクニカル目盛り
                    float tickX = smoothstep(0.003, 0.0, abs(cUV.y)) * step(cUV.x, 0.035);
                    float tickY = smoothstep(0.003, 0.0, abs(cUV.x)) * step(cUV.y, 0.035);

                    float hudPattern = saturate(cornerBracket * 1.5 + cornerDot * 1.2 + (tickX + tickY) * 1.0);
                    hudEmissive = _HUDColor.rgb * (hudPattern * 2.2);
                    hudAlpha = hudPattern * _HUDColor.a;
                }

                // 矢印とHUD装飾の統合
                half3 combinedUIEmissive = arrowEmissive + hudEmissive;
                half  combinedUIAlpha = saturate(arrowAlpha + hudAlpha);

                // ─────────────────────────────────────────────────────────────
                // 3. 背景モード（0=FrostedGlass, 1=PureHologram, 2=MinimalGlass,
                //              3=CyberGridHolo, 4=DarkAeroVisor, 5=EnergyShield,
                //              6=AuroraPrism, 7=NeonWireframeHolo, 8=StarDustMatrix）
                // ─────────────────────────────────────────────────────────────

                if (_SignMode > 0.5 && _SignMode < 1.5)
                {
                    // 🌟 モード①：Pure Hologram（純粋空中ホログラム・ガラス板なし）
                    // 加算発光またはアルファ半透明で、空気中に浮かび上がる純粋エネルギー
                    half3 finalRGB = combinedUIEmissive;
                    half  finalAlpha = combinedUIAlpha;
                    return half4(finalRGB, finalAlpha);
                }

                // --- 幾何・光学的共通要素（丸角境界・環境光・スペキュラー・反射スイープ） ---
                float2 pBox = abs((input.uv - 0.5) * float2(aspect, 1.0));
                float cornerR = 0.08 * min(aspect, 1.0);
                float2 bBox = float2(0.5 * aspect - cornerR, 0.5 - cornerR);
                float2 dBox = pBox - bBox;
                float distToEdge = cornerR - (length(max(dBox, 0.0)) + min(max(dBox.x, dBox.y), 0.0));
                float bevelGlow = smoothstep(0.025, 0.002, distToEdge) * _EdgeGlowIntensity;
                float3 bevelCol = _EdgeGlowColor.rgb * bevelGlow;

                // 太陽光鏡面反射（Specular Sheen）
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir  = normalize(lightDir + viewDirWS);
                float  NdotH    = saturate(dot(normalWS, halfDir));
                float  spec     = pow(NdotH, _SpecularSmoothness) * _SpecularIntensity;
                float3 specCol  = mainLight.color * spec;

                // 動的ガラス光沢スイープ（位相オフセット付きで看板ごとに個別のタイミングで流延）
                float totalSweepPhase = (_Time.y * _SweepSpeed * 0.25) + _SweepPhaseOffset;
                float sweepCoord = (input.uv.x * 0.75 + input.uv.y * 0.25) - frac(totalSweepPhase) * 2.2;
                float sweep = smoothstep(0.06, 0.0, abs(sweepCoord)) * _SweepIntensity;
                float3 sweepCol = float3(0.95, 0.98, 1.0) * sweep;

                // フレネル発光
                float NdotV   = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                float3 rimCol = _FresnelColor.rgb * (fresnel * _FresnelColor.a);

                half3 finalRGB;
                half  finalAlpha;

                if (_SignMode > 2.5 && _SignMode < 3.5)
                {
                    // 🌟 モード③：Cyber Grid Holo（サイバーグリッド・ホログラム / 走査線・方眼メッシュ浮遊）
                    // 空中に金具なしで投影されるデジタルマトリクス板
                    float2 gridUV = input.uv * float2(aspect * 8.0, 8.0);
                    float2 gridF = abs(frac(gridUV) - 0.5);
                    float gridLine = smoothstep(0.05, 0.0, min(gridF.x, gridF.y)) * 0.35;

                    float scanline = sin(input.uv.y * 220.0 - _Time.y * 5.0) * 0.5 + 0.5;
                    half3 cyberTint = half3(0.015, 0.045, 0.075);
                    half3 cyberGridCol = _HUDColor.rgb * (gridLine * (0.6 + 0.4 * scanline) * 2.0);
                    half3 holoBody = cyberTint + cyberGridCol + rimCol * 1.2 + bevelCol * 1.4;

                    finalRGB = holoBody + combinedUIEmissive;
                    finalAlpha = saturate(0.20 + gridLine * 0.45 + combinedUIAlpha);
                }
                else if (_SignMode > 3.5 && _SignMode < 4.5)
                {
                    // 🌟 モード④：Dark Aero Visor（ダークエアロ・バイザー / 偏光スモーク・直射日光耐性・極限高対比）
                    // 昼間の屋外コース・青空・太陽光下でも矢印サインが100%くっきり視認できるレーシングバイザー調
                    float topDark = lerp(0.85, 0.72, input.uv.y);
                    half3 visorBase = half3(0.012, 0.022, 0.032);
                    half3 visorBody = visorBase + specCol * 1.3 + sweepCol * 0.7 + rimCol * 0.5 + bevelCol * 0.7;

                    finalRGB = visorBody + combinedUIEmissive;
                    finalAlpha = saturate(topDark + combinedUIAlpha);
                }
                else if (_SignMode > 4.5 && _SignMode < 5.5)
                {
                    // 🌟 モード⑤：Energy Shield（反重力エネルギーシールド / ハニカム六角・プラズマ波境界）
                    // 空中に金具なしで浮かんでいる理由を視覚的に納得させる反重力フォースフィールド
                    float shieldPulse = sin(distToEdge * 35.0 - _Time.y * 4.0) * 0.5 + 0.5;
                    float shieldEdge = smoothstep(0.08, 0.005, distToEdge);
                    float shieldHex = HexGridPattern(input.uv * float2(aspect * 14.0, 14.0));

                    half3 shieldTint = lerp(_ArrowColor.rgb * 0.12, _HUDColor.rgb * 0.30, shieldPulse);
                    half3 shieldEmissive = (_ArrowColor.rgb * (shieldHex * 0.55) + _HUDColor.rgb * (shieldEdge * 1.6 * shieldPulse));
                    half3 shieldBody = shieldTint + shieldEmissive + specCol * 0.4;

                    finalRGB = shieldBody + combinedUIEmissive;
                    finalAlpha = saturate(0.20 + shieldHex * 0.28 + shieldEdge * 0.45 + combinedUIAlpha);
                }
                else if (_SignMode > 5.5 && _SignMode < 6.5)
                {
                    // 🌟 モード⑥：Aurora Prism（オーロラプリズム・光彩ガラス / 虹色偏光クリスタル）
                    // 見る角度と光の当たり方で虹色に揺らめく未来型光彩ガラス
                    float viewFactor = saturate(dot(normalWS, viewDirWS));
                    float prismPhase = (input.uv.x * 0.7 + input.uv.y * 0.3 + (1.0 - viewFactor) * 1.5 + _Time.y * 0.15);
                    half3 rainbow = 0.5 + 0.5 * cos(float3(0.0, 0.33, 0.67) * 6.2831853 + prismPhase * 6.2831853);
                    half3 prismBody = rainbow * 0.35 + specCol * 1.5 + sweepCol * 1.1 + rimCol * 1.3 + bevelCol * 1.4;

                    finalRGB = prismBody + combinedUIEmissive;
                    finalAlpha = saturate(0.32 + (1.0 - viewFactor) * 0.35 + combinedUIAlpha);
                }
                else if (_SignMode > 6.5 && _SignMode < 7.5)
                {
                    // 🌟 モード⑦：Neon Wireframe Holo（ネオンエッジ・ホロフレーム / 外周サイバー発光クリア）
                    // 外周境界のみが多重ネオンパルスで鮮やかに発光し、中央は完全透過。
                    // 空中に金具なしで浮くサインの領域を、背景コース景色を遮ることなく美しく明示。
                    float outerEdge = smoothstep(0.035, 0.002, distToEdge);
                    float innerNeonLine = smoothstep(0.007, 0.000, abs(distToEdge - 0.032));
                    float edgePulse = sin((input.uv.x + input.uv.y) * 4.0 - _Time.y * 3.0) * 0.5 + 0.5;

                    half3 wireBody = _ArrowColor.rgb * (innerNeonLine * 2.4 + outerEdge * (0.8 + 0.5 * edgePulse) * 1.5) + _HUDColor.rgb * (outerEdge * 0.4);
                    wireBody += bevelCol * 1.8 + rimCol * 1.2 + specCol * 0.5;

                    finalRGB = wireBody + combinedUIEmissive;
                    finalAlpha = saturate(innerNeonLine * 0.8 + outerEdge * 0.45 + combinedUIAlpha);
                }
                else if (_SignMode > 7.5)
                {
                    // 🌟 モード⑧：Star Dust Matrix（スターダスト・微粒子マトリクス / 空中光粒子ダスト＆浮遊）
                    // 空間内部を微細な光のナノダスト・データ微粒子がキラキラと漂う高密度SF浮遊フィールド
                    float2 dustUV1 = input.uv * float2(aspect * 24.0, 24.0) + float2(_Time.y * 0.08, _Time.y * 0.14);
                    float2 dustCell1 = floor(dustUV1);
                    float2 dustFract1 = frac(dustUV1) - 0.5;
                    float2 dustPos1 = (float2(Hash12(dustCell1), Hash12(dustCell1 + 73.1)) - 0.5) * 0.65;
                    float distP1 = length(dustFract1 - dustPos1);
                    float sparkle1 = smoothstep(0.09, 0.01, distP1) * (0.4 + 0.6 * sin(_Time.y * 3.5 + Hash12(dustCell1) * 6.283));

                    float2 dustUV2 = input.uv * float2(aspect * 36.0, 36.0) + float2(-_Time.y * 0.12, _Time.y * 0.06);
                    float2 dustCell2 = floor(dustUV2);
                    float2 dustFract2 = frac(dustUV2) - 0.5;
                    float2 dustPos2 = (float2(Hash12(dustCell2 + 19.3), Hash12(dustCell2 + 47.7)) - 0.5) * 0.65;
                    float distP2 = length(dustFract2 - dustPos2);
                    float sparkle2 = smoothstep(0.08, 0.01, distP2) * (0.4 + 0.6 * cos(_Time.y * 4.2 + Hash12(dustCell2) * 6.283));

                    half3 stardustTint = half3(0.012, 0.035, 0.065);
                    half3 stardustEmissive = _ArrowColor.rgb * (sparkle1 * 2.4) + _HUDColor.rgb * (sparkle2 * 2.0);
                    half3 dustBody = stardustTint + stardustEmissive + rimCol * 1.3 + bevelCol * 1.4 + specCol * 0.5;

                    finalRGB = dustBody + combinedUIEmissive;
                    finalAlpha = saturate(0.18 + (sparkle1 + sparkle2) * 0.55 + combinedUIAlpha);
                }
                else
                {
                    // 🌟 モード⓪ & ②：フロストガラス (0) / ミニマルガラス (2)
                    float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                    float3 normalVS = TransformWorldToViewDir(normalWS);
                    float2 normalScreenOffset = normalVS.xy * _RefractionAmount;
                    float2 jitter = float2(Hash12(input.uv * 512.0), Hash12(input.uv * 512.0 + 17.13));

                    float3 blurredBg = float3(0.0, 0.0, 0.0);
                    if (_SignMode < 0.5 && _UseSceneBlur > 0.5)
                    {
                        blurredBg = SampleFrostedScene(screenUV + normalScreenOffset, _BlurRadius, _RoughnessNoise, jitter);
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

                    float3 glassBody = lerp(blurredBg, _GlassTint.rgb, _GlassTint.a);
                    glassBody += specCol + sweepCol + rimCol + bevelCol;

                    if (_SignMode < 0.5 && _UseSceneBlur > 0.5)
                    {
                        // フロストガラス透過合成
                        float bloomHalo = combinedUIAlpha * 0.35;
                        finalRGB = lerp(glassBody, combinedUIEmissive, combinedUIAlpha) + (combinedUIEmissive * bloomHalo);
                        finalAlpha = 1.0;
                    }
                    else
                    {
                        // ミニマル半透明ガラス合成
                        finalRGB = glassBody * _GlassTint.a + combinedUIEmissive;
                        finalAlpha = saturate(_GlassTint.a * 1.5 + combinedUIAlpha);
                    }
                }

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
