Shader "Custom/Hologram/ModelHologram"
{
    Properties
    {
        [Header(Base Texture and Color)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB Ratio", Float) = 0.0
        [HDR] _MainColor ("Hologram Base Color (HDR)", Color) = (0.0, 1.8, 2.5, 0.45)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.4, 3.5, 4.0, 0.95)

        [Header(Fresnel Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.5
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.2

        [Header(Scanlines)]
        [Toggle] _UseWorldSpaceScan ("Use World Space Scanlines (OFF = Local Space)", Float) = 1.0
        _ScanlineDensity ("Fine Scanline Density", Float) = 28.0
        _ScanlineSpeed ("Fine Scanline Scroll Speed", Float) = 1.5
        _ScanlineContrast ("Fine Scanline Contrast", Range(0.0, 1.0)) = 0.55
        _SweepDensity ("Wide Laser Sweep Density", Float) = 1.2
        _SweepSpeed ("Wide Laser Sweep Speed", Float) = -0.4
        _SweepIntensity ("Wide Laser Sweep Boost", Range(0.0, 2.0)) = 0.8

        [Header(Hologram Glitch and Flicker)]
        _GlitchIntensity ("Vertex Slice Glitch Distance", Range(0.0, 0.5)) = 0.06
        _GlitchFrequency ("Glitch Trigger Rate", Range(0.0, 1.0)) = 0.15
        _FlickerIntensity ("Brightness Flicker Intensity", Range(0.0, 0.5)) = 0.1

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
            Name "ModelHologramPass"
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
                float _UseWorldSpaceScan;
                float _ScanlineDensity;
                float _ScanlineSpeed;
                float _ScanlineContrast;
                float _SweepDensity;
                float _SweepSpeed;
                float _SweepIntensity;
                float _GlitchIntensity;
                float _GlitchFrequency;
                float _FlickerIntensity;
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

                // --------------------------------------------------------
                // 水平スライス単位のランダムグリッチ（ホログラムの映像乱れ）
                // --------------------------------------------------------
                if (_GlitchIntensity > 0.0001 && _GlitchFrequency > 0.001)
                {
                    float timeStep = floor(_Time.y * 18.0);
                    float sliceY = floor(posOS.y * 8.0 + timeStep * 0.3);
                    float trigger = Hash11(timeStep * 1.37);

                    if (trigger < _GlitchFrequency)
                    {
                        float sliceRand = Hash11(sliceY + timeStep * 7.13) * 2.0 - 1.0;
                        float sliceMask = step(0.65, abs(sliceRand));
                        posOS.x += sliceRand * sliceMask * _GlitchIntensity;
                    }
                }

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.positionOS = posOS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. テクスチャサンプリング
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 baseTexFactor = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                // 2. フレネル輪郭発光（外周を強く光らせ、正面中央を透かす）
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 3. 2重スキャンライン（細かい走査線 ＋ ゆっくり流れる太いレーザー帯）
                float scanCoord = (_UseWorldSpaceScan > 0.5) ? input.positionWS.y : input.positionOS.y;
                float fineWave = sin(scanCoord * _ScanlineDensity + _Time.y * _ScanlineSpeed * 6.28318) * 0.5 + 0.5;
                float fineScan = lerp(1.0, fineWave, _ScanlineContrast);

                float sweepPhase = frac(scanCoord * _SweepDensity * 0.1 + _Time.y * _SweepSpeed);
                float sweepBar = smoothstep(0.82, 0.96, sweepPhase) * (1.0 - smoothstep(0.96, 1.0, sweepPhase));
                float totalScan = fineScan + sweepBar * _SweepIntensity;

                // 4. 明滅フリッカー
                float flicker = 1.0 - (Hash11(floor(_Time.y * 24.0)) * _FlickerIntensity);

                // 5. カラーとアルファの合成
                half3 holoColor = lerp(_MainColor.rgb * baseTexFactor, _RimColor.rgb, fresnel);
                holoColor *= totalScan * flicker;

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a * texCol.a;
                half finalAlpha = saturate(baseAlpha * totalScan * flicker);

                return half4(holoColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}

