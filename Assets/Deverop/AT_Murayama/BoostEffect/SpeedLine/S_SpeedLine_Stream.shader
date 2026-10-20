Shader "Hidden/Custom/SpeedLine_Stream"
{
    Properties
    {
        [Header(Base Settings)]
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _LineColor ("Line Color", Color) = (1, 1, 1, 0.95)

        // 強さは _SpeedLineStrength をグローバル変数として外から渡す

        [Header(Line Count and Pool)]
        // 最大同時本数（1本〜30本まで直接指定可能）
        _MaxLineCount ("Max Line Count", Range(1, 30)) = 8
        // 発生タイミングと同時本数のゆらぎ（0=常にMax本数、上げるほど本数がランダムに増減する）
        _SpawnFlexibility ("Count and Timing Randomness", Range(0.0, 0.9)) = 0.45
        // 発生地点の角度プール数（本数を減らしても、このプール数の中からランダムな角度に出る）
        _SlotCount ("Spawn Angle Pool Count", Range(12, 360)) = 90

        [Header(Line Shape and Sharpness)]
        // 線の基準の太さ（画面の縁での三角形の根元の太さ。小さいほど細い針になる）
        _LineThickness ("Line Thickness", Range(0.02, 0.6)) = 0.14
        // 線ごとの太さのバラつき（上げるほど極細の線と太めの線がランダムに混ざる）
        _ThicknessRandomness ("Thickness Randomness", Range(0.0, 1.0)) = 0.70
        // 三角形の鋭さ（1.0=直線的な二等辺三角形、上げるほど先端が細く鋭い針状になる）
        _TriangleTaper ("Triangle Sharpness", Range(0.6, 3.0)) = 1.45
        // 輪郭のシャープさ（小さいほどパキッとした境界になる）
        _LineSoftness ("Line Edge Softness", Range(0.0, 0.15)) = 0.01
        // たまに混じる強調線（太く長い線）のアクセント強さ
        _BigLineAccent ("Big Line Accent", Range(0.0, 2.0)) = 0.8

        [Header(Flow and Length)]
        // 伸びて消えるサイクルの速さ
        _FlowSpeed ("Speed", Range(0.5, 10.0)) = 3.5
        // 一番短い線の長さ（画面の縁から中心へ向かう割合）
        _MinLength ("Min Line Length", Range(0.05, 0.8)) = 0.18
        // 長い線の最大長（画面の縁から中心へ向かう割合）
        _MaxLength ("Max Line Length", Range(0.2, 1.0)) = 0.78
        // ベース線の長さのランダム性（上げるほど短い線〜長い線が毎回ランダムにバラける）
        _LengthRandomness ("Length Randomness", Range(0.0, 1.0)) = 0.80

        [Header(Mask Settings)]
        // 中心の絶対クリア領域（これより内側には線が侵入しない）
        _CenterRadius ("Center Clear Radius", Range(0.1, 0.8)) = 0.28
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float2 _Center;
    half4 _LineColor;

    // 外部（SpeedLineEffect / PreviewController）からセットされるグローバル変数
    float _SpeedLineStrength;

    float _MaxLineCount;
    float _SpawnFlexibility;
    float _SlotCount;

    float _LineThickness;
    float _ThicknessRandomness;
    float _TriangleTaper;
    float _LineSoftness;
    float _BigLineAccent;

    float _FlowSpeed;
    float _MinLength;
    float _MaxLength;
    float _LengthRandomness;

    float _CenterRadius;

    float2 Hash21(float p)
    {
        float3 p3 = frac(float3(p, p, p) * float3(0.1031, 0.1030, 0.0973));
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.xx + p3.yz) * p3.zy);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // 画面縦横比を補正した中心からのベクトル（画面の高さ = 1.0 基準のユークリッド空間）
        float aspect = _ScreenParams.x / _ScreenParams.y;
        float2 rawOffset = uv - _Center.xy;
        float2 aspectDir = rawOffset * float2(aspect, 1.0);
        float aspectDist = length(aspectDir);

        float poolCount = max(8.0, round(_SlotCount));
        int maxLines = clamp((int)round(_MaxLineCount), 1, 30);

        // 画面端での三角形の根元の半幅（非常に細くシャープな針状になるスケールに変換）
        float baseHalfWidth = _LineThickness * 0.016;
        // 1ピクセル相当のシャープなアンチエイリアス幅 ＋ 微調整用のソフトネス
        float aaWidth = (1.2 / max(1.0, _ScreenParams.y)) + _LineSoftness * 0.012;

        float totalLineMask = 0.0;

        // 少数の独立したラインエミッターを回す（本数と発生プールが完全に独立）
        [loop]
        for (int i = 0; i < maxLines; i++)
        {
            float fi = (float)i;
            float2 emitterSeed = Hash21(fi * 37.13 + 11.7);

            // 線ごとにサイクル速度を少しずつ変える
            float speed = _FlowSpeed * lerp(0.8, 1.25, emitterSeed.x);
            float cycleTime = _Time.y * speed + emitterSeed.y * 100.0;
            float cycleId = floor(cycleTime);
            float cyclePhase = frac(cycleTime);

            float eventSeed = fi * 127.1 + cycleId * 311.7;
            float2 randA = Hash21(eventSeed);
            float2 randB = Hash21(eventSeed + 71.3);
            float2 randC = Hash21(eventSeed + 193.7);

            // 発生タイミングのランダムな休止期間（同時本数の増減・ゆらぎを作る）
            float restPortion = _SpawnFlexibility * lerp(0.35, 0.95, randA.x);
            float activePortion = max(0.15, 1.0 - restPortion);

            if (cyclePhase >= activePortion)
            {
                continue;
            }

            // この線の進行度（0.0 = 画面の縁から発生開始, 1.0 = 消滅完了）
            float life = cyclePhase / activePortion;

            // 角度プール（_SlotCount）の中からランダムな角度を選択
            float chosenSlot = floor(randA.y * poolCount);
            float slotJitter = (randB.x - 0.5) * 0.8;
            float lineAngle01 = frac((chosenSlot + 0.5 + slotJitter) / poolCount);

            // 線の方向ベクトル（直線座標系で計算することで、歪みのない鋭い二等辺三角形を作る）
            float angleRad = (lineAngle01 - 0.5) * TWO_PI;
            float2 lineDir = float2(cos(angleRad), sin(angleRad));

            // 中心からその線の向きへの距離（反対側180度は即スキップ）
            float radialDist = dot(aspectDir, lineDir);
            if (radialDist <= 0.0)
            {
                continue;
            }

            // 線の中心軸からの垂直距離（画面上の実際の太さ）
            float perpDist = abs(dot(aspectDir, float2(-lineDir.y, lineDir.x)));

            // たまに混じる強調アクセント成分（太さ・長さとは別の乱数でレア発生）
            float accent = pow(randB.y, 2.8) * (_BigLineAccent * 0.5);

            // 太さのランダム計算：極細(0.28倍)〜太め(1.4倍)まで線ごとに独立してバラけさせる
            float randomThick = lerp(1.0, lerp(0.28, 1.40, randC.x), _ThicknessRandomness);
            float thicknessScale = randomThick + accent * 0.85;
            float maxHalfWidth = baseHalfWidth * thicknessScale;

            // 三角形の根元幅より遠いピクセルは早期スキップ
            if (perpDist > maxHalfWidth + aaWidth)
            {
                continue;
            }

            // その角度の直線が「画面の縁（上下左右の枠）」と交差する正確な距離
            float edgeDist = 0.5 / max(abs(lineDir.x) / aspect, abs(lineDir.y));

            // ベース線の長さのランダム計算：
            // 普段の線自体も Min〜Max の間でランダムに長さが変わり、単調さをなくす
            float baseLenRand = lerp(0.25, randC.y, _LengthRandomness);
            float finalLenT = saturate(lerp(baseLenRand, 1.0, saturate(accent)));
            float lenRatio = saturate(lerp(_MinLength, _MaxLength, finalLenT));
            float targetTipDist = lerp(edgeDist * 0.94, _CenterRadius, lenRatio);
            if (targetTipDist >= edgeDist)
            {
                continue;
            }

            // 線上の位置 u（0.0 = 画面の縁の根元、1.0 = 中心側の先端）
            // 根元は常に u=0（画面の縁）に固定され、線自体は移動しない
            float u = (edgeDist - radialDist) / max(0.001, edgeDist - targetTipDist);

            // --- 1. 画面の縁(u=0)から中心側の先端(u=1)へスッと伸びてくるフェーズ ---
            const float extendPhaseEnd = 0.28;
            float extendU = 1.0 - pow(1.0 - saturate(life / extendPhaseEnd), 3.0);

            if (u > extendU)
            {
                continue;
            }

            // --- 2. 伸び切った後、線自体は移動せず「先端(u=1)から」消えていくフェーズ ---
            float eraseProgress = saturate((life - extendPhaseEnd) / (1.0 - extendPhaseEnd));
            // eraseFrontU が 1.0（中心側の先端）から 0.0（画面の縁）へ戻る＝先端から順に消える
            float eraseFrontU = 1.0 - pow(eraseProgress, 1.2);

            if (u > eraseFrontU)
            {
                continue;
            }

            float clampedU = max(0.0, u);

            // --- 細くシャープな三角形（針状）の形状計算 ---
            // 根元(u=0)が画面の縁に固定され、先端に向かって直線〜鋭角に幅0へ絞られる
            float normU = saturate(clampedU / max(0.001, extendU));
            float triangleProfile = pow(1.0 - normU, _TriangleTaper);

            // 先端(u=1)から消える際、消え際の先端をボカさず「鋭い針先」のまま削っていく幅マスク
            float tipErasePinch = (eraseProgress <= 0.001)
                ? 1.0
                : saturate((eraseFrontU - clampedU) / 0.22);
            // 消滅の終盤にかけて全体の太さもスッと細くする
            float rootThin = smoothstep(1.0, 0.35, eraseProgress);

            float currentHalfWidth = maxHalfWidth * triangleProfile * pow(tipErasePinch, 0.7) * rootThin;

            // 内部を半透明でボカさず、パキッとしたシャープな三角形として描画
            float angularMask = 1.0 - smoothstep(max(0.0, currentHalfWidth - aaWidth), currentHalfWidth + aaWidth * 0.5, perpDist);
            angularMask *= step(0.0001, currentHalfWidth);

            totalLineMask = max(totalLineMask, angularMask);
        }

        // 中心のクリア領域マスク
        float centerSafeMask = smoothstep(_CenterRadius * 0.85, _CenterRadius, aspectDist);

        float alpha = saturate(totalLineMask * centerSafeMask * _SpeedLineStrength * _LineColor.a);
        color.rgb = lerp(color.rgb, _LineColor.rgb, alpha);
        return color;
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SpeedLineStreamPass"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
