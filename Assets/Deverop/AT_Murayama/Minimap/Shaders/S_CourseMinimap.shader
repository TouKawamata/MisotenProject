Shader "Custom/CourseMinimap"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _SameHeightColor ("Same Height Color (Current Level)", Color) = (0.0, 2.2, 2.8, 0.85)
        [HDR] _HighColor ("Higher Path Color (Up)", Color) = (0.2, 2.8, 0.6, 0.85)
        [HDR] _LowColor ("Lower Path Color (Down)", Color) = (0.1, 0.4, 1.8, 0.55)
        [HDR] _EdgeColor ("Road Edge / Wire Color", Color) = (0.5, 3.5, 4.0, 1.0)
        [HDR] _PlayerSpotColor ("Player Spot Glow Color", Color) = (2.0, 3.5, 4.0, 1.0)

        [Header(Road and Cylinder Shape)]
        [Toggle] _IsCylinder ("Is Cylinder Tube Mode", Float) = 0.0
        _EdgeWidth ("Ribbon Edge Width", Range(0.01, 0.3)) = 0.08
        _EdgeSharpness ("Edge Sharpness", Range(1.0, 8.0)) = 3.0
        _CenterFillAlpha ("Base Fill Alpha", Range(0.0, 1.0)) = 0.25
        _CylinderWireCount ("Cylinder Longitudinal Wire Count", Float) = 8.0
        _CylinderWireIntensity ("Cylinder Wire & Ring Intensity", Range(0.0, 1.0)) = 0.65
        _GridInterval ("Distance Ring / Grid Interval (m)", Float) = 25.0
        _GridIntensity ("Ribbon Grid Intensity", Range(0.0, 1.0)) = 0.35

        [Header(3D Height and Clipping in Course Meters)]
        _VisibleRadius ("Visible Sphere Radius (m)", Float) = 250.0
        _FadeSoftness ("Sphere Edge Fade Ratio", Range(0.05, 0.8)) = 0.3
        _HeightColorRange ("Height Diff Range for Color (m)", Float) = 35.0
        _PlayerSpotRadius ("Player Spot Glow Radius (m)", Float) = 18.0

        [Header(Icon Mode)]
        [Toggle] _IsIcon ("Is Player/Racer Icon", Float) = 0.0
        [HDR] _IconColor ("Icon Color", Color) = (3.5, 2.8, 0.2, 1.0)
        [HideInInspector] _PlayerLocalPos ("Player Local Pos", Vector) = (0, 0, 0, 1)

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (4=LEqual, 8=Always)", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CourseMinimapPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

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
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 normalOS   : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float3 normalWS   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SameHeightColor;
                float4 _HighColor;
                float4 _LowColor;
                float4 _EdgeColor;
                float4 _PlayerSpotColor;
                float _IsCylinder;
                float _EdgeWidth;
                float _EdgeSharpness;
                float _CenterFillAlpha;
                float _CylinderWireCount;
                float _CylinderWireIntensity;
                float _GridInterval;
                float _GridIntensity;
                float _VisibleRadius;
                float _FadeSoftness;
                float _HeightColorRange;
                float _PlayerSpotRadius;
                float _IsIcon;
                float4 _IconColor;
                float4 _PlayerLocalPos; // xyz: 自機のコース座標（メートル単位）, w: 有効フラグ
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionOS = input.positionOS.xyz;
                output.uv         = input.uv;
                output.normalOS   = input.normalOS;
                output.positionWS = posWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // --------------------------------------------------------
                // アイコン（自機・他機マーカー）描画モード
                // --------------------------------------------------------
                if (_IsIcon > 0.5)
                {
                    float3 n = normalize(input.normalOS);
                    float shading = saturate(n.y * 0.4 + 0.6);
                    return half4(_IconColor.rgb * shading, saturate(_IconColor.a));
                }

                // --------------------------------------------------------
                // 1. 球体クリッピング（自機からのコースローカル距離による切り抜き）
                // ※Object Space で計算するため、方式A（原寸）でも方式B（1/1000縮小）でも同じメートル単位で機能する
                // --------------------------------------------------------
                float3 playerPos = _PlayerLocalPos.xyz;
                float distFromPlayer = distance(input.positionOS, playerPos);

                float safeRadius = max(1.0, _VisibleRadius);
                float fadeStart = safeRadius * (1.0 - saturate(_FadeSoftness));
                float sphereMask = 1.0 - smoothstep(fadeStart, safeRadius, distFromPlayer);

                if (sphereMask <= 0.001)
                {
                    discard;
                }

                // --------------------------------------------------------
                // 2. 高低差（立体交差）による色分けと階層透過
                // --------------------------------------------------------
                float heightDiff = input.positionOS.y - playerPos.y;
                float safeHRange = max(1.0, _HeightColorRange);
                float hNorm = clamp(heightDiff / safeHRange, -1.0, 1.0);

                float4 heightColor = (hNorm >= 0.0)
                    ? lerp(_SameHeightColor, _HighColor, hNorm)
                    : lerp(_SameHeightColor, _LowColor, -hNorm);

                float levelAlphaMul = lerp(1.0, 0.45, abs(hNorm));

                // --------------------------------------------------------
                // 3. 形状別シェーディング（平らな帯リボン vs 円柱トンネルチューブ）
                // --------------------------------------------------------
                float edgeMask = 0.0;
                float gridMask = 0.0;
                float baseFillAlpha = _CenterFillAlpha;

                // 進行方向距離に応じた横リングライン（帯・円柱共通）
                float transverseLine = 0.0;
                if (_GridInterval > 0.5)
                {
                    float phaseU = frac(input.uv.x / _GridInterval);
                    float distToRing = min(phaseU, 1.0 - phaseU) * _GridInterval;
                    float aaU = clamp(fwidth(input.uv.x) * 1.5, 0.18, 2.5);
                    transverseLine = 1.0 - smoothstep(0.0, aaU, distToRing);
                }

                if (_IsCylinder > 0.5)
                {
                    // === 【円柱（トンネルチューブ）モード】 ===
                    // (A) 視線に対するフレネル輪郭強調（チューブの外周シルエットを立体的に光らせる）
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float3 nWS = normalize(input.normalWS);
                    float ndotv = abs(dot(nWS, viewDirWS));
                    float fresnel = pow(1.0 - saturate(ndotv), 2.2);

                    // (B) 円周方向（UV.y = 0〜1）の縦ワイヤーライン（ギズモのトンネル線風）
                    float longiLine = 0.0;
                    if (_CylinderWireCount > 0.5)
                    {
                        float vScaled = input.uv.y * _CylinderWireCount;
                        float phaseV = frac(vScaled);
                        float distV = min(phaseV, 1.0 - phaseV);
                        float aaV = clamp(fwidth(vScaled) * 1.5, 0.03, 0.25);
                        longiLine = 1.0 - smoothstep(0.0, aaV, distV);
                    }

                    float wireCombined = max(transverseLine, longiLine * 0.75) * _CylinderWireIntensity;

                    // 円柱の中央部は透けさせて中の自機アイコンを見やすくし、輪郭とワイヤーをくっきり光らせる
                    edgeMask = saturate(fresnel * 0.85 + wireCombined * 0.9);
                    gridMask = wireCombined;
                    baseFillAlpha = _CenterFillAlpha * 0.55;
                }
                else
                {
                    // === 【帯（フラットリボン）モード】 ===
                    float distToEdge = min(input.uv.y, 1.0 - input.uv.y);
                    float edgeRaw = 1.0 - smoothstep(0.0, max(0.001, _EdgeWidth), distToEdge);
                    edgeMask = pow(saturate(edgeRaw), _EdgeSharpness);

                    if (_GridInterval > 0.5 && _GridIntensity > 0.001)
                    {
                        float phaseU = frac(input.uv.x / _GridInterval);
                        float centerDist = abs(input.uv.y - 0.5);
                        float centerLine = (1.0 - smoothstep(0.0, 0.025, centerDist)) * step(0.4, phaseU);
                        gridMask = saturate(transverseLine + centerLine * 0.6) * _GridIntensity;
                    }
                }

                // --------------------------------------------------------
                // 4. 自機周辺のスポット発光
                // --------------------------------------------------------
                float spotMask = 0.0;
                if (_PlayerSpotRadius > 0.1)
                {
                    spotMask = 1.0 - smoothstep(0.0, _PlayerSpotRadius, distFromPlayer);
                    spotMask = spotMask * spotMask;
                }

                // --------------------------------------------------------
                // 最終カラーとアルファの合成
                // --------------------------------------------------------
                float3 roadColor = heightColor.rgb * (1.0 + gridMask * 1.1);
                roadColor = lerp(roadColor, _EdgeColor.rgb, edgeMask);
                roadColor += _PlayerSpotColor.rgb * spotMask * 0.65;

                float baseAlpha = lerp(baseFillAlpha, _EdgeColor.a, edgeMask);
                baseAlpha = max(baseAlpha, gridMask * 0.75);
                baseAlpha = max(baseAlpha, spotMask * 0.85);

                float finalAlpha = saturate(baseAlpha * heightColor.a * levelAlphaMul * sphereMask);

                return half4(roadColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
