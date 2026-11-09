# エントリー演出 設計メモ

エントリー画面（`AT_Miyata/Scenes/EntryTest.unity`）の演出。3D 側（リング・ホログラム）と UI 側（帯・テキスト・線と粒）に分かれる。
作業ルール（作業場所・既存ファイルを変えるときは承認を得る・コードの書き方・パスの書き方）は `AT_Miyata/CPU/CPU_DesignNotes.md` と同じ。
作業の進め方：まず設計を提示して、了承をもらってから実装する。

## 構成（`AT_Miyata/Entry/Scripts/`）
```
EntryEffectsController.cs   3D側。リング展開 → 上昇（_RevealHeight をマテリアルとVFXに渡す）。今回は変更なし
EntryCanvasController.cs    UI側。背景の帯とテキストの進入＋バウンド。背景が着いた瞬間に EdgeLineParticleEmitter を再生
EdgeLineParticleEmitter.cs  帯の縁を流れる白い線＋線の先端に出る四角い粒
GradientLineGraphic.cs      左側が徐々に薄くなる線（MaskableGraphic、頂点カラーで描く）
EntryTestInput.cs           テスト入力。P: 3D開始 / O: UI開始(StartEffect) / R・T: リセット
```
Canvas は Screen Space - Overlay。そのため VFX Graph や ParticleSystem は UI の上に出ない → 粒は UI の Image をプールして作っている。
アニメーションは DOTween の Tween は使わず、Update でタイマーを進めて `DOVirtual.EasedValue` でイージングする書き方に統一している。

## EntryCanvasController
状態は `AnimState { None, Approach, BounceBack, BounceReturn, Done }` を背景とテキストで別々に持つ。
`StartEffect()` は内部でリセットしてから開始（連打しても最初から）。初期化は `Awake`。

### 背景（`_backgroundImages`）
1. Approach：全要素が同時に、自分の初期位置 + `_backgroundOffsetX`（-2000 = 左外）から初期位置へ。
2. BounceBack：全員同時に `_backgroundBounceDistance` だけ左へ戻る。**この切り替わりの瞬間に `_edgeEmitters` を `Play()`**。
3. BounceReturn：同時に初期位置へ戻る。

### テキスト（`_textImages`、3つ）
- ターゲットは **3つとも [0] の初期位置**（重なる）。[1][2] は [0] を追う残像の扱い。
- Approach：`_textStartDelay` 後、上から順に `_textStagger` ずつ遅れて左から進入（OutQuad）。
- [0] が着いた瞬間に全員の位置を記録（`_textBouncePositions`）→ その位置関係を保ったまま同じ距離だけ左へ（BounceBack）。
- BounceReturn：全員が [0] と同じタイミングでターゲットへ（[1][2] は [0] の左側にいたまま追いつく）。着いたら [1][2] を `SetActive(false)`。
- `ResetEffect()` で全員 `SetActive(true)` に戻す。

### 補足
- シリアライズの型は `GameObject[]` のまま（シーンの参照を切らないため）。RectTransform は Awake でキャッシュ。
- `_effectDuration` は `_backgroundDuration` に改名（`FormerlySerializedAs` 付き）。
- フィールドの初期値は 2026-11-05 時点のシーンの値に合わせてある。

## EdgeLineParticleEmitter（帯の上の縁・下の縁に1つずつ）
参考にしたのは「帯が入ってきた後、縁に沿って白い線が流れ、先端の周りに白い四角が散る」演出。
1. `Play()`：`_lineStart`（画面左外）→ `_lineEnd`（画面右端、手動で配置）の経路を、このオブジェクトのローカル座標で計算する。
2. 線：長さ `_lineLength`・太さ `_lineThickness`。先端が進む距離は `(経路長 + _lineLength) × Eased(t, _moveEase)`（時間 `_moveDuration`）なので、最後は線全体が終点の先へ抜けて非表示になる。経路の向きに合わせて回転させる。pivot は右端（先端）。
3. 粒：**先端が `_lineEnd` に着くまで**、`_spawnInterval` ごとに `_spawnPerTick` 個を先端の周りに出す。
   - 線に沿った方向：先端から後ろへ `0〜_spreadAlong`。線に垂直な方向：`±_spreadPerp × pow(乱数, _falloff)`（線の近くに集まる）。
   - **粒は動かない・透明度も変えない**。サイズだけ `初期サイズ × (1 - Eased(t, _shrinkEase))` で中心に向かって縮み、寿命で消える。
   - 四角は傾けない（水平・垂直のまま）。先に出た左の粒ほど小さくなるので、右に偏って見える。
4. Image は `_maxCount` 個を起動時に生成してプール。足りないときは出さない。`raycastTarget = false`。
5. 他スクリプトの Awake から `ResetEffect()` が呼ばれても大丈夫なように、遅延初期化（`EnsureInitialized`）にしている。

### GradientLineGraphic
- 左端から `_fadeRatio`（線の長さに対する割合）の位置までの透明度を 0→1 にし、そこから右は不透明。
- フェード部分は `_fadeSegments` 分割して `_fadeEase` のカーブで濃さを変える。
- 色は Graphic の `color`。

### シーンでの置き方
- Canvas の下にエミッター（`EdgeLineParticleEmitter`）を置き、**帯より後ろの順番**（Hierarchy で下）にする → 帯の手前に描画される。
- エミッターの子に `GradientLineGraphic` を置いて `_line` に割り当てる。
- 帯の縁に沿って空の RectTransform を2つ置き、`_lineStart` / `_lineEnd` に割り当てる。
- `EntryCanvasController._edgeEmitters` に登録する。

## 現状と残っていること
- 2026-11-05：上記を実装。`EntryTest.unity` にはエミッターが2つ配置済みで、`_edgeEmitters` にも登録済み。
- コンパイルと見た目の確認は Unity 上でユーザーが行う。値の調整はこれから（線と粒のパラメータは仮の初期値）。
