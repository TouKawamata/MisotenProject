Shader "Custom/DistantOcean"
{
    Properties
    {
        [Header(Ocean Colors)]
        // 真下〜近くを見たときの深い海の色
        _DeepColor ("Deep Ocean Color", Color) = (0.02, 0.14, 0.32, 1.0)
        // 斜め・遠くを見たときの明るい海の色（フレネル反射）
        _ShallowColor ("Grazing Ocean Color", Color) = (0.12, 0.42, 0.65, 1.0)
        // 水平線（0度）の空と溶け合う色（スカイボックスの水平線の色と合わせる）
        _HorizonColor ("Horizon Sky Color", Color) = (0.62, 0.82, 0.95, 1.0)
        // 視線角度による色の変化カーブ
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.8

        [Header(Virtual Sea Surface and Waves)]
        // 仮想海面のワールドY座標（板ポリがどこにあっても、この高さに海があるとして水平線まで投影する）
        _SeaLevel ("Virtual Sea Level Y", Float) = -500.0
        // カメラから見た最低限の海の深さ（海面より下にカメラが行った場合の安全マージン）
        _MinSeaDepth ("Min Virtual Sea Depth", Float) = 100.0
        // 波の全体の細かさ（ワールド座標基準）
        _WaveScale ("Wave Scale", Range(0.0005, 0.1)) = 0.015
        // 波が流れる速さ
        _WaveSpeed ("Wave Scroll Speed", Range(0.0, 3.0)) = 0.45
        // 波の法線の強さ（うねりの立体感）
        _WaveNormalStrength ("Wave Normal Strength", Range(0.0, 2.0)) = 0.65

        [Header(Whitecaps and Foam)]
        // 白波の色
        _FoamColor ("Whitecap Color", Color) = (0.92, 0.97, 1.0, 1.0)
        // 白波の強さ（0で白波なし）
        _FoamIntensity ("Whitecap Intensity", Range(0.0, 2.0)) = 0.85
        // 白波が出る波の高さのしきい値（下げるほど白波が増える）
        _FoamThreshold ("Whitecap Threshold", Range(0.3, 0.95)) = 0.64
        // 白波の輪郭のボケ具合
        _FoamSoftness ("Whitecap Softness", Range(0.01, 0.3)) = 0.10

        [Header(Sun Reflection)]
        // 太陽反射の色（Directional Lightの色に掛け合わされる）
        _SunSpecColor ("Sun Specular Color", Color) = (1.0, 0.96, 0.85, 1.0)
        // 太陽の光の道（水平線から伸びる広い反射）の強さ
        _SunPathIntensity ("Sun Path Intensity", Range(0.0, 3.0)) = 1.0
        // 太陽の光の道の絞り具合
        _SunPathGloss ("Sun Path Sharpness", Range(8.0, 256.0)) = 42.0
        // 水面のキラキラ（細かい強反射）の強さ
        _SparkleIntensity ("Sun Sparkle Intensity", Range(0.0, 5.0)) = 2.2
        // 水面のキラキラの鋭さ
        _SparkleGloss ("Sun Sparkle Sharpness", Range(64.0, 1024.0)) = 360.0

        [Header(Horizon Blend and Anti Shimmer)]
        // 遠方で波のザラつき（モアレ）を抑える減衰率
        _DistanceDetailFade ("Distant Anti-Shimmer Fade", Range(0.0001, 0.003)) = 0.00045
        // 水平線（0度）付近の大気霞の広がり（大きいほど水平線の手前から空の色に溶ける）
        _HorizonHazeRange ("Horizon Haze Width", Range(0.01, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            // 不透明オブジェクト（都市のビル等 = 2000）がすべて描画された後、Skybox(2500)の直前に描画する
            // これにより都市で隠れているピクセルは100% Early-Z でスキップされ、負荷がゼロになる
            "Queue" = "Geometry+450"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "DistantOceanForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _HorizonColor;
                float _FresnelPower;

                float _SeaLevel;
                float _MinSeaDepth;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveNormalStrength;

                half4 _FoamColor;
                float _FoamIntensity;
                float _FoamThreshold;
                float _FoamSoftness;

                half4 _SunSpecColor;
                float _SunPathIntensity;
                float _SunPathGloss;
                float _SparkleIntensity;
                float _SparkleGloss;

                float _DistanceDetailFade;
                float _HorizonHazeRange;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // 高速な2Dハッシュ
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 高さ(x)と傾き微分(yz)を1回の計算で同時に返す超軽量ノイズ
            float3 WaveNoiseDeriv(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float2 u = f * f * (3.0 - 2.0 * f);
                float2 du = 6.0 * f * (1.0 - f);

                float a = Hash21(i + float2(0.0, 0.0));
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                float k0 = a;
                float k1 = b - a;
                float k2 = c - a;
                float k3 = a - b - c + d;

                float height = k0 + k1 * u.x + k2 * u.y + k3 * u.x * u.y;
                float2 deriv = du * (float2(k1, k2) + k3 * u.yx);

                return float3(height, deriv.x, deriv.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // QuadでもPlaneでも、カメラを中心に半径50,000m（水平線0.001度まで）を覆う仮想スクリーン面へ展開
                float2 corner = clamp(float2(input.positionOS.x, input.positionOS.y + input.positionOS.z) * 2.0, -1.0, 1.0);
                float3 proxyWS = float3(
                    _WorldSpaceCameraPos.x + corner.x * 50000.0,
                    _WorldSpaceCameraPos.y - 1.0,
                    _WorldSpaceCameraPos.z + corner.y * 50000.0
                );

                float4 posCS = TransformWorldToHClip(proxyWS);

                // カメラのFar Clip (1000m) で絶対に切り落とされないよう、
                // クリップ空間のZ値を「Skyboxのすぐ手前（最奥背景）」に固定する
                // （posCS.w の定数倍なので、カメラ背後のクリッピングやパース補間が100%正確に保たれる）
                #if UNITY_REVERSED_Z
                    posCS.z = posCS.w * 0.00001;
                #else
                    posCS.z = posCS.w * 0.99998;
                #endif

                output.positionCS = posCS;
                output.positionWS = proxyWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // カメラからそのピクセルへ向かう視線ベクトル（rayDir.y < 0 が水平線より下）
                float3 rayDir = normalize(input.positionWS - _WorldSpaceCameraPos);

                // 水平線（0度）より上のピクセルは描画しない
                clip(-rayDir.y - 0.0001);

                float3 viewDirWS = -rayDir;

                // --- 仮想海面レイキャスト（カメラの視線が Y = _SeaLevel の海面に当たる正確な座標を算出） ---
                float camHeightAboveSea = max(_MinSeaDepth, _WorldSpaceCameraPos.y - _SeaLevel);
                float downCos = max(0.0002, -rayDir.y);
                float virtualDist = camHeightAboveSea / downCos;

                // 仮想海面上のワールドXZ座標（何万メートル先の水平線まで途切れず続く）
                float2 seaXZ = _WorldSpaceCameraPos.xz + rayDir.xz * virtualDist;

                // 遠方のモアレ（砂嵐のようなチラつき）を防ぐ距離・角度減衰
                float detailAtten = exp(-virtualDist * _DistanceDetailFade) * smoothstep(0.0, 0.06, downCos);

                // ワールドXZ座標をベースに、角度とスケールを変えた3層の波をスクロール合成
                float2 baseUV = seaXZ * _WaveScale;
                float t = _Time.y * _WaveSpeed;

                // 第1層：大きなうねり
                float2 uv1 = baseUV + float2(t * 0.8, t * 0.35);
                float3 wave1 = WaveNoiseDeriv(uv1);

                // 第2層：約37度回転させた逆方向の波
                float2 rotUV = float2(baseUV.x * 0.8 - baseUV.y * 0.6, baseUV.x * 0.6 + baseUV.y * 0.8);
                float2 uv2 = rotUV * 1.65 + float2(-t * 0.65, t * 0.75) + 17.3;
                float3 wave2 = WaveNoiseDeriv(uv2);

                // 第3層：細かいさざ波（キラキラ・白波のディテール用）
                float2 uv3 = float2(rotUV.x * 0.6 + rotUV.y * 0.8, -rotUV.x * 0.8 + rotUV.y * 0.6) * 3.4 + float2(t * 1.1, -t * 0.9) + 43.1;
                float3 wave3 = WaveNoiseDeriv(uv3);

                // --- 1. 法線の構築 ---
                float2 slope = (wave1.yz * 0.50 + wave2.yz * 0.35 + wave3.yz * 0.15) * (_WaveNormalStrength * detailAtten);
                float3 worldNormal = normalize(float3(-slope.x, 1.0, -slope.y));

                // --- 2. 海のベース色（視線角度によるフレネルグラデーション） ---
                float NdotV = saturate(dot(worldNormal, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                half3 oceanColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, fresnel);

                // うねりの高低差でわずかに明暗をつけて遠目でも立体感を出す
                float swellShade = saturate(wave1.x * 0.6 + wave2.x * 0.4);
                oceanColor *= lerp(0.88, 1.10, swellShade);

                // --- 3. 洋上の白波（波の山と山が重なった頂点に発生） ---
                float waveCrest = wave1.x * 0.42 + wave2.x * 0.38 + wave3.x * 0.20 + (wave1.x * wave2.x) * 0.25;
                float foamMask = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSoftness, waveCrest);
                foamMask *= _FoamIntensity * detailAtten;
                oceanColor = lerp(oceanColor, _FoamColor.rgb, saturate(foamMask));

                // --- 4. 太陽の反射（広い光の道 ＋ 細かいキラキラ） ---
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                if (dot(lightDir, lightDir) < 0.001)
                {
                    lightDir = normalize(float3(0.4, 0.6, 0.7));
                }

                float3 halfDir = normalize(lightDir + viewDirWS);

                // (A) 太陽の光の道（水平線から手前へ伸びる広い反射）
                float3 broadNormal = normalize(float3(-slope.x * 0.6, 1.0, -slope.y * 0.6));
                float NdotH_broad = saturate(dot(broadNormal, halfDir));
                float sunPath = pow(NdotH_broad, _SunPathGloss) * _SunPathIntensity * smoothstep(0.0, 0.03, downCos);

                // (B) 水面のキラキラ（さざ波の頂点がパキッと光る反射）
                float2 sparkleSlope = (wave1.yz * 0.3 + wave2.yz * 0.35 + wave3.yz * 0.65) * (_WaveNormalStrength * 1.35 * detailAtten);
                float3 sparkleNormal = normalize(float3(-sparkleSlope.x, 1.0, -sparkleSlope.y));
                float NdotH_sparkle = saturate(dot(sparkleNormal, halfDir));
                float sparkle = pow(NdotH_sparkle, _SparkleGloss) * _SparkleIntensity * detailAtten;

                half3 sunColor = _SunSpecColor.rgb * mainLight.color;
                oceanColor += (sunPath + sparkle) * sunColor;

                // --- 5. 水平線（0度）へ向かって空の色へシームレスに溶け込む大気霞 ---
                // 水平線（downCos = 0）で確実に100% _HorizonColor になるため、切れ目が絶対に発生しない
                float horizonHaze = exp(-downCos / max(0.005, _HorizonHazeRange));
                oceanColor = lerp(oceanColor, _HorizonColor.rgb, saturate(horizonHaze));

                return half4(oceanColor, 1.0);
            }
            ENDHLSL
        }
    }
}
