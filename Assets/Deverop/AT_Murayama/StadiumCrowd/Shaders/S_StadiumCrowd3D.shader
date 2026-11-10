Shader "AT_Murayama/StadiumCrowd/S_StadiumCrowd3D"
{
    Properties
    {
        [Header(Crowd Color Palette)]
        _OutfitColorA ("Outfit Color 1 (Primary)", Color) = (0.15, 0.38, 0.82, 1.0)
        _OutfitColorB ("Outfit Color 2 (Secondary)", Color) = (0.85, 0.22, 0.25, 1.0)
        _OutfitColorC ("Outfit Color 3 (Light/Neutral)", Color) = (0.90, 0.92, 0.95, 1.0)
        _OutfitColorD ("Outfit Color 4 (Accent/Yellow)", Color) = (0.95, 0.72, 0.15, 1.0)
        _PantsColor   ("Lower Body / Pants Color", Color) = (0.14, 0.16, 0.20, 1.0)
        _SkinColor    ("Skin Base Color", Color) = (0.88, 0.72, 0.62, 1.0)
        _HairHatColor ("Hair & Cap Base Color", Color) = (0.18, 0.20, 0.25, 1.0)
        [HDR] _AccentEmissionColor ("Accent / Visor / Flag HDR Color", Color) = (0.2, 0.7, 1.0, 1.0)
        _AccentEmissionStrength ("Accent Emission Strength", Range(0.0, 3.0)) = 0.25
        _ColorVariation ("Individual Brightness Variation", Range(0.0, 0.5)) = 0.22

        [Header(Lighting and Visibility)]
        _AmbientMin ("Minimum Ambient Brightness", Range(0.05, 0.8)) = 0.32
        _RimColor ("Silhouette Rim Color", Color) = (0.65, 0.80, 1.0, 1.0)
        _RimPower ("Rim Power", Range(1.0, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity (Distance Readability)", Range(0.0, 1.5)) = 0.45

        [Header(LookAt Tracking)]
        _TargetPosOS ("Current LookAt Target (Object Space)", Vector) = (0, 30, 0, 1)
        _DelayedTargetPosOS ("Delayed LookAt Target (Object Space)", Vector) = (0, 35, 0, 1)
        _LookAtWeight ("Overall LookAt Tracking Weight", Range(0.0, 1.0)) = 1.0
        _ReactionLagWeight ("Individual Reaction Lag Spread", Range(0.0, 1.0)) = 0.65
        _MaxYawRad ("Max Horizontal Turn (Radians)", Range(0.2, 2.4)) = 1.45
        _MaxPitchUpRad ("Max Look Up Angle (Radians)", Range(0.1, 1.4)) = 1.10
        _MaxPitchDownRad ("Max Look Down Angle (Radians)", Range(0.1, 1.2)) = 0.75

        [Header(Crowd Motion and Variation)]
        _AnimSpeed ("Animation Master Speed", Range(0.0, 3.0)) = 1.0
        _Excitement ("Crowd Excitement Level", Range(0.0, 1.0)) = 0.45
        _JumpHeight ("Max Jump Height (m)", Range(0.0, 0.6)) = 0.22
        _JumpRatio ("Jumping Spectator Ratio", Range(0.0, 1.0)) = 0.55
        _SwayAmount ("Body Sway Angle (Radians)", Range(0.0, 0.35)) = 0.10
        _ArmWaveAmount ("Arm Wave Amplitude", Range(0.0, 1.5)) = 0.85
        _HeightVariation ("Body Scale Variation", Range(0.0, 0.25)) = 0.10

        [Header(Stadium Wave)]
        _WaveIntensity ("Stadium Mexican Wave Intensity", Range(0.0, 1.0)) = 0.0
        _WaveSpeed ("Stadium Wave Speed", Range(0.05, 1.5)) = 0.25
        _WaveCount ("Stadium Wave Repeat Count", Range(1.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
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
            float _RimPower;
            float _RimIntensity;

            float4 _TargetPosOS;
            float4 _DelayedTargetPosOS;
            float _LookAtWeight;
            float _ReactionLagWeight;
            float _MaxYawRad;
            float _MaxPitchUpRad;
            float _MaxPitchDownRad;

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

        // Rotate 2D vector (a, b) by angle rad
        void Rotate2D(inout float a, inout float b, float rad)
        {
            float s, c;
            sincos(rad, s, c);
            float na = a * c - b * s;
            float nb = a * s + b * c;
            a = na;
            b = nb;
        }

        // Core GPU Vertex Animation for Stadium Crowd
        void ApplyCrowdVertexAnimation(
            inout float3 positionOS,
            inout float3 normalOS,
            float4 vColor,
            float4 uv0,
            float4 uv1,
            out float outExcitement,
            out float outPitchNormalized)
        {
            float3 pivotOS = uv0.xyz;
            float baseYaw  = uv0.w;

            float seedA    = uv1.x; // 0..1 phase & speed seed
            float seedB    = uv1.y; // 0..1 style & color seed
            float angle01  = uv1.z; // 0..1 stadium azimuth
            float row01    = uv1.w; // 0..1 row depth

            float lookWeight = vColor.g;
            float armCode    = vColor.b;
            float height01   = vColor.a;

            // Basis vectors of this spectator's seat in Object Space
            float sBase, cBase;
            sincos(baseYaw, sBase, cBase);
            float2 rightXZ = float2(cBase, -sBase);
            float2 fwdXZ   = float2(sBase,  cBase);

            // Convert vertex offset from chunk space into spectator local space (+X right, +Y up, +Z forward)
            float3 relOS = positionOS - pivotOS;
            float3 localPos = float3(dot(relOS.xz, rightXZ), relOS.y, dot(relOS.xz, fwdXZ));
            float3 localNrm = float3(dot(normalOS.xz, rightXZ), normalOS.y, dot(normalOS.xz, fwdXZ));

            // 1. Individual Body Build & Height Variation
            float hScale = lerp(1.0 - _HeightVariation, 1.0 + _HeightVariation, frac(seedB * 3.73));
            float wScale = lerp(1.0 - _HeightVariation * 0.65, 1.0 + _HeightVariation * 0.65, frac(seedA * 5.17));
            localPos.y  *= hScale;
            localPos.xz *= wScale;

            // 2. Time, Excitement & Stadium Mexican Wave
            float t = _Time.y * _AnimSpeed;
            float wavePhase = frac(angle01 * _WaveCount - _Time.y * _WaveSpeed + row01 * 0.06);
            float waveFactor = smoothstep(0.16, 0.0, abs(wavePhase - 0.5)) * _WaveIntensity;

            float indivExcite = saturate(_Excitement * lerp(0.75, 1.25, seedA) + waveFactor);
            outExcitement = indivExcite;

            // 3. Target Tracking with Per-Person Reaction Lag
            float lagFactor = frac(seedA * 7.13) * _ReactionLagWeight;
            float3 myTargetOS = lerp(_TargetPosOS.xyz, _DelayedTargetPosOS.xyz, lagFactor);

            float3 eyeOS = pivotOS + float3(0.0, 1.48 * hScale, 0.0);
            float3 toTarget = myTargetOS - eyeOS;

            float targetRight = dot(toTarget.xz, rightXZ);
            float targetFwd   = dot(toTarget.xz, fwdXZ);
            float horizDist   = max(length(toTarget.xz), 0.01);

            // Subtle natural head/gaze micro-movement so spectators never freeze completely
            float microYaw   = sin(t * lerp(0.9, 1.8, seedB) + seedA * 6.28318) * 0.08;
            float microPitch = cos(t * lerp(1.1, 2.0, seedA) + seedB * 6.28318) * 0.04;

            float rawYaw   = atan2(targetRight, targetFwd) + microYaw;
            float rawPitch = atan2(toTarget.y, horizDist) + microPitch;

            float clampedYaw   = clamp(rawYaw, -_MaxYawRad, _MaxYawRad) * _LookAtWeight;
            float clampedPitch = clamp(rawPitch, -_MaxPitchDownRad, _MaxPitchUpRad) * _LookAtWeight;
            outPitchNormalized = saturate((clampedPitch + _MaxPitchDownRad) / max(_MaxPitchUpRad + _MaxPitchDownRad, 0.01));

            // 4. Articulated Arm Animation (Cheering / Pointing / Waving)
            if (armCode > 0.005)
            {
                bool isRightArm = (armCode > 0.505);
                float armWeight = isRightArm ? saturate((armCode - 0.50) * 2.0) : saturate(armCode * 2.0);

                // Behavior archetype based on seedB:
                // 0.00..0.30: Double-arm cheer
                // 0.30..0.60: Right-arm dominant cheer
                // 0.60..0.82: Left-arm dominant cheer
                // 0.82..1.00: Calm until high excitement or stadium wave
                float armActive = 1.0;
                if (seedB >= 0.30 && seedB < 0.60 && !isRightArm) armActive = 0.25 + 0.75 * indivExcite;
                else if (seedB >= 0.60 && seedB < 0.82 && isRightArm) armActive = 0.25 + 0.75 * indivExcite;
                else if (seedB >= 0.82) armActive = smoothstep(0.35, 0.85, indivExcite);

                float3 shoulderPivot = float3((isRightArm ? 0.21 : -0.21) * wScale, 1.30 * hScale, 0.0);
                float3 armVec = localPos - shoulderPivot;

                // Raise arm forward & upward toward target pitch + rhythmic wave
                float waveFreq = lerp(3.5, 7.5, frac(seedA * 4.3)) * (0.8 + 0.5 * indivExcite);
                float waveOsc  = sin(t * waveFreq + seedA * 6.28318 + (isRightArm ? 0.0 : 1.7));

                // Base raise angle: arms go from resting down to raised forward/skyward
                float raiseBase = lerp(0.25, 2.35, saturate(indivExcite * 0.85 + 0.25)) * armActive;
                // Follow vertical look pitch so arms point toward descending character
                float pitchFollow = max(0.0, clampedPitch) * 0.45 * armActive;
                float armPitch = (raiseBase + pitchFollow + waveOsc * 0.42 * _ArmWaveAmount * armActive) * armWeight;

                // Slight outward splay and side-to-side wave
                float armRoll = ((isRightArm ? -0.18 : 0.18) + cos(t * waveFreq * 0.5 + seedB * 6.28) * 0.16 * _ArmWaveAmount) * armWeight * armActive;

                // Rotate around shoulder X axis (forward/upward raise)
                Rotate2D(armVec.z, armVec.y, armPitch);
                Rotate2D(localNrm.z, localNrm.y, armPitch);

                // Rotate around shoulder Z axis (side wave)
                Rotate2D(armVec.x, armVec.y, armRoll);
                Rotate2D(localNrm.x, localNrm.y, armRoll);

                localPos = shoulderPivot + armVec;
            }

            // 5. Upper Body & Head Pitch (Tilting Up to Sky / Down to Stadium Floor)
            float chestPivotY = 1.05 * hScale;
            if (localPos.y > chestPivotY && lookWeight > 0.01)
            {
                float pitchFactor = saturate((localPos.y - chestPivotY) / (0.65 * hScale)) * lookWeight;
                float tiltAngle = clampedPitch * pitchFactor;

                float dy = localPos.y - chestPivotY;
                float dz = localPos.z;
                // Positive tiltAngle = look UP (+Y, -Z tilt back; face points +Y/+Z)
                Rotate2D(dz, dy, tiltAngle);
                localPos.y = chestPivotY + dy;
                localPos.z = dz;

                Rotate2D(localNrm.z, localNrm.y, tiltAngle);
            }

            // 6. Body Rhythmic Sway (Left-Right Groove)
            float swayFreq = lerp(1.6, 3.6, frac(seedA * 2.9));
            float swayAngle = sin(t * swayFreq + seedB * 6.28318) * _SwayAmount * (0.45 + 0.65 * indivExcite) * height01;
            Rotate2D(localPos.x, localPos.y, swayAngle);
            Rotate2D(localNrm.x, localNrm.y, swayAngle);

            // 7. Horizontal Yaw Tracking (Feet Stay Planted, Torso & Head Turn Toward Target)
            float yawFactor = saturate(lookWeight * 1.15);
            float turnYaw = clampedYaw * yawFactor;
            Rotate2D(localPos.z, localPos.x, turnYaw);
            Rotate2D(localNrm.z, localNrm.x, turnYaw);

            // 8. Individual Jumping (Phase-Desynchronized Hopping)
            float canJump = step(1.0 - _JumpRatio, frac(seedB * 9.17 + indivExcite * 0.35));
            float jumpFreq = lerp(3.4, 6.2, frac(seedA * 3.1));
            float rawSin = sin(t * jumpFreq + seedA * 12.56637);
            float hop = pow(max(0.0, rawSin), 1.7) * _JumpHeight * saturate(indivExcite * 1.15 + 0.15) * canJump;
            // Slight knee-bend squash before jump
            float squash = min(0.0, rawSin) * 0.03 * height01 * canJump * indivExcite;
            localPos.y += hop + squash;

            // Reconstruct Object Space position and normal
            positionOS.xz = pivotOS.xz + rightXZ * localPos.x + fwdXZ * localPos.z;
            positionOS.y  = pivotOS.y  + localPos.y;

            normalOS.xz   = rightXZ * localNrm.x + fwdXZ * localNrm.z;
            normalOS.y    = localNrm.y;
            normalOS      = normalize(normalOS);
        }
        ENDHLSL

        // =====================================================================
        // PASS 1: UniversalForward
        // =====================================================================
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
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
                float4 vColor     : TEXCOORD2;
                float4 params     : TEXCOORD3; // x: seedA, y: seedB, z: excitement, w: pitchNormalized
                float  fogFactor  : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                float excite, pitchNorm;

                ApplyCrowdVertexAnimation(posOS, nrmOS, input.color, input.uv0, input.uv1, excite, pitchNorm);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs nrmInputs   = GetVertexNormalInputs(nrmOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = nrmInputs.normalWS;
                output.vColor     = input.color;
                output.params     = float4(input.uv1.x, input.uv1.y, excite, pitchNorm);
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
                float partId    = input.vColor.r;
                float height01  = input.vColor.a;
                float seedA     = input.params.x;
                float seedB     = input.params.y;
                float excite    = input.params.z;
                float pitchNorm = input.params.w;

                // Determine base albedo by body part ID encoded in vertex color.r
                // 0.00: Pants/Legs, 0.25: Torso/Outfit, 0.50: Skin/Head, 0.75: Hair/Cap, 1.00: Accent/Flag/Visor
                half3 baseAlbedo;
                half3 emission = half3(0, 0, 0);

                half3 outfitCol = PickOutfitColor(seedB, seedA).rgb;
                float shadeVar  = lerp(1.0 - _ColorVariation, 1.0 + _ColorVariation, frac(seedA * 11.3 + seedB * 5.7));

                if (partId < 0.12)
                {
                    baseAlbedo = _PantsColor.rgb * shadeVar;
                }
                else if (partId < 0.38)
                {
                    baseAlbedo = outfitCol * shadeVar;
                }
                else if (partId < 0.62)
                {
                    // Skin tone with subtle natural variation
                    float skinVar = lerp(0.88, 1.08, frac(seedA * 6.1));
                    baseAlbedo = _SkinColor.rgb * skinVar;
                }
                else if (partId < 0.88)
                {
                    // Cap / Hair: half of spectators wear team-colored caps, half have dark/natural hair
                    bool wearTeamCap = (frac(seedB * 8.3) > 0.45);
                    baseAlbedo = wearTeamCap ? (outfitCol * 0.85) : (_HairHatColor.rgb * shadeVar);
                }
                else
                {
                    // Accent part (Flag / Towel / Visor)
                    baseAlbedo = lerp(outfitCol, _AccentEmissionColor.rgb, 0.65);
                    emission = _AccentEmissionColor.rgb * (_AccentEmissionStrength * (0.7 + 0.6 * excite));
                }

                // Lighting calculation
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(N, mainLight.direction));
                float halfLambert = NdotL * 0.65 + 0.35;

                // Vertical ambient occlusion (darker near feet inside bleachers, brighter at head/shoulders)
                float verticalAO = lerp(0.55, 1.08, smoothstep(0.0, 0.85, height01));

                // Subtle skyward face highlight when looking up at the descending character
                // Makes head pitch shift clearly readable even from high altitude
                float skyCatch = lerp(0.92, 1.18, pitchNorm) * smoothstep(0.55, 0.95, height01);

                half3 ambient = max(SampleSH(N), half3(_AmbientMin, _AmbientMin, _AmbientMin));
                half3 diffuse = baseAlbedo * (mainLight.color * (halfLambert * mainLight.shadowAttenuation) + ambient) * verticalAO * skyCatch;

                // Fresnel rim light for clean crowd silhouette separation at distance
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimIntensity * smoothstep(0.3, 0.95, height01);
                half3 rimTerm = _RimColor.rgb * rim;

                half3 finalColor = diffuse + rimTerm + emission;
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
            Cull Back

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
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                float excite, pitchNorm;
                ApplyCrowdVertexAnimation(posOS, nrmOS, input.color, input.uv0, input.uv1, excite, pitchNorm);

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
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 3: DepthOnly (Required for URP Forward+ Depth Prepass)
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

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
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                float excite, pitchNorm;
                ApplyCrowdVertexAnimation(posOS, nrmOS, input.color, input.uv0, input.uv1, excite, pitchNorm);
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 4: DepthNormals (Required for URP Forward+ SSAO / Normals)
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

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
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                float excite, pitchNorm;
                ApplyCrowdVertexAnimation(posOS, nrmOS, input.color, input.uv0, input.uv1, excite, pitchNorm);
                output.positionCS = TransformObjectToHClip(posOS);
                output.normalWS   = TransformObjectToWorldNormal(nrmOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

