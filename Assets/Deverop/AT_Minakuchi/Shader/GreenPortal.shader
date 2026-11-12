Shader "Misoten/Effects/Green Portal"
{
    Properties
    {
        [HDR] _Color ("Energy Color", Color) = (0.08, 1, 0.16, 1)
        [HDR] _CoreColor ("Hot Core Color", Color) = (0.65, 1, 0.7, 1)
        _Intensity ("Emission", Range(0, 12)) = 4
        _Radius ("Radius", Range(0.1, 0.8)) = 0.65
        _Width ("Edge Width", Range(0.002, 0.08)) = 0.014
        _Distortion ("Edge Distortion", Range(0, 0.08)) = 0.018
        _Speed ("Animation Speed", Range(0, 5)) = 1
        _Interior ("Interior Glow", Range(0, 1)) = 0.08
        _Sparkles ("Sparkle Brightness", Range(0, 4)) = 0.7
        _Opacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "GreenPortal"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _CoreColor;
                float _Intensity, _Radius, _Width, _Distortion;
                float _Speed, _Interior, _Sparkles, _Opacity;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float t = _Time.y * _Speed;
                // Sampling on a circle keeps the animated edge seamless at the angle wrap.
                float2 direction = p / max(r, 0.0001);
                float wave = (Noise(direction * 18 + float2(t * 1.8, -t)) - 0.5)
                           + 0.45 * (Noise(direction * 47 - t * 2.7) - 0.5);
                float distanceToRing = abs(r - _Radius - wave * _Distortion);
                float aa = max(fwidth(r), 0.0005);
                float core = 1 - smoothstep(_Width * 0.25, _Width + aa, distanceToRing);
                float edge = exp2(-distanceToRing / max(_Width * 2, aa) * 2);
                float halo = exp2(-distanceToRing * 22) * 0.24;
                float inside = 1 - smoothstep(_Radius - 0.02, _Radius, r);
                float mist = Noise(p * 5 + float2(t * 0.15, -t * 0.2));
                float interior = inside * _Interior * (0.25 + mist) * (0.3 + r / _Radius);
                float2 starUV = (p + float2(0, -t * 0.035)) * 23;
                float2 cell = floor(starUV);
                float seed = Hash(cell);
                float2 center = 0.2 + 0.6 * float2(Hash(cell + 13.7), Hash(cell + 39.1));
                float starDistance = length(frac(starUV) - center);
                float stars = (1 - smoothstep(0.015, 0.065, starDistance))
                            * step(0.88, seed) * pow(0.5 + 0.5 * sin(t * 2 + seed * 60), 3)
                            * inside * _Sparkles;
                float boundaryFade = 1 - smoothstep(0.9, 1, max(abs(p.x), abs(p.y)));
                float3 energy = _Color.rgb * (edge + halo + interior + stars)
                              + _CoreColor.rgb * core;
                return half4(energy * _Intensity * _Opacity * boundaryFade, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
