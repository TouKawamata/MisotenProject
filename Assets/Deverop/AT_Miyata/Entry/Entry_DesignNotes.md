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
EntryTransitionController.cs 遷移演出（合図の後、シーンを移るまで）。背景が下がって戻る＋テキストが時間をずらして動く＋色の切り替え（制作中）
ScreenTransition.cs         画面トランジション（UIの上から画面全体を覆う。見た目は Shader/ScreenTransition.shader）。K で覆う／C で戻す
EntryTestInput.cs           テスト用のキーボード入力（Entry のキー入力はすべてここ。下の「テスト用のキーボード」）
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

## トラッキングの流れ（EntryTrackingFlow）
`EntryTest.unity` に `TrackingReceiver`（Prefab。uOscServer＋Receiver）と `EntryTrackingFlow`（＋`TrackingLossWarningView`）を置いている。PoseCalibrator は実行時に自動で追加、EntryEffectsController はシーン内から自動で探す。
1. **待機**（WaitingPerson）：**肩と腰の4点**（`PoseCalibrator.IsBodyVisible`）が `_personStableTime`（1秒）続けて見えるまで待つ。測り直しも同じ条件で始める。
   - 2026-11-19：最初は `IsTracking`（何か映っている）だけで始めていたため、腰が見えないうちに始まって、リングに合わせた測定が毎回「0 / 15」で失敗し、リングと無関係な測り直しで成功していた（Editor.log で確認）。そのため条件を変えた。開始時に `EntryTrackingFlow: キャリブレーション開始（秒数）` を Console に出す。
2. **キャリブレーション**（Calibrating）：`PoseCalibrator.StartCalibration(秒数)` と `EntryEffectsController.StartAnimation()` を**同時に**始める（キャンバスの演出は EntryEffectsController が続けて出す）。
   - `_syncCalibrationWithRise`（ON）：時間では終わらない測定（`StartCalibration(0)`）を始め、`EntryEffectsController.RiseProgress` が1になった（**リングが上がり切った**）フレームで `PoseCalibrator.CompleteNow()` を呼んで終える。EntryTest の値では約3.7秒。
     - 2026-11-19：最初は測定時間を秒数（展開＋遅延＋上昇の合計）で合わせていたが、うまくいかなかった。VFX を初めて出すときなどに画面が数秒止まると、リング（`deltaTime`。1フレーム最大0.33秒）は止まった分だけ遅れるが、測定（`unscaledDeltaTime`）は止まった分も進む。そのため、止まりが明けた最初のフレームで受信を処理する前に測定が終わり、「0 / 15」で失敗していた（推定）。それを受けて、リングの状態で終える方式に変えた。失敗の理由には受信したフレーム数と測定時間も出す。
   - このときの「%」は `EntryEffectsController.RiseProgress`（リングの上がり具合。上昇前は0%）を出す。リングが上がり始めてから0→100%になる。測り直しのときは `PoseCalibrator.Progress`。
   - 失敗したら `_retryDelay`（1秒）待って測り直す（RetryWait）。測り直しの時間は PoseCalibrator の Duration（1.5秒）。演出はやり直さない。
