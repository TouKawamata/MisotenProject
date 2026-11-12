# Green Portal (URP)

画像を参考にした緑色のエネルギーポータル。外周の揺らぎ、白緑のコア、広いグロー、内部の薄い光と明滅する粒を UV から生成します。追加テクスチャ不要です。

## 配置

Unity の `GameObject > Misoten > Green Portal` で、マテリアル付きの縦長 Quad を作成します。初回に同じフォルダーへ `GreenPortal.mat` を作成します。シーンの保存は通常どおり行ってください。

手動の場合は `Misoten/Effects/Green Portal` のマテリアルを Quad に割り当て、Scale を `(3, 4.5, 1)` にします。通常の Plane は UV と向きが異なるため Quad を推奨します。

## 見た目の調整

- Energy Color / Hot Core Color: 周囲の緑と白緑の縁。
- Emission: 発光の強さ。初期値 4。
- Edge Width / Edge Distortion: 縁の太さと揺らぎ。
- Animation Speed: アニメーション速度。0 で静止。
- Interior Glow / Sparkle Brightness: 内部の光と粒の強さ。
- Opacity: 全体のフェード。
- 楕円の縦横比は Quad の Scale で調整。

画像のようににじむ光には、URP の HDR とカメラの Post Processing、Global Volume の Bloom を有効にします。Bloom は Threshold 1、Intensity 1 前後から調整してください。シェーダー自体にもグローがあるため Bloom なしでも表示されます。

加算合成・両面描画・深度書き込みなしです。背後の景色が透ける装飾エフェクトで、別空間の描画や転送処理は含みません。床を照らしたい場合は緑の Point Light を別途配置してください。
