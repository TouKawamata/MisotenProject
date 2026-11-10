# Wind Tackle — 新規 VFX Graph 版

白〜薄い水色の風が前方の半球を覆い、細かく不揃いなギザギザの円が
中心から外側へUVスクロールするエフェクトです。初期持続時間は2.5秒。
以前の `TackleEffect` とは独立した新規アセットです。

## 確認する

`WindTackle_VFX_Preview.unity` を開いて再生してください。
または `Tools > Misoten > Wind Tackle VFX > Open Preview` から開けます。
プレビュー用グラフは3秒おきに再生します。
元のシーンは保存してから切り替えてください。戻すときは元のシーンをダブルクリックします。

## 本体を編集する

`WindTackle.vfx` をダブルクリックしてVFX Graphを開きます。
`WindTackle_VFX.prefab` は本体グラフを使い、OnPlayで1回再生します。
Visual Effectコンポーネントから再生・Reinitできます。

Blackboardの公開プロパティ:

| プロパティ | 内容 |
| --- | --- |
| Duration | 持続時間（秒） |
| Shape X | 横方向の半径（m） |
| Shape Y | 縦方向の半径（m） |
| Shape Z | 前方への奥行き（m） |
| ScrollSpeed | 円と風の流れる速さ。正の値で前方の中心から外周へ流れます |
| WindColor | HDRのRGB。初期値は白〜薄い水色 |

Shapeは初期値 `(1.5, 1.5, 1.05)`。各値は正の数で指定してください。
ローカル **+Zが前方、Xが横、Yが縦** です。
Shapeは半径なので、実際の横幅・高さはX・Yの2倍です。
各プロパティはPrefabのVisual Effectコンポーネントからも変更できます。
Durationは生成時に決まるため、変更したらReinitしてください。

## ノード構成

- Spawn: Single Burstで半球メッシュを1枚生成
- Initialize: Lifetime、Size
- Update: Age更新・寿命終了時に消去
- Output Particle Unlit Mesh: 半球、加算合成、両面表示
- Age → Multiply（ScrollSpeed）→ Multiply（0, -1）→ UV Bias
- Set Scale / Set Color: 公開プロパティを接続
- Set Alpha over Life: 出始めと消え際のフェード

UV Modeは `Scale And Bias`。模様は新規の `WindPattern.png`、
形状は `WindTackle_Hemisphere.asset` を使います。
独自の実行時C#や旧シェーダーには依存しません。
細かな尖りの形はテクスチャで、移動速度はVFXノードで編集します。

`WindTackle_LoopPreview.vfx` は同じノード構成の**確認用別グラフ**です。
本体と変更は自動同期しません。本体の調整を確認する場合は、
プレビューシーンのVisual EffectのAssetを `WindTackle.vfx` に替えてReinitしてください。

`Editor/WindTackleVFXTools.cs` はプレビューを開く操作とインポート検証専用です。
`Tools > Misoten > Wind Tackle VFX > Validate Assets` で参照と公開プロパティを確認できます。

## 検証済み

Unity 6000.3.23f1 / VFX Graph 17.3.0 の隔離プロジェクトで、両グラフのインポート、公開プロパティ、メッシュ、Prefabの参照を確認しました。Direct3D 11のPlay Mode描画テストも成功しています（生存パーティクル1、風が描画された画素3195、ピンクのエラー色0）。GPU描画の確認画像も取得済みです。

