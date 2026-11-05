Shader "AT_Murayama/DayNightCity/RealisticCityBuilding"
{
    Properties
    {
        [Header(Day Night Blend Control)]
        [Toggle] _AutoDayNight ("Auto Day/Night from Sun Light", Float) = 0.0
        _NightFactor ("Night Factor (0=Day, 1=Night)", Range(0.0, 1.0)) = 0.0

        [Header(Daytime Exterior Facade)]
        _WallColor ("Wall Panel Color (Day)", Color) = (0.86, 0.88, 0.91, 1.0)
        _WallMetallic ("Wall Metallic", Range(0.0, 1.0)) = 0.12
        _WallSmoothness ("Wall Smoothness", Range(0.0, 1.0)) = 0.38
        _GlassColorDay ("Glass Color (Day)", Color) = (0.05, 0.08, 0.12, 1.0)
        _GlassMetallic ("Glass Metallic (Reflection)", Range(0.0, 1.0)) = 0.75
        _GlassSmoothness ("Glass Smoothness (Reflection)", Range(0.0, 1.0)) = 0.94
        _MullionColor ("Mullion Frame Color", Color) = (0.16, 0.17, 0.19, 1.0)
        _RoofColor ("Rooftop Surface Color", Color) = (0.18, 0.20, 0.23, 1.0)

        [Header(Facade Grid Scale)]
        _FloorHeight ("Floor Height (Meters)", Float) = 3.8
        _WindowWidth ("Window Bay Width (Meters)", Float) = 1.8
        _SpandrelRatio ("Spandrel Panel Ratio", Range(0.1, 0.5)) = 0.22
        _MullionRatio ("Mullion Frame Ratio", Range(0.02, 0.25)) = 0.08
        _CornerColumnWidth ("Corner Column Width (Meters)", Float) = 0.85
        _LobbyHeight ("Ground Lobby Zone Height (Meters)", Float) = 10.0
        _BuildingSeed ("Building Variation Seed", Float) = 1.0

        [Header(Nighttime Office Lights)]
        _WindowLitRate ("Window Lit Probability", Range(0.0, 1.0)) = 0.32
        _WindowIntensity ("Window Emission Intensity", Range(0.0, 8.0)) = 2.4
        [HDR] _WindowColorWarm ("Office Light Warm", Color) = (2.6, 1.9, 1.1, 1.0)
        [HDR] _WindowColorCool ("Office Light Cool", Color) = (1.1, 2.2, 2.9, 1.0)

        [Header(Nighttime Skyline Accents)]
        [HDR] _AccentColor ("Skyline Accent Color", Color) = (0.2, 2.8, 4.5, 1.0)
        _AccentIntensity ("Accent Glow Intensity", Range(0.0, 8.0)) = 2.2
        [HDR] _LobbyLightColor ("Ground Lobby Light Color", Color) = (2.8, 2.2, 1.2, 1.0)
        _LobbyIntensity ("Ground Lobby Glow Intensity", Range(0.0, 6.0)) = 1.8
        [Toggle] _AviationBeaconEnable ("Rooftop Aviation Beacon", Float) = 1.0

        [Header(Procedural Neon Sign)]
        [Toggle] _NeonSignEnable ("Enable Neon Signboard", Float) = 1.0
        [HDR] _NeonSignColor ("Neon Sign Color", Color) = (3.8, 0.4, 1.5, 1.0)
        _NeonSignIntensity ("Neon Sign Intensity", Range(0.0, 8.0)) = 2.5

        [HideInInspector] _CityGroundY ("City Ground Level Y", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite On
        ZTest LEqual
        Cull Back

        // ─────────────────────────────────────────────────────────────
        // 1. UniversalForward Lit Pass (URP Forward+ / PBR Complete)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "RealisticCityBuildingInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 posLocalM  : TEXCOORD2; // xyz: local pos in meters, w: elevation above ground
                float3 bldgScale  : TEXCOORD3;
                float3 normalOS   : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34f, 456.21f));
                p += dot(p, p + 45.32f);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS   = input.normalOS;

                // 各軸のスケールをワールド変換行列から算出
                float3 scale = float3(
                    length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                    length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)),
                    length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))
                );
                output.bldgScale = max(scale, float3(0.01f, 0.01f, 0.01f));

                // 底面のワールド座標Yから、都市基準面（_CityGroundY）に対する段差・高さを算出
                float3 bottomOS = float3(0.0f, -0.5f, 0.0f);
                float bottomWS_Y = TransformObjectToWorld(bottomOS).y;
                float elevation = max(0.0f, bottomWS_Y - _CityGroundY);
                output.posLocalM = float4(input.positionOS.xyz * output.bldgScale, elevation);

                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(posInputs);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 nrmWS = normalize(input.normalWS);
                float3 nrmOS = normalize(input.normalOS);
                float3 posLocalM = input.posLocalM.xyz;
                float3 bldgScale = input.bldgScale;
                float elevation = input.posLocalM.w;

                // 昼夜係数：太陽光角度から自動算出またはマテリアルプロパティ
                float night = _NightFactor;
                if (_AutoDayNight > 0.5f)
                {
                    night = saturate((0.15f - _MainLightPosition.y) / 0.25f);
                }

                // インスタンス（棟）ごとの空間座標から自動シードを生成（SRP Batcherを100%維持しながら棟ごとの完全ランダム化を実現）
                float3 instancePivot = TransformObjectToWorld(float3(0.0f, 0.0f, 0.0f));
                float instanceSeed = Hash21(floor(instancePivot.xz * 0.1f)) * 1000.0f + _BuildingSeed;

                // ─────────────────────────────────────────────────────
                // 1. 建築部位の判定（屋上 / 基礎 / 垂直外壁）
                // ─────────────────────────────────────────────────────
                bool isRoof = (nrmOS.y > 0.6f);
                bool isGroundBase = (nrmOS.y < -0.6f);

                float hFromBase = posLocalM.y + bldgScale.y * 0.5f + elevation; // 地上からの高さ（0〜bldgScale.y + elevation）
                float distToRoof = max(0.0f, bldgScale.y * 0.5f - posLocalM.y); // ブロック頂部までの距離

                // 外壁面の水平座標・面幅（X面 or Z面）
                float faceWidth;
                float posHoriz;
                if (abs(nrmOS.x) > 0.5f)
                {
                    faceWidth = bldgScale.z;
                    posHoriz = posLocalM.z + faceWidth * 0.5f;
                }
                else
                {
                    faceWidth = bldgScale.x;
                    posHoriz = posLocalM.x + faceWidth * 0.5f;
                }

                float distToCorner = min(posHoriz, max(0.0f, faceWidth - posHoriz));

                // ─────────────────────────────────────────────────────
                // 2. サーフェス属性（Albedo, Metallic, Smoothness, Emission）
                // ─────────────────────────────────────────────────────
                float3 albedo = _WallColor.rgb;
                float metallic = _WallMetallic;
                float smoothness = _WallSmoothness;
                float3 emission = float3(0.0f, 0.0f, 0.0f);

                if (isRoof)
                {
                    // 屋上（防水アスファルト・設備エリア・ヘリポート）
                    albedo = _RoofColor.rgb;
                    metallic = 0.05f;
                    smoothness = 0.24f;

                    // 屋上パラペット外周部
                    float roofEdgeDist = min(bldgScale.x * 0.5f - abs(posLocalM.x), bldgScale.z * 0.5f - abs(posLocalM.z));
                    if (roofEdgeDist < 0.6f)
                    {
                        albedo = _WallColor.rgb;
                        metallic = _WallMetallic;
                        smoothness = _WallSmoothness;
                    }

                    // ヘリポートサークル・ターゲットマーク
                    float r = length(posLocalM.xz);
                    float helipadRing = 1.0f - smoothstep(0.0f, 0.35f, abs(r - 7.0f));
                    albedo = lerp(albedo, float3(0.85f, 0.85f, 0.2f), helipadRing * 0.65f);

                    // 航空障害灯（屋上4隅）
                    if (roofEdgeDist < 1.4f && _AviationBeaconEnable > 0.5f)
                    {
                        float beaconDist = length(float2(abs(posLocalM.x) - (bldgScale.x * 0.5f - 0.7f),
                                                         abs(posLocalM.z) - (bldgScale.z * 0.5f - 0.7f)));
                        float beaconFw = clamp(fwidth(beaconDist), 0.001f, 0.5f);
                        float beaconMask = 1.0f - smoothstep(0.4f - beaconFw, 0.4f + beaconFw, beaconDist);
                        float flash = step(0.55f, frac(_Time.y * 0.8f));
                        emission += float3(4.5f, 0.1f, 0.1f) * (beaconMask * flash * night);
                    }
                }
                else if (isGroundBase)
                {
                    // ビル底面
                    albedo = float3(0.1f, 0.1f, 0.1f);
                    metallic = 0.0f;
                    smoothness = 0.1f;
                }
                else
                {
                    // ─────────────────────────────────────────────────
                    // 垂直外壁（現代〜未来カーテンウォール）
                    // ─────────────────────────────────────────────────

                    // (A) コーナー柱 ＆ コーナー垂直アクセント発光ライン
                    float cornerColWidth = max(0.2f, _CornerColumnWidth);
                    bool isCornerCol = (distToCorner < cornerColWidth);

                    // 12cmの極細LEDアクセントライン（fwidthアンチエイリアスで高速飛行時もモアレなし）
                    float accentLineWidth = 0.12f;
                    float accentFw = clamp(fwidth(distToCorner), 0.001f, 0.45f);
                    float isAccentLine = (1.0f - smoothstep(accentLineWidth - accentFw, accentLineWidth + accentFw, distToCorner));

                    // (B) 屋上クラウン（パラペット帯 ＆ 頂部発光アクセント）
                    float crownHeight = 2.4f;
                    bool isCrown = (distToRoof < crownHeight);
                    float crownFw = clamp(fwidth(distToRoof), 0.001f, 0.45f);
                    float isCrownLED = (1.0f - smoothstep(0.35f - crownFw, 0.35f + crownFw, distToRoof));

                    // 頂部コーナー航空障害灯
                    if (isCrown && distToCorner < 1.2f && _AviationBeaconEnable > 0.5f)
                    {
                        float flash = step(0.55f, frac(_Time.y * 0.8f));
                        emission += float3(4.5f, 0.1f, 0.1f) * flash * night;
                    }

                    // (C) 地上エントランス・ロビー（地上付近にあるブロックの低層部のみ。空中セットバックではロビーを無効化）
                    float lobbyH = max(3.0f, _LobbyHeight);
                    bool isElevated = (elevation > 3.0f);
                    bool isLobby = (hFromBase < lobbyH) && !isElevated;

                    // (D) 一般階カーテンウォール（窓・サッシ・スパンドレル）
                    float spanX = max(1.0f, faceWidth - 2.0f * cornerColWidth);
                    float relX = posHoriz - cornerColWidth;
                    float relY = max(0.0f, hFromBase - lobbyH);

                    float floorH = max(1.0f, _FloorHeight);
                    float windowW = max(0.5f, _WindowWidth);

                    float floorIndex = floor(relY / floorH);
                    float floorFrac  = frac(relY / floorH);
                    float bayIndex   = floor(relX / windowW);
                    float bayFrac    = frac(relX / windowW);

                    // 遠景アンチエイリアス（fwidthによるミップマップ補間）
                    float2 gridUV = float2(relX / windowW, relY / floorH);
                    float2 fw = clamp(fwidth(gridUV), 0.001f, 1.0f);
                    float maxFw = max(fw.x, fw.y);
                    float mipFade = saturate((maxFw - 0.15f) / 0.55f);

                    // スパンドレルパネル（横帯）とサッシ枠（縦柱）のマスク
                    float spandrelMask = 1.0f - smoothstep(_SpandrelRatio - fw.y, _SpandrelRatio + fw.y, floorFrac);
                    float mullionMask  = 1.0f - (smoothstep(_MullionRatio - fw.x, _MullionRatio + fw.x, bayFrac) *
                                                (1.0f - smoothstep((1.0f - _MullionRatio) - fw.x, (1.0f - _MullionRatio) + fw.x, bayFrac)));
                    float isFrame = saturate(spandrelMask + mullionMask);
                    float isGlass = saturate(1.0f - isFrame);

                    // ロビー領域・クラウン領域・コーナー柱の優先上書き
                    if (isCrown || isCornerCol)
                    {
                        isGlass = 0.0f;
                        isFrame = 1.0f;
                    }
                    else if (isLobby)
                    {
                        // ロビー階はサッシ間隔を広く（開放感のある大開口ガラス）
                        float lobbyMullionFw = clamp(fwidth(relX / 3.2f), 0.001f, 0.5f);
                        float lobbyMullion = 1.0f - (smoothstep(0.05f - lobbyMullionFw, 0.05f + lobbyMullionFw, frac(relX / 3.2f)) *
                                                     (1.0f - smoothstep(0.95f - lobbyMullionFw, 0.95f + lobbyMullionFw, frac(relX / 3.2f))));
                        isGlass = saturate(1.0f - lobbyMullion);
                        isFrame = lobbyMullion;
                    }

                    // ── 昼間の外装マテリアル合成 ──
                    // ガラス：鏡面反射（高Smoothness / 高Metallic）、暗色スレートブルー
                    float3 glassAlbedo = _GlassColorDay.rgb * lerp(1.0f, 0.40f, night);
                    float glassMetallic = _GlassMetallic;
                    float glassSmoothness = _GlassSmoothness;

                    // 外装パネル（スパンドレル・コーナー柱・クラウン）：白亜・シルバークリーンパネル
                    float3 wallAlbedo = _WallColor.rgb;
                    float wallMetallic = _WallMetallic;
                    float wallSmoothness = _WallSmoothness;

                    // サッシ枠：ダークチタンメタル
                    float3 mullionAlbedo = _MullionColor.rgb;
                    float mullionMetallic = 0.70f;
                    float mullionSmoothness = 0.62f;

                    // スパンドレルとサッシの合成
                    float3 frameAlbedo = lerp(wallAlbedo, mullionAlbedo, mullionMask);
                    float frameMetallic = lerp(wallMetallic, mullionMetallic, mullionMask);
                    float frameSmoothness = lerp(wallSmoothness, mullionSmoothness, mullionMask);

                    // ガラス面とフレーム面の合成
                    float3 facadeAlbedo = lerp(frameAlbedo, glassAlbedo, isGlass);
                    float facadeMetallic = lerp(frameMetallic, glassMetallic, isGlass);
                    float facadeSmoothness = lerp(frameSmoothness, glassSmoothness, isGlass);

                    // 遠景（LOD）：個別の窓枠からフロア平均の滑らかな反射・陰影へシームレス移行
                    float3 avgAlbedo = lerp(wallAlbedo, glassAlbedo, 0.70f);
                    float avgMetallic = lerp(wallMetallic, glassMetallic, 0.70f);
                    float avgSmoothness = lerp(wallSmoothness, glassSmoothness, 0.70f);

                    albedo = lerp(facadeAlbedo, avgAlbedo, mipFade);
                    metallic = lerp(facadeMetallic, avgMetallic, mipFade);
                    smoothness = lerp(facadeSmoothness, avgSmoothness, mipFade);

                    // ── 夜間のプロシージャルオフィス窓発光 ──
                    if (night > 0.001f)
                    {
                        float floorSeed = Hash21(float2(floorIndex, instanceSeed * 7.1f));
                        float roomSeed  = Hash21(float2(floorIndex * 19.3f + bayIndex, instanceSeed * 13.7f));

                        // フロアごとの残業率バイアス（一部のフロアは活発、一部は消灯）
                        float floorBias = lerp(0.08f, 0.85f, floorSeed);
                        float litProb = saturate(lerp(_WindowLitRate, floorBias, 0.45f));
                        float isLit = step(1.0f - litProb, roomSeed);

                        // 温白色〜蛍光色〜未来シアンの部屋ごとのランダム光色
                        float colorPick = Hash21(float2(floorIndex * 3.3f, bayIndex * 5.7f));
                        float3 officeCol = lerp(_WindowColorWarm.rgb, _WindowColorCool.rgb, step(0.60f, colorPick));

                        // ブラインド・シェード演出（一部の窓は半分ブラインドが降りている）
                        float hasBlind = step(0.50f, frac(roomSeed * 9.1f)) * step(0.52f, floorFrac);
                        float interiorFactor = lerp(1.0f, 0.22f, hasBlind);

                        float3 sharpOffice = officeCol * (isLit * interiorFactor * _WindowIntensity * night);
                        float3 avgOffice = lerp(_WindowColorWarm.rgb, _WindowColorCool.rgb, 0.35f) * (_WindowLitRate * 0.42f * _WindowIntensity * night);

                        emission += lerp(sharpOffice, avgOffice, mipFade) * isGlass;

                        // ロビー階の温かい広域発光
                        if (isLobby)
                        {
                            float lobbyFade = 1.0f - saturate(hFromBase / lobbyH);
                            emission += _LobbyLightColor.rgb * (_LobbyIntensity * night * lobbyFade * (0.6f + 0.4f * isGlass));
                        }
                    }

                    // ── 夜間のスカイライン建築アクセント（LEDライン ＆ クラウン） ──
                    if (night > 0.001f)
                    {
                        // コーナー垂直LEDライン
                        emission += _AccentColor.rgb * (_AccentIntensity * night * isAccentLine);

                        // 屋上クラウンLED帯
                        if (isCrown)
                        {
                            emission += _AccentColor.rgb * (_AccentIntensity * 0.85f * night * isCrownLED);
                        }
                    }

                    // ── プロシージャル・ネオンサイン看板（特定ビル・特定フロアにアクセント配置） ──
                    if (_NeonSignEnable > 0.5f && night > 0.001f)
                    {
                        float hasNeon = step(0.58f, Hash21(float2(instanceSeed * 5.9f, 29.3f)));
                        if (hasNeon > 0.5f && !isCrown && !isLobby)
                        {
                            float signMinY = bldgScale.y * 0.65f;
                            float signMaxY = bldgScale.y * 0.85f;
                            if (hFromBase > signMinY && hFromBase < signMaxY)
                            {
                                float signRelX = distToCorner - cornerColWidth - 0.5f;
                                if (signRelX > 0.0f && signRelX < 4.0f)
                                {
                                    // 縦長サイバー看板（走査バー・ロゴグリッド）
                                    float signUVy = (hFromBase - signMinY) / max(1.0f, signMaxY - signMinY);
                                    float signUVx = signRelX / 4.0f;
                                    float signBars = step(0.35f, frac(signUVy * 12.0f)) * step(0.15f, frac(signUVx * 4.0f));
                                    float pulse = 0.85f + 0.15f * sin(_Time.y * 2.5f + instanceSeed);
                                    emission += _NeonSignColor.rgb * (signBars * _NeonSignIntensity * pulse * night);
                                }
                            }
                        }
                    }
                }

                // ─────────────────────────────────────────────────────
                // 3. URP Forward+ 物理ベースライティング（InputData / SurfaceData）
                // ─────────────────────────────────────────────────────
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = nrmWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0f;
                surfaceData.alpha = 1.0f;
                surfaceData.clearCoatMask = 0.0f;
                surfaceData.clearCoatSmoothness = 1.0f;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 2. ShadowCaster Pass (太陽光・影の正確な投影)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "RealisticCityBuildingInput.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadow vertShadow(AttributesShadow input)
            {
                VaryingsShadow output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 fragShadow(VaryingsShadow input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 3. DepthOnly Pass (URP 深度プリパス・遮蔽カリング)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RealisticCityBuildingInput.hlsl"

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepth vertDepth(AttributesDepth input)
            {
                VaryingsDepth output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragDepth(VaryingsDepth input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 4. DepthNormals Pass (SSAO / DepthNormals テクスチャ対応)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepthNormals
            #pragma fragment fragDepthNormals
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RealisticCityBuildingInput.hlsl"

            struct AttributesDepthNormals
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDepthNormals
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepthNormals vertDepthNormals(AttributesDepthNormals input)
            {
                VaryingsDepthNormals output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 fragDepthNormals(VaryingsDepthNormals input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
