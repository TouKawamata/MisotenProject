Shader "Custom/SkyAquaCascade/S_PrismaticRainbowHalo"
{
    Properties
    {
        [Header(Prismatic Spectrum Optics)]
        _FluidNoiseTex ("Mist and Droplet Sparkle Texture", 2D) = "white" {}
        [HDR] _RainbowTint ("Rainbow Overall Tint (HDR)", Color) = (1.0, 1.0, 1.0, 1.0)
        _RainbowIntensity ("Rainbow HDR Emission Intensity", Range(0.2, 8.0)) = 2.6
        _PrimaryRadius ("Primary Bow Normalized Radius", Range(0.35, 0.80)) = 0.62
        _RingThickness ("Primary Bow Band Thickness", Range(0.03, 0.30)) = 0.11
        _SecondaryBowOpacity ("Secondary Outer Bow Opacity", Range(0.0, 1.0)) = 0.42
        _SecondaryRadiusOffset ("Secondary Bow Radial Offset", Range(0.08, 0.30)) = 0.18

        [Header(Solar Halo and Droplet Sparkle)]
        _InnerGlowIntensity ("Inner Supernumerary Halo Glow", Range(0.0, 2.0)) = 0.38
        _SparkleIntensity ("Sunlit Droplet Sparkle Intensity", Range(0.0, 4.0)) = 1.5
        _ShimmerSpeed ("Atmospheric Shimmer Speed", Range(0.0, 4.0)) = 0.85
        _AzimuthFade ("Bottom Mist Immersion Boost", Range(0.0, 1.0)) = 0.35

        [Header(Soft Depth and Fly Through Fade)]
        _SoftDepthDistance ("Building Intersection Soft Fade (m)", Range(0.2, 15.0)) = 4.0
        _CameraFadeStart ("Camera Fly-Through Fade Start (m)", Range(0.5, 8.0)) = 2.0
        _CameraFadeEnd ("Camera Fly-Through Fade End (m)", Range(2.0, 25.0)) = 8.0

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One, 10=OneMinusSrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 2=Back)", Float) = 0
        [Toggle] _ZWrite ("ZWrite (0=Off, 1=On)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (4=LEqual, 8=Always)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+15"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                float4 vertColor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_FluidNoiseTex);
            SAMPLER(sampler_FluidNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FluidNoiseTex_ST;
                float4 _RainbowTint;
                float  _RainbowIntensity;
                float  _PrimaryRadius;
                float  _RingThickness;
                float  _SecondaryBowOpacity;
                float  _SecondaryRadiusOffset;
                float  _InnerGlowIntensity;
                float  _SparkleIntensity;
                float  _ShimmerSpeed;
                float  _AzimuthFade;
                float  _SoftDepthDistance;
                float  _CameraFadeStart;
                float  _CameraFadeEnd;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            // 物理的な連続スペクトル分散関数 (t = 0.0: 紫/内縁 -> t = 1.0: 赤/外縁)
            // 滑らかなガウス・コサイン合成により、人工的な段差のない自然光学7色スペクトルを生成
            float3 EvaluatePhysicalSpectrum(float t)
            {
                float x = saturate(t);
                // 波長400nm(紫: x=0) 〜 700nm(赤: x=1) の連続光学近似
                float3 phase = float3(0.78, 0.52, 0.22);
                float3 width = float3(0.24, 0.22, 0.20);
                float3 diff  = (x.xxx - phase) / width;
                float3 spec  = exp(-diff * diff * 2.2);

                // 短波長側（紫・藍: x < 0.22）の赤み成分（バイオレットの再現）
                float violetR = exp(-pow((x - 0.08) / 0.12, 2.0)) * 0.48;
                spec.r += violetR;

                // 自然な彩度と輝度バランス補正
                spec.g *= 0.95;
                return saturate(spec);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.vertColor  = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 極座標 (r: 中心からの正規化距離 0..1, angle: -PI..PI)
                float2 centered = input.uv * 2.0 - 1.0;
                float r = length(centered);
                if (r > 0.995)
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                float angle = atan2(centered.y, centered.x);
                float normAngle = (angle + 3.14159265) / 6.2831853;

                // 2. 深度フェード（ビル壁面交差消去 ＆ カメラ近接通過フェード）
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float surfaceEyeDepth = input.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float depthDiff = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                float softIntersection = saturate(depthDiff / max(0.05, _SoftDepthDistance));
                float cameraNearFade = smoothstep(_CameraFadeStart, max(_CameraFadeStart + 0.5, _CameraFadeEnd), surfaceEyeDepth);

                // 3. 水煙ノイズ・水滴スパークルサンプリング
                float time = _Time.y * _ShimmerSpeed;
                float2 polarUV1 = float2(normAngle * 6.0 + time * 0.05, r * 3.0 - time * 0.12);
                float2 polarUV2 = centered * 2.2 + float2(0.0, time * 0.18);

                float4 noise1 = SAMPLE_TEXTURE2D_LOD(_FluidNoiseTex, sampler_FluidNoiseTex, polarUV1, 0.0);
                float4 noise2 = SAMPLE_TEXTURE2D_LOD(_FluidNoiseTex, sampler_FluidNoiseTex, polarUV2, 0.0);

                float mistModulation = lerp(0.78, 1.25, noise1.b * 0.6 + noise2.b * 0.4);
                float dropletSparkle = pow(saturate(noise1.a * noise2.g * 2.2), 2.5) * _SparkleIntensity;

                // 4. 主虹（42度 Primary Rainbow: 内側が紫 -> 外側が赤）
                float halfThick = max(0.015, _RingThickness * 0.5);
                float primaryT = (r - (_PrimaryRadius - halfThick)) / (halfThick * 2.0);
                float primaryEnvelope = exp(-pow((r - _PrimaryRadius) / (halfThick * 0.72), 2.0));
                primaryEnvelope *= smoothstep(0.0, 0.15, primaryT) * smoothstep(1.0, 0.85, primaryT);
                float3 primaryColor = EvaluatePhysicalSpectrum(primaryT) * primaryEnvelope;

                // 5. 副虹（51度 Secondary Rainbow: 主虹の外側・色の並び順が反転＝内側が赤 -> 外側が紫、帯が広く淡い）
                float secRadius = min(0.92, _PrimaryRadius + _SecondaryRadiusOffset);
                float secHalfThick = halfThick * 1.45;
                float secondaryT = 1.0 - ((r - (secRadius - secHalfThick)) / (secHalfThick * 2.0));
                float secondaryEnvelope = exp(-pow((r - secRadius) / (secHalfThick * 0.75), 2.0));
                secondaryEnvelope *= smoothstep(0.0, 0.18, secondaryT) * smoothstep(1.0, 0.82, secondaryT);
                secondaryEnvelope *= smoothstep(0.99, 0.90, r); // 外周端クリップ防止
                float3 secondaryColor = EvaluatePhysicalSpectrum(secondaryT) * (secondaryEnvelope * _SecondaryBowOpacity);

                // 6. 主虹内側の光学グローハロー（ミー散乱＆過剰虹の柔らかな輝き、アレキサンダーの暗帯との対比）
                float innerDisk = smoothstep(_PrimaryRadius + halfThick * 0.2, _PrimaryRadius - halfThick * 0.9, r);
                float centerHoleFade = smoothstep(0.04, 0.28, r); // 中心部はすっきり抜けてコース視界を確保
                float innerHaloMask = innerDisk * centerHoleFade * _InnerGlowIntensity * 0.35;
                float3 innerHaloColor = float3(0.88, 0.95, 1.0) * innerHaloMask;

                // 7. 視線角度・太陽角度・方位角による有機的な光学変化
                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);

                // 視線に対するエッジ減衰（真横から見た際の板ポリ感を消去）
                float viewFacing = smoothstep(0.08, 0.42, abs(dot(N, V)));

                // 水煙が濃い下半分〜側面での輝きブースト（円形を保ちつつ自然な強弱を付与）
                float azimuthBoost = 1.0 + _AzimuthFade * (0.35 * sin(angle - 1.570796) + 0.20 * sin(angle * 2.0 + time));

                // 8. 最終スペクトル合成（HDR発光 ＋ 水滴スパークル）
                float combinedBandMask = saturate(primaryEnvelope + secondaryEnvelope * _SecondaryBowOpacity);
                float3 sparkleRGB = (primaryColor + float3(0.9, 0.96, 1.0) * 0.5) * (dropletSparkle * combinedBandMask);

                float3 totalSpectrum = (primaryColor + secondaryColor + innerHaloColor + sparkleRGB)
                    * _RainbowTint.rgb
                    * (_RainbowIntensity * mistModulation * azimuthBoost);

                // 外縁と中心の完全ソフトフェード
                float outerEdgeFade = smoothstep(0.99, 0.88, r);
                float alpha = saturate(combinedBandMask * 0.85 + innerHaloMask + dropletSparkle * combinedBandMask * 0.4)
                    * outerEdgeFade
                    * viewFacing
                    * softIntersection
                    * cameraNearFade
                    * _RainbowTint.a;

                return half4(totalSpectrum, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
