Shader "AT_Murayama/AtmosphericScattering/CyberCityTestBuilding"
{
    Properties
    {
        [Header(Building Base)]
        _WallColor ("Wall Base Color", Color) = (0.02, 0.03, 0.05, 1.0)
        _BuildingSeed ("Random Seed", Float) = 1.0

        [Header(Window Lights)]
        [HDR] _WindowColorA ("Window Color A", Color) = (0.4, 1.8, 3.2, 1.0)
        [HDR] _WindowColorB ("Window Color B", Color) = (2.2, 1.9, 1.4, 1.0)
        _WindowDensity ("Window Grid Scale", Vector) = (0.35, 0.25, 0, 0)
        _WindowLitRate ("Window Lit Probability", Range(0.0, 1.0)) = 0.28

        [Header(Vertical Neon Sign)]
        [HDR] _NeonSignColor ("Neon Sign Color", Color) = (0.3, 2.5, 4.5, 1.0)
        _NeonIntensity ("Neon Intensity", Range(0.0, 3.0)) = 1.2

        [Header(Street Level Lights)]
        [HDR] _StreetLightColor ("Street Level Light Color", Color) = (0.8, 2.4, 3.8, 1.0)
        _StreetLightHeight ("Street Light Zone Height", Float) = 28.0
        _StreetLightDensity ("Street Light Density", Range(0.0, 2.0)) = 1.0
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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WallColor;
                float _BuildingSeed;
                float4 _WindowColorA;
                float4 _WindowColorB;
                float4 _WindowDensity;
                float _WindowLitRate;
                float4 _NeonSignColor;
                float _NeonIntensity;
                float4 _StreetLightColor;
                float _StreetLightHeight;
                float _StreetLightDensity;
            CBUFFER_END

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
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 posWS = input.positionWS;
                float3 nrm = normalize(input.normalWS);

                // 屋上面は窓を描かない
                if (abs(nrm.y) > 0.7f)
                {
                    return half4(_WallColor.rgb * 0.5f, 1.0f);
                }

                float horizCoord = (abs(nrm.x) > 0.5f) ? posWS.z : posWS.x;
                float vertCoord = max(0.0f, posWS.y);

                // 1. 基本の暗いビル壁色
                float3 col = _WallColor.rgb;

                // 2. 窓グリッドのプロシージャル発光（fwidthによる遠景アンチエイリアス・チラつき防止付き）
                float2 gridUV = float2(horizCoord * _WindowDensity.x, vertCoord * _WindowDensity.y);
                float2 cellId = floor(gridUV);
                float2 cellLocal = frac(gridUV);

                // 1ピクセルあたりのUV変化量（ミップマップ相当：遠景ほど大きくなる）
                float2 fw = clamp(fwidth(gridUV), 0.001f, 1.0f);
                float mipFade = saturate((max(fw.x, fw.y) - 0.18f) / 0.55f);

                // 窓枠のエッジを fwidth で滑らかに補間してサブピクセル点滅を防ぐ
                float wx = smoothstep(0.18f - fw.x, 0.18f + fw.x, cellLocal.x)
                         * (1.0f - smoothstep(0.82f - fw.x, 0.82f + fw.x, cellLocal.x));
                float wy = smoothstep(0.22f - fw.y, 0.22f + fw.y, cellLocal.y)
                         * (1.0f - smoothstep(0.78f - fw.y, 0.78f + fw.y, cellLocal.y));
                float windowFrame = wx * wy;

                float floorRand = Hash21(float2(cellId.y, _BuildingSeed));
                float roomRand = Hash21(cellId + _BuildingSeed * 13.7f);

                float lowFloorBoost = saturate(1.0f - vertCoord / max(1.0f, _StreetLightHeight * 2.0f)) * 0.35f;
                float litProb = saturate(_WindowLitRate + lowFloorBoost);
                float isLit = step(1.0f - litProb, roomRand) * step(0.15f, floorRand);

                float3 winColor = lerp(_WindowColorA.rgb, _WindowColorB.rgb, step(0.65f, Hash21(cellId + 7.1f)));
                float brightnessVar = lerp(0.4f, 1.3f, Hash21(cellId + 3.3f));

                // 近〜中景は個別の窓、超遠景（1窓が1ピクセル未満）はフロア単位の滑らかな平均発光へ繋がることでカメラ移動時のモアレ点滅を解消
                float3 sharpWindow = winColor * (windowFrame * isLit * brightnessVar);
                float3 avgWindow = lerp(_WindowColorA.rgb, _WindowColorB.rgb, 0.35f) * (litProb * 0.32f * step(0.2f, floorRand));
                col += lerp(sharpWindow, avgWindow, mipFade);

                // 3. 縦長のサイバーネオン看板ライン（遠景アンチエイリアス付き）
                float colCoord = horizCoord * 0.08f;
                float colFw = clamp(fwidth(colCoord), 0.001f, 0.5f);
                float columnId = floor(colCoord);
                float columnLocal = frac(colCoord);
                float hasNeonColumn = step(0.78f, Hash21(float2(columnId, _BuildingSeed * 5.0f)));
                float neonWidthMask = smoothstep(0.40f - colFw, 0.46f + colFw, columnLocal)
                                    * (1.0f - smoothstep(0.54f - colFw, 0.60f + colFw, columnLocal));

                float vDashCoord = vertCoord * 0.35f;
                float vDashFw = saturate(fwidth(vDashCoord));
                float neonDash = lerp(step(0.35f, Hash21(float2(columnId, floor(vDashCoord)))), 0.65f, vDashFw);
                col += _NeonSignColor.rgb * (hasNeonColumn * neonWidthMask * neonDash * _NeonIntensity);

                // 4. 地上付近（道路・高架下・店舗看板・街灯）の高輝度ライト密集
                if (vertCoord < _StreetLightHeight)
                {
                    float streetZone = 1.0f - saturate(vertCoord / max(1.0f, _StreetLightHeight));
                    float2 streetGrid = float2(horizCoord * 0.18f, vertCoord * 0.30f);
                    float2 stFw = clamp(fwidth(streetGrid), 0.001f, 1.0f);
                    float stMip = saturate((max(stFw.x, stFw.y) - 0.2f) / 0.6f);

                    float2 stId = floor(streetGrid);
                    float2 stLocal = frac(streetGrid);
                    float stShape = smoothstep(0.12f - stFw.x, 0.28f + stFw.x, stLocal.x)
                                  * (1.0f - smoothstep(0.72f - stFw.x, 0.88f + stFw.x, stLocal.x))
                                  * smoothstep(0.12f - stFw.y, 0.28f + stFw.y, stLocal.y)
                                  * (1.0f - smoothstep(0.72f - stFw.y, 0.88f + stFw.y, stLocal.y));
                    float stLit = step(0.45f, Hash21(stId + _BuildingSeed * 31.0f));
                    float stFactor = lerp(stShape * stLit, 0.38f, stMip);
                    col += _StreetLightColor.rgb * (stFactor * streetZone * _StreetLightDensity * 1.6f);
                }

                return half4(col, 1.0f);
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // DepthOnly Pass (URP Depth Prepass で確実に深度テクスチャに書き込む)
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragDepth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // DepthNormals Pass (SSAO 等で法線＋深度バッファに確実に書き込む)
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertDepthNormals(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 fragDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(normalWS, 0.0);
            }
            ENDHLSL
        }
    }
}
