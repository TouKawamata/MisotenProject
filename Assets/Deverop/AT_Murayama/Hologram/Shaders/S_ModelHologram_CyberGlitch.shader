Shader "Custom/Hologram/ModelHologram_CyberGlitch"
{
    Properties
    {
        [Header(Base Texture and Colors)]
        _MainTex ("Base Texture", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 1.0
        [HDR] _MainColor ("Hologram Core Color (HDR)", Color) = (0.1, 2.5, 3.8, 0.6)
        [HDR] _GlitchGlowColor ("Glitch Spike Color (HDR)", Color) = (3.5, 0.4, 2.0, 1.0)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.5, 3.5, 4.5, 0.95)

        [Header(RGB Chromatic Aberration)]
        _ChromaticSplit ("RGB Channel Separation Distance", Range(0.0, 0.1)) = 0.025
        _SyncRollSpeed ("V-Sync Rolling Error Bar Speed", Float) = 0.8
        _SyncRollIntensity ("V-Sync Rolling Error Intensity", Range(0.0, 1.0)) = 0.45

        [Header(Vertex Block Glitch)]
        _VertexGlitchIntensity ("Vertex Block Tearing Distance (m)", Range(0.0, 0.6)) = 0.12
        _VertexGlitchRate ("Vertex Glitch Frequency", Range(0.0, 1.0)) = 0.35
        _SliceDensity ("Glitch Horizontal Slice Density", Float) = 12.0

        [Header(Noise and Flicker)]
        _StaticNoiseIntensity ("Digital Static Noise", Range(0.0, 1.0)) = 0.3
        _FlickerDropRate ("Signal Drop/Flicker Rate", Range(0.0, 0.6)) = 0.15

        [Header(Fresnel and Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.5
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.18

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
            Name "ModelHologramGlitchPass"
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
                float  glitchTrigger : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainColor;
                float4 _GlitchGlowColor;
                float4 _RimColor;
                float _UseTexColor;
                float _ChromaticSplit;
                float _SyncRollSpeed;
                float _SyncRollIntensity;
                float _VertexGlitchIntensity;
                float _VertexGlitchRate;
                float _SliceDensity;
                float _StaticNoiseIntensity;
                float _FlickerDropRate;
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
                float glitchActive = 0.0;

                if (_VertexGlitchIntensity > 0.001)
                {
                    float timeStep = floor(_Time.y * 22.0);
                    float slice = floor(posOS.y * _SliceDensity + timeStep * 0.2);
                    float trigger = Hash11(timeStep * 3.17);

                    if (trigger < _VertexGlitchRate)
                    {
                        float sliceShift = Hash11(slice * 7.13 + timeStep) * 2.0 - 1.0;
                        float mask = step(0.55, abs(sliceShift));
                        posOS.x += sliceShift * mask * _VertexGlitchIntensity;
                        posOS.z += Hash11(slice * 11.3) * mask * (_VertexGlitchIntensity * 0.5);
                        glitchActive = mask;
                    }
                }

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.positionOS = posOS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.glitchTrigger = glitchActive;
                return output;
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
                float splitAmount = _ChromaticSplit * (1.0 + input.glitchTrigger * 2.5);
                float2 uvR = input.uv + float2(splitAmount, 0.0);
                float2 uvG = input.uv;
                float2 uvB = input.uv - float2(splitAmount, 0.0);

                half rChan = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).r;
                half4 gSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvG);
                half bChan = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).b;
                half3 chromaTex = half3(rChan, gSample.g, bChan);
                half texLum = dot(chromaTex, half3(0.299, 0.587, 0.114));
                half3 baseTexFactor = lerp(half3(texLum, texLum, texLum), chromaTex, _UseTexColor);

                // 3. V-Sync ローリングエコー帯（下から上へ流れる同期乱れバー）
                float syncPhase = frac(input.positionWS.y * 0.4 - _Time.y * _SyncRollSpeed);
                float syncBar = smoothstep(0.88, 0.98, syncPhase) * (1.0 - smoothstep(0.98, 1.0, syncPhase));
                syncBar *= _SyncRollIntensity;

                // 4. デジタルスタティックノイズ ＆ 信号ドロップアウト明滅
                float timeStep = floor(_Time.y * 24.0);
                float staticNoise = Hash11(floor(input.positionWS.y * 50.0) + timeStep * 3.31);
                float staticFactor = lerp(1.0, staticNoise * 1.5, _StaticNoiseIntensity);

                float dropTrigger = Hash11(timeStep * 1.83);
                float signalAlive = step(_FlickerDropRate, dropTrigger);

                // 5. グリッチスパイク時のカラー遷移
                half3 baseHolo = lerp(_MainColor.rgb * baseTexFactor, _RimColor.rgb, fresnel);
                half3 glitchGlow = _GlitchGlowColor.rgb * (input.glitchTrigger + syncBar * 1.5);
                half3 finalRGB = (baseHolo + glitchGlow) * staticFactor * signalAlive;

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a;
                half finalAlpha = saturate((baseAlpha + input.glitchTrigger * 0.4 + syncBar * 0.3) * gSample.a * signalAlive);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

