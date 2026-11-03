Shader "Custom/Hologram/ModelHologram_GhostRim"
{
    // 最軽量・汎用ベース：フレネル輪郭 ＋ ゆっくり呼吸する明滅 ＋ 高さフェード（任意）
    Properties
    {
        [Header(Base Texture and Colors)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 0.0
        [HDR] _MainColor ("Body Color (HDR)", Color) = (0.0, 1.6, 2.6, 0.30)
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (0.4, 3.5, 4.5, 0.95)

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.5
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.12

        [Header(Breathing Pulse)]
        _BreatheSpeed ("Breathe Speed", Float) = 1.6
        _BreatheAmount ("Breathe Amount (0 = off)", Range(0.0, 0.8)) = 0.25

        [Header(Height Fade)]
        [Toggle] _UseHeightFade ("Fade Out Toward Bottom", Float) = 0.0
        _GradientMinY ("Fade Bottom Y (Object Space)", Float) = -1.0
        _GradientMaxY ("Fade Top Y (Object Space)", Float) = 1.0

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
            Name "ModelHologramGhostRimPass"
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
                float4 _RimColor;
                float _UseTexColor;
                float _FresnelPower;
                float _CenterAlpha;
                float _BreatheSpeed;
                float _BreatheAmount;
                float _UseHeightFade;
                float _GradientMinY;
                float _GradientMaxY;
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
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                float breathe = 1.0 + sin(_Time.y * _BreatheSpeed * 6.28318) * _BreatheAmount;

                float heightFade = 1.0;
                if (_UseHeightFade > 0.5)
                {
                    float range = max(0.001, _GradientMaxY - _GradientMinY);
                    heightFade = saturate((input.positionOS.y - _GradientMinY) / range);
                }

                half3 rgb = lerp(_MainColor.rgb * texTint, _RimColor.rgb, fresnel) * breathe;
                half alpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a * texCol.a * breathe * heightFade;

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}

