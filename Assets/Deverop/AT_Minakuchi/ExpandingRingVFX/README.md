# Expanding Ring VFX

白い円が中心から外へ広がり、フェードして消える独立したVFXです。
`ExpandingRing_Preview.unity` を開いて再生すると2秒ごとに確認できます。
ゲーム内で使う場合は `ExpandingRing.prefab` を配置してください。

VFX Graph標準のParticle Stripで129個の点を接続しています。
テクスチャ、外部メッシュ、独自シェーダー、実行時スクリプトはありません。
円はローカルXY平面に広がります。地面に沿わせる場合はTransformのX回転を90度にしてください。

`ExpandingRing.vfx` のBlackboardで調整できます。

| 項目 | 内容 | 初期値 |
| --- | --- | --- |
| Duration | 広がって消えるまでの秒数 | 1.5 |
| StartRadius | 最初の半径 | 0.05 |
| EndRadius | 最後の半径 | 2 |
| LineWidth | 線の太さ | 0.035 |
| RingColor | HDR RGB | (2, 2, 2) 白 |

DurationとLineWidthは正、StartRadiusは0以上、EndRadiusはStartRadiusより大きくしてください。
半径はage / Durationに比例して広がります。透明度はOutputのSet Alpha over Lifeで編集できます。
本体はOnPlayで一度再生します。再生途中のやり直しはReinitを使用してください。

確認用の `ExpandingRing_Loop.vfx` は別コピーです。本体の編集は自動反映されません。
本体を調整する場合は確認シーンのVisual Effectに本体のグラフを割り当て、Reinitで確認してください。
固定Boundsは32ユニット四方。半径を16以上にする場合はInitializeのBoundsも広げてください。
