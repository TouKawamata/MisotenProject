Shader "Custom/Hologram/ModelHologram_GradientEnergy"
{
    // 高さグラデーション（下色→上色）＋ 上へ流れるエネルギーノイズ ＋ フレネル。
    // 色味で見せたい汎用ベース（ゴール・チェックポイント・アイテム・回復エリア等）。
    Properties
    {
        [Header(Height Gradient Colors)]
        [HDR] _BottomColor ("Bottom Color (HDR)", Color) = (0.0, 0.9, 3.0, 0.35)
        [HDR] _TopColor ("Top Color (HDR)", Color) = (2.5, 0.6, 3.5, 0.35)
        [Toggle] _UseWorldSpace ("Gradient/Flow In World Space (OFF = Object Space)", Float) = 0.0
        _GradientMinY ("Gradient Bottom Y", Float) = -1.0
        _GradientMaxY ("Gradient Top Y", Float) = 1.0

        [Header(Flowing Energy Noise)]
        [HDR] _FlowColor ("Energy Streak Color (HDR)", Color) = (3.0, 4.0, 4.5, 1.0)
        _FlowScale ("Noise Scale", Float) = 3.0
        _FlowSpeed ("Flow Speed (Upward)", Float) = 0.8
        _FlowContrast ("Streak Contrast", Range(1.0, 8.0)) = 3.0
        _FlowIntensity ("Streak Intensity", Range(0.0, 2.0)) = 0.9

        [Header(Fresnel)]
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (0.6, 3.5, 4.5, 0.95)
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.2
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.25

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One/Additive, 10=OneMinusSrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (2=Back, 0=Off/Both)", Float) = 2
        [Toggle] _ZWrite ("ZWrite", Float) = 0
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
            Name "ModelHologramGradientEnergyPass"
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float4 _FlowColor;
                float4 _RimColor;
                float _UseWorldSpace;
                float _GradientMinY;
                float _GradientMaxY;
                float _FlowScale;
                float _FlowSpeed;
                float _FlowContrast;
                float _FlowIntensity;
                float _FresnelPower;
                float _CenterAlpha;
            CBUFFER_END

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 p = (_UseWorldSpace > 0.5) ? input.positionWS : input.positionOS;

                // 1. 高さグラデーション
                float range = max(0.001, _GradientMaxY - _GradientMinY);
                float t = saturate((p.y - _GradientMinY) / range);
                half4 gradCol = lerp(_BottomColor, _TopColor, t);

                // 2. 上に流れるエネルギーノイズ
                float n = ValueNoise3D(p * _FlowScale + float3(0.0, -_Time.y * _FlowSpeed, 0.0));
                float streak = pow(saturate(n), _FlowContrast) * _FlowIntensity;

                // 3. フレネル
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                half3 rgb = gradCol.rgb;
                rgb += _FlowColor.rgb * streak;
                rgb = lerp(rgb, _RimColor.rgb, fresnel);

                half alpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * gradCol.a;
                alpha = saturate(alpha + streak * _FlowColor.a * 0.4);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

