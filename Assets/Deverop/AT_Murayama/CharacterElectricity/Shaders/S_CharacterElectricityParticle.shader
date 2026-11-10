Shader "Custom/CharacterElectricity/S_CharacterElectricityParticle"
{
    Properties
    {
        [Header(Particle Texture and Color)]
        _MainTex ("Particle Texture (RGBA)", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color (HDR)", Color) = (0.25, 0.85, 2.5, 1.0)
        _EmissionBoost ("HDR Bloom Boost", Range(0.5, 8.0)) = 2.5
        _CoreWhiteness ("Blinding Core Whiteness", Range(0.0, 1.0)) = 0.6

        [Header(Soft Particle and Camera Fade)]
        _SoftDepthDistance ("Soft Particle Distance (m)", Range(0.01, 2.0)) = 0.25
        _CameraFadeStart ("Camera Near Fade Start (m)", Range(0.05, 2.0)) = 0.3
        _CameraFadeEnd ("Camera Near Fade End (m)", Range(0.2, 5.0)) = 1.0

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
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 color      : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TintColor;
                float  _EmissionBoost;
                float  _CoreWhiteness;
                float  _SoftDepthDistance;
                float  _CameraFadeStart;
                float  _CameraFadeEnd;
                float  _SrcBlend;
                float  _DstBlend;
                float  _Cull;
                float  _ZWrite;
                float  _ZTest;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.color      = input.color;
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // パーティクル頂点カラーとティントカラーの合成
                half4 baseColor = input.color * _TintColor;

                // ソフトパーティクル深度計算 (キャラクター装甲や床とのポリゴンめり込み境界を滑らかにフェード)
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float surfaceEyeDepth = input.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float depthDiff = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                float softParticle = saturate(depthDiff / max(0.01, _SoftDepthDistance));

                // レースカメラ近接フェード
                float cameraNearFade = smoothstep(_CameraFadeStart, max(_CameraFadeStart + 0.15, _CameraFadeEnd), surfaceEyeDepth);

                float alphaFade = softParticle * cameraNearFade;

                // コア白熱化（白熱する電撃芯の再現）
                half luminance = dot(texColor.rgb, half3(0.299, 0.587, 0.114));
                half coreWhite = saturate((luminance - 0.5) * 2.0) * _CoreWhiteness;
                half3 emissionRgb = lerp(texColor.rgb * baseColor.rgb, half3(1.5, 1.5, 1.5), coreWhite);

                // HDR Bloom 発光ブースト
                emissionRgb *= _EmissionBoost;

                // 加算（Blend One One: _SrcBlend=1）および半透明加算（Blend SrcAlpha One: _SrcBlend=5）両対応のフェード制御
                half isOneBlend = (_SrcBlend < 1.5) ? 1.0 : 0.0;
                emissionRgb *= lerp(1.0, alphaFade * texColor.a * baseColor.a, isOneBlend);

                half finalAlpha = texColor.a * baseColor.a * alphaFade;

                return half4(emissionRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
