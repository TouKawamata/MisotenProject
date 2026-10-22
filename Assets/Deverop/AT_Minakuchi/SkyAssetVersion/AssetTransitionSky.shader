Shader "Minakuchi/AssetTransitionSky"
{
    Properties
    {
        [Toggle] _UseAssetSky ("アセットの背景を使用", Float) = 1
        _CloudySky ("曇りの背景", Cube) = "grey" {}
        _PartlyCloudySky ("薄曇りの背景", Cube) = "grey" {}
        _NightSky ("夜空の背景", Cube) = "black" {}
        _AtmosphereSky ("大気圏付近の背景", Cube) = "black" {}
        _BackgroundExposure ("背景の明るさ", Float) = 1
        _BackgroundRotation ("背景の向き（度）", Float) = 0
        _DynamicCloudOpacity ("動く雲の重ね具合", Range(0,1)) = 0.25
        [Toggle] _AdditionalCelestials ("動く太陽と月を重ねる", Float) = 0
        _SunsetAmount ("夕焼けの出現度", Range(0,1)) = 0
        _SunsetColor ("夕焼けの色", Color) = (1,0.28,0.055,1)
        _SunsetIntensity ("夕焼けの強さ", Range(0,1)) = 0.85
        _SunsetSpread ("夕焼けの広がり", Range(0.05,1)) = 0.85
        _Night ("夜の度合い", Range(0,1)) = 0
        _Space ("大気圏への移行度", Range(0,1)) = 0
        _Clouds ("雲の量", Range(0,1)) = 0.65
        _Altitude ("見た目の高度", Float) = 0
        _CloudHeight ("雲の高さ", Float) = 1200
        [Toggle] _CloudHaze ("雲の近くの霞を表示", Float) = 1
        _CloudScale ("雲の模様の大きさ", Float) = 1800
        _Clock ("アニメーション時間", Float) = 0
        _SunDir ("太陽の方向", Vector) = (0,0.6,0.8,0)
        _MoonDir ("月の方向", Vector) = (0,0.4,-0.8,0)
        _Wind ("雲の流れ", Vector) = (0.015,0.008,0,0)
        _CloudOffset ("雲の流れの移動量", Vector) = (0,0,0,0)
        _Observer ("観測位置", Vector) = (0,0,0,0)
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            TEXTURECUBE(_CloudySky); SAMPLER(sampler_CloudySky);
            TEXTURECUBE(_PartlyCloudySky); SAMPLER(sampler_PartlyCloudySky);
            TEXTURECUBE(_NightSky); SAMPLER(sampler_NightSky);
            TEXTURECUBE(_AtmosphereSky); SAMPLER(sampler_AtmosphereSky);
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            CBUFFER_START(UnityPerMaterial)
            float _UseAssetSky, _BackgroundExposure, _BackgroundRotation, _DynamicCloudOpacity, _AdditionalCelestials;
            float _SunsetAmount, _SunsetIntensity, _SunsetSpread;
            float4 _SunsetColor;
            float4 _CloudySky_HDR, _PartlyCloudySky_HDR, _NightSky_HDR, _AtmosphereSky_HDR;
            float _Night, _Space, _Clouds, _Altitude, _CloudHeight, _CloudScale, _Clock;
            float _CloudHaze;
            float4 _SunDir, _MoonDir, _Wind, _Observer, _CloudOffset;
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
            float cloudNoise(float2 p)
            {
                float n=0, w=0.55;
                for(int k=0;k<5;k++) { n+=noise(p)*w; p=p*2.03+7.1; w*=0.5; }
                return n;
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
                float3 col=lerp(day,night,_Night);
                col=lerp(col,float3(0.0006,0.001,0.005),_Space);
                col+=horizon*lerp(float3(0.18,0.25,0.3),float3(0.06,0.28,0.7),_Space)*(1-_Night*0.7);
                if (_UseAssetSky > 0.5)
                {
                    float a=radians(_BackgroundRotation);
                    float3 v=float3(d.x*cos(a)-d.z*sin(a),d.y,d.x*sin(a)+d.z*cos(a));
                    float3 cloudy=DecodeHDREnvironment(SAMPLE_TEXTURECUBE(_CloudySky,sampler_CloudySky,v),_CloudySky_HDR);
                    float3 partly=DecodeHDREnvironment(SAMPLE_TEXTURECUBE(_PartlyCloudySky,sampler_PartlyCloudySky,v),_PartlyCloudySky_HDR);
                    float3 nightImage=DecodeHDREnvironment(SAMPLE_TEXTURECUBE(_NightSky,sampler_NightSky,v),_NightSky_HDR);
                    float3 atmosphere=DecodeHDREnvironment(SAMPLE_TEXTURECUBE(_AtmosphereSky,sampler_AtmosphereSky,v),_AtmosphereSky_HDR);
                    float cloudBlend=smoothstep(0.34,0.72,_Clouds);
                    col=lerp(lerp(lerp(partly,cloudy,cloudBlend),nightImage,_Night),atmosphere,_Space)*_BackgroundExposure;
                }
                float starVisibility=max(_Night,_Space*0.85)*smoothstep(-0.04,0.12,d.y);
                col+=stars(d)*starVisibility*2.5*(1-saturate(_UseAssetSky));
                float s=dot(d,normalize(_SunDir.xyz));
                col+=float3(1,0.84,0.55)*(smoothstep(0.99975,0.9999,s)*5+pow(saturate(s),180)*0.3)*(1-_Night)*_AdditionalCelestials;
                float m=dot(d,normalize(_MoonDir.xyz));
                float moon=smoothstep(0.99935,0.99955,m);
                col=lerp(col,float3(0.72,0.79,0.94)*(0.8+0.2*noise(d.xz*600)),moon*_Night*_AdditionalCelestials);
                // Intersect a world-height cloud sheet: it moves below the observer during ascent.
                float delta=_CloudHeight-_Altitude;
                float t=delta/(abs(d.y)>0.002?d.y:(d.y<0?-0.002:0.002));
                float2 p=(_Observer.xz+d.xz*t)/max(_CloudScale,1)-_CloudOffset.xy;
                float n=cloudNoise(p);
                float threshold=lerp(1.05,0.12,_Clouds);
                float alpha=smoothstep(threshold,threshold+0.17,n)*step(0,t);
                alpha*=1-smoothstep(18000,45000,abs(t));
                alpha*=lerp(1,_DynamicCloudOpacity,saturate(_UseAssetSky));
                // Keep the upper atmosphere clear; independently placed cloud layers below remain visible.
                alpha*=1-saturate(_UseAssetSky)*_Space*smoothstep(-0.03,0.12,d.y);
                float3 cloud=lerp(float3(0.48,0.54,0.62),float3(1,0.98,0.92),saturate(n*1.5));
                cloud=lerp(cloud,float3(0.045,0.055,0.095),_Night*0.94);
                col=lerp(col,cloud,alpha);
                // Apply after the HDR background and cloud layer so both can take the sunset tint.
                float sunsetBand=1-smoothstep(0,max(0.05,_SunsetSpread),abs(d.y));
                float sunsetWeight=saturate(_SunsetAmount*_SunsetIntensity*sunsetBand)*(1-_Space);
                float luminance=dot(col,float3(0.2126,0.7152,0.0722));
                col=lerp(col,_SunsetColor.rgb*(0.35+luminance*0.9),sunsetWeight);
                float inside=(1-smoothstep(30,160,abs(delta)))*_Clouds*saturate(_CloudHaze);
                col=lerp(col,lerp(float3(0.7,0.76,0.81),float3(0.055,0.065,0.1),_Night),inside*0.85);
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
