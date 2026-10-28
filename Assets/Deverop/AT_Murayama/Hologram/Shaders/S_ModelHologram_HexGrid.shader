Shader "Custom/Hologram/ModelHologram_HexGrid"
{
    Properties
    {
        [Header(Base Texture and Colors)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 0.0
        [HDR] _MainColor ("Hologram Base Color (HDR)", Color) = (0.0, 1.4, 2.5, 0.35)
        [HDR] _HexWireColor ("Hex Grid Wire Color (HDR)", Color) = (0.2, 4.0, 3.8, 1.0)
        [HDR] _RimColor ("Fresnel Rim Color (HDR)", Color) = (0.5, 3.5, 4.5, 0.95)

        [Header(Hexagonal Grid Settings)]
        [Toggle] _UseWorldSpaceHex ("Use World Space Projection (OFF = UV Space)", Float) = 1.0
        _HexScale ("Hexagon Grid Scale", Float) = 12.0
        _HexWireWidth ("Hex Wire Line Width", Range(0.01, 0.25)) = 0.06
        _HexPulseSpeed ("Hex Cell Pulse Speed", Float) = 2.5
        _WavePulseSpeed ("Vertical Energy Wave Speed", Float) = 3.0
        _WavePulseWidth ("Vertical Energy Wave Width", Float) = 4.0

        [Header(Fresnel and Silhouette)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.2
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.15

        [Header(Fine Scanlines)]
        _ScanlineDensity ("Fine Scanline Density", Float) = 35.0
        _ScanlineIntensity ("Scanline Contrast", Range(0.0, 1.0)) = 0.35

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
            Name "ModelHologramHexPass"
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
                float4 _HexWireColor;
                float4 _RimColor;
                float _UseTexColor;
                float _UseWorldSpaceHex;
                float _HexScale;
                float _HexWireWidth;
                float _HexPulseSpeed;
                float _WavePulseSpeed;
                float _WavePulseWidth;
                float _FresnelPower;
                float _CenterAlpha;
                float _ScanlineDensity;
                float _ScanlineIntensity;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 高速ヘックス座標計算 (gv: セル内座標, d: 辺からの正規化距離, id: セルID)
            float4 CalcHexCoords(float2 p)
            {
                float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = fmod(p, r) - h;
                float2 b = fmod(p - h, r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                float x = abs(gv.x);
                float y = abs(gv.y);
                float d = max(x, dot(float2(x, y), normalize(float2(1.0, 1.7320508))));
                float2 id = floor((p - gv) * 10.0);
                return float4(gv.x, gv.y, d, Hash21(id));
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
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. テクスチャ
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 baseTexFactor = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                // 2. フレネル輪郭
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                float ndotv = abs(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                // 3. ヘキサゴナル（六角形）シールド網目の計算
                float2 hexCoordSource = (_UseWorldSpaceHex > 0.5)
                    ? (input.positionWS.xy + input.positionWS.zy * 0.5) * _HexScale
                    : input.uv * _HexScale;

                float4 hex = CalcHexCoords(hexCoordSource);
                float distToEdge = 0.5 - hex.z; // 0が外周線、0.5がセル中央
                float wireMask = 1.0 - smoothstep(0.0, max(0.001, _HexWireWidth), distToEdge);

                // 各セルのランダムパルス点滅
                float cellRand = hex.w;
                float cellPulse = sin(_Time.y * _HexPulseSpeed + cellRand * 6.28) * 0.5 + 0.5;

                // 上下に流れるエネルギー波動（スキャンウェーブ）
                float energyWave = sin(input.positionWS.y * (1.0 / max(0.1, _WavePulseWidth)) - _Time.y * _WavePulseSpeed) * 0.5 + 0.5;
                energyWave = pow(energyWave, 3.0);

                // 4. 細かい走査線
                float scan = sin(input.positionWS.y * _ScanlineDensity + _Time.y * 5.0) * 0.5 + 0.5;
                float scanFactor = lerp(1.0, scan, _ScanlineIntensity);

                // 5. カラー・アルファ合成
                half3 hexGlow = _HexWireColor.rgb * (wireMask * 1.5 + cellPulse * energyWave * 0.8);
                half3 baseHolo = lerp(_MainColor.rgb * baseTexFactor, _RimColor.rgb, fresnel);
                half3 finalRGB = (baseHolo + hexGlow) * scanFactor;

                half baseAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a;
                half hexAlpha = wireMask * _HexWireColor.a + (cellPulse * energyWave * 0.4);
                half finalAlpha = saturate((baseAlpha + hexAlpha) * scanFactor * texCol.a);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

