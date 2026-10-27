Shader "Minakuchi/MeshCloud"
{
    Properties
    {
        _Thickness ("厚み", Float) = 400
        _Scale ("模様の大きさ", Float) = 1200
        _Coverage ("雲量", Range(0,1)) = 1
        _Density ("濃さ", Float) = 2
        _Tint ("夕焼け", Color) = (1,0.28,0.055,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Only the outer face belongs to the visible cloud surface.
            // Drawing backfaces reveals the bottom through cutouts in the top (and vice versa).
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Thickness, _Scale, _Coverage, _Density;
            float4 _Offset, _SunDir, _Tint;
            float _Night, _Twilight, _Sunset;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float side : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float hash(float3 p) { p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(float3(a,0)),hash(float3(a+float2(1,0),0)),f.x),
                    lerp(hash(float3(a+float2(0,1),0)),hash(float3(a+1,0)),f.x),f.y);
            }
            float cloudNoise(float2 p)
            {
                float n=0,w=0.55;
                [unroll] for(int k=0;k<5;k++) { n+=noise(p)*w; p=p*2.03+7.1; w*=0.5; }
                return n;
            }
            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=TransformObjectToWorld(float3(input.positionOS.x,0,input.positionOS.z));
                float n=cloudNoise(p.xz/max(_Scale,1)-_Offset.xy);
                // Smooth bulges on both faces, with a real, enclosed vertical thickness.
                p.y+=input.positionOS.y*_Thickness*(0.16+0.34*smoothstep(0.15,0.85,n));
                o.positionWS=p;
                o.side=input.positionOS.y;
                o.positionCS=TransformWorldToHClip(p);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.positionWS.xz/max(_Scale,1)-_Offset.xy;
                float n=cloudNoise(p);
                float threshold=lerp(1.05,0.12,_Coverage);
                float alpha=smoothstep(threshold,threshold+0.17,n)*saturate(_Coverage*8);
                float distance=length(i.positionWS.xz-GetCameraPositionWS().xz);
                alpha*=1-smoothstep(6000,8500,distance);
                alpha*=saturate(_Density*0.6);
                // Dithered cutout keeps the nearer surface in depth without transparent sorting artifacts.
                float dither=hash(float3(floor(i.positionCS.xy),0));
                clip(alpha-max(0.005,dither));
                float3 normal=normalize(cross(ddy(i.positionWS),ddx(i.positionWS)));
                if(dot(normal,GetCameraPositionWS()-i.positionWS)<0) normal=-normal;
                float sunlight=saturate(dot(normal,normalize(_SunDir.xyz)));
                float raised=smoothstep(0.2,0.85,n);
                float brightness=saturate(0.2+sunlight*0.55+raised*0.2+saturate(i.side)*0.1);
                float3 color=lerp(float3(0.32,0.40,0.52),float3(1,0.98,0.93),brightness);
                color=lerp(color*lerp(1,0.45,_Twilight),float3(0.045,0.055,0.095),_Night*0.94);
                color=lerp(color,color*_Tint.rgb*1.4,saturate(_Sunset)*0.65);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
