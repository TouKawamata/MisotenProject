Shader "Custom/SkyAquaCascade/S_AquaMistParticle"
{
    Properties
    {
        [Header(Mist and Droplet Particle Settings)]
        _FluidNoiseTex ("Fluid Noise Texture (RGBA)", 2D) = "white" {}
        _ParticleMode ("Particle Mode (0=Rising Mist Plume, 1=Sunlit Droplet Sparkle)", Float) = 0.0
        [HDR] _TintColor ("Particle Tint Color (HDR)", Color) = (0.94, 0.98, 1.0, 0.55)
        _SunScattering ("Sunlight Forward Scattering Boost", Range(0.0, 3.0)) = 1.25
        _NoiseScrollSpeed ("Internal Cloud Drift Speed", Range(0.0, 2.0)) = 0.35

        [Header(Soft Particle Depth Fade)]
        _SoftDepthDistance ("Soft Particle Intersection Distance (m)", Range(0.1, 10.0)) = 2.8
        _CameraFadeStart ("Camera Near Fade Start (m)", Range(0.5, 5.0)) = 1.2
        _CameraFadeEnd ("Camera Near Fade End (m)", Range(1.5, 15.0)) = 4.5

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (10=OneMinusSrcAlpha, 1=One)", Float) = 10
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
            "Queue" = "Transparent+5"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            TEXTURE2D(_FluidNoiseTex);
            SAMPLER(sampler_FluidNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FluidNoiseTex_ST;
                float4 _TintColor;
                float  _ParticleMode;
                float  _SunScattering;
                float  _NoiseScrollSpeed;
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
                output.uv         = input.uv;
                output.color      = input.color;
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float r2 = dot(centered, centered);
                if (r2 > 1.0)
                {
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                float r = sqrt(r2);
                float radialMask = smoothstep(1.0, 0.15, r);

                // ソフトパーティクル深度比較 ＆ カメラ近接フェード
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float surfaceEyeDepth = input.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float depthDiff = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                float softParticle = saturate(depthDiff / max(0.05, _SoftDepthDistance));
                float cameraNearFade = smoothstep(_CameraFadeStart, max(_CameraFadeStart + 0.3, _CameraFadeEnd), surfaceEyeDepth);

                // 太陽光・環境光による水煙散乱ライティング
                Light mainLight = GetMainLight();
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                float3 L = normalize(mainLight.direction);
                float miePhase = pow(saturate(dot(V, -L) * 0.5 + 0.5), 2.5) * _SunScattering;
                float3 envLight = mainLight.color * (0.75 + 0.45 * miePhase) + SampleSH(float3(0.0, 1.0, 0.0)) * 0.55;

                if (_ParticleMode < 0.5)
                {
                    // Mode 0: Rising Mist Plume (湧き上がる水煙クラウド)
                    float2 cloudUV1 = input.uv * 0.65 + float2(_Time.y * _NoiseScrollSpeed * 0.25, -_Time.y * _NoiseScrollSpeed * 0.4);
                    float2 cloudUV2 = input.uv * 1.15 + float2(-_Time.y * _NoiseScrollSpeed * 0.31, -_Time.y * _NoiseScrollSpeed * 0.22);

                    float4 n1 = SAMPLE_TEXTURE2D(_FluidNoiseTex, sampler_FluidNoiseTex, cloudUV1);
                    float4 n2 = SAMPLE_TEXTURE2D(_FluidNoiseTex, sampler_FluidNoiseTex, cloudUV2);

                    float cloudDensity = lerp(n1.b, n2.b, 0.5) * 0.75 + n1.g * 0.25;
                    float puffyRim = smoothstep(0.0, 0.65, radialMask * (0.45 + 0.85 * cloudDensity));

                    // 中心部がほんのり立体的に明るいソフトクラウド陰影
                    float domeShade = 0.82 + 0.28 * (1.0 - r * 0.65) + 0.15 * n1.b;
                    float3 mistRGB = _TintColor.rgb * input.color.rgb * envLight * domeShade;
                    float mistAlpha = puffyRim * radialMask * _TintColor.a * input.color.a * softParticle * cameraNearFade;

                    return half4(mistRGB, saturate(mistAlpha));
                }
                else
                {
                    // Mode 1: Sunlit Droplet Sparkles (太陽光で煌めく3D飛沫粒)
                    float coreGlint = exp(-r2 * 14.0) * 2.2;
                    float haloGlow  = exp(-r2 * 3.5) * 0.65;
                    float crossStar = exp(-abs(centered.x) * 16.0) * exp(-abs(centered.y) * 4.5)
                                    + exp(-abs(centered.y) * 16.0) * exp(-abs(centered.x) * 4.5);

                    float sparkleShape = saturate(coreGlint + haloGlow + crossStar * 0.35) * radialMask;
                    float3 dropletRGB = _TintColor.rgb * input.color.rgb * (mainLight.color * 1.35 + float3(0.85, 0.96, 1.0) * 0.65) * (1.0 + coreGlint);
                    float dropletAlpha = sparkleShape * _TintColor.a * input.color.a * softParticle * cameraNearFade;

                    return half4(dropletRGB, saturate(dropletAlpha));
                }
            }
            ENDHLSL
        }
    }
}
