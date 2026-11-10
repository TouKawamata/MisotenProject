#ifndef REALISTIC_CITY_BUILDING_INPUT_INCLUDED
#define REALISTIC_CITY_BUILDING_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _WallColor;
    float4 _GlassColorDay;
    float4 _MullionColor;
    float4 _RoofColor;
    float4 _WindowColorWarm;
    float4 _WindowColorCool;
    float4 _AccentColor;
    float4 _LobbyLightColor;
    float4 _NeonSignColor;

    float _WallMetallic;
    float _WallSmoothness;
    float _GlassMetallic;
    float _GlassSmoothness;

    float _FloorHeight;
    float _WindowWidth;
    float _SpandrelRatio;
    float _MullionRatio;

    float _CornerColumnWidth;
    float _BuildingSeed;
    float _NightFactor;
    float _AutoDayNight;

    float _WindowLitRate;
    float _WindowIntensity;
    float _AccentIntensity;
    float _LobbyHeight;

    float _LobbyIntensity;
    float _NeonSignEnable;
    float _NeonSignIntensity;
    float _AviationBeaconEnable;

    float _CityGroundY;
    float _Pad0;
    float _Pad1;
    float _Pad2;
CBUFFER_END

#endif // REALISTIC_CITY_BUILDING_INPUT_INCLUDED
