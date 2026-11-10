Shader "Skybox/ProceduralLabHex"
{
    Properties
    {
        _BaseColor ("Background Color", Color) = (0.05, 0.05, 0.08, 1)
        _HexColor ("Hex Neon Color", Color) = (0.0, 0.8, 1.0, 1)
        _HexScale ("Hex Scale", Float) = 8.0
        _LineThickness ("Line Thickness", Range(0.001, 0.1)) = 0.03
        _EmissionStrength ("Emission Strength", Range(1.0, 10.0)) = 2.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            float4 _BaseColor;
            float4 _HexColor;
            float _HexScale;
            float _LineThickness;
            float _EmissionStrength;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            // 六角形の中心からの距離を計算する関数
            float hexDist(float2 p)
            {
                p = abs(p);
                // 1.7320508 は √3。六角形の縦横比に対応
                float d = dot(p, normalize(float2(1.0, 1.7320508))); 
                return max(d, p.x);
            }

            // UV座標から最も近い六角形のエッジまでの距離を取得
            float getHexEdge(float2 uv)
            {
                float2 s = float2(1.0, 1.7320508);
                float2 h = 0.5 * s;
                
                // 六角形グリッドは、半分ズレた2つの四角グリッドを重ねて判定する
                float2 p1 = frac(uv / s) - 0.5;
                float2 p2 = frac((uv - h) / s) - 0.5;
                
                p1 *= s;
                p2 *= s;
                
                // 2つの中心点のうち、近い方を採用
                float2 p = dot(p1, p1) < dot(p2, p2) ? p1 : p2;
                
                // 六角形の半径(0.5)からの差分がエッジまでの距離
                return 0.5 - hexDist(p);
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.texcoord);
                
                // 部屋（キューブ）の壁面へプロジェクション
                float3 m = abs(dir);
                float maxCoord = max(max(m.x, m.y), m.z);
                float3 boxUvw = dir / maxCoord;

                // 参照している面（XYZ）に応じて2DのUV座標に変換
                float2 uv;
                if (maxCoord == m.x) uv = boxUvw.yz;
                else if (maxCoord == m.y) uv = boxUvw.xz;
                else uv = boxUvw.xy;

                // 六角形のエッジ距離を算出
                float edgeDist = getHexEdge(uv * _HexScale);
                
                // エッジが太さの範囲内なら発光させる（滑らかさのためにsmoothstepを使用）
                float lineFactor = 1.0 - smoothstep(0.0, _LineThickness, edgeDist);
                
                float4 finalColor = lerp(_BaseColor, _HexColor * _EmissionStrength, lineFactor);
                
                return finalColor;
            }
            ENDCG
        }
    }
}