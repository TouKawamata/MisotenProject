#ifndef CHARACTER_ELECTRICITY_INPUT_INCLUDED
#define CHARACTER_ELECTRICITY_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _ElectricityColor;
    float4 _BaseMap_ST;
    float4 _MeshBoundsMin;
    float4 _MeshBoundsInvSize;
    float4 _ImpactConfig;
    float _Metallic;
    float _Smoothness;
    float _PatternMode;
    float _ElectricityIntensity;
    float _ElectricitySpeed;
    float _ElectricityScale;
    float _ElectricityBranching;
    float _RimPower;
    float _RimBoost;
    float _PulseFrequency;
    float _GlowSpread;
    float _JitterIntensity;
    float _Cull;
CBUFFER_END

#endif // CHARACTER_ELECTRICITY_INPUT_INCLUDED
