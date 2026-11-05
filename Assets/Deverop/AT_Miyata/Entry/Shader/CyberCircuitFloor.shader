// サーバールーム風の床シェーダー（URP / Lit ベース）
//  - 暗いメタリックなタイル + 目地（溝）
//  - 全タイルがつながった回路ネットワーク（タイル境界の「ポート」で隣と接続）
//  - 回路の線に沿って光のパケットが走る
//  - 高スムースネスで反射プローブを映し込む光沢床
//
// ネットワーク構造:
//  各タイルには「ハブ」が1つあり、上下左右のタイル境界上の「ポート」へ配線が伸びる。
//  ポート位置は境界ごとに決まる乱数なので、隣のタイルと必ず同じ点でつながる。
//  横方向の接続は常に有効、縦方向は _VerticalKeepChance で間引くが、
//  4列に1本は必ず残すため、床全体が1つのネットワークとして連結される。
//  光は「ハブ → ポート → 隣のハブ」の1本の経路上を、経路長に沿って流れる。
//
// 床の映り込みを強くしたい場合は、シーンに Reflection Probe を置いて Bake してください。
Shader "Entry/CyberCircuitFloor"
{
    Properties
    {
        [Toggle] _UseWorldSpace ("Use World XZ (else Mesh UV)", Float) = 1
        _TileSize ("Tile Size (world units or UV)", Float) = 2.0

        _BaseColor ("Tile Color", Color) = (0.035, 0.05, 0.07, 1)
        _TileVariation ("Per-Tile Brightness Variation", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0.7
        _Smoothness ("Smoothness", Range(0, 1)) = 0.88
        _GrainScale ("Grain Scale", Float) = 180
        _GrainStrength ("Grain Strength (roughness noise)", Range(0, 1)) = 0.25

        _SeamWidth ("Seam Width (tile fraction)", Range(0, 0.05)) = 0.006
        _BevelWidth ("Bevel Width (tile fraction)", Range(0.001, 0.05)) = 0.012
        _BevelStrength ("Bevel Normal Strength", Range(0, 2)) = 0.6
        _SeamColor ("Seam Color", Color) = (0.005, 0.007, 0.01, 1)
        [HDR] _SeamGlowColor ("Seam Glow Color", Color) = (0.0, 0.25, 0.5, 1)
        _SeamGlow ("Seam Glow", Range(0, 2)) = 0.1

        [HDR] _CircuitColor ("Groove Light Color", Color) = (0.0, 0.65, 1.0, 1)
        _CircuitIntensity ("Groove Light Intensity", Range(0, 20)) = 1.2
        _GrooveWidth ("Groove Bottom Half Width (tile fraction)", Range(0.0005, 0.02)) = 0.0025
        _GrooveBevel ("Groove Wall Width (tile fraction)", Range(0.0005, 0.02)) = 0.004
        _GrooveDepth ("Groove Depth (normal strength)", Range(0, 3)) = 1.0
        _GrooveColor ("Groove Wall Color", Color) = (0.008, 0.012, 0.018, 1)
        _GrooveWallGlow ("Light Spill On Walls", Range(0, 1)) = 0.25
        _HubJitter ("Hub Position Jitter", Range(0, 0.1)) = 0.08
        _PortJitter ("Port Position Jitter", Range(0, 0.25)) = 0.22
        _VerticalKeepChance ("Vertical Link Keep Chance", Range(0, 1)) = 0.5
        _SpurChance ("Branch Chance (x3 per tile)", Range(0, 1)) = 0.45
        _PadRadius ("Pad Radius (tile fraction)", Range(0, 0.05)) = 0.012

        [HDR] _PulseColor ("Pulse Color", Color) = (0.4, 0.9, 1.0, 1)
        _PulseIntensity ("Pulse Intensity", Range(0, 30)) = 8
        _PulseSpeed ("Pulse Speed (world units/s)", Float) = 1.5
        _PulseChance ("Pulse Spawn Chance", Range(0, 1)) = 0.35
        _SpawnRandomness ("Spawn Point Randomness", Range(0, 1)) = 0.8
        _DespawnRandomness ("Despawn Point Randomness", Range(0, 1)) = 0.5
        _SpawnFade ("Spawn/Despawn Fade (world units)", Float) = 0.15
        _PulseSpacing ("Pulse Spacing (world units)", Float) = 6.0
        _PulseLength ("Pulse Tail Length (world units)", Float) = 1.0
        _PulseHead ("Pulse Head Softness (world units)", Float) = 0.04
        _PulseSpeedVariation ("Pulse Speed Variation", Range(0, 1)) = 0.3

        [HDR] _TileGlowColor ("Tile Glow Color", Color) = (0.0, 0.35, 0.7, 1)
        _TileGlowChance ("Tile Glow Chance", Range(0, 1)) = 0.06
        _TileGlowIntensity ("Tile Glow Intensity", Range(0, 5)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _UseWorldSpace;
            float _TileSize;
            half4 _BaseColor;
            half _TileVariation;
            half _Metallic;
            half _Smoothness;
            float _GrainScale;
            half _GrainStrength;
            float _SeamWidth;
            float _BevelWidth;
            half _BevelStrength;
            half4 _SeamColor;
            half4 _SeamGlowColor;
            half _SeamGlow;
            half4 _CircuitColor;
            half _CircuitIntensity;
            float _GrooveWidth;
            float _GrooveBevel;
            half _GrooveDepth;
            half4 _GrooveColor;
            half _GrooveWallGlow;
            float _HubJitter;
            float _PortJitter;
            half _VerticalKeepChance;
            half _SpurChance;
            float _PadRadius;
            half4 _PulseColor;
            half _PulseIntensity;
            float _PulseSpeed;
            half _PulseChance;
            half _SpawnRandomness;
            half _DespawnRandomness;
            float _SpawnFade;
            float _PulseSpacing;
            float _PulseLength;
            float _PulseHead;
            half _PulseSpeedVariation;
            half4 _TileGlowColor;
            half _TileGlowChance;
            half _TileGlowIntensity;
        CBUFFER_END

        // ---------- 共通ユーティリティ ----------
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // 模様空間の座標（タイル単位）
        float2 PatternCoord(float2 uv, float3 positionWS)
        {
            float2 p = lerp(uv, positionWS.xz, _UseWorldSpace);
            return p / max(_TileSize, 1e-4);
        }

        // タイル端までの距離（タイル単位 0..0.5）
        float EdgeDist(float2 t)
        {
            float2 e = min(t, 1.0 - t);
            return min(e.x, e.y);
        }

        // 目地による法線の傾き（模様空間の勾配）
        // 高さ h = smoothstep(_SeamWidth, _SeamWidth+_BevelWidth, e) を解析的に微分する
        // （旧：差分法で3回評価 → 1回の計算で済む）
        float2 SeamGradient(float2 t)
        {
            float2 e2 = min(t, 1.0 - t);
            float e = min(e2.x, e2.y);
            float invBevel = 1.0 / max(_BevelWidth, 1e-5);
            float x = saturate((e - _SeamWidth) * invBevel);
            float dhde = 6.0 * x * (1.0 - x) * invBevel;
            // 最寄りの辺から内側へ向かう方向 = e が増える方向
            float2 dir = (e2.x < e2.y) ? float2(t.x < 0.5 ? 1.0 : -1.0, 0.0)
                                       : float2(0.0, t.y < 0.5 ? 1.0 : -1.0);
            return dir * dhde;
        }

        // ================= 回路ネットワーク =================

        // 線分までの距離・最近点の経路長（sOffset からの累積）・線から外向きのベクトル g を更新
        void Seg(float2 q, float2 a, float2 b, float sOffset, inout float d, inout float s, inout float2 g)
        {
            float2 pa = q - a, ba = b - a;
            float l2 = max(dot(ba, ba), 1e-8);
            float h = saturate(dot(pa, ba) / l2);
            float2 v = pa - ba * h;
            float dd = length(v);
            if (dd < d) { d = dd; s = sOffset + h * sqrt(l2); g = v; }
        }

        // リング（端子）までの距離と外向きベクトル
        float RingDist(float2 q, float2 c, float r, out float2 g)
        {
            float2 v = q - c;
            float l = max(length(v), 1e-6);
            float dd = abs(l - r);
            g = v / l * ((l >= r) ? dd : -dd);
            return dd;
        }

        // タイル内のハブ位置（タイル単位）
        float2 HubPos(float2 tileId)
        {
            float2 r = float2(Hash21(tileId + 13.1), Hash21(tileId + 29.7)) - 0.5;
            return 0.5 + r * 2.0 * _HubJitter;
        }

        // 4方向を「ポートが +X 側の辺 (x=1) にある」正準座標へ変換（等長変換）
        // dir 0:右  1:左  2:上(+Y)  3:下(-Y)
        float2 ToCanon(float2 x, int dir)
        {
            if (dir == 0) return x;
            if (dir == 1) return float2(1.0 - x.x, x.y);
            if (dir == 2) return x.yx;
            return float2(1.0 - x.y, x.x);
        }

        // 正準座標のベクトルをタイル座標へ戻す（ToCanon の逆変換・ベクトル用）
        float2 FromCanonVec(float2 c, int dir)
        {
            if (dir == 0) return c;
            if (dir == 1) return float2(-c.x, c.y);
            if (dir == 2) return c.yx;
            return float2(c.y, -c.x);
        }

        // 正準座標でのハブ→ポート配線（横 → 45°斜め → 横）
        void RoutePoints(float2 h, float p, float k, out float2 P1, out float2 P2, out float L)
        {
            float ady = abs(p - h.y);
            float freeLen = max(1.0 - h.x - ady, 0.0);
            float a = h.x + freeLen * k;
            P1 = float2(a, h.y);
            P2 = float2(min(a + ady, 1.0), p);
            L = (a - h.x) + length(P2 - P1) + (1.0 - P2.x);
        }

        // 経路上の光のパケット。u: 経路上の位置, total: 経路全長（ともにワールド単位）
        float PulseAt(float u, float total, float2 seed)
        {
            float speed = _PulseSpeed * lerp(1.0 - _PulseSpeedVariation, 1.0 + _PulseSpeedVariation, Hash21(seed + 5.5));
            float spacing = max(_PulseSpacing, 1e-3);
            // 経路が間隔より長い → 経路上に等間隔で複数（端でつながるよう整数個）
            // 経路が間隔より短い → 1つが通過した後、しばらく何も走らない
            float period = (total >= spacing) ? total / round(total / spacing) : spacing;
            float phase = (u - _Time.y * speed) / period + Hash21(seed + 8.8);

            // パケット番号：1つの光の粒は移動中ずっと同じ番号を持つ
            // → 番号ごとに乱数を引けば、粒ごと（＝時間ごと）に出現の有無・場所が変わる
            float k = floor(phase);
            float x = phase - k;
            float2 pk = seed + float2(k * 0.6180339, k * 0.4142135);
            if (Hash21(pk + 12.4) >= _PulseChance) return 0.0;

            // 発生点と消滅点（経路上のワールド位置）をランダムに決める
            // 発生点より手前・消滅点より先では見えない → 経路の途中から湧き出て、途中で消える
            // ランダムさが 0 のときは乱数を引かない（マテリアル単位の分岐なので分岐コストはほぼ無い）
            float birth = (_SpawnRandomness > 0.0) ? Hash21(pk + 21.7) * total * _SpawnRandomness : 0.0;
            float death = (_DespawnRandomness > 0.0) ? total - Hash21(pk + 33.1) * (total - birth) * _DespawnRandomness : total;
            float fade = max(_SpawnFade, 1e-4);
            // 経路の端（ハブ／端子）まで届く粒は端でフェードさせない（隣の経路や端子へ自然につなぐため）
            float aliveIn  = (birth <= 1e-4) ? 1.0 : smoothstep(birth, birth + fade, u);
            float aliveOut = (death >= total - 1e-4) ? 1.0 : 1.0 - smoothstep(death - fade, death, u);
            float alive = aliveIn * aliveOut;
            if (alive <= 0.0) return 0.0;
            // x=1 が先頭（進行方向側）。先頭から後方（経路を逆にたどる方向）へ尾を引く
            float behind = (1.0 - x) * period;                       // 先頭からの距離（ワールド単位）
            float ahead  = x * period;                                // 次の先頭までの距離
            float tail = saturate(1.0 - behind / max(_PulseLength, 1e-3));
            float head = saturate(ahead / max(_PulseHead, 1e-4));    // 先頭のエッジをわずかに柔らかく
            return tail * tail * head * alive;
        }

        // 溝の中の光量（溝の底=1、壁=_GrooveWallGlow、溝の外=0）
        float GrooveLight(float d, float aa)
        {
            float w = _GrooveWidth;
            float bottom = (1.0 - smoothstep(w - aa, w + aa, d)) * saturate(w * 2.0 / aa);
            float wall = saturate((1.0 - smoothstep(w, w + _GrooveBevel + aa, d)) - bottom);
            return bottom + wall * _GrooveWallGlow;
        }

        // 溝を合成（溝同士は和集合 = 最短距離、光は経路ごとに溝の中だけに乗せる）
        void AccumLine(float d, float2 g, float pulse, float aa, inout float dMin, inout float2 gMin, inout float pulseM)
        {
            if (d < dMin) { dMin = d; gMin = g; }
            if (pulse > 0.0) pulseM = max(pulseM, GrooveLight(d, aa) * pulse);
        }

        // 点 q が、矩形 [lo, hi] を reach だけ広げた範囲の外なら true（配線の早期除外用）
        bool OutsideBox(float2 q, float2 lo, float2 hi, float reach)
        {
            return any(q < lo - reach) || any(q > hi + reach);
        }

        // タイル1枚分の回路を評価。t: タイル内座標(0..1), aa: タイル単位の画素幅
        // dMin: 最寄りの溝中心までの距離, gMin: 溝から外向きのベクトル, pulseM: 流れる光
        void EvaluateCircuit(float2 tileId, float2 t, float aa, out float dMin, out float2 gMin, out float pulseM)
        {
            dMin = 1e5; gMin = float2(0, 1); pulseM = 0;
            float reach = _GrooveWidth + _GrooveBevel + aa * 2.0;
            float2 hub = HubPos(tileId);
            // 流れる光が見えない設定なら、光の計算（乱数・経路長）を丸ごと省略
            bool pulseOn = (_PulseIntensity > 0.0) && (_PulseChance > 0.0);

            // ---- ハブ → 4方向のポート（隣のタイルと接続）----
            [unroll]
            for (int dir = 0; dir < 4; dir++)
            {
                // 共有する境界の情報
                bool isA = (dir == 0 || dir == 2);              // 境界の「左/下」側のタイルか
                bool vertSeam = (dir <= 1);                       // 縦の目地をまたぐ（左右接続）
                float2 edgeCoord = tileId + (dir == 0 ? float2(1, 0) : (dir == 2 ? float2(0, 1) : float2(0, 0)));
                float2 key = edgeCoord + (vertSeam ? float2(0.0, 0.0) : float2(31.7, 57.3));
                float2 other = tileId + (dir == 0 ? float2(1, 0) : dir == 1 ? float2(-1, 0) : dir == 2 ? float2(0, 1) : float2(0, -1));
                int otherDir = (dir == 0) ? 1 : (dir == 1) ? 0 : (dir == 2) ? 3 : 2;

                float p = 0.5 + (Hash21(key + 0.7) - 0.5) * 2.0 * _PortJitter;

                // 【早期除外】配線は正準座標で x∈[ハブ, 1], y∈[ハブとポートの間] に収まる。
                // その外側の画素はこの配線に関する計算を全て省略（大半の画素がここで抜ける）
                float2 hc = ToCanon(hub, dir);
                float2 qc = ToCanon(t, dir);
                if (OutsideBox(qc, float2(hc.x, min(hc.y, p)), float2(1.0, max(hc.y, p)), reach)) continue;

                // 上下接続は間引く。ただし行ごとに4列に1本は必ず残す → 全体が連結
                if (!vertSeam)
                {
                    float offset = floor(Hash21(float2(edgeCoord.y, 3.3)) * 4.0);
                    bool guaranteed = frac((edgeCoord.x - offset) * 0.25) < 0.01;
                    if (!guaranteed && Hash21(key + 9.1) >= _VerticalKeepChance) continue;
                }

                float kMe = Hash21(key + (isA ? 1.1 : 2.2));

                // 自タイル側の配線
                float2 P1, P2; float Lme;
                RoutePoints(hc, p, kMe, P1, P2, Lme);
                float d = 1e5, s = 0;
                float2 gc = float2(0, 1);
                float l1 = P1.x - hc.x;
                float l2 = length(P2 - P1);
                Seg(qc, hc, P1, 0.0, d, s, gc);
                Seg(qc, P1, P2, l1, d, s, gc);
                Seg(qc, P2, float2(1.0, p), l1 + l2, d, s, gc);

                // 溝から離れた画素は処理を省略
                if (d > reach) continue;

                float pulse = 0.0;
                if (pulseOn)
                {
                    // 隣タイル側の配線長（経路全体の長さを求めるため）。光を計算する時だけ必要
                    float kOther = Hash21(key + (isA ? 2.2 : 1.1));
                    float2 Q1, Q2; float Lother;
                    RoutePoints(ToCanon(HubPos(other), otherDir), p, kOther, Q1, Q2, Lother);

                    // 経路「Aのハブ → ポート → Bのハブ」上の位置
                    float LA = isA ? Lme : Lother;
                    float total = Lme + Lother;
                    float u = isA ? s : LA + (Lme - s);
                    if (Hash21(key + 3.3) < 0.5) u = total - u;   // 流れる向きを経路ごとにランダム化

                    pulse = PulseAt(u * _TileSize, total * _TileSize, key);
                }
                AccumLine(d, FromCanonVec(gc, dir), pulse, aa, dMin, gMin, pulseM);
            }

            // ---- 枝（ハブから伸びる行き止まりの配線。ハブにつながっているので連結は保たれる）----
            [unroll]
            for (int i = 0; i < 3; i++)
            {
                float2 sk = tileId + float2(i * 7.13 + 101.0, 3.7 + i * 1.9);
                if (Hash21(sk) >= _SpurChance) continue;

                float2 E = 0.15 + 0.7 * float2(Hash21(sk + 1.3), Hash21(sk + 2.7));

                // 【早期除外】枝と端子はハブ〜先端を囲む矩形に収まる
                float padReach = _PadRadius + reach;
                if (OutsideBox(t, min(hub, E), max(hub, E), padReach)) continue;

                float2 dv = E - hub;
                float ax = abs(dv.x), ay = abs(dv.y);
                float2 elbow = (ax > ay) ? hub + float2(sign(dv.x) * (ax - ay), 0.0)
                                         : hub + float2(0.0, sign(dv.y) * (ay - ax));
                float len1 = length(elbow - hub);
                float L = len1 + length(E - elbow);

                float d = 1e5, s = 0;
                float2 g = float2(0, 1);
                Seg(t, hub, elbow, 0.0, d, s, g);
                Seg(t, elbow, E, len1, d, s, g);
                if (d <= reach)
                {
                    float pulse = pulseOn ? PulseAt(s * _TileSize, L * _TileSize, sk) : 0.0;   // ハブから外へ流れる
                    AccumLine(d, g, pulse, aa, dMin, gMin, pulseM);
                }

                // 先端の端子（リング）：光が届いた瞬間に光る。端子の近くの画素だけ計算
                float2 vPad = t - E;
                if (dot(vPad, vPad) <= padReach * padReach)
                {
                    float2 gPad;
                    float dPad = RingDist(t, E, _PadRadius, gPad);
                    float pulseEnd = pulseOn ? PulseAt(L * _TileSize, L * _TileSize, sk) : 0.0;
                    AccumLine(dPad, gPad, pulseEnd, aa, dMin, gMin, pulseM);
                }
            }

            // ---- ハブの端子（近くの画素だけ計算）----
            float hubR = _PadRadius * 1.4;
            float2 vHub = t - hub;
            if (dot(vHub, vHub) <= (hubR + reach) * (hubR + reach))
            {
                float2 gHub;
                float dHub = RingDist(t, hub, hubR, gHub);
                AccumLine(dHub, gHub, 0.0, aa, dMin, gMin, pulseM);
            }
        }
        ENDHLSL

        // ================= Forward =================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = float4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 P = PatternCoord(IN.uv, IN.positionWS);
                float2 tileId = floor(P);
                float2 t = frac(P);
                float aaP = max(length(fwidth(P)), 1e-5);

                float tileRnd  = Hash21(tileId);
                float tileRnd2 = Hash21(tileId + 91.7);

                // ---- 目地 ----
                float edge = EdgeDist(t);
                float seamMask = 1.0 - smoothstep(_SeamWidth - aaP, _SeamWidth + aaP, edge);
                float bevelMask = 1.0 - smoothstep(_SeamWidth, _SeamWidth + _BevelWidth + aaP, edge);

                // ---- 回路の溝 + 流れる光 ----
                float dG; float2 gG; float pulseM;
                EvaluateCircuit(tileId, t, aaP, dG, gG, pulseM);
                float grooveOuter = _GrooveWidth + _GrooveBevel;
                float grooveMask = 1.0 - smoothstep(grooveOuter - aaP, grooveOuter + aaP, dG);
                float grooveLight = GrooveLight(dG, aaP);

                // ---- 法線（目地のベベル + 回路の溝の壁）----
                float3 N0 = normalize(IN.normalWS);
                float3 T_uv = normalize(IN.tangentWS.xyz);
                float3 B_uv = cross(N0, T_uv) * IN.tangentWS.w;
                float3 T = normalize(lerp(T_uv, float3(1, 0, 0), _UseWorldSpace));
                float3 B = normalize(lerp(B_uv, float3(0, 0, 1), _UseWorldSpace));
                float2 grad = SeamGradient(t) * _BevelStrength * 0.02;
                grad *= saturate(_BevelWidth / (aaP * 2.0));   // 遠景でのちらつき防止

                // 溝の壁：底(d=_GrooveWidth)から表面(d=_GrooveWidth+_GrooveBevel)へ立ち上がる斜面
                float wx = saturate((dG - _GrooveWidth) / max(_GrooveBevel, 1e-5));
                float wallSlope = 6.0 * wx * (1.0 - wx) * _GrooveDepth;
                wallSlope *= saturate(_GrooveBevel / (aaP * 2.0));   // 遠景でのちらつき防止
                float2 upDir = gG / max(length(gG), 1e-6);           // 溝から外向き = 上り方向
                grad += upDir * wallSlope;

                float3 normalWS = normalize(N0 - (grad.x * T + grad.y * B));

                // ---- ベース色・質感 ----
                // 粒状ノイズ：ノイズの粒が画素より細かくなる遠景では平均値(0.5)に置き換えて計算を省く
                // （ちらつきも減る）。強さ 0 のときは丸ごと省略
                float grain = 0.5;
                if (_GrainStrength > 0.0)
                {
                    float f1 = aaP * _GrainScale;          // ノイズ1マスあたりの画素数の逆数
                    float f2 = f1 * 2.7;
                    float n1 = (f1 < 1.0) ? lerp(ValueNoise(P * _GrainScale), 0.5, saturate(f1 * 2.0 - 1.0)) : 0.5;
                    float n2 = (f2 < 1.0) ? lerp(ValueNoise(P * _GrainScale * 2.7), 0.5, saturate(f2 * 2.0 - 1.0)) : 0.5;
                    grain = n1 * 0.6 + n2 * 0.4;
                }
                half tileBright = lerp(1.0 - _TileVariation, 1.0 + _TileVariation, tileRnd);
                half3 albedo = _BaseColor.rgb * tileBright * lerp(1.0, 0.85 + grain * 0.3, _GrainStrength);
                albedo = lerp(albedo, _SeamColor.rgb, seamMask);

                half smoothness = _Smoothness * lerp(1.0, 0.75 + grain * 0.25, _GrainStrength);
                smoothness *= lerp(1.0, 0.92 + tileRnd2 * 0.08, _TileVariation);
                smoothness = lerp(smoothness, 0.3, seamMask);
                half metallic = _Metallic;

                // ---- 溝の色と発光（光は溝の幅の中だけ）----
                albedo = lerp(albedo, _GrooveColor.rgb, grooveMask);
                smoothness = lerp(smoothness, 0.55, grooveMask);
                half3 emission = _CircuitColor.rgb * _CircuitIntensity * grooveLight
                               + _PulseColor.rgb * _PulseIntensity * pulseM;

                // ---- 目地の淡い発光 ----
                emission += _SeamGlowColor.rgb * _SeamGlow * bevelMask * (1.0 - seamMask * 0.5);

                // ---- 一部タイルの発光 ----
                if (tileRnd2 < _TileGlowChance)
                {
                    float inner = smoothstep(0.0, 0.35, edge);
                    emission += _TileGlowColor.rgb * _TileGlowIntensity * lerp(0.6, 1.0, inner) * (1.0 - seamMask);
                }

                // ---- ライティング（URP PBR）----
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo = albedo;
                surf.metallic = metallic;
                surf.specular = 0;
                surf.smoothness = smoothness;
                surf.normalTS = half3(0, 0, 1);
                surf.emission = emission;
                surf.occlusion = lerp(1.0, 0.4, max(seamMask, grooveMask * 0.8));
                surf.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surf);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ================= DepthOnly =================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct AttributesD { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryingsD { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            VaryingsD vertDepth(AttributesD IN)
            {
                VaryingsD OUT = (VaryingsD)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half fragDepth(VaryingsD IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }

        // ================= DepthNormals (SSAO 等用) =================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing

            struct AttributesDN { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryingsDN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            VaryingsDN vertDN(AttributesDN IN)
            {
                VaryingsDN OUT = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 fragDN(VaryingsDN IN) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }

        // ================= ShadowCaster =================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesS { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryingsS { float4 positionCS : SV_POSITION; };

            VaryingsS vertShadow(AttributesS IN)
            {
                VaryingsS OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                cs = ApplyShadowClamping(cs);
                OUT.positionCS = cs;
                return OUT;
            }
            half4 fragShadow(VaryingsS IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    CustomEditor "CyberCircuitFloorGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