3. **テキスト待ち**（WaitingText）：成功したら、`EntryCanvasController.IsTextArrived`（テキスト[0]がターゲットに着いた瞬間に true）を待つ。この間はメッセージも帯も出さない。リングが上がり切ってから `_entryCanvasDelay`（0.5）＋ `_textStartDelay`（0.05）＋ `_textDuration`（0.15）≒ 0.7秒後。測り直しで遅れたときは、着いていればすぐ次へ。
4. **合図待ち**（WaitingTrigger）：**テキストが入り込むのと同時に** `_triggerMessage` を出し、合図のポーズ `_triggerButton`（初期値：妨害弾＝片手を前に突き出す。Boost・Shield にも変えられる）を待つ。出してから `_triggerEnableDelay`（0.3秒）は受け付けない。Enter（SkipCalibration）で飛ばしたときもテキストを待つ。
5. **遷移演出**（Transitioning）：合図が来たら `EntryTransitionController.Play()` と `_onTriggered` を呼ぶ。遷移演出が **delay2 に入ったら**（`HasEnteredSecondDelay`）`ScreenTransition.Cover()` で画面を覆い始め、**覆い切ったら**（`IsCovered`）`ScreenTransition.LoadSceneAndReveal(_nextSceneName)`（初期値 `GameScene`）でシーンを移る（GameScene で黒から明ける）。EntryTransitionController が無いときは合図ですぐ覆い始める。
   - **シーンの読み込みは ScreenTransition だけが行う**（2026-11-25 に統一。それまでは EntryTrackingFlow の `SceneManager.LoadScene` と ScreenTransition の `LoadSceneAsync` の2か所にあった）。空・Build Settings に無いときは ScreenTransition がエラーを出して false を返し、フローは合図の前の状態に戻る。
   - `_transitionController`・`_screenTransition` は未指定ならシーン内から探す。ScreenTransition がシーンに無ければ自動で作る（読み込みが必ずそこを通るように）。
   - `_transitionDuration` は廃止（覆う時間は ScreenTransition の `_coverDuration`）。
   - 合図の前の状態に戻るときも演出は消さない（終わった時点の見た目のまま。リセットは EntryTestInput の F キー）。
   - `_loadNextScene`（初期値 ON）を OFF にすると、覆い切った時点で**合図の前の状態に戻る**（演出の確認用）。画面は黒のまま残るので C（画面トランジション）・F（遷移演出）で戻す。
   - 遷移演出中は見失いでのやり直しをしない。キャリブレーション中に入ったときは、その間に届いた測定の結果も受け取っておき、戻ってから処理する。
   - 2026-11-25：追加。シーンを移るときに入力が届いたか分かるように演出を入れるため。
- 演出が始まった後に人物が `_abandonTime`（5秒）映らなかったら、プレイヤーが去ったとみなして最初に戻る（測定の中止・`ResetAnimation()`・`PoseCalibration.Clear()`）。
- 合図の判定は `TrackingRaceInput`（設定アセットを共有）。「腕を戻すまで次は出ない」仕組みを保つため、どの状態でも毎フレーム評価している（合図待ちになる前から突き出していた腕では反応しない）。
- キーボードは読まない。テスト用の窓口：`PressTrigger(ボタン)`（`_triggerButton` と同じボタンのときだけ、次の Update で合図として使う。合図待ちでなければ捨てる）／`SkipCalibration()`（待機とキャリブレーションを飛ばす。待機中だけ）／`StartTransitionForTest()`（どの状態からでも遷移演出。遷移演出中・シーンを移っている最中は効かない）／`ToggleMessages()`（案内と見失いの警告の表示を切り替える）。どれも EntryTestInput から呼ぶ。
- 合図はトラッキングの入力（`TrackingRaceInput`）だけで判定する。Receiver が無いときは入力なし（開始と合図は EntryTestInput からだけ）。
- 表示のオンオフ（演出の確認用）：`_showMessages`（下の案内）と `TrackingLossWarningView._showWarning`（中央の警告）。どちらも再生中に Inspector で変えてもすぐ反映する。`_warningView` は未指定ならシーン内から探す。
- 案内のメッセージは画面の下の帯の上に出す（仮の表示。自分で Canvas を作る。sortingOrder 900＝見失いの警告より奥）。文言と帯の色はメッセージごとに Inspector で変えられる（初期値はどれも見失いの警告と同じ赤。透明度0で帯なし）。合図のポーズを変えたら `_triggerMessage` も変える。
- イベント：On Entry Started / On Ready / On Triggered / On Abandoned（正式な UI・演出をつなぐ用）。

## EntryTransitionController（遷移演出）
- 窓口：`Play()`（最初から始める）／`ResetEffect()`（最初の状態に戻す）／`IsPlaying`／`IsFinished`／`TotalDuration`／`HasEnteredSecondDelay`（delay2 に入ったか。画面トランジションを始める合図）。
- 時間は `Time.deltaTime`、イージングは `DOVirtual.EasedValue`（ほかのエントリー演出と同じ）。全体の長さは `_firstDelay + _secondDelay + _moveTime`。
- 区間ごとの動き（2026-11-25 にユーザーの指定で作り直し）：

