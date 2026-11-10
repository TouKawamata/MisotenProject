# Wind Tackle — 頂点をつなぐ VFX Graph 版

`WindTackle_Strips_Preview.unity` を開いて再生すると、3秒間隔で確認できます。
使用するPrefabは `WindTackle_Strips.prefab`、編集するグラフは `WindTackle_Strips.vfx` です。

テクスチャ・外部メッシュ・Shader Graph・独自HLSL・実行時C#は使用していません。
Unity標準の **Output ParticleStrip Unlit Quad** が隣り合うパーティクルの位置を帯状につなぎます。
Color Mapping は None。模様の画像を貼るのではなく、位置そのものがギザギザに動きます。

## 調整

| Blackboard | 内容 | 初期値 |
| --- | --- | --- |
| Shape | X: 横半径、Y: 縦半径、Z: 奥行き。前方はローカル +Z | (1.5, 1.5, 1.05) |
| Duration | 1回の持続時間（秒） | 2.5 |
| FlowSpeed | 先端から外周へ流れる回数 / 秒 | 1.3 |
| LineWidth | 帯の太さ | 0.009 |
| Jaggedness | 細い突起の大きさ。目安 0〜0.3 | 0.16 |
| WindColor | HDR RGB。白〜薄い青 | (1.8, 2.4, 2.8) |

Shape、Duration、LineWidth は正の値にしてください。
遠くで線が途切れて見える場合は LineWidth を少し上げてください。
固定Boundsは32ユニット四方です。極端に拡大する場合はInitializeのBoundsも調整してください。

## ノードの構成

1. Single Burstで1028点を生成。`Floor(spawnIndex / 257)` で4本のStripに分けます。
2. 各Stripは257点。最初と最後を同じ角度に配置し、256区間の輪を閉じます。
3. `particleIndexInStrip × 2π / 256` が円周の角度です。
4. `Fractional(age × FlowSpeed + stripIndex / 4)` で4本の輪をずらして繰り返し流します。
5. Sine / Cosineで半球上の座標を作り、細い尖りと不均一な高さを算術ノードで加えます。
6. UpdateのSet Positionが点の座標を毎フレーム更新。Outputでは線幅と色、寿命・周回に応じた透明度を設定します。

これはUVスクロールではなく、頂点位置の繰り返し移動です。前の表現と同じ方向へ流れる動きを形状で作っています。
グラフを開いて各ノードから直接変更できます。輪の本数や点数を変える場合はBurst Count、Strip Capacity、Particle Per Strip Countと角度・Strip Indexの計算値を一緒に変更してください。

`WindTackle_Strips_Loop.vfx` は確認用の別コピーです。本体の変更は自動同期しません。
本体を調整して確認する場合は、シーンのVisual Effectへ `WindTackle_Strips.vfx` を割り当て、Reinitで再生してください。
本体はOnPlayで1回再生。再生中の再スタートにはReinitを使ってください。

旧版の `WindTackleVFX` と `TackleEffect` はそのまま残しています。
