// リング状のプログレスゲージ（uGUI用）。RingGaugeGraphic と組み合わせて使う。
// パラメータは頂点データで渡すので、1つのマテリアルを複数のゲージで共有できる。
//   TEXCOORD0.xy : 四角形内のUV（0～1）
//   TEXCOORD1    : x = 進捗(0～1), y = 内径, z = 外径（半径1 = 四角形の端）
//   TEXCOORD2    : x = 開始角(rad, 0 = 真下), y = 弧の長さ(rad), z = 向き(+1 = 下→右→上（反時計回り） / -1 = 下→左→上（時計回り）)
//   TEXCOORD3    : 色が付いた部分の色
//   TANGENT      : 色が付く前の部分の色
//   COLOR        : 全体に掛かる色（Graphic.color と CanvasGroup のアルファ）
Shader "UI/AT_Miyata/RingGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define TWO_PI 6.28318530718

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 gauge    : TEXCOORD1;
                float4 arc      : TEXCOORD2;
                float4 fill     : TEXCOORD3;
                float4 empty    : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 gauge         : TEXCOORD1;
                float4 arc           : TEXCOORD2;
                float4 fill          : TEXCOORD3;
                float4 empty         : TEXCOORD4;
                float4 worldPosition : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.gauge = v.gauge;
                o.arc = v.arc;
                o.fill = v.fill;
                o.empty = v.empty;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float progress = saturate(i.gauge.x);
                float innerR = i.gauge.y;
                float outerR = i.gauge.z;
                float startAngle = i.arc.x;
                float arcLength = i.arc.y;
                float direction = i.arc.z;

                // 中心が(0,0)、端が半径1になる座標
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);

                // 1ピクセル分の幅（アンチエイリアス用）
                float aa = max(fwidth(r), 1e-5);

                // リングの太さ方向のマスク
                float ringMask = smoothstep(innerR - aa, innerR + aa, r)
                               * (1.0 - smoothstep(outerR - aa, outerR + aa, r));

                // 角度：真下を0とし、向きに沿って0～2πで増える
                float angle = atan2(p.x, -p.y) * direction - startAngle;
                angle -= TWO_PI * floor(angle / TWO_PI);

                // 角度の差を「ピクセル数」に直してアンチエイリアスする
                float pixelScale = r / aa;

                // ゲージとして使う弧の範囲
                float arcMask = saturate((arcLength - angle) * pixelScale + 0.5);

                // 色が付いている範囲
                float fillEnd = arcLength * progress;
                float fillMask = saturate((fillEnd - angle) * pixelScale + 0.5) * step(1e-4, progress);

                fixed4 col = lerp(i.empty, i.fill, fillMask) * i.color;
                col.a *= ringMask * arcMask;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
