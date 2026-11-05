#ifndef MINAKUCHI_DISTANT_CLOUD_INCLUDED
#define MINAKUCHI_DISTANT_CLOUD_INCLUDED
TEXTURE2D(_DistantCloudImage);
SAMPLER(sampler_DistantCloudImage);
static const float CloudImageStart = 3000;
static const float CloudImageEnd = 6000;

// Coordinates are world-space for the sky layer and object-space for placed clouds.
// The image never uses the animated cloud offset or per-pixel jitter.
float4 DistantCloud(float3 origin, float3 direction, float entry, float exit)
{
    float start=max(entry,CloudImageStart);
    if(exit<=start) return float4(0,0,0,1);
    float distance=(start+exit)*0.5;
    if(abs(direction.y)>0.00001)
    {
        distance=(_CloudHeight-origin.y)/direction.y;
        // Never move the image onto an opaque surface that clips the cloud plane.
        if(distance<start || distance>exit) return float4(0,0,0,1);
    }
    float3 position=origin+direction*distance;
    float scale=max(1,_CloudScale);
    float2 uv=position.xz/(scale*8);
    // Explicit mip selection also removes subpixel detail on grazing, distant rays.
    float lod=max(0,log2(max(1,distance/scale*0.5)));
    float shape=SAMPLE_TEXTURE2D_LOD(_DistantCloudImage,sampler_DistantCloudImage,uv,lod).r;
    float threshold=lerp(1.05,0.12,_Clouds);
    float alpha=smoothstep(threshold,threshold+0.22,shape);
    alpha*=saturate(_CloudDensity*0.7)*smoothstep(CloudImageStart,CloudImageEnd,distance);
    alpha*=1-smoothstep(16000,24000,distance);
    // Use the volume clouds' palette and time-of-day settings, with fixed image shading.
    // No light-ray marching or density-gradient calculation is needed for this layer.
    float3 sun=normalize(_SunDir.xyz);
    float day=smoothstep(-0.12,0.12,sun.y)*(1-_Night*0.95);
    float facing=pow(saturate(dot(normalize(direction),sun)),12);
    float3 sunColor=lerp(float3(0.92,0.97,1),_SunsetColor.rgb,
        saturate(_SunsetAmount*_SunsetIntensity)*0.6);
    float light=lerp(0.35,0.95,smoothstep(threshold,threshold+0.4,shape));
    float3 ambient=lerp(float3(0.28,0.32,0.38),float3(0.025,0.035,0.065),_Night);
    float direct=light;
    float fill=0.14;
    #if defined(DISTANT_SKY_LAYER)
        float upperView=smoothstep(0.05,0.55,(origin.y-_CloudHeight)/max(1,_CloudThickness));
        float morningShadow=lerp(_CloudMorningShadow,1,smoothstep(10,14,_CloudHour));
        float contrast=upperView*_CloudTopContrast*morningShadow;
        ambient*=lerp(1,lerp(0.05,0.46,light),contrast*day);
        direct=pow(light,1+contrast*0.7)*(1+contrast*0.2);
        fill=lerp(0.14,0.025,contrast);
    #endif
    float3 color=ambient+sunColor*day*(direct*(0.68+facing*0.18)+fill);
    #if defined(DISTANT_SKY_LAYER)
        float shadowLift=lerp(0.42,1,smoothstep(0.08,0.65,direct));
        color*=lerp(1,shadowLift,contrast*day);
        color*=lerp(float3(1,1,1),float3(0.78,0.9,1.12),upperView*_CloudTopBlueTint*day);
    #endif
    color*=lerp(1,0.65,_Twilight*(1-_Night));
    return float4(color*alpha,1-alpha);
}
#endif
