Shader "Misoten/TackleGlow"
{
    Properties
    {
        [HDR] _Color("Wind Color", Color) = (1.8,2.4,2.8,1)
        _Phase("UV Scroll", Float) = 0
        _WindStrength("Wind Strength", Range(0,2)) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Phase;
            float _WindStrength;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv;
                return o;
            }
            float variation(float cell, float seed)
            {
                return frac(sin(cell*127.1+seed*311.7)*43758.5453);
            }
            float unevenTeeth(float u, float count, float seed)
            {
                float cell=min(floor(u*count),count-1);
                float local=frac(u*count);
                float center=lerp(0.3,0.7,variation(cell,seed));
                float width=lerp(0.08,0.28,variation(cell,seed+1));
                float toothTip=saturate(1-abs(local-center)/width);
                float height=lerp(0.015,0.085,pow(variation(cell,seed+2),2));
                // Each pointed tooth returns to zero before its cell boundary, including the UV seam.
                return toothTip*height;
            }
            half4 frag(Varyings i):SV_Target
            {
                // U runs around the tackle axis, V runs from the nose to the open rim.
                // Integer tooth counts keep the jagged circular bands seamless at U=0/1.
                float jagged=unevenTeeth(i.uv.x,64,1)
                    +unevenTeeth(i.uv.x,109,7)*0.35;
                float scrolling=i.uv.y-jagged-_Phase;
                float distance=abs(frac(scrolling*2+0.5)-0.5);
                float aa=max(fwidth(distance),0.002);
                float angle=i.uv.x*6.2831853;
                float thickness=lerp(0.008,0.021,0.5+0.5*sin(angle*17+sin(angle*9)));
                float crest=1-smoothstep(max(0,thickness-aa),thickness+aa,distance);
                float glow=exp2(-distance*distance*340);
                // Broken pressure arcs and pointed streamlines sweeping nose-to-rim.
                // The pattern remains seamless around the forward axis.
                float breakup=smoothstep(-0.5,0.65,sin(angle*7+i.uv.y*4)+sin(angle*11-i.uv.y*3)*0.4);
                float lane=frac(i.uv.x*43);
                float laneId=min(floor(i.uv.x*43),42);
                float head=frac(_Phase*1.35+sin(laneId*7.13)*2.4);
                float tail=head-i.uv.y;
                float length=lerp(0.055,0.24,variation(laneId,11));
                float taper=saturate(1-tail/length);
                float width=lerp(0.018,0.065,variation(laneId,12))*taper;
                float laneDistance=abs(lane-lerp(0.25,0.75,variation(laneId,13)));
                float laneAA=max(fwidth(laneDistance),0.002);
                float wind=(1-smoothstep(width,width+laneAA,laneDistance))
                    *step(0,tail)*step(tail,length)*taper;
                float edge=smoothstep(0,0.10,i.uv.y)*(1-smoothstep(0.88,1,i.uv.y));
                float mask=(crest*(0.16+breakup*0.44)+glow*0.06+wind*_WindStrength)*edge;
                half3 color=lerp(_Color.rgb,half3(3.4,3.7,4),saturate(crest*0.5+wind));
                return half4(color,saturate(mask)*_Color.a);
            }
            ENDHLSL
        }
    }
}
