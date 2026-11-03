Shader "Custom/Hologram/ModelHologram_WireframeGrid"
{
    Properties
    {
        [Header(Base Texture and Colors)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 0.0
        [HDR] _MainColor ("Hologram Base Color (HDR)", Color) = (0.0, 1.2, 2.2, 0.25)
        [HDR] _GridColor ("Wireframe Grid Color (HDR)", Color) = (0.2, 3.5, 4.5, 0.95)
        [HDR] _ScanPlaneColor ("Laser Scan Slice Color (HDR)", Color) = (2.5, 4.2, 4.8, 1.0)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.4, 2.8, 4.0, 0.9)

        [Header(3D Elevation Contour Lines)]
        [Toggle] _UseWorldSpace ("World Space Elevation (OFF = Local Space)", Float) = 1.0
        _ContourInterval ("Horizontal Contour Interval (m)", Float) = 0.5
        _ContourLineWidth ("Horizontal Line Thickness", Range(0.01, 0.2)) = 0.04
        _VerticalGridDensity ("Vertical Grid Density", Float) = 18.0
        _VerticalGridIntensity ("Vertical Grid Intensity", Range(0.0, 1.0)) = 0.65

        [Header(Scanning Laser Slice Plane)]
        _ScanSliceSpeed ("Laser Slice Sweep Speed", Float) = 1.2
        _ScanSliceRange ("Laser Slice Height Range (m)", Float) = 3.0
        _ScanSliceThickness ("Laser Slice Thickness (m)", Range(0.02, 0.5)) = 0.12

        [Header(Fresnel and Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.4
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.12

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One/Additive, 10=OneMinusSrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (2=Back, 0=Off/Both)", Float) = 0
        [Toggle] _ZWrite ("ZWrite (ON for solid silhouette)", Float) = 0
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
            Name "ModelHologramWireframePass"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
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
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainColor;
                float4 _GridColor;
                float4 _ScanPlaneColor;
                float4 _RimColor;
                float _UseTexColor;
                float _UseWorldSpace;
                float _ContourInterval;
                float _ContourLineWidth;
                float _VerticalGridDensity;
                float _VerticalGridIntensity;
                float _ScanSliceSpeed;
                float _ScanSliceRange;
                float _ScanSliceThickness;
                float _FresnelPower;
                float _CenterAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 baseTexFactor = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                // 1. フレネル輪郭
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 2. 3D等高線（水平スライス輪郭線）
                float heightCoord = (_UseWorldSpace > 0.5) ? input.positionWS.y : input.positionOS.y;
                float interval = max(0.05, _ContourInterval);
                float phaseH = frac(heightCoord / interval);
                float distToHLine = min(phaseH, 1.0 - phaseH) * interval;
                float aaH = clamp(fwidth(heightCoord) * 1.5, 0.005, 0.05);
                float hLineMask = 1.0 - smoothstep(0.0, max(aaH, _ContourLineWidth), distToHLine);

                // 3. 垂直グリッドライン（経度・ラジアル方向）
                float2 planarCoord = (_UseWorldSpace > 0.5) ? input.positionWS.xz : input.positionOS.xz;
                float angle = atan2(planarCoord.y, planarCoord.x) / 6.28318 + 0.5;
                float vPhase = frac(angle * _VerticalGridDensity);
                float distToVLine = min(vPhase, 1.0 - vPhase);
                float aaV = clamp(fwidth(angle * _VerticalGridDensity) * 1.5, 0.02, 0.2);
                float vLineMask = (1.0 - smoothstep(0.0, aaV, distToVLine)) * _VerticalGridIntensity;

                float wireGrid = saturate(hLineMask + vLineMask);

                // 4. 上下に往復スイープするレーザー切断面（スキャンプレーン）
                float sweepPhase = sin(_Time.y * _ScanSliceSpeed) * 0.5 + 0.5; // 0〜1
                float currentScanY = (sweepPhase * 2.0 - 1.0) * (_ScanSliceRange * 0.5);
                float distToScanPlane = abs(heightCoord - currentScanY);
                float scanPlaneMask = 1.0 - smoothstep(0.0, max(0.01, _ScanSliceThickness), distToScanPlane);
                scanPlaneMask = pow(saturate(scanPlaneMask), 2.0);

                // 5. 合成
                half3 wireCol = _GridColor.rgb * wireGrid * 1.4;
                half3 scanCol = _ScanPlaneColor.rgb * scanPlaneMask * 2.2;
                half3 baseHolo = lerp(_MainColor.rgb * baseTexFactor, _RimColor.rgb, fresnel);

                half3 finalRGB = baseHolo + wireCol + scanCol;

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a;
                half gridAlpha = wireGrid * _GridColor.a + scanPlaneMask * _ScanPlaneColor.a;
                half finalAlpha = saturate((baseAlpha + gridAlpha) * texCol.a);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

