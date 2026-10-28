Shader "Custom/Hologram/ModelHologram_Dissolve"
{
    // 出現・消滅（マテリアライズ）用：ノイズ／下から／上からのディゾルブ ＋ 発光エッジ
    // _DissolveAmount: 0 = 完全に見える / 1 = 完全に消える（スクリプトやAnimatorから制御可）
    Properties
    {
        [Header(Base Texture and Colors)]
        _MainTex ("Base Texture (Optional)", 2D) = "white" {}
        [Toggle] _UseTexColor ("Keep Texture Original RGB", Float) = 0.0
        [HDR] _MainColor ("Body Color (HDR)", Color) = (0.0, 1.8, 2.8, 0.45)
        [HDR] _RimColor ("Rim Color (HDR)", Color) = (0.4, 3.5, 4.5, 0.95)
        [HDR] _EdgeColor ("Dissolve Edge Color (HDR)", Color) = (3.0, 4.0, 2.0, 1.0)

        [Header(Dissolve Control)]
        _DissolveAmount ("Dissolve Amount (0=Visible, 1=Gone)", Range(0.0, 1.0)) = 0.0
        _AutoLoopSpeed ("Auto Loop Speed (0 = Manual Amount)", Float) = 0.0
        [KeywordEnum(NoiseOnly, BottomToTop, TopToBottom)] _DirectionMode ("Dissolve Direction", Float) = 0
        _DirectionBlend ("Direction Strength (0=Noise, 1=Pure Gradient)", Range(0.0, 1.0)) = 0.65
        _GradientMinY ("Gradient Bottom Y (Object Space)", Float) = -1.0
        _GradientMaxY ("Gradient Top Y (Object Space)", Float) = 1.0

        [Header(Noise and Edge)]
        _NoiseScale ("Noise Scale", Float) = 4.0
        _NoiseScrollSpeed ("Noise Scroll Speed", Float) = 0.3
        _EdgeWidth ("Edge Glow Width", Range(0.01, 0.4)) = 0.1

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.3
        _CenterAlpha ("Center Fill Alpha", Range(0.0, 1.0)) = 0.18

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
            Name "ModelHologramDissolvePass"
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
                float4 _EdgeColor;
                float _UseTexColor;
                float _DissolveAmount;
                float _AutoLoopSpeed;
                float _DirectionMode;
                float _DirectionBlend;
                float _GradientMinY;
                float _GradientMaxY;
                float _NoiseScale;
                float _NoiseScrollSpeed;
                float _EdgeWidth;
                float _FresnelPower;
                float _CenterAlpha;
            CBUFFER_END

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // 3D バリューノイズ（0〜1）
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
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
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

                // 1. ディゾルブ量（自動ループ or 手動）
                float amount = _DissolveAmount;
                if (_AutoLoopSpeed > 0.001)
                {
                    amount = 1.0 - abs(frac(_Time.y * _AutoLoopSpeed * 0.5) * 2.0 - 1.0); // 0→1→0 往復
                }

                // 2. ノイズ ＋ 方向グラデーション
                float noise = ValueNoise3D(input.positionOS * _NoiseScale + float3(0.0, -_Time.y * _NoiseScrollSpeed, 0.0));
                float range = max(0.001, _GradientMaxY - _GradientMinY);
                float heightT = saturate((input.positionOS.y - _GradientMinY) / range); // 下0 → 上1

                float dissolveField = noise;
                if (_DirectionMode > 0.5 && _DirectionMode < 1.5)
                {
                    // BottomToTop：下から先に消える（下ほど値が小さい）
                    dissolveField = lerp(noise, heightT, _DirectionBlend);
                }
                else if (_DirectionMode >= 1.5)
                {
                    // TopToBottom：上から先に消える
                    dissolveField = lerp(noise, 1.0 - heightT, _DirectionBlend);
                }

                // 3. しきい値（amount=0 で全表示、amount=1 で全消滅）
                float threshold = lerp(-_EdgeWidth, 1.0, amount);
                if (dissolveField <= threshold)
                {
                    discard;
                }

                float edgeMask = 1.0 - smoothstep(0.0, max(0.001, _EdgeWidth), dissolveField - threshold);
                edgeMask *= step(0.001, amount); // amount=0 の完全表示時はエッジを出さない

                // 4. 通常のホログラム本体
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half texLum = dot(texCol.rgb, half3(0.299, 0.587, 0.114));
                half3 texTint = lerp(half3(texLum, texLum, texLum), texCol.rgb, _UseTexColor);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndotv = abs(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - saturate(ndotv), _FresnelPower);

                half3 bodyRGB = lerp(_MainColor.rgb * texTint, _RimColor.rgb, fresnel);
                half3 finalRGB = lerp(bodyRGB, _EdgeColor.rgb, edgeMask);

                half bodyAlpha = lerp(_CenterAlpha, _RimColor.a, fresnel) * _MainColor.a * texCol.a;
                half finalAlpha = saturate(lerp(bodyAlpha, _EdgeColor.a, edgeMask));

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}

