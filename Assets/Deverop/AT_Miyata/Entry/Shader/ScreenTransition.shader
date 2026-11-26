// 画面トランジション（UI 用。画面いっぱいの Image に付ける。ScreenTransition.cs から値を入れる）
// _Progress（0→1）で次の順に出す：
//  ① 左の画面外の一点（_Origin）から、光の筋が右へ扇状に飛び出す
//  ② 左から右へ暗くなる（_DarkColor → 最後は黒）
//  ③ 光の筋が消え、上下の真ん中に横向きの光の線が1本残る（左端が強く光る）
//  ④ 線も消えて真っ黒（_Progress = 1）
// _Reveal（0→1）で全体を透明にしていく（シーンを移った後に開ける用）。
// 色は乗算済みアルファで出す（光は加算、暗さは上に被せる）。
Shader "Entry/ScreenTransition"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1, 1, 1, 1)

		[Header(Progress)]
		_Progress ("進み具合（0：何もない → 1：真っ黒）", Range(0, 1)) = 0
		_Reveal ("開け具合（0：そのまま → 1：透明）", Range(0, 1)) = 0
		_Aspect ("画面の横÷縦（C# が毎フレーム入れる）", Float) = 1.7777778

		[Header(Timing  Progress)]
		_StreakRange ("光の筋：出始め(x) 出切る(y) 消え始め(z) 消え切る(w)", Vector) = (0, 0.1, 0.45, 0.7)
		_DarkRange ("暗くなる：始め(x) 終わり(y) 左右の差(z)", Vector) = (0.1, 0.7, 1.5, 0)
		_LineRange ("真ん中の線：出始め(x) 出切る(y) 消え始め(z) 消え切る(w)", Vector) = (0.35, 0.55, 0.8, 0.97)
		_BlackRange ("暗い色から黒へ：始め(x) 終わり(y)", Vector) = (0.75, 1, 0, 0)

		[Header(Streaks)]
		_Origin ("光の中心（UV。x はマイナスで画面の左外）", Vector) = (-0.35, 0.5, 0, 0)
		_StreakCount ("筋の数（角度の分割数）", Float) = 60
		_StreakAngle ("筋が広がる角度（度。上下それぞれ）", Range(1, 89)) = 40
		_StreakDensity ("筋を置く割合", Range(0, 1)) = 0.45
		_StreakWidth ("筋の太さ 最小(x) 最大(y)（分割1つに対する割合）", Vector) = (0.04, 0.3, 0, 0)
		_StreakLength ("筋の長さ 最小(x) 最大(y)（画面の高さ=1）", Vector) = (0.4, 1.6, 0, 0)
		_StreakSpeed ("筋の速さ 最小(x) 最大(y)（画面の高さ/進み具合1）", Vector) = (12, 24, 0, 0)
		_StreakDelay ("筋の出るずれ（進み具合）", Float) = 0.15
		_StreakLoop ("筋が出直すまでの距離（画面の高さ=1。画面の右端より遠くする）", Float) = 4
		_StreakColor1 ("筋の色1", Color) = (1, 1, 1, 1)
		_StreakColor2 ("筋の色2", Color) = (0.35, 0.75, 1, 1)
		_StreakColor3 ("筋の色3", Color) = (0.7, 0.45, 1, 1)
		_StreakIntensity ("筋の明るさ", Float) = 1.2

		[Header(Center Line)]
		_LineColor ("線の色", Color) = (0.1, 0.45, 1, 1)
		_LineCoreColor ("線の芯の色", Color) = (0.75, 0.9, 1, 1)
		_LineWidth ("線の太さ（ぼかし。画面の高さ=1）", Float) = 0.012
		_LineIntensity ("線の明るさ", Float) = 1.2

		[Header(Left Glow)]
		_GlowColor ("左端の光の色", Color) = (0.1, 0.55, 1, 1)
		_GlowSize ("左端の光の大きさ 横(x) 縦(y)（UV）", Vector) = (0.12, 0.25, 0, 0)
		_GlowIntensity ("左端の光の明るさ", Float) = 1

		[Header(Dark)]
		_DarkColor ("暗くなるときの色（最後は黒になる）", Color) = (0.01, 0.03, 0.09, 1)

		// UI のマスク用（Mask の中に置いたときの警告を出さないため）
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
		Blend One OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			fixed4 _Color;
			float _Progress;
			float _Reveal;
			float _Aspect;
			float4 _StreakRange;
			float4 _DarkRange;
			float4 _LineRange;
			float4 _BlackRange;
			float4 _Origin;
			float _StreakCount;
			float _StreakAngle;
			float _StreakDensity;
			float4 _StreakWidth;
			float4 _StreakLength;
			float4 _StreakSpeed;
			float _StreakDelay;
			float _StreakLoop;
			fixed4 _StreakColor1;
			fixed4 _StreakColor2;
			fixed4 _StreakColor3;
			float _StreakIntensity;
			fixed4 _LineColor;
			fixed4 _LineCoreColor;
			float _LineWidth;
			float _LineIntensity;
			fixed4 _GlowColor;
			float4 _GlowSize;
			float _GlowIntensity;
			fixed4 _DarkColor;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color * _Color;
				o.uv = v.uv;
				return o;
			}

			// 0～1 の乱数（id と種から）
			float Hash(float id, float seed)
			{
				return frac(sin(id * 127.1 + seed * 311.7) * 43758.5453);
			}

			// range.x→range.y で 0→1、range.z→range.w で 1→0 になる強さ
			float Envelope(float p, float4 range)
			{
				return smoothstep(range.x, range.y, p) * (1.0 - smoothstep(range.z, range.w, p));
			}

			// 光の筋（1層ぶん）。cellCoord：角度の分割の座標、r：中心からの距離、seed：層ごとの種
			float3 Streaks(float cellCoord, float r, float seed)
			{
				float id = floor(cellCoord);
				// 分割の中での位置（-0.5～0.5）
				float f = frac(cellCoord) - 0.5;
				float aa = max(fwidth(cellCoord), 1e-4);

				// 速さと出るずれは分割ごとに固定（何周しても同じ）
				float speed = lerp(_StreakSpeed.x, _StreakSpeed.y, Hash(id, seed + 4.0));
				float delay = Hash(id, seed + 5.0) * _StreakDelay;
				// 中心から進んだ距離（始まる前はマイナス）
				float travel = (_Progress - _StreakRange.x - delay) * speed;
				// 判定：まだ出ていないか
				if (travel <= 0.0)
				{
					return 0;
				}

				// 画面の右端を抜けたら中心から出直す（周ごとに太さ・長さ・色などを選び直す）
				float loopIndex = floor(travel / _StreakLoop);
				float loopSeed = seed + loopIndex * 17.0;
				// 判定：この分割のこの周に筋を置くか
				if (Hash(id, loopSeed + 1.0) > _StreakDensity)
				{
					return 0;
				}

				float width = lerp(_StreakWidth.x, _StreakWidth.y, Hash(id, loopSeed + 2.0));
				float len = lerp(_StreakLength.x, _StreakLength.y, Hash(id, loopSeed + 3.0));
				// 筋の中心の角度方向のずれ（分割の中で少しずらして、並びを不揃いにする）
				float center = (Hash(id, loopSeed + 6.0) - 0.5) * (1.0 - width * 2.0) * 0.8;

				// 先端の位置（中心からの距離）
				float head = travel - loopIndex * _StreakLoop;
				// 先端から後ろへの割合（0：先端 → 1：後ろの端）
				float s = (head - r) / len;
				// 判定：筋の上にいるか
				if (s < 0.0 || s > 1.0)
				{
					return 0;
				}

				// 両端がとがった形（先端寄りが太い）
				float shape = pow(saturate(s * 4.0), 0.5) * pow(1.0 - s, 1.5);
				float w = width * shape;
				float d = abs(f - center);
				float mask = 1.0 - smoothstep(w - aa, w + aa, d);
				// 判定：筋の外か
				if (mask <= 0.0)
				{
					return 0;
				}

				// 色：外側は色1～3のどれか、芯は白っぽく
				float pick = Hash(id, loopSeed + 7.0);
				float3 edgeColor = pick < 0.34 ? _StreakColor1.rgb : (pick < 0.67 ? _StreakColor2.rgb : _StreakColor3.rgb);
				float core = 1.0 - saturate(d / max(w, 1e-4));
				float3 color = lerp(edgeColor, _StreakColor1.rgb, core * core);
				return color * mask;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float p = _Progress;

				// 縦横比を直した座標（画面の高さ = 1）
				float2 pos = float2(uv.x * _Aspect, uv.y);
				float2 origin = float2(_Origin.x * _Aspect, _Origin.y);
				float2 dir = pos - origin;
				float r = length(dir);
				float angle = degrees(atan2(dir.y, dir.x));

				// ① 光の筋（3層。層ごとに分割をずらして重ねる）
				float3 glow = 0;
				float streakStrength = Envelope(p, _StreakRange);
				if (streakStrength > 0.0)
				{
					float cellCoord = (angle / _StreakAngle * 0.5 + 0.5) * _StreakCount;
					float3 streaks = Streaks(cellCoord, r, 0.0);
					streaks += Streaks(cellCoord * 1.37 + 0.5, r, 10.0);
					streaks += Streaks(cellCoord * 0.71 + 0.25, r, 20.0) * 0.7;
					// 広がる角度の外は出さない
					float inside = 1.0 - smoothstep(_StreakAngle * 0.9, _StreakAngle, abs(angle));
					glow += streaks * streakStrength * inside * _StreakIntensity;
				}

				// ③ 真ん中の線（ぼかした線＋細い芯。右へ行くほど弱く）
				float lineStrength = Envelope(p, _LineRange);
				float dy = (uv.y - _Origin.y) / max(_LineWidth, 1e-4);
				float lineFade = 1.0 - smoothstep(0.6, 1.0, uv.x) * 0.8;
				float3 lineColor = _LineColor.rgb * exp(-dy * dy) + _LineCoreColor.rgb * exp(-dy * dy * 16.0);
				glow += lineColor * lineStrength * lineFade * _LineIntensity;

				// 左端の光（筋か線が出ている間）
				float glowStrength = max(streakStrength, lineStrength);
				float2 g = float2(uv.x, uv.y - _Origin.y) / max(_GlowSize.xy, 1e-4);
				glow += _GlowColor.rgb * exp(-g.x) * exp(-g.y * g.y) * glowStrength * _GlowIntensity;

				// ② 暗くなる（左から先に。z が大きいほど左右の差が大きい）
				float darkT = saturate((p - _DarkRange.x) / max(_DarkRange.y - _DarkRange.x, 1e-4));
				float dark = saturate(darkT * (1.0 + _DarkRange.z) - uv.x * _DarkRange.z);
				dark = dark * dark * (3.0 - 2.0 * dark);
				// ④ 暗い色から黒へ
				float toBlack = smoothstep(_BlackRange.x, _BlackRange.y, p);
				float3 darkColor = _DarkColor.rgb * (1.0 - toBlack);

				// 乗算済みアルファ：暗さを被せて、その上に光を足す
				float3 rgb = darkColor * dark + glow;
				float alpha = dark;

				// 開ける（全体を透明にしていく）
				float keep = (1.0 - saturate(_Reveal)) * i.color.a;
				return fixed4(rgb * keep, saturate(alpha) * keep);
			}
			ENDCG
		}
	}
}
