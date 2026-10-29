Shader "Minakuchi/TransitionSky"
{
    Properties
    {
        _Night ("夜の度合い", Range(0,1)) = 0
        _Twilight ("夕暮れの青さの暗さ", Range(0,1)) = 0
        _SunsetAmount ("夕焼けの出現度", Range(0,1)) = 0
        _SunsetColor ("夕焼けの色", Color) = (1,0.28,0.055,1)
        _SunsetIntensity ("夕焼けの強さ", Range(0,1)) = 0.85
        _SunsetSpread ("夕焼けの広がり", Range(0.05,1)) = 0.85
        _Space ("大気圏への移行度", Range(0,1)) = 0
        _Clouds ("雲の量", Range(0,1)) = 1
        _CloudHeight ("雲の高さ", Float) = -400
        _CloudScale ("雲の模様の大きさ", Float) = 1200
        _CloudThickness ("雲の厚み", Float) = 400
        _CloudDensity ("雲の濃さ", Range(0.1,5)) = 2
        _Clock ("アニメーション時間", Float) = 0
        _SunDir ("太陽の方向", Vector) = (0,0.6,0.8,0)
        _MoonDir ("月の方向", Vector) = (0,0.4,-0.8,0)
        [Toggle] _ShowMoon ("月を表示", Float) = 1
        _Wind ("雲の流れ", Vector) = (0.015,0.008,0,0)
        _CloudOffset ("雲の流れの移動量", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _SunsetAmount, _SunsetIntensity, _SunsetSpread;
            float4 _SunsetColor;
            float _Night, _Space, _Clouds, _CloudHeight, _CloudScale, _Clock;
            float _Twilight;
            float _ShowMoon;
            float _CloudThickness, _CloudDensity;
            float4 _SunDir, _MoonDir, _Wind, _CloudOffset;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.direction = i.positionOS.xyz;
                return o;
            }
            float hash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x+p.y)*p.z); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(float3(a,0)),hash(float3(a+float2(1,0),0)),f.x),
                    lerp(hash(float3(a+float2(0,1),0)),hash(float3(a+1,0)),f.x),f.y);
            }
            float stars(float3 d)
            {
                // A 3D lattice avoids a longitude seam or stretched polar stars.
                float3 p=d*330, cell=floor(p);
                float h=hash(cell);
                float3 center=0.2+0.6*float3(hash(cell+1),hash(cell+2),hash(cell+3));
                float radius=length(frac(p)-center);
                float aa=max(length(fwidth(p)),0.015);
                return step(0.992,h)*(1-smoothstep(0.055,0.055+aa,radius))*(0.6+0.4*sin(_Clock*0.8+h*120));
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=normalize(i.direction);
                float horizon=pow(1-saturate(abs(d.y)),5);
                float3 day=lerp(float3(0.36,0.64,0.9),float3(0.035,0.23,0.58),sqrt(saturate(d.y)));
                float3 night=lerp(float3(0.018,0.028,0.075),float3(0.001,0.003,0.014),saturate(d.y));
                // The blue sky remains after sunset; a soft night front then descends from the zenith.
                float nightFront=lerp(1.2,-0.2,saturate(_Night));
                float localNight=smoothstep(nightFront-0.15,nightFront+0.15,saturate(d.y));
                float3 col=lerp(day*lerp(1,0.35,_Twilight),night,localNight);
                col=lerp(col,float3(0.0006,0.001,0.005),_Space);
                col+=horizon*lerp(float3(0.18,0.25,0.3),float3(0.06,0.28,0.7),_Space)*lerp(1,0.06,localNight)*lerp(1,0.35,_Twilight);
                float starVisibility=max(localNight,_Space*0.85)*smoothstep(-0.04,0.12,d.y);
                col+=stars(d)*starVisibility*2.5;
                float s=dot(d,normalize(_SunDir.xyz));
                float sunVisible=smoothstep(-0.012,0.012,_SunDir.y)*smoothstep(-0.002,0.003,d.y);
                col+=float3(1,0.84,0.55)*(smoothstep(0.99975,0.9999,s)*5+pow(saturate(s),180)*0.3)*sunVisible;
                float m=dot(d,normalize(_MoonDir.xyz));
                float moon=smoothstep(0.99935,0.99955,m);
                col=lerp(col,float3(0.72,0.79,0.94)*(0.8+0.2*noise(d.xz*600)),moon*_Night*saturate(_ShowMoon));
                // CloudVolumeFeature composites clouds against scene depth after the sky.
                float sunsetBand=1-smoothstep(0,max(0.01,_SunsetSpread),abs(d.y));
                float2 sunHorizontal=_SunDir.xz/max(length(_SunDir.xz),0.001);
                float sunsetFacing=pow(saturate(dot(d.xz,sunHorizontal)),2);
                float sunsetWeight=saturate(_SunsetAmount*_SunsetIntensity*sunsetBand*0.6*lerp(0.15,1,sunsetFacing))*(1-_Space);
                float luminance=dot(col,float3(0.2126,0.7152,0.0722));
                col=lerp(col,_SunsetColor.rgb*(0.08+luminance*1.2),sunsetWeight);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
