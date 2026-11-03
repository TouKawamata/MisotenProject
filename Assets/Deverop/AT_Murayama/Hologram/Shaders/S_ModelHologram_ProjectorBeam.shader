Shader "Custom/Hologram/ModelHologram_ProjectorBeam"
{
    Properties
    {
        [Header(Base Texture and Builtin Pattern)]
        _MainTex ("Base Texture", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 1.0
        [KeywordEnum(TextureOnly, CheckerFlag, CyberArrows, RacingStripes)] _PatternMode ("Built-in Procedural Pattern", Float) = 0
        _PatternScale ("Pattern Repeat Scale (X, Y)", Vector) = (6, 3, 0, 0)
        _PatternScrollSpeed ("Pattern Scroll Speed", Float) = 1.2

        [Header(Projector Colors and Emitter Base)]
        [HDR] _MainColor ("Hologram Core Color (HDR)", Color) = (0.1, 2.4, 3.5, 0.75)
        [HDR] _EmitterColor ("Pedestal Emitter Glow Color (HDR)", Color) = (1.5, 4.0, 4.5, 1.0)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.5, 3.5, 4.5, 0.95)
        _EmitterGlowHeight ("Base Emitter Glow Height (Object Space Y)", Range(0.02, 1.0)) = 0.35
        _EmitterBaseY ("Emitter Origin Bottom Y", Float) = -1.0

        [Header(Projector Beams and Top Dissolve)]
        _BeamDensity ("Radial Projector Beam Density", Float) = 28.0
        _BeamSpeed ("Beam Noise Flicker Speed", Float) = 4.0
        _BeamIntensity ("Projector Beam Contrast", Range(0.0, 1.0)) = 0.55
        _TopDissolveStartY ("Top Dissolve Start Height (Object Space Y)", Float) = 0.6
        _TopDissolveHeight ("Top Dissolve Softness (m)", Float) = 0.5

        [Header(RGB Chromatic Aberration Glitch)]
        _ChromaticSplit ("RGB Channel Separation Distance", Range(0.0, 0.08)) = 0.015
        _GlitchStrength ("Block Glitch Strength", Range(0.0, 0.3)) = 0.05
        _GlitchRate ("Block Glitch Trigger Rate", Range(0.0, 1.0)) = 0.25

        [Header(Fresnel Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.2
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.15

        [Header(Render States)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend (5=SrcAlpha, 1=One)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (1=One/Additive, 10=OneMinusSrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (2=Back, 0=Off/Both)", Float) = 2
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
            Name "ModelHologramProjectorBeamPass"
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
                float4 _PatternScale;
                float4 _MainColor;
                float4 _EmitterColor;
                float4 _RimColor;
                float _UseTexColor;
                float _PatternMode;
                float _PatternScrollSpeed;
                float _EmitterGlowHeight;
                float _EmitterBaseY;
                float _BeamDensity;
                float _BeamSpeed;
                float _BeamIntensity;
                float _TopDissolveStartY;
                float _TopDissolveHeight;
                float _ChromaticSplit;
                float _GlitchStrength;
                float _GlitchRate;
                float _FresnelPower;
                float _CenterAlpha;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;

                // 水平スライスの微細グリッチ
                if (_GlitchStrength > 0.001)
                {
                    float timeStep = floor(_Time.y * 18.0);
                    float slice = floor(posOS.y * 10.0 + timeStep * 0.3);
                    float trigger = step(1.0 - _GlitchRate, Hash11(timeStep * 1.71));
                    float sliceShift = (Hash11(slice * 3.91 + timeStep) * 2.0 - 1.0) * trigger * _GlitchStrength;
                    posOS.x += sliceShift;
                }

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.positionOS = posOS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float EvaluateProceduralPattern(float2 uv)
            {
                if (_PatternMode < 0.5) return 1.0;

                float2 p = uv * _PatternScale.xy;
                if (_PatternMode < 1.5)
                {
                    float2 cell = floor(p);
                    return fmod(cell.x + cell.y, 2.0);
                }
                else if (_PatternMode < 2.5)
                {
                    float centeredY = abs(frac(uv.y) - 0.5) * 2.0;
                    float arrowPhase = frac(p.x - _Time.y * _PatternScrollSpeed - centeredY * 0.85);
                    return smoothstep(0.35, 0.42, arrowPhase) * (1.0 - smoothstep(0.75, 0.82, arrowPhase));
                }
                else
                {
                    float stripePhase = frac(p.x + p.y * 0.6 - _Time.y * _PatternScrollSpeed * 0.5);
                    return step(0.4, stripePhase);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. フレネル輪郭
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 2. RGB色収差（Chromatic Aberration）サンプリング
                float2 uvR = input.uv + float2(_ChromaticSplit, 0.0);
                float2 uvG = input.uv;
                float2 uvB = input.uv - float2(_ChromaticSplit, 0.0);

                half rChan = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                half4 gSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG);
                half bChan = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                half3 chromaTex = half3(rChan, gSample.g, bChan);
                half texLum = dot(chromaTex, half3(0.299, 0.587, 0.114));
                half3 baseTexFactor = lerp(half3(texLum, texLum, texLum), chromaTex, _UseTexColor);

                float patMask = EvaluateProceduralPattern(input.uv);
                half3 coreColor = _MainColor.rgb * baseTexFactor * lerp(0.3, 1.0, patMask);

                // 3. 垂直照射ビームライン（円周・ラジアル方向の光条）
                float angle = atan2(input.positionOS.z, input.positionOS.x) / 6.28318 + 0.5;
                float beamLineId = floor(angle * _BeamDensity);
                float beamNoise = Hash11(beamLineId + floor(_Time.y * _BeamSpeed));
                float beamFactor = lerp(1.0, beamNoise * 1.5, _BeamIntensity);

                // 4. 台座・底面エミッター発光（下部がプロジェクター光源として強烈に発光）
                float distFromBase = max(0.0, input.positionOS.y - _EmitterBaseY);
                float emitterGlow = 1.0 - smoothstep(0.0, max(0.01, _EmitterGlowHeight), distFromBase);

                // 5. 天面ディゾルブ（上部に向かって光が空気中に拡散・消滅）
                float topDissolve = 1.0 - smoothstep(_TopDissolveStartY, _TopDissolveStartY + max(0.05, _TopDissolveHeight), input.positionOS.y);

                // 6. カラー・アルファ合成
                half3 holoRGB = coreColor * beamFactor + _RimColor.rgb * fresnel * 0.7;
                holoRGB += _EmitterColor.rgb * emitterGlow * 1.4;

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a * gSample.a;
                half finalAlpha = saturate((baseAlpha * beamFactor * topDissolve) + emitterGlow);

                return half4(holoRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

