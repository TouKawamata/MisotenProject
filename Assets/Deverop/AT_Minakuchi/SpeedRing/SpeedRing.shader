Shader "Misoten/Speed Ring"
{
    Properties
    {
        [HDR] _RingColor ("リングの色", Color) = (0.08, 1, 0.16, 1)
        _Intensity ("発光の強さ", Range(0, 12)) = 4
        _Width ("縁の太さ", Range(0.002, 0.08)) = 0.014
        _Distortion ("縁の揺らぎ", Range(0, 0.08)) = 0.018
        _Speed ("アニメーション速度", Range(0, 5)) = 1
        _Interior ("内側の光の強さ", Range(0, 1)) = 0.08
        _Sparkles ("粒の明るさ", Range(0, 4)) = 0.7
        _ParticleSize ("粒の大きさ", Range(0.002, 0.05)) = 0.012
        _ParticleHoverRatio ("外周で浮遊する時間の割合", Range(0.1, 0.8)) = 0.5
        _ParticleHoverWidth ("外周の浮遊範囲", Range(0, 0.15)) = 0.07
        [HideInInspector] _ParticleTravel ("粒の移動量", Float) = 0
        [HideInInspector] _TrailSpan ("尾の長さ", Float) = 0
        _TrailBrightness ("尾の明るさ", Range(0, 2)) = 0.65
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RingColor;
                float _Intensity, _Width, _Distortion, _Speed, _Interior, _Sparkles;
                float _ParticleSize, _ParticleTravel;
                float _ParticleHoverRatio, _ParticleHoverWidth;
                float _TrailSpan, _TrailBrightness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
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
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float2 ParticlePosition(float life, float seed, float angle)
            {
                float hoverRatio = clamp(_ParticleHoverRatio, 0.1, 0.8);
                float inward = saturate((life - hoverRatio) / (1 - hoverRatio));
                float wobble = sin(life * 18 + seed * 60) * 0.65
                             + sin(life * 31 + seed * 27) * 0.35;
                float radius = (0.65 + _ParticleHoverWidth * wobble) * (1 - inward * inward);
                angle += sin(life * 12 + seed * 40) * 0.12 * (1 - inward) + inward * 0.9;
                return float2(cos(angle), sin(angle)) * radius;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float r = length(p);
                float t = _Time.y * _Speed;
                // Circular noise coordinates avoid a seam on the ring.
                float2 direction = p / max(r, 0.0001);
                float wave = Noise(direction * 18 + float2(t * 1.8, -t)) - 0.5
                           + 0.45 * (Noise(direction * 47 - t * 2.7) - 0.5);
                float d = abs(r - 0.65 - wave * _Distortion);
                float aa = max(fwidth(r), 0.0005);
                float core = 1 - smoothstep(_Width * 0.25, _Width + aa, d);
                float edge = exp2(-d / max(_Width * 2, aa) * 2);
                float halo = exp2(-d * 22) * 0.24;
                float inside = 1 - smoothstep(0.63, 0.65, r);
                float mist = Noise(p * 5 + float2(t * 0.15, -t * 0.2));
                float interior = inside * _Interior * (0.25 + mist) * (0.3 + r / 0.65);
                float stars = 0;
                // Hover around the rim first, then spiral inward. Staggered lifetimes
                // keep a visible cloud around the ring while other particles are absorbed.
                [loop]
                for (int i = 0; i < 32; i++)
                {
                    float seed = Hash(float2(i, 7.3));
                    float angle = Hash(float2(i, 19.1)) * 6.2831853;
                    float life = frac(_ParticleTravel + seed);
                    float hoverRatio = clamp(_ParticleHoverRatio, 0.1, 0.8);
                    float inward = saturate((life - hoverRatio) / (1 - hoverRatio));
                    float2 center = ParticlePosition(life, seed, angle);
                    float size = _ParticleSize * lerp(0.7, 1.3, seed);
                    float particleAA = aa;
                    float particleMask = 1 - smoothstep(size * 0.3, size + particleAA, length(p - center));
                    float visibility = smoothstep(0, 0.08, life) * (1 - smoothstep(0.85, 1, inward));
                    stars += particleMask * visibility * (0.65 + 0.35 * sin(t * 2 + seed * 60));
                    if (_TrailSpan > 0.0001 && _TrailBrightness > 0)
                    {
                        float trailMask = 0;
                        float2 segmentStart = center;
                        // Follow earlier positions on the same path; never connect across respawns.
                        [unroll]
                        for (int j = 1; j <= 5; j++)
                        {
                            float tailFraction = j / 5.0;
                            float pastLife = max(0, life - _TrailSpan * tailFraction);
                            float2 segmentEnd = ParticlePosition(pastLife, seed, angle);
                            float2 segment = segmentEnd - segmentStart;
                            float along = saturate(dot(p - segmentStart, segment) / max(dot(segment, segment), 0.000001));
                            float distanceToTrail = length(p - (segmentStart + segment * along));
                            float taper = 1 - (j - 1 + along) / 5.0;
                            float tailSize = size * lerp(0.15, 0.75, taper);
                            float stroke = 1 - smoothstep(tailSize * 0.2, tailSize + aa, distanceToTrail);
                            trailMask = max(trailMask, stroke * taper * smoothstep(0, 0.08, pastLife));
                            segmentStart = segmentEnd;
                        }
                        stars += trailMask * visibility * _TrailBrightness;
                    }
                }
                stars *= _Sparkles;
                float fade = 1 - smoothstep(0.9, 1, max(abs(p.x), abs(p.y)));
                float3 hotCore = lerp(_RingColor.rgb, float3(1, 1, 1), 0.6);
                float3 energy = _RingColor.rgb * (edge + halo + interior + stars) + hotCore * core;
                return half4(energy * _Intensity * fade, 0);
            }
            ENDHLSL
        }
    }
}