| 区間 | 背景（`_parentObject`） | 色（`_Color2`） | テキスト（`_textObject`、背景の子） |
|---|---|---|---|
| delay1（`_firstDelay`） | 元の位置 | = `_Color1` | 何もしない（表示状態も触らない） |
| delay2（`_secondDelay`） | 入った瞬間に `_targetPosition` へ直接移動（補間なし）して止まる | = 元の `_Color2` | 表示（`SetActive(true)`）し、元の位置 + `_textOffset1` |
| move（`_moveTime`） | `_targetPosition` → 元の位置へ上がる（`_moveEase`） | = `_Color1` | 入った瞬間に元の位置 + `_textOffset2`、そこから「背景が着いたときの位置」へ、背景より `_textMoveLead` 秒早く着く |

- テキストの元の位置は **`Play()` を呼んだ瞬間の位置**（他の演出で初期位置からずれているため）。背景の元の位置は初期化のとき。
- テキストは子なので、画面上での位置（背景が `_targetPosition` にいるときが基準）から背景が上がった分を引いてローカル位置にする（背景の拡大・回転は無い前提）。
- **終わっても元に戻さない**（色は `_Color2 = _Color1` のまま）。戻すのは `ResetEffect()`（テストでは EntryTestInput の **F キー**）：背景の位置と色を戻し、テキストは `Play()` のときの位置に戻して**非表示**にする。`Play()` はテキストの表示状態には触らない。
- 色のマテリアルは実行時に複製する（`Image.material` は共有のマテリアルを返すので、そのまま書き換えるとアセットが変わってしまうため）。
- 見た目の確認のため、シーンでは長めの時間にしている。確認が終わったら短くする。

## ScreenTransition（画面トランジション）
参考にしたのは「左の画面外の一点から光の筋が右へ扇状に飛び出し → 左から暗くなり → 真ん中に横向きの光の線が1本残り → 真っ黒」の演出（ユーザーの録画。コマ送りなので秒数は参考にならない）。
- **シェーダー `Entry/ScreenTransition`**（`Entry/Shader/ScreenTransition.shader`、HLSL の UI 用。乗算済みアルファ `Blend One OneMinusSrcAlpha`）：`_Progress` 0→1 で上の順に出す。`_Reveal` 0→1 で全体を透明にする（開ける用）。`_Aspect` は C# が毎フレーム入れる。
  - 光の筋：`_Origin` を中心に角度を `_StreakCount` に分け、乱数で筋を置く（3層を重ねる）。先端は `(進み具合 − ずれ) × 速さ` で外へ進み、`_StreakLoop` 進んだら中心から出直す（周ごとに太さ・長さ・色を選び直す）。両端がとがった形。
  - 各段階の出る・消える範囲は `_StreakRange`・`_DarkRange`・`_LineRange`・`_BlackRange`（進み具合で指定）。値はマテリアルの Inspector で調整する。
  - Unity が無いと確認できないので、Python で同じ計算をして見た目を確かめた（2026-11-25）。
- **ScreenTransition.cs**：自分の子に Canvas（Overlay、sortingOrder 1100＝見失いの警告より手前）と画面いっぱいの Image を作り、マテリアルを複製して動かす。`_material` が未指定ならシェーダーを名前で探す（エディタ用。ビルドではマテリアルの指定が必要）。
  - 状態：Idle → Covering（`_coverDuration` 1.2秒）→ Covered（`IsCovered`）→ Loading（`LoadSceneAsync`）→ WaitingReveal（`_revealDelay`）→ Revealing（`_revealDuration`、黒からフェードで明ける）→ 自分を消す。
  - シーンをまたぐため、読み込む前に親から外して DontDestroyOnLoad にする（GameScene は変えない）。
  - 時間は `unscaledDeltaTime` を `_maxDeltaTime`（0.05）で頭打ちにして使う（読み込み直後の長いフレームで飛ばないように）。
  - 窓口：`Cover()`／`LoadSceneAndReveal(シーン名)`／`ResetTransition()`／`State`／`IsCovered`。
  - `LoadSceneAndReveal` が Entry で唯一シーンを読み込む場所。読めないシーンなら false。マテリアルが無くて覆えないときは、すぐ覆い切った扱いにする（遷移は止めない）。
  - テスト：EntryTestInput の **K** で `Cover()`（シーンは移らない）、**C** で `ResetTransition()`（シーンを移っている最中は効かない）。シーンを移るまでの確認は L で行う。
  - EntryTrackingFlow から、遷移演出の delay2 に入ったときに `Cover()`、覆い切ったら `LoadSceneAndReveal()` が呼ばれる。

