Shader "Minakuchi/PlacedCloudVolume"
{
    Properties
    {
        _Clouds ("雲量", Float) = 1
        _CloudHeight ("高さ", Float) = -400
        _CloudThickness ("厚み", Float) = 400
        _CloudScale ("模様", Float) = 1200
        _CloudDensity ("濃さ", Float) = 2
        _CloudNoise ("形状ノイズ", 3D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            // Store light and transmittance separately; the second pass filters then composites them.
            Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord=GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }
            CBUFFER_START(UnityPerMaterial)
            float _Clouds, _CloudHeight, _CloudThickness, _CloudScale, _CloudDensity;
            float4 _CloudOffset, _SunDir, _SunsetColor;
            float _Night, _Twilight, _SunsetAmount, _SunsetIntensity;
            float _CloudShadowStrength;
            float _CloudPassageStrength, _CloudWorldThickness;
            float4x4 _CloudWorldToLocal;
            float3 _CloudHalfSize;
            int _CloudSteps;
            CBUFFER_END
            TEXTURE3D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);
            float Noise(float3 p)
            {
                return SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,p/8,0).g;
            }
            float Hash(float2 p)
            {
                float3 q=frac(float3(p,0)*0.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float OriginalNoise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),
                    lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);
            }
            float CloudFootprint(float2 p)
            {
                float n=0, weight=0.55;
                [unroll] for(int k=0;k<5;k++)
                {
                    n+=OriginalNoise(p)*weight;
                    p=p*2.03+7.1;
                    weight*=0.5;
                }
                return n;
            }
            float CloudField(float3 world, bool detail)
            {
                world=mul(_CloudWorldToLocal,float4(world,1)).xyz;
                float thickness=max(1,_CloudThickness);
                float h=(world.y-_CloudHeight)/thickness+0.5;
                float scale=max(1,_CloudScale);
                float2 footprintPosition=world.xz/scale-_CloudOffset.xy;
                float footprint=CloudFootprint(footprintPosition);
                float3 p=float3(footprintPosition.x,h*thickness/scale,footprintPosition.y);
                float turbulence=Noise(p*5.3+float3(2.1,7.3,4.6));
                float wisps=detail ? Noise(p*19.1+19.3) : 0.5;
                float edge=pow(abs(h*2-1),1.6);
                float threshold=lerp(1.05,0.12,_Clouds)+edge*0.65;
                float shape=footprint+(turbulence-0.5)*0.38+(wisps-0.5)*0.16;
                return shape-threshold;
            }
            float Density(float3 world, bool detail)
            {
                float3 local=mul(_CloudWorldToLocal,float4(world,1)).xyz;
                float3 edgeDistance=_CloudHalfSize-abs(local);
                if(any(edgeDistance<=0)) return 0;
                float h=(local.y-_CloudHeight)/max(1,_CloudThickness)+0.5;
                float envelope=smoothstep(0,0.06,h)*(1-smoothstep(0.94,1,h));
                float border=smoothstep(0,max(1,_CloudScale*0.15),min(edgeDistance.x,edgeDistance.z));
                return smoothstep(0,0.15,CloudField(world,detail))*envelope*border*saturate(_Clouds*8);
            }
            float SunTransmission(float3 position, float3 lightDir)
            {
                float opticalDepth=0;
                float stepLength=max(1,_CloudWorldThickness)*0.18;
                [unroll] for(int k=0;k<5;k++)
                    opticalDepth+=Density(position+lightDir*((k+0.5)*stepLength),false)*stepLength;
                return exp(-opticalDepth*_CloudDensity*_CloudShadowStrength/140);
            }
            float PassageVapor(float3 position, float distance, float passage)
            {
                // A local traversal effect, separate from the distant cloud footprint.
                // It is strongest inside the layer, reaches zero outside, and respects scene depth.
                position=mul(_CloudWorldToLocal,float4(position,1)).xyz;
                if(any(abs(position)>=_CloudHalfSize)) return 0;
                float size=max(8,min(_CloudThickness*0.15,_CloudScale*0.06));
                float3 p=float3(position.x/size-_CloudOffset.x*_CloudScale/size,
                    (position.y-_CloudHeight)/size,
                    position.z/size-_CloudOffset.y*_CloudScale/size);
                float patches=Noise(p+11.7)*0.7+Noise(p*2.7+31.1)*0.3;
                return (0.16+smoothstep(0.23,0.72,patches)*0.9)*passage*exp(-distance/110)*3;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.texcoord;
                float rawDepth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float farDepth=0;
                    bool hasGeometry=rawDepth>0.000001;
                #else
                    rawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
                    float farDepth=1;
                    bool hasGeometry=rawDepth<0.999999;
                #endif
                float3 origin=GetCameraPositionWS();
                float3 farPosition=ComputeWorldSpacePosition(uv,farDepth,UNITY_MATRIX_I_VP);
                float3 direction=normalize(farPosition-origin);
                // Orthographic scene/game cameras use parallel rays with per-pixel origins.
                if(unity_OrthoParams.w>0.5)
                {
                    #if UNITY_REVERSED_Z
                        float nearDepth=1;
                    #else
                        float nearDepth=UNITY_NEAR_CLIP_VALUE;
                    #endif
                    origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                    direction=normalize(farPosition-origin);
                }
                float limit=24000;
                if(hasGeometry)
                {
                    float3 surface=ComputeWorldSpacePosition(uv,rawDepth,UNITY_MATRIX_I_VP);
                    limit=min(limit,length(surface-origin));
                }
                float thickness=max(1,_CloudThickness);
                // Intersect the placed object's box. Keep t in world units by not normalizing localDirection.
                float3 localOrigin=mul(_CloudWorldToLocal,float4(origin,1)).xyz;
                float3 localDirection=mul((float3x3)_CloudWorldToLocal,direction);
                float entry=0, exit=limit;
                [unroll] for(int axis=0;axis<3;axis++)
                {
                    if(abs(localDirection[axis])<0.000001)
                    {
                        if(abs(localOrigin[axis])>_CloudHalfSize[axis]) return half4(0,0,0,1);
                    }
                    else
                    {
                        float a=(-_CloudHalfSize[axis]-localOrigin[axis])/localDirection[axis];
                        float b=(_CloudHalfSize[axis]-localOrigin[axis])/localDirection[axis];
                        entry=max(entry,min(a,b));
                        exit=min(exit,max(a,b));
                    }
                }
                if(exit<=entry || _Clouds<=0) return half4(0,0,0,1);
                int steps=clamp(_CloudSteps,16,64);
                // Grazing rays cross a much longer distance through the layer.
                steps*=abs(normalize(localDirection).y)<0.4 ? 2 : 1;
                float3 sun=normalize(_SunDir.xyz);
                float day=smoothstep(-0.12,0.12,sun.y)*(1-_Night*0.95);
                float facing=pow(saturate(dot(direction,sun)),12);
                float3 sunColor=lerp(float3(0.92,0.97,1),_SunsetColor.rgb,
                    saturate(_SunsetAmount*_SunsetIntensity)*0.6);
                float transmittance=1;
                float3 scattering=0;
                float observerHeight=localOrigin.y/thickness+0.5;
                float passage=smoothstep(0,0.18,observerHeight)
                    *(1-smoothstep(0.82,1,observerHeight))*saturate(_Clouds*3)*_CloudPassageStrength;
                passage*=all(abs(localOrigin.xz)<_CloudHalfSize.xz) ? 1 : 0;
                // Stratified spatial samples turn coherent contour bands into small, filterable error.
                // The pattern is fixed in time so it does not introduce animated grain.
                float jitter=frac(sin(dot(floor(i.positionCS.xy),float2(12.9898,78.233)))*43758.5453);
                // Bias samples toward the entry point so nearby fog is not skipped on long horizontal rays.
                [loop] for(int k=0;k<steps;k++)
                {
                    float u0=k/(float)steps, u1=(k+1)/(float)steps;
                    float start=entry+(exit-entry)*u0*u0;
                    float end=entry+(exit-entry)*u1*u1;
                    float stepLength=end-start;
                    float distance=lerp(start,end,0.1+0.8*jitter);
                    float3 position=origin+direction*distance;
                    float density=Density(position,true);
                    float vapor=passage>0.001 ? PassageVapor(position,distance,passage) : 0;
                    float originalDensity=density;
                    density=max(density,vapor);
                    if(density>0.001)
                    {
                        float light=SunTransmission(position,sun);
                        // One lighting model for every viewing direction.
                        float3 ambient=lerp(float3(0.28,0.32,0.38),float3(0.025,0.035,0.065),_Night);
                        float3 color=ambient+sunColor*day*(light*(0.68+facing*0.18)+0.14);
                        color*=lerp(1,0.65,_Twilight*(1-_Night));
                        float3 vaporColor=lerp(float3(0.73,0.77,0.81),float3(0.055,0.065,0.09),_Night);
                        color=lerp(color,vaporColor,saturate((vapor-originalDensity)/max(density,0.001)));
                        float fade=1-smoothstep(16000,24000,distance);
                        float alpha=1-exp(-density*_CloudDensity*stepLength*fade/140);
                        scattering+=transmittance*alpha*color;
                        transmittance*=1-alpha;
                        if(transmittance<0.01) break;
                    }
                }
                return half4(scattering,transmittance);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthAwareComposite"
            Cull Off ZWrite Off ZTest Always
            Blend One SrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_X(_PlacedCloudVolumeBuffer);
            float4 _PlacedCloudVolumeBuffer_TexelSize;
            struct Attributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
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
                output.positionCS=GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv=GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }
            float DepthAt(float2 uv)
            {
                float raw=SampleSceneDepth(uv);
                if(unity_OrthoParams.w>0.5) return LinearDepthToEyeDepth(raw);
                return LinearEyeDepth(raw,_ZBufferParams);
            }
            half4 Composite(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float centerDepth=DepthAt(i.uv);
                // Gather around actual low-resolution texel centers so the depth test matches the ray sample.
                float2 center=(floor(i.uv*_PlacedCloudVolumeBuffer_TexelSize.zw)+0.5)*_PlacedCloudVolumeBuffer_TexelSize.xy;
                float4 total=0;
                float weights=0;
                [unroll] for(int y=-2;y<=2;y++)
                [unroll] for(int x=-2;x<=2;x++)
                {
                    float2 uv=center+float2(x,y)*_PlacedCloudVolumeBuffer_TexelSize.xy;
                    float2 offset=(uv-i.uv)/_PlacedCloudVolumeBuffer_TexelSize.xy;
                    float spatial=exp(-dot(offset,offset)*0.45);
                    float depthDifference=abs(DepthAt(uv)-centerDepth)/max(1,centerDepth*0.015);
                    float weight=spatial*exp(-depthDifference*depthDifference);
                    total+=SAMPLE_TEXTURE2D_X_LOD(_PlacedCloudVolumeBuffer,sampler_PointClamp,uv,0)*weight;
                    weights+=weight;
                }
                // Do not bleed a background cloud onto a foreground silhouette with no matching sample.
                return weights>0.0001 ? total/weights : half4(0,0,0,1);
            }
            ENDHLSL
        }
    }
}
