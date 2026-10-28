Shader "Custom/Hologram/ModelHologram_TriplanarDots"
{
    // UV不要：3軸投影（トリプラナー）でドット網を貼るデータ点群風ホログラム。
    // UVが無い／崩れているモデル（プリミティブ・スキャンデータ・複雑なモデル）でも破綻しない。
    Properties
    {
        [Header(Colors)]
        [HDR] _MainColor ("Body Color (HDR)", Color) = (0.0, 1.2, 2.2, 0.20)
        [HDR] _DotColor ("Dot Color (HDR)", Color) = (0.3, 3.8, 4.2, 1.0)
        [HDR] _WaveColor ("Wave Highlight Color (HDR)", Color) = (2.5, 4.5, 3.0, 1.0)
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (0.4, 3.5, 4.5, 0.95)

        [Header(Triplanar Dot Grid)]
        [Toggle] _UseWorldSpace ("Project In World Space (OFF = Object Space)", Float) = 0.0
        _DotScale ("Dot Grid Density (cells per m)", Float) = 8.0
        _DotRadius ("Dot Radius", Range(0.05, 0.5)) = 0.22
        _DotSoftness ("Dot Edge Softness", Range(0.01, 0.3)) = 0.08
        _BlendSharpness ("Axis Blend Sharpness", Range(1.0, 16.0)) = 6.0

        [Header(Vertical Wave Pulse)]
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _WaveLength ("Wave Length (m)", Float) = 2.0
        _WaveDotBoost ("Dot Size Boost In Wave", Range(0.0, 1.0)) = 0.6

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.4
        _CenterAlpha ("Body Fill Alpha", Range(0.0, 1.0)) = 0.10

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
            Name "ModelHologramTriplanarDotsPass"
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
                float3 normalOS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _DotColor;
                float4 _WaveColor;
                float4 _RimColor;
                float _UseWorldSpace;
                float _DotScale;
                float _DotRadius;
                float _DotSoftness;
                float _BlendSharpness;
                float _WaveSpeed;
                float _WaveLength;
                float _WaveDotBoost;
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
                output.normalOS   = input.normalOS;
                return output;
            }

            // 1平面ぶんのドットマスク（半径 radius）
            float DotMask(float2 uv, float radius)
            {
                float2 cell = frac(uv) - 0.5;
                float d = length(cell);
                return 1.0 - smoothstep(radius - _DotSoftness, radius + _DotSoftness, d);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                bool useWS = (_UseWorldSpace > 0.5);
                float3 p = useWS ? input.positionWS : input.positionOS;
                float3 n = useWS ? normalize(input.normalWS) : normalize(input.normalOS);

                // 上下に流れる波（ドットが膨らむ）
                float wave = sin(p.y / max(0.1, _WaveLength) * 6.28318 - _Time.y * _WaveSpeed * 6.28318) * 0.5 + 0.5;
                wave = pow(wave, 4.0);
                float radius = _DotRadius * (1.0 + wave * _WaveDotBoost);

                // トリプラナー重み
                float3 w = pow(abs(n), _BlendSharpness);
                w /= max(0.0001, w.x + w.y + w.z);

                float2 uvX = p.zy * _DotScale; // X軸に垂直な面
                float2 uvY = p.xz * _DotScale; // Y軸に垂直な面
                float2 uvZ = p.xy * _DotScale; // Z軸に垂直な面

                float dots = DotMask(uvX, radius) * w.x
                           + DotMask(uvY, radius) * w.y
                           + DotMask(uvZ, radius) * w.z;

                // フレネル
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                half3 dotRGB = lerp(_DotColor.rgb, _WaveColor.rgb, wave);
                half3 rgb = _MainColor.rgb;
                rgb = lerp(rgb, _RimColor.rgb, fresnel);
                rgb += dotRGB * dots * 1.4;

                half bodyAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a;
                half alpha = saturate(bodyAlpha + dots * _DotColor.a);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

