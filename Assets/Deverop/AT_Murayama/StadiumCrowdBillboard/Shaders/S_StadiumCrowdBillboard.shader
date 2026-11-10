Shader "AT_Murayama/StadiumCrowdBillboard/S_StadiumCrowdBillboard"
{
    Properties
    {
        [Header(Billboard Mode and Texture)]
        [Toggle] _UseCustomTexture ("Use Custom Sprite Texture", Float) = 0.0
        _MainTex ("Custom Spectator Sprite (RGBA)", 2D) = "white" {}
        _AlphaCutoff ("Alpha Clip Cutoff", Range(0.05, 0.9)) = 0.45
        _BillboardOrientationMode ("Orientation (0=HybridSkyTilt, 1=YAxis, 2=SeatTarget)", Float) = 0.0
        _VerticalTiltWeight ("Sky-View Vertical Tilt Compensation", Range(0.0, 0.85)) = 0.55

        [Header(Crowd Color Palette)]
        _OutfitColorA ("Outfit Color 1 (Primary)", Color) = (0.16, 0.42, 0.85, 1.0)
        _OutfitColorB ("Outfit Color 2 (Secondary)", Color) = (0.86, 0.22, 0.24, 1.0)
        _OutfitColorC ("Outfit Color 3 (Light/Neutral)", Color) = (0.90, 0.92, 0.95, 1.0)
        _OutfitColorD ("Outfit Color 4 (Accent/Yellow)", Color) = (0.95, 0.72, 0.16, 1.0)
        _PantsColor   ("Lower Body / Pants Color", Color) = (0.14, 0.16, 0.20, 1.0)
        _SkinColor    ("Skin Base Color", Color) = (0.88, 0.72, 0.62, 1.0)
        _HairHatColor ("Hair & Cap Base Color", Color) = (0.18, 0.20, 0.25, 1.0)
        [HDR] _AccentEmissionColor ("Accent / Visor / Flag HDR Color", Color) = (0.25, 0.75, 1.0, 1.0)
        _AccentEmissionStrength ("Accent Emission Strength", Range(0.0, 3.0)) = 0.0
        _ColorVariation ("Individual Brightness Variation", Range(0.0, 0.5)) = 0.22

        [Header(Lighting and Visibility)]
        _AmbientMin ("Minimum Ambient Brightness", Range(0.05, 0.8)) = 0.35
        _RimColor ("Silhouette Rim Color", Color) = (0.65, 0.80, 1.0, 1.0)
        _RimIntensity ("Rim Highlight Intensity", Range(0.0, 1.5)) = 0.45

        [Header(LookAt Tracking)]
        _TargetPosOS ("Current LookAt Target (Object Space)", Vector) = (0, 30, 0, 1)
        _DelayedTargetPosOS ("Delayed LookAt Target (Object Space)", Vector) = (0, 35, 0, 1)
        _LookAtWeight ("Overall LookAt Tracking Weight", Range(0.0, 1.0)) = 1.0
        _ReactionLagWeight ("Individual Reaction Lag Spread", Range(0.0, 1.0)) = 0.68

        [Header(Crowd Motion and Variation)]
        _AnimSpeed ("Animation Master Speed", Range(0.0, 3.0)) = 1.0
        _Excitement ("Crowd Excitement Level", Range(0.0, 1.0)) = 0.45
        _JumpHeight ("Max Jump Height (m)", Range(0.0, 0.6)) = 0.22
        _JumpRatio ("Jumping Spectator Ratio", Range(0.0, 1.0)) = 0.55
        _SwayAmount ("Body Sway Amplitude", Range(0.0, 0.35)) = 0.10
        _ArmWaveAmount ("Arm Wave Amplitude", Range(0.0, 1.5)) = 0.85
        _HeightVariation ("Body Scale Variation", Range(0.0, 0.25)) = 0.10

        [Header(Stadium Wave)]
        _WaveIntensity ("Stadium Mexican Wave Intensity", Range(0.0, 1.0)) = 0.18
        _WaveSpeed ("Stadium Wave Speed", Range(0.05, 1.5)) = 0.22
        _WaveCount ("Stadium Wave Repeat Count", Range(1.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float _UseCustomTexture;
            float4 _MainTex_ST;
            float _AlphaCutoff;
            float _BillboardOrientationMode;
            float _VerticalTiltWeight;

            half4 _OutfitColorA;
            half4 _OutfitColorB;
            half4 _OutfitColorC;
            half4 _OutfitColorD;
            half4 _PantsColor;
            half4 _SkinColor;
            half4 _HairHatColor;
            half4 _AccentEmissionColor;
            float _AccentEmissionStrength;
            float _ColorVariation;

            float _AmbientMin;
            half4 _RimColor;
            float _RimIntensity;

            float4 _TargetPosOS;
            float4 _DelayedTargetPosOS;
            float _LookAtWeight;
            float _ReactionLagWeight;

            float _AnimSpeed;
            float _Excitement;
            float _JumpHeight;
            float _JumpRatio;
            float _SwayAmount;
            float _ArmWaveAmount;
            float _HeightVariation;

            float _WaveIntensity;
            float _WaveSpeed;
            float _WaveCount;
        CBUFFER_END

        // 2D Capsule Signed Distance Function
        float sdCapsule2D(float2 p, float2 a, float2 b, float r)
        {
            float2 pa = p - a;
            float2 ba = b - a;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
            return length(pa - ba * h) - r;
        }

        // 2D Rounded Box Signed Distance Function
        float sdRoundBox2D(float2 p, float2 b, float r)
        {
            float2 q = abs(p) - b;
            return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
        }

        // Compute billboard vertex position & gaze parameters
        void EvaluateBillboardVertex(
            float4 vColor,
            float4 uv0,
            float4 uv1,
            float quadScale,
            out float3 outPosOS,
            out float3 outNormalOS,
            out float2 outQuadUV,
            out float4 outAnimParams, // x: seedA, y: seedB, z: excitement, w: styleId
            out float2 outGazeAngles) // x: gazeYaw (-1..1), y: gazePitch (-1..1)
        {
            float3 pivotOS = uv0.xyz;
            float baseYaw  = uv0.w;

            float seedA   = uv1.x;
            float seedB   = uv1.y;
            float angle01 = uv1.z;
            float row01   = uv1.w;

            float cornerX = vColor.r; // 0..1
            float cornerY = vColor.g; // 0..1
            float planeId = vColor.b; // 0 = main quad, 1 = cross quad
            float styleId = vColor.a;

            outQuadUV = float2(cornerX, cornerY);

            // 1. Scale variation
            float hScale = lerp(1.0 - _HeightVariation, 1.0 + _HeightVariation, frac(seedB * 3.73)) * quadScale;
            float wScale = lerp(1.0 - _HeightVariation * 0.65, 1.0 + _HeightVariation * 0.65, frac(seedA * 5.17)) * quadScale;

            // 2. Time, Excitement & Stadium Wave
            float t = _Time.y * _AnimSpeed;
            float wavePhase = frac(angle01 * _WaveCount - _Time.y * _WaveSpeed + row01 * 0.06);
            float waveFactor = smoothstep(0.16, 0.0, abs(wavePhase - 0.5)) * _WaveIntensity;
            float indivExcite = saturate(_Excitement * lerp(0.75, 1.25, seedA) + waveFactor);
            outAnimParams = float4(seedA, seedB, indivExcite, styleId);

            // 3. Target Tracking with Per-Person Reaction Lag
            float lagFactor = frac(seedA * 7.13) * _ReactionLagWeight;
            float3 myTargetOS = lerp(_TargetPosOS.xyz, _DelayedTargetPosOS.xyz, lagFactor);

            float3 eyeOS = pivotOS + float3(0.0, 1.45 * hScale, 0.0);
            float3 toTarget = myTargetOS - eyeOS;
            float horizDist = max(length(toTarget.xz), 0.01);

            float sBase, cBase;
            sincos(baseYaw, sBase, cBase);
            float2 seatRightXZ = float2(cBase, -sBase);
            float2 seatFwdXZ   = float2(sBase,  cBase);

            // Camera vector in Object Space
            float3 pivotWS = TransformObjectToWorld(pivotOS);
            float3 toCamWS = GetCameraPositionWS() - pivotWS;
            float3 toCamOS = TransformWorldToObjectDir(normalize(toCamWS));

            // Determine Billboard Basis (rightOS, upOS, normalOS)
            float3 rightOS;
            float3 upOS = float3(0.0, 1.0, 0.0);
            float3 fwdOS;

            if (_BillboardOrientationMode < 0.5)
            {
                // Mode 0: Hybrid Camera-Facing + Sky-View Vertical Tilt Compensation
                float2 camXZ = toCamOS.xz;
                float camXZLen = length(camXZ);
                float2 faceDirXZ = (camXZLen > 0.001) ? (camXZ / camXZLen) : seatFwdXZ;

                rightOS = float3(faceDirXZ.y, 0.0, -faceDirXZ.x);
                fwdOS   = float3(faceDirXZ.x, 0.0,  faceDirXZ.y);

                // Tilt top of quad slightly toward camera when viewed from high above so it never looks paper-thin
                float skyTilt = saturate(toCamOS.y) * _VerticalTiltWeight;
                upOS  = normalize(float3(-faceDirXZ.x * skyTilt, 1.0, -faceDirXZ.y * skyTilt));
                fwdOS = normalize(cross(rightOS, upOS));
            }
            else if (_BillboardOrientationMode < 1.5)
            {
                // Mode 1: Pure Cylindrical Y-Axis Billboard
                float2 camXZ = toCamOS.xz;
                float camXZLen = length(camXZ);
                float2 faceDirXZ = (camXZLen > 0.001) ? (camXZ / camXZLen) : seatFwdXZ;

                rightOS = float3(faceDirXZ.y, 0.0, -faceDirXZ.x);
                upOS    = float3(0.0, 1.0, 0.0);
                fwdOS   = float3(faceDirXZ.x, 0.0,  faceDirXZ.y);
            }
            else
            {
                // Mode 2: Seat / Target-Oriented Quad (and 90-degree Cross Quad if planeId > 0.5)
                float2 dirXZ = (horizDist > 0.1) ? normalize(toTarget.xz) : seatFwdXZ;
                dirXZ = normalize(lerp(seatFwdXZ, dirXZ, _LookAtWeight * 0.75));
                if (planeId > 0.5)
                {
                    dirXZ = float2(dirXZ.y, -dirXZ.x);
                }
                rightOS = float3(dirXZ.y, 0.0, -dirXZ.x);
                upOS    = float3(0.0, 1.0, 0.0);
                fwdOS   = float3(dirXZ.x, 0.0,  dirXZ.y);
            }

            // Compute relative gaze angles (Yaw & Pitch) in the Billboard's visual frame
            float targetAlongRight = dot(toTarget.xz, rightOS.xz) / horizDist;
            float rawPitch = atan2(toTarget.y, horizDist);

            float microYaw   = sin(t * lerp(0.9, 1.8, seedB) + seedA * 6.28318) * 0.08;
            float microPitch = cos(t * lerp(1.1, 2.0, seedA) + seedB * 6.28318) * 0.05;

            float gazeYaw   = clamp(targetAlongRight + microYaw, -1.0, 1.0) * _LookAtWeight;
            float gazePitch = clamp((rawPitch / 1.05) + microPitch, -0.8, 1.0) * _LookAtWeight;
            outGazeAngles = float2(gazeYaw, gazePitch);

            // 4. Quad Vertex Animation (Jump, Sway, Upper-Body Target Lean)
            float canJump = step(1.0 - _JumpRatio, frac(seedB * 9.17 + indivExcite * 0.35));
            float jumpFreq = lerp(3.4, 6.2, frac(seedA * 3.1));
            float rawSin = sin(t * jumpFreq + seedA * 12.56637);
            float hop = pow(max(0.0, rawSin), 1.7) * _JumpHeight * saturate(indivExcite * 1.15 + 0.15) * canJump;

            float swayFreq = lerp(1.6, 3.6, frac(seedA * 2.9));
            float swayOffset = sin(t * swayFreq + seedB * 6.28318) * _SwayAmount * (0.45 + 0.65 * indivExcite) * cornerY;

            // Lean upper body toward the target horizontally & vertically
            float targetLeanX = gazeYaw * 0.14 * cornerY;

            float quadWidth  = 0.92 * wScale;
            float quadHeight = 1.92 * hScale;

            float localX = (cornerX - 0.5) * quadWidth + swayOffset + targetLeanX;
            float localY = cornerY * quadHeight + hop;

            outPosOS = pivotOS + rightOS * localX + upOS * localY;
            outNormalOS = normalize(fwdOS + upOS * (0.25 + 0.25 * max(0.0, gazePitch)));
        }

        // Procedural 2.5D Spectator Silhouette + Dynamic Gaze & Arm Animation inside the Quad
        bool EvaluateSpectatorBillboardMask(
            float2 uv,
            float4 animParams,
            float2 gazeAngles,
            out float outPartId,
            out float3 outLocalNormal)
        {
            outPartId = 0.25;
            outLocalNormal = float3(0.0, 0.15, 0.98);

            if (_UseCustomTexture > 0.5)
            {
                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (texCol.a < _AlphaCutoff) return false;
                outPartId = (uv.y < 0.42) ? 0.0 : ((uv.y > 0.78) ? 0.50 : 0.25);
                return true;
            }

            float seedA     = animParams.x;
            float seedB     = animParams.y;
            float excite    = animParams.z;
            float styleId   = animParams.w;
            float gazeYaw   = gazeAngles.x; // -1..1
            float gazePitch = gazeAngles.y; // -0.8..1.0 (Positive = looking UP at sky)

            float t = _Time.y * _AnimSpeed;

            // Map UV (0..1, 0..1) to metric body space: x in [-0.46, +0.46], y in [0.0, 1.92]
            float2 p = float2((uv.x - 0.5) * 0.92, uv.y * 1.92);

            // 1. Legs (y: 0.00 .. 0.76)
            float dLeftLeg  = sdCapsule2D(p, float2(-0.10, 0.02), float2(-0.095, 0.74), 0.068);
            float dRightLeg = sdCapsule2D(p, float2( 0.10, 0.02), float2( 0.095, 0.74), 0.068);
            float dLegs = min(dLeftLeg, dRightLeg);

            // 2. Torso (y: 0.70 .. 1.34) - shifts subtly with gazeYaw
            float2 torsoCenter = float2(gazeYaw * 0.035, 1.03 + gazePitch * 0.02);
            // Tapered torso (wider at shoulders)
            float torsoHalfW = lerp(0.145, 0.195, saturate((p.y - 0.72) / 0.60));
            float dTorso = sdRoundBox2D(p - torsoCenter, float2(torsoHalfW, 0.29), 0.055);

            // 3. Head & Neck (y: 1.35 .. 1.72) - shifts with gazeYaw & gazePitch!
            float2 neckA = float2(gazeYaw * 0.04, 1.32);
            float2 headCenter = float2(gazeYaw * 0.085, 1.53 + gazePitch * 0.055);
            float dNeck = sdCapsule2D(p, neckA, headCenter, 0.058);
            float dHead = length((p - headCenter) * float2(1.05, 0.92)) - 0.112;

            // Cap / Hair & Brim / Visor (shifts to show whether spectator is looking UP at sky or DOWN at arena!)
            float2 brimCenter = headCenter + float2(gazeYaw * 0.065, 0.035 + gazePitch * 0.065);
            float dCapBrim = sdRoundBox2D(p - brimCenter, float2(0.095, 0.026), 0.015);

            // 4. Articulated Left & Right Arms (wave independently & point toward sky when character is high!)
            bool leftActive  = !(seedB >= 0.30 && seedB < 0.60 && excite < 0.55);
            bool rightActive = !(seedB >= 0.60 && seedB < 0.82 && excite < 0.55);
            if (seedB >= 0.82 && excite < 0.45)
            {
                leftActive = false;
                rightActive = false;
            }

            float waveFreq = lerp(3.5, 7.5, frac(seedA * 4.3)) * (0.8 + 0.5 * excite);
            float waveL = sin(t * waveFreq + seedA * 6.28318 + 1.7) * 0.35 * _ArmWaveAmount;
            float waveR = sin(t * waveFreq + seedA * 6.28318) * 0.35 * _ArmWaveAmount;

            // Base arm angle: Down when calm, raised skyward (+Y) when cheering or looking up at descending character
            float raiseFactor = saturate(excite * 0.85 + max(0.0, gazePitch) * 0.55 + 0.20);

            float2 shoulderL = torsoCenter + float2(-0.185, 0.24);
            float2 shoulderR = torsoCenter + float2( 0.185, 0.24);

            float angleL = leftActive
                ? lerp(-1.25, 1.15, raiseFactor) + waveL - gazeYaw * 0.30
                : -1.35 + sin(t * 1.5 + seedA * 6.28) * 0.08;
            float angleR = rightActive
                ? lerp(-1.25, 1.15, raiseFactor) + waveR + gazeYaw * 0.30
                : -1.35 + cos(t * 1.5 + seedB * 6.28) * 0.08;

            float2 handL = shoulderL + float2(-cos(angleL) * 0.22, sin(angleL) * 0.42);
            float2 handR = shoulderR + float2( cos(angleR) * 0.22, sin(angleR) * 0.42);

            float dArmL = sdCapsule2D(p, shoulderL, handL, 0.048);
            float dArmR = sdCapsule2D(p, shoulderR, handR, 0.048);

            // Optional Racing Flag / Rally Towel in Right Hand (styleId == 1)
            float dProp = 10.0;
            if (styleId > 0.20 && styleId < 0.50)
            {
                float2 flagTip = handR + float2(0.08 + sin(t * waveFreq) * 0.05, 0.16);
                dProp = sdCapsule2D(p, handR, flagTip, 0.055);
            }

            // Combine all body SDF parts
            float minDist = min(min(dLegs, dTorso), min(min(dNeck, dHead), min(dArmL, min(dArmR, dCapBrim))));
            minDist = min(minDist, dProp);

            if (minDist > 0.0)
            {
                return false; // Outside spectator silhouette -> discard pixel
            }

            // Determine which body part won & compute pseudo-3D normal for convincing lighting
            if (dProp <= minDist + 0.004)
            {
                outPartId = 1.00; // Flag / Towel
                outLocalNormal = float3(0.0, 0.3, 0.95);
            }
            else if (dHead <= minDist + 0.008 || dCapBrim <= minDist + 0.008)
            {
                float2 relH = (p - headCenter) / 0.115;
                float nz = sqrt(max(0.05, 1.0 - dot(relH, relH)));
                outLocalNormal = normalize(float3(relH.x * 0.7, relH.y * 0.7 + gazePitch * 0.35, nz));

                // Distinguish Cap/Hair vs Cyber Visor vs Skin Face
                // When looking UP at the sky (gazePitch > 0.2), more of the face/visor is visible on the upper half
                float capThreshold = 0.02 + gazePitch * 0.045;
                if (styleId > 0.55 && abs(p.y - (headCenter.y + gazePitch * 0.02)) < 0.028 && abs(p.x - (headCenter.x + gazeYaw * 0.03)) < 0.085)
                {
                    outPartId = 1.00; // Cyber Visor
                }
                else if (dCapBrim < 0.0 || (p.y - headCenter.y) > capThreshold)
                {
                    outPartId = 0.75; // Cap / Hair
                }
                else
                {
                    outPartId = 0.50; // Skin / Face
                }
            }
            else if (dArmL <= minDist + 0.005 || dArmR <= minDist + 0.005)
            {
                // Forearm/hand tip is skin (0.50), sleeve is outfit (0.25)
                float distToHand = min(length(p - handL), length(p - handR));
                outPartId = (distToHand < 0.075) ? 0.50 : 0.25;
                outLocalNormal = normalize(float3((p.x - torsoCenter.x) * 1.5, 0.35, 0.85));
            }
            else if (dTorso <= minDist + 0.006 || dNeck <= minDist + 0.006)
            {
                outPartId = 0.25; // Torso Outfit
                float nx = clamp((p.x - torsoCenter.x) / 0.20, -0.8, 0.8);
                float ny = clamp((p.y - torsoCenter.y) / 0.32, -0.5, 0.7) + gazePitch * 0.25;
                outLocalNormal = normalize(float3(nx, ny, 0.85));
            }
            else
            {
                outPartId = 0.00; // Legs / Pants
                outLocalNormal = normalize(float3(p.x * 1.2, -0.1, 0.9));
            }

            return true;
        }
        ENDHLSL

        // =====================================================================
        // PASS 1: UniversalForward
        // =====================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 quadUV     : TEXCOORD2;
                float4 animParams : TEXCOORD3; // seedA, seedB, excite, styleId
                float2 gazeAngles : TEXCOORD4; // gazeYaw, gazePitch
                float  fogFactor  : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 posOS, nrmOS;
                float2 quadUV;
                float4 animParams;
                float2 gazeAngles;
                float quadScale = max(input.normalOS.y, 0.1);

                EvaluateBillboardVertex(input.color, input.uv0, input.uv1, quadScale,
                    posOS, nrmOS, quadUV, animParams, gazeAngles);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs nrmInputs   = GetVertexNormalInputs(nrmOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = nrmInputs.normalWS;
                output.quadUV     = quadUV;
                output.animParams = animParams;
                output.gazeAngles = gazeAngles;
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 PickOutfitColor(float seedB, float seedA)
            {
                float idx = frac(seedB * 4.0 + seedA * 0.37) * 4.0;
                if (idx < 1.0) return _OutfitColorA;
                if (idx < 2.0) return _OutfitColorB;
                if (idx < 3.0) return _OutfitColorC;
                return _OutfitColorD;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float partId;
                float3 localNrm;
                if (!EvaluateSpectatorBillboardMask(input.quadUV, input.animParams, input.gazeAngles, partId, localNrm))
                {
                    discard;
                }

                float seedA     = input.animParams.x;
                float seedB     = input.animParams.y;
                float excite    = input.animParams.z;
                float gazePitch = input.gazeAngles.y;
                float height01  = input.quadUV.y;

                half3 outfitCol = PickOutfitColor(seedB, seedA).rgb;
                float shadeVar  = lerp(1.0 - _ColorVariation, 1.0 + _ColorVariation, frac(seedA * 11.3 + seedB * 5.7));

                half3 baseAlbedo;
                half3 emission = half3(0, 0, 0);

                if (_UseCustomTexture > 0.5)
                {
                    half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.quadUV);
                    baseAlbedo = texCol.rgb * outfitCol * shadeVar;
                }
                else if (partId < 0.12)
                {
                    baseAlbedo = _PantsColor.rgb * shadeVar;
                }
                else if (partId < 0.38)
                {
                    baseAlbedo = outfitCol * shadeVar;
                }
                else if (partId < 0.62)
                {
                    float skinVar = lerp(0.88, 1.08, frac(seedA * 6.1));
                    baseAlbedo = _SkinColor.rgb * skinVar;
                }
                else if (partId < 0.88)
                {
                    bool wearTeamCap = (frac(seedB * 8.3) > 0.45);
                    baseAlbedo = wearTeamCap ? (outfitCol * 0.85) : (_HairHatColor.rgb * shadeVar);
                }
                else
                {
                    baseAlbedo = lerp(outfitCol, _AccentEmissionColor.rgb, 0.65);
                    emission = _AccentEmissionColor.rgb * (_AccentEmissionStrength * (0.7 + 0.6 * excite));
                }

                // Combine billboard world normal with pseudo-3D silhouette normal
                float3 N = normalize(input.normalWS + float3(localNrm.x * 0.35, localNrm.y * 0.45, 0.0));
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(N, mainLight.direction));
                float halfLambert = NdotL * 0.65 + 0.35;

                float verticalAO = lerp(0.55, 1.08, smoothstep(0.0, 0.85, height01));
                float skyCatch = lerp(0.92, 1.18, saturate(gazePitch * 0.5 + 0.5)) * smoothstep(0.50, 0.95, height01);

                half3 ambient = max(SampleSH(N), half3(_AmbientMin, _AmbientMin, _AmbientMin));
                half3 diffuse = baseAlbedo * (mainLight.color * (halfLambert * mainLight.shadowAttenuation) + ambient) * verticalAO * skyCatch;

                // Edge rim highlight on the 2D silhouette
                float edgeRim = saturate(length(localNrm.xy)) * _RimIntensity * smoothstep(0.3, 0.95, height01);
                half3 finalColor = diffuse + _RimColor.rgb * edgeRim + emission;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 2: ShadowCaster
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 quadUV     : TEXCOORD0;
                float4 animParams : TEXCOORD1;
                float2 gazeAngles : TEXCOORD2;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS, nrmOS;
                float quadScale = max(input.normalOS.y, 0.1);
                EvaluateBillboardVertex(input.color, input.uv0, input.uv1, quadScale,
                    posOS, nrmOS, output.quadUV, output.animParams, output.gazeAngles);

                float3 posWS = TransformObjectToWorld(posOS);
                float3 nrmWS = TransformObjectToWorldNormal(nrmOS);
                Light mainLight = GetMainLight();
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, mainLight.direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float partId;
                float3 localNrm;
                if (!EvaluateSpectatorBillboardMask(input.quadUV, input.animParams, input.gazeAngles, partId, localNrm))
                {
                    discard;
                }
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 3: DepthOnly
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 quadUV     : TEXCOORD0;
                float4 animParams : TEXCOORD1;
                float2 gazeAngles : TEXCOORD2;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS, nrmOS;
                float quadScale = max(input.normalOS.y, 0.1);
                EvaluateBillboardVertex(input.color, input.uv0, input.uv1, quadScale,
                    posOS, nrmOS, output.quadUV, output.animParams, output.gazeAngles);
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                float partId;
                float3 localNrm;
                if (!EvaluateSpectatorBillboardMask(input.quadUV, input.animParams, input.gazeAngles, partId, localNrm))
                {
                    discard;
                }
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 4: DepthNormals
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 quadUV     : TEXCOORD1;
                float4 animParams : TEXCOORD2;
                float2 gazeAngles : TEXCOORD3;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS, nrmOS;
                float quadScale = max(input.normalOS.y, 0.1);
                EvaluateBillboardVertex(input.color, input.uv0, input.uv1, quadScale,
                    posOS, nrmOS, output.quadUV, output.animParams, output.gazeAngles);
                output.positionCS = TransformObjectToHClip(posOS);
                output.normalWS   = TransformObjectToWorldNormal(nrmOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                float partId;
                float3 localNrm;
                if (!EvaluateSpectatorBillboardMask(input.quadUV, input.animParams, input.gazeAngles, partId, localNrm))
                {
                    discard;
                }
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

