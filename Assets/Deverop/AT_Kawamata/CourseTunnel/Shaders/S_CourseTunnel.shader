// コースのトンネル（CourseTunnelMeshが作るチューブ）の見た目。BotWの祠エレベーターのVFXを、コースの向きに置き換えたもの。
// ・進む方向に沿って走る細い光の線（エレベーターの縦線にあたる）
// ・テクスチャを使わないノイズを進む方向へ流した、炎のようなゆらぎ
// ・進む方向へ流れる光の輪（パルス）と、流れる光の粒
// ・縁の光（斜めから見る面ほど明るい）、カメラの近く・遠く・両端で薄くするフェード
// 加算合成なので、暗いところでは光り、明るいところでは目立たない。色はHDRにしてBloomで光らせる。
// ・区画ごとの不透明度（CourseTunnelMeshがMaterialPropertyBlockで渡す。本線の距離で区切る）
// ・レーサーが壁に当たったところを濃くする（CourseTunnelHitGlowが最大8人分の位置を渡す。区画で消していても見える）
// メッシュのUV：uv0 =（円周0〜1, 距離m）、uv1 =（本線の距離m, 両端のフェード）、uv2 =（半径m, 0）。
Shader "Misoten/Course Tunnel"
{
    Properties
    {
        [Header(Overall)]
        _Opacity ("不透明度（小さいほど薄く、向こうが透けて見える）", Range(0, 1)) = 0.35
        _Intensity ("全体の強さ（HDR。Bloomの光り方も変わる）", Range(0, 5)) = 1
        [HDR] _BaseColor ("壁のうっすらとした色", Color) = (0.02, 0.08, 0.25, 1)

        [Header(Lines)]
        [HDR] _LineColor ("光の線の色", Color) = (0.3, 0.7, 2.5, 1)
        _LineCount ("光の線の本数（円周）", Range(4, 256)) = 48
        _LineWidth ("光の線の太さ（間隔に対する割合）", Range(0.005, 0.5)) = 0.06
        _LineFlicker ("線ごとの明るさのばらつき", Range(0, 1)) = 0.5

        [Header(Flame Noise)]
        [HDR] _FlameColor ("ゆらぎの色", Color) = (0.1, 0.35, 2, 1)
        _FlameStrength ("ゆらぎの強さ", Range(0, 3)) = 0.8
        _NoiseScaleAround ("ゆらぎの細かさ（円周、整数）", Range(1, 32)) = 6
        _NoiseScaleAlong ("ゆらぎの細かさ（1mあたり）", Range(0.001, 0.5)) = 0.05
        _FlowSpeed ("流れる速さ（m/s）", Range(0, 200)) = 25
        _NoiseToLines ("ゆらぎで線を明滅させる量", Range(0, 1)) = 0.7

        [Header(Pulse Rings)]
        [HDR] _PulseColor ("光の輪の色", Color) = (0.5, 1.2, 3, 1)
        _PulseInterval ("光の輪の間隔（m）", Range(1, 500)) = 60
        _PulseWidth ("光の輪の幅（m）", Range(0.05, 20)) = 1.5
        _PulseSpeed ("光の輪の速さ（m/s）", Range(-200, 200)) = 40

        [Header(Sparkles)]
        [HDR] _SparkleColor ("光の粒の色", Color) = (0.6, 1.5, 3, 1)
        _SparkleColumns ("光の粒のマス（円周）", Range(4, 256)) = 64
        _SparkleCellLength ("光の粒のマスの長さ（m）", Range(0.5, 50)) = 4
        _SparkleAmount ("光の粒が出る割合", Range(0, 1)) = 0.12
        _SparkleSize ("光の粒の大きさ（m）", Range(0.02, 3)) = 0.25
        _SparkleSpeed ("光の粒の流れる速さ（m/s）", Range(-100, 100)) = 8

        [Header(Fresnel)]
        _FresnelPower ("縁の光の鋭さ", Range(0.1, 8)) = 2
        _FresnelMin ("正面から見たときの明るさ", Range(0, 1)) = 0.15

        [Header(Distance Fade)]
        _NearFadeStart ("カメラの近くで消える距離（m）", Range(0, 200)) = 4
        _NearFadeEnd ("カメラの近くで消え終わる距離（m）", Range(0, 400)) = 25
        _FarFadeStart ("遠くで薄くなり始める距離（m）", Range(0, 5000)) = 400
        _FarFadeEnd ("遠くで消える距離（m）", Range(0, 10000)) = 900

        [Header(Wall Hit)]
        _HitRadius ("壁に当たったときに濃くなる範囲（m）", Range(1, 100)) = 14
        _HitOpacity ("当たったところの不透明度", Range(0, 3)) = 1
        [HDR] _HitColor ("当たったところに足す色", Color) = (0.6, 1.4, 4, 1)
        _HitNearFade ("当たったところをカメラの近くで消す距離（m）", Range(0, 20)) = 1

        [Header(Intersection)]
        [Toggle(_INTERSECTION_ON)] _Intersection ("地形と交わるところを光らせる（URPのDepth Textureが必要）", Float) = 0
        [HDR] _IntersectionColor ("交わるところの色", Color) = (0.5, 1.2, 3, 1)
        _IntersectionWidth ("交わるところの幅（m）", Range(0.05, 10)) = 1
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
            Name "CourseTunnel"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _INTERSECTION_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_INTERSECTION_ON)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float _Intensity;
                float4 _BaseColor;
                float4 _LineColor;
                float _LineCount;
                float _LineWidth;
                float _LineFlicker;
                float4 _FlameColor;
                float _FlameStrength;
                float _NoiseScaleAround;
                float _NoiseScaleAlong;
                float _FlowSpeed;
                float _NoiseToLines;
                float4 _PulseColor;
                float _PulseInterval;
                float _PulseWidth;
                float _PulseSpeed;
                float4 _SparkleColor;
                float _SparkleColumns;
                float _SparkleCellLength;
                float _SparkleAmount;
                float _SparkleSize;
                float _SparkleSpeed;
                float _FresnelPower;
                float _FresnelMin;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _FarFadeStart;
                float _FarFadeEnd;
                float4 _IntersectionColor;
                float _IntersectionWidth;
                float _HitRadius;
                float _HitOpacity;
                float4 _HitColor;
                float _HitNearFade;
            CBUFFER_END

            // 以下はマテリアルではなく、スクリプトがMaterialPropertyBlockで渡す。渡されていなければ0なので、区画は無効・当たりなしになる。
            #define MAX_TUNNEL_SECTIONS 16
            #define MAX_TUNNEL_HITS 8
            float _TunnelSectionEnabled;                         // 1なら区画を使う
            float _TunnelSectionCount;
            float _TunnelOutsideOpacity;                         // 区画の外の不透明度の倍率
            float _TunnelSectionFade;                            // 区画の境目をなめらかにする長さ（m）
            float4 _TunnelSections[MAX_TUNNEL_SECTIONS];         // x=開始, y=終了（本線の距離m）, z=不透明度の倍率
            float4 _TunnelHits[MAX_TUNNEL_HITS];                 // xyz=当たった位置（ワールド）, w=強さ0〜1

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 uv : TEXCOORD2;      // xy = uv0（円周, 距離m）、z = 両端のフェード、w = 半径m
                float fogFactor : TEXCOORD3;
                float mainDistance : TEXCOORD4;
                #if defined(_INTERSECTION_ON)
                float4 screenPos : TEXCOORD5;
                #endif
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float WrapCell(float x, float period)
            {
                return x - period * floor(x / period);
            }

            // 円周方向（x）にperiodで繰り返す値ノイズ。継ぎ目が出ないようにする。
            float PeriodicValueNoise(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float x0 = WrapCell(i.x, period);
                float x1 = WrapCell(i.x + 1.0, period);
                float a = Hash12(float2(x0, i.y));
                float b = Hash12(float2(x1, i.y));
                float c = Hash12(float2(x0, i.y + 1.0));
                float d = Hash12(float2(x1, i.y + 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float PeriodicFbm(float2 p, float period)
            {
                float value = PeriodicValueNoise(p, period) * 0.5;
                value += PeriodicValueNoise(p * 2.0, period * 2.0) * 0.25;
                value += PeriodicValueNoise(p * 4.0, period * 4.0) * 0.125;
                return value / 0.875;
            }

            // 本線の距離mainDistanceでの、区画による不透明度の倍率。区画が重なっていたら後ろの区画を優先する。
            float SectionOpacity(float mainDistance)
            {
                if (_TunnelSectionEnabled < 0.5)
                {
                    return 1.0;
                }

                float halfFade = max(_TunnelSectionFade, 0.001) * 0.5;
                float opacity = _TunnelOutsideOpacity;
                for (int i = 0; i < MAX_TUNNEL_SECTIONS; i++)
                {
                    if (i >= (int)_TunnelSectionCount)
                    {
                        break;
                    }

                    float4 section = _TunnelSections[i];
                    float inside = smoothstep(section.x - halfFade, section.x + halfFade, mainDistance) *
                                   (1.0 - smoothstep(section.y - halfFade, section.y + halfFade, mainDistance));
                    opacity = lerp(opacity, section.z, inside);
                }

                return opacity;
            }

            // レーサーが壁に当たったところの強さ（一番強いもの）。
            float WallHitStrength(float3 positionWS)
            {
                float strength = 0.0;
                for (int i = 0; i < MAX_TUNNEL_HITS; i++)
                {
                    float4 hit = _TunnelHits[i];
                    float falloff = 1.0 - smoothstep(_HitRadius * 0.25, _HitRadius, distance(positionWS, hit.xyz));
                    strength = max(strength, hit.w * falloff);
                }

                return strength;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = float4(input.uv0, input.uv1.y, input.uv2.x);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.mainDistance = input.uv1.x;
                #if defined(_INTERSECTION_ON)
                output.screenPos = positionInputs.positionNDC;
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float around = input.uv.x;
                float along = input.uv.y;
                float endFade = input.uv.z;
                float radius = max(input.uv.w, 0.01);
                float time = _Time.y;

                // 炎のようなゆらぎ（進む方向へ流す）
                float period = max(1.0, round(_NoiseScaleAround));
                float2 noiseUV = float2(around * period, (along - time * _FlowSpeed) * _NoiseScaleAlong);
                float noise = PeriodicFbm(noiseUV, period);
                float flame = smoothstep(0.5, 0.9, noise);

                // 光の線（遠くで線が画素より細くなったら、平均の明るさに寄せてチラつきを抑える）
                float lineCoord = around * _LineCount;
                float lineDistance = abs(frac(lineCoord + 0.5) - 0.5);
                float lineAA = fwidth(lineCoord);
                float halfWidth = _LineWidth * 0.5;
                float lineMask = 1.0 - smoothstep(halfWidth, halfWidth + lineAA * 1.5, lineDistance);
                lineMask = lerp(lineMask, _LineWidth, saturate(lineAA * 1.5));
                float lineRandom = Hash12(float2(WrapCell(floor(lineCoord + 0.5), _LineCount), 17.0));
                lineMask *= lerp(1.0, 0.35 + lineRandom * 1.3, _LineFlicker);
                lineMask *= lerp(1.0, saturate(noise * 1.8 - 0.2), _NoiseToLines);

                // 光の輪
                float pulsePhase = WrapCell(along - time * _PulseSpeed, _PulseInterval);
                float pulseDistance = min(pulsePhase, _PulseInterval - pulsePhase);
                float pulse = 1.0 - smoothstep(0.0, _PulseWidth, pulseDistance);

                // 光の粒（マスごとに出るかどうか・位置をハッシュで決める）
                float columns = max(1.0, round(_SparkleColumns));
                float2 sparkleCoord = float2(around * columns, (along - time * _SparkleSpeed) / _SparkleCellLength);
                float2 sparkleCell = floor(sparkleCoord);
                sparkleCell.x = WrapCell(sparkleCell.x, columns);
                float sparkleHash = Hash12(sparkleCell + 3.7);
                float2 sparklePoint = Hash22(sparkleCell) * 0.7 + 0.15;
                float cellWidth = 6.28318530718 * radius / columns;
                float2 sparkleOffset = (frac(sparkleCoord) - sparklePoint) * float2(cellWidth, _SparkleCellLength);
                float sparkle = 1.0 - smoothstep(_SparkleSize * 0.3, _SparkleSize, length(sparkleOffset));
                sparkle *= step(sparkleHash, _SparkleAmount);
                sparkle *= 0.6 + 0.4 * sin(time * 6.0 + sparkleHash * 40.0);

                float3 color = _BaseColor.rgb;
                color += _LineColor.rgb * lineMask;
                color += _FlameColor.rgb * flame * _FlameStrength;
                color += _PulseColor.rgb * pulse;
                color += _SparkleColor.rgb * sparkle;

                // 縁の光（中からも外からも見るので、法線の向きは問わない）
                float3 viewDirection = normalize(GetCameraPositionWS() - input.positionWS);
                float facing = abs(dot(normalize(input.normalWS), viewDirection));
                float fresnel = lerp(_FresnelMin, 1.0, pow(saturate(1.0 - facing), _FresnelPower));

                // カメラからの距離で薄くする（中を飛ぶので、すぐ近くの壁は消す）
                float cameraDistance = distance(GetCameraPositionWS(), input.positionWS);
                float nearFade = smoothstep(_NearFadeStart, max(_NearFadeStart + 0.01, _NearFadeEnd), cameraDistance);
                float farFade = 1.0 - smoothstep(_FarFadeStart, max(_FarFadeStart + 0.01, _FarFadeEnd), cameraDistance);

                // 加算合成なので、明るさを下げるほど後ろの景色が透けて見える。区画ごとの倍率もここで掛ける。
                float opacity = _Opacity * SectionOpacity(input.mainDistance);
                float3 pattern = color;
                color = pattern * fresnel * nearFade * farFade * opacity;

                #if defined(_INTERSECTION_ON)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragmentEyeDepth = input.screenPos.w;
                float intersection = 1.0 - saturate((sceneEyeDepth - fragmentEyeDepth) / _IntersectionWidth);
                color += _IntersectionColor.rgb * intersection * farFade * opacity;
                #endif

                // 壁に当たったところ：不透明度・区画・カメラ近くのフェードに関係なく濃くする（自分が当たったところも見えるように）。
                float hit = WallHitStrength(input.positionWS);
                float hitNearFade = smoothstep(0.0, max(_HitNearFade, 0.001), cameraDistance);
                color += (pattern * _HitOpacity + _HitColor.rgb) * hit * hitNearFade * farFade;

                color *= endFade * _Intensity;

                // 加算合成なので、フォグの中では黒へ寄せて消す。
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