## テスト用のキーボード（EntryTestInput）
2026-11-25：**Entry のキーボード入力は EntryTestInput に集めた**（ユーザーの希望。各スクリプトは窓口のメソッドを持つだけで、キーボードを読まない）。`_enableKeyboard` で全部のキーを一括でオン・オフする。本番のシーンには置かない（置かなければキーボードでは何も起きない）。参照は未指定ならシーン内から探す（ScreenTransition は K・C が押されたときに探す。流れが自動で作ることがあるため）。

| キー | 呼ぶもの |
|---|---|
| P／O | `EntryEffectsController.StartAnimation()`／`EntryCanvasController.StartEffect()` |
| R／T | `EntryEffectsController.ResetAnimation()`／`EntryCanvasController.ResetEffect()` |
| Q／Space／左Shift | `EntryTrackingFlow.PressTrigger(Jammer／Boost／Shield)`（合図にしているボタンだけ効く） |
| Enter | `EntryTrackingFlow.SkipCalibration()`（カメラなしのテスト用） |
| L | `EntryTrackingFlow.StartTransitionForTest()` |
| M | `EntryTrackingFlow.ToggleMessages()` |
| F | `EntryTransitionController.ResetEffect()` |
| K／C | `ScreenTransition.Cover()`／`ScreenTransition.ResetTransition()` |

## 現状と残っていること
- 2026-11-25：キーボード入力を EntryTestInput に集めた（EntryTrackingFlow の `_allowKeyboard`・キーボードの合図、EntryTransitionController の `_allowResetKey`、ScreenTransition の `_allowKeyboard` は廃止）。コンパイルは確認済み。Unity での確認はまだ。
- 2026-11-25：画面トランジションの見た目はユーザーが OK。遷移演出の delay2 で画面トランジションを始め、覆い切ったらシーンを移るようにつないだ。シーンの読み込みを ScreenTransition に統一。コンパイルは確認済み。Unity での確認はまだ。
- 2026-11-25：画面トランジション（ScreenTransition.cs ＋ ScreenTransition.shader）を追加。C# のコンパイルは確認済み。シェーダーと見た目は Unity での確認がまだ。EntryTrackingFlow へのつなぎ込みはこれから。
- 2026-11-25：EntryTransitionController を delay1 → delay2 → move の3区間に作り直した（上の表）。終わってもリセットせず、F キーでリセット。コンパイルは確認済み。Unity での確認はまだ。
- 2026-11-25：EntryTransitionController にテキストのずれ（遅れて下がり、先に戻る）・イージング・Tooltip を追加し、変数を整理した。コンパイルは確認済み。Unity での確認はまだ。
- 2026-11-25：EntryTransitionController（窓口と仮の中身）を作り、EntryTrackingFlow とつないだ。コンパイルは確認済み。シーンへの配置と Unity での確認はまだ。
- 2026-11-25：案内と見失いの警告の表示のオンオフ（Inspector・M キー）を追加。コンパイルは確認済み。Unity での確認はまだ。
- 2026-11-25：L キー・`_loadNextScene`・`_transitionDuration`（Transitioning の状態）を追加。コンパイルは確認済み。Unity での確認はまだ。遷移演出の中身はこれから作って `_onTriggered` につなぐ。
- 2026-11-19：EntryTrackingFlow を追加し、EntryTest に配置。
- 2026-11-19：**リングの上昇・%表示・キャリブレーションの終わりが連動することを Unity で確認済み**（リングが上がり切ったときに測定を終える方式）。合図のメッセージ・妨害弾のポーズでのシーン遷移・プレイヤーが去ったときのやり直しは、まだ確認していない。待機中は、見失いの警告（赤い帯、3秒後）と下の案内が両方出る。気になる場合は相談する。
- 2026-11-05：上記を実装。`EntryTest.unity` にはエミッターが2つ配置済みで、`_edgeEmitters` にも登録済み。
- コンパイルと見た目の確認は Unity 上でユーザーが行う。値の調整はこれから（線と粒のパラメータは仮の初期値）。
