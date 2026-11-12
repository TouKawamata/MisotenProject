# トラッキング入力 設計メモ

カメラの姿勢推定（Python）から受け取った姿勢を、レースの入力（`IRaceInput`）に変える機能の仕様・構成・経緯をまとめたもの。トラッキングの作業を始めるときは、まずこのファイルを読む。
作業ルール（作業場所・既存ファイルを変えるときは承認を得る・コードの書き方・パスの書き方）は `AT_Miyata/CPU/CPU_DesignNotes.md` と同じ。
作業の進め方：まず設計を提示して、了承をもらってから実装する。

## 全体の流れ
```
カメラ → pose_sender.py（YOLO / MediaPipe）→ OSC（UDP 9000番）
  → PoseLandmarkReceiver（受信・座標変換）
  → TrackingRaceInput（IRaceInput：Move / Boost / Shield の判定）
  → RaceInputProvider（AT_Kawamata）→ PlayerManager
```
- `TrackingRaceInputBootstrapper` が起動時に `RaceInputProvider.SetInputSource` で `TrackingRaceInput` に差し替える（Receiver が無い・デバッグ設定ONならキーボード）。
- しゃがみの判定に使う直立姿勢は、Entry シーンで `PoseCalibrator` が測って `PoseCalibration`（static）に保存する。

## 前提（既存の仕組み。AT_Kawamata側、変更しない）
- `IRaceInput`（2026-11-07 に変更）：
  - `Vector2 Move`：x が右＋／左－、y が上＋／下－。**大きさは0～1に制限して渡す**（`Vector2.ClampMagnitude`）。向きと強さはそのまま渡し、8方向への丸めは飛行制御側（`MoveDirectionResolver`）が行う。デッドゾーン・ぶれの除去は入力側の仕事。
  - `BoostPressed / BoostHeld / ShieldPressed / ShieldHeld`。Pressed は成立した1フレームだけ true、Held は続けている間 true。
  - `JammerPressed`（2026-11-12 に develop で追加。妨害弾の発射）：初期値が `=> false` の既定の実装。発射間隔・残弾は `JammerLauncher` 側が管理する。キーボードは Q。
  - ショートカットは仕様から廃止（`ShortcutPressed` は無い）。

## 構成（`AT_Miyata/Scripts/`）
```
PoseLandmarkReceiver.cs           OSC受信。2D/3Dを33点の並びにそろえ、縦横比を補正。IsTracking（見失い判定）
                                  PoseLandmark（構造体）・PoseLandmarkIndex（関節番号の定数）もここ
TrackingRaceInput.cs              IRaceInput実装。Move（傾き・バンザイ・しゃがみ）とBoost/Shield/Jammerの判定
TrackingRaceInputSettings.cs      判定の調整値（Serializable。Bootstrapper・DebugViewerのInspectorで編集）
TrackingRaceInputBootstrapper.cs  起動時に RaceInputProvider の入力元を差し替える
PoseCalibration.cs                キャリブレーション結果（static。シーンをまたいで残る）
PoseCalibrator.cs                 直立姿勢を測るコンポーネント（Entryシーン用）
TrackingLossMonitor.cs            見失いの見張り役（Ok / PersonLost / Disconnected）。表示はしない
TrackingLossWarningView.cs        見失いの警告の表示（仮）。UIが未指定なら専用のCanvasを自動で作る
TrackingRaceInputDebugViewer.cs   PoseTestシーン用の表示（Move・Boost/Shield・トラッキング状態・キャリブレーション）
PoseDebugViewer.cs                受信状況の表示（FPS・受信回数）
PoseLandmarkVisualizer.cs         33点を球で表示
```
テストシーン：`AT_Miyata/Scenes/PoseTest.unity`。Prefab：`AT_Miyata/Prefabs/Completed/TrackingInput.prefab`。

## Python 側（pose_sender.py）から届くデータ
Python は別リポジトリ（`AI_PoseTracker/PoseTracker/pose_sender.py`）。起動時にカメラ・モード（2D/3D）・送信先IP・カメラの向き（回転補正）を選ぶ。

| アドレス | 内容 |
|---|---|
| `/pose/landmarks3d` | 3Dモード（YOLOで人物を切り出し → MediaPipe）。33点 × (x, y, z, visibility) = 132要素 |
| `/pose/landmarks` | 2Dモード（YOLO-Pose）。COCO順の17点 × (x, y, confidence) = 51要素 |
| `/pose/frame_size` | 回転補正後の映像サイズ [幅, 高さ]（**int**）。毎フレーム、ランドマークより先に届く |

- x, y は映像の幅・高さでそれぞれ 0～1 に正規化されている（左上が原点、y は下向き）。
- 画面の中央に近く大きく映っている1人だけを送る。**人物がいないフレームではランドマークを送らない**（frame_size だけ届く）。
- 推定は反転していない映像で行っているので、左右（Left/Right）は本人から見た左右。

## 受信（PoseLandmarkReceiver）
- 2Dモードの17点は、MediaPipe の33点の並びに詰め替える（対応表 `CocoToMediaPipe`）。対応の無い関節（指先・かかと等）は信頼度0。これで判定・表示側はモードを意識しない。
  - 使える関節：鼻0、目2/5、耳7/8、肩11/12、肘13/14、手首15/16、腰23/24、膝25/26、足首27/28。
- 2種類の座標を持つ：
  - `NormalizedLandmarks`（**判定用**）：映像の中心が原点、右＋x・上＋y、y は映像の高さを1とし、**x にも縦横比（幅÷高さ）を掛けて同じ単位にそろえる**。鏡像反転（`_mirrorX`）は反映済み。Transform・scale の影響を受けないので、シーンをまたいで値を比べられる（キャリブレーションの値もこの単位）。
  - `Landmarks`（表示用）：上の値に `_scale` を掛けて Transform でワールド座標にしたもの。
- `IsTracking`：最後にランドマークを受信してから `_lostTimeout`（0.3秒）以内なら true。
- `IsConnected`：最後に何かのメッセージ（frame_size・ランドマーク）を受信してから `_disconnectTimeout`（1秒）以内なら true。frame_size は人物がいなくても届くので、false なら Python・カメラの停止かネットワークの断線。`HasConnected` は一度でも受信したか。
- uOSC の値は object 型で届く。frame_size は int なので、`Convert.ToSingle` で変換する（`(float)` でキャストすると例外になる）。
- `OnPoseUpdated` は 2D・3D どちらでも呼ばれる。

## 判定（TrackingRaceInput）
1フレームに1回だけ評価してキャッシュする（`Time.frameCount`）。しきい値はすべて**胴の長さ（肩の中点〜腰の中点。画面上の x, y の距離）に対する比率**で、カメラからの距離によらない。
**見失っている（`IsTracking` が false）ときは、Move を0、Boost/Shield を離した扱いにする。**

### Move
| 方向 | 体の動き | 計算 |
|---|---|---|
| x | 上半身を左右に傾ける | 腰の中点→肩の中点のベクトルの傾き角度。`tiltDeadZoneAngle`〜`maxTiltAngle` を0～1にしてカーブをかける |
| y＋ | 両手を上げる（バンザイ） | **低い方の手**が鼻よりどれだけ上か。`raiseStartRatio`〜`raiseFullRatio` を0～1に。手首が映らない腕は肘で代用 |
| y－ | しゃがむ | キャリブレーションした直立時の肩の高さから、肩がどれだけ下がったか（**直立時の**胴の長さに対する比率）。`crouchDeadZoneRatio`〜`crouchFullRatio`。**キャリブレーションしていないと0** |

- y は「上 − 下」。x・y をそれぞれ平滑化（`horizontalSmoothingTime` / `verticalSmoothingTime`）してから `ClampMagnitude(…, 1)`。
- 反転：`invertHorizontal` / `invertVertical`。

### ボタン
- **Boost**：空手の構え。両手首が「腰〜肩を `boostHeightRatio` で補間した高さ」の近くで、体の横にある。MediaPipe の腰は股関節付近なので、腰の高さそのものを基準にすると腕を下げただけで成立してしまうため。
- **Shield**：片手（どちらでも）を胸の前で水平に構える。手首が胸の高さ・前腕が水平・手首が肘より体の中心側・**前腕が画面上で `shieldMinForearmRatio`（0.3）以上の長さに見える**、の4条件。4つめは、前に突き出した腕（Jammer）は前腕が縮んで見えて「水平」を満たしてしまうため。
- **Jammer**（`JammerPressed`）：片手（どちらでも）をカメラに向けて前に突き出す。正面からは手首・肘・肩が重なって見えることを使う。片腕ごとに：
  1. 肩・肘・手首の信頼度が `jammerMinVisibility`（0.3）以上（腕がカメラを向くと関節が重なって信頼度が下がるので、共通の 0.5 より低い）
  2. 手首と肩の画面上の距離 ≤ `jammerWristRadiusRatio`（0.35）
  3. 肘と肩の画面上の距離 ≤ `jammerElbowRadiusRatio`（0.3）（手で顔・肩を触ったときは肘が下がっているので区別できる）
  4. 3Dモードかつ `jammerUseDepth` のときだけ：手首が肩より `jammerDepthRatio`（0.2）以上手前（z。MediaPipe の z は映像の幅の単位なので縦横比を掛けてそろえる）
  - ポーズが `jammerHoldTime`（0.08秒）続いたら1回だけ true。腕を戻して `jammerReleaseTime`（0.15秒）たつまで次は撃てない（突き出したままでは連射しない）。時間は `unscaledDeltaTime`。
  - Held は無い。デバッグ用に `JammerPoseActive`、腕ごとの途中経過 `LeftJammerDebug` / `RightJammerDebug`（`JammerArmDebug`）を公開。

## 見失いの警告（TrackingLossMonitor / TrackingLossWarningView）
- 目的：検出がしばらくできないとき、画面に警告を出す（今は表示だけ。レースは止めない。入力は見失った時点で0になっている）。
- `IsTracking`（0.3秒。入力用）とは別に、`TrackingLossMonitor` が警告を出すかどうかを決める。時間は `Time.unscaledTime`。
  | 状態 | 条件 |
  |---|---|
  | `Ok` | 検出できている（途切れても `_warningDelay`（2秒）未満ならここ） |
  | `PersonLost` | `IsConnected` だが、検出できない時間が `_warningDelay` を過ぎた |
  | `Disconnected` | 検出できず、`IsConnected` も false |
  - 警告を消すのは、検出が `_recoverTime`（0.5秒）続いてから（一瞬映っただけで点滅しないように）。
  - 起動してから一度も受信していないときは `_startupGrace`（3秒）を過ぎてから警告。Receiver が無いシーンでも猶予の後 `Disconnected`。
  - 公開：`State`・`IsWarning`・`LostDuration`、C# の `StateChanged`、Inspector の On Warning Started / On Warning Ended。
- `TrackingLossWarningView`（仮の見た目）：画面中央の横帯＋メッセージ（点滅）、フェード 0.2秒。
  - `PersonLost`：「カメラの前に立ってください」／`Disconnected`：「カメラとの接続が切れています（スタッフを呼んでください）」。文言は Inspector で変えられる。
  - `_canvasGroup`・`_messageText` が未指定なら、自分の子に Canvas（Overlay、sortingOrder 1000、1920x1080基準）を作る → **空の GameObject に付けるだけで動く**。見た目を差し替えるときは参照を割り当てる。
  - Monitor が無ければ自分のオブジェクトに追加する。フォントは追加したとき（Reset）に `NotoSerifJP-Bold SDF`（TeamResources）を自動で入れる。
  - **TrackingInput.prefab には入れていない**（他のメンバーのシーン・GameScene でも使われているため）。各シーンに個別に置く。

## キャリブレーション（PoseCalibrator / PoseCalibration）
- 目的：しゃがみ（y－）の基準になる直立時の肩の高さと胴の長さを測る。
- `StartCalibration()` から `_duration`（1.5秒）の間、受信したフレームのうち肩・腰の4点が見えているものを集めて平均する。
- 失敗の条件（失敗したら保存済みの値は変えない。理由は `LastFailureReason`）：
  - 集まったフレームが `_minSampleCount`（15）未満
  - 肩の高さのぶれ（標準偏差）が胴の長さ × `_maxShoulderDeviationRatio`（0.1）を超えた
- 結果の受け取り：C# の `Finished(bool)` イベント、または Inspector の On Succeeded / On Failed。進み具合は `Progress`・`ElapsedTime`・`Duration`。
- `PoseCalibration` は static なので、Entry シーンで測った値がレースのシーンでも使える。プレイヤーが交代したら `PoseCalibration.Clear()`。
- **注意**：基準は画面上の肩の高さなので、キャリブレーションの後に立つ位置（カメラからの距離）が変わるとずれる。Entry とレースで立つ位置を固定する前提。

## デバッグ（PoseTest シーン）
- `TrackingRaceInputDebugViewer`：Jammer の行（ポーズ中は HOLDING、発射回数）と、左右の腕の「手首/しきい値・肘/しきい値・z/しきい値」（成立した腕は緑）。ほかに Source、トラッキング状態、見失いの警告の状態（Monitor がシーンにあれば。検出なしの秒数）（OK/LOST・2D/3D・縦横比）、キャリブレーション状態、Move の数値・スティック・x/y のグラフ、Boost/Shield の HOLDING とトリガー回数を表示する。
- **C キー**：`_calibrationStartDelay`（1秒）待ってからキャリブレーション開始（テスト用）。待ち時間（「開始まで 0.6秒」）と測定の経過（「測定中 0.8 / 1.5秒」）を表示する。待ち中・測定中に押し直すとやり直し。
  - シーンに `PoseCalibrator` が無ければ、DebugViewer と同じオブジェクトに自動で追加する。
- **X キー**：キャリブレーションの結果を消す（`PoseCalibration.Clear()`）。開始待ち・測定中なら中止する。未キャリブレーションの動きを確かめる用。
  - なお、このプロジェクトは再生のたびに Domain Reload するので、再生し直しても static の結果は消える。
- `_useKeyboardForDebug`：機材なしで画面を確認する（WASD／矢印、Space、左Shift）。
- Unity を使わないコンパイル確認は CPU_DesignNotes.md の「デバッグ」のコマンドを使う。対象のファイルを差し替え、参照に `uOSC.Runtime.dll` と `Unity.InputSystem.dll`（どちらも `Library/ScriptAssemblies/`）を足す。`Assembly-CSharp.dll` が古いときは `AT_Kawamata/Input/Scripts` の `IRaceInput.cs`・`KeyboardRaceInput.cs`・`RaceInputProvider.cs` も一緒にコンパイルする。

## 経緯
- 2026-11-07（川又）：`IRaceInput` が `Horizontal`（float）から `Move`（Vector2）に変更、ショートカット廃止。トラッキング側は暫定対応（Move.x に傾きを入れ、y は0）。
- 2026-11-11：
  - 上下の入力を追加：上＝バンザイ、下＝しゃがみ（キャリブレーション基準）。ショートカットの処理と `shortcutRaiseMarginRatio` を削除。
  - Python 側の変更に対応：2Dモード（YOLO 17点）、`/pose/frame_size` による縦横比の補正、見失い時の入力なし。
  - 判定を `NormalizedLandmarks`（Transform に依存しない座標）で行うように変更。胴の長さは 2D に合わせて画面上の距離で測る。
  - `PoseCalibrator` / `PoseCalibration` を追加。PoseTest で C キー（1秒後に開始）。
  - Receiver・DebugViewer のフィールド名を変えたところは `FormerlySerializedAs` を付けて、シーン・Prefab の値を引き継いでいる。

- 2026-11-12：見失いの警告を追加（`TrackingLossMonitor`・`TrackingLossWarningView`、Receiver に `IsConnected`）。表示だけ・レースと Entry の両方に出す・時間は 2秒／0.5秒／1秒、で了承済み。UI は仮。

- 2026-11-12：しゃがみの反応が遅かったため、`verticalSmoothingTime` を 0.1 → **0.01** に変更（PoseTest で確認して OK）。上下共通の値なので、バンザイ（y＋）も同じく速くなる。
- 2026-11-12：妨害弾（`JammerPressed`）に「片手を前に突き出す」を割り当て。xy の重なり＋3Dモードでは z。成立までの待ち 0.08秒・z を使う・シールドに前腕の長さの条件を足す、で了承済み。しきい値は仮。

## 現状と残っていること
- コンパイル確認済み（Unity 外）。**Unity での動作確認と値の調整はまだ**。
- 縦横比の補正で、傾きの角度や横方向の距離が以前より正しく（大きく）出るようになった。3Dモードの胴の長さも以前（z を含む距離）と少し変わる。**Boost/Shield/傾きのしきい値は実機で調整し直す必要がありそう**。上下のしきい値（`raise*`・`crouch*`）は仮の値。
- **【次回の課題】妨害弾とシールドが同時に発動する**（2026-11-12、PoseTest で確認）。値か条件の修正が必要。まず DebugViewer の `L:` `R:` の行と Shield の HOLDING を見て、どのケースかを切り分ける：
  - (a) 突き出しきった状態でもシールドが成立している → 腕が真っすぐカメラを向いておらず、前腕が 0.3 以上に見えている。`shieldMinForearmRatio` を上げる（0.45 前後）。上げすぎると普通のシールドが出にくくなるので、シールドのポーズ時の前腕の長さも見る。
  - (b) 腕を突き出す途中で一瞬シールドの形を通っている（Shield の Trigger だけ先に増える）→ シールドは待ち時間なしで成立するため。候補：シールドにも短い待ち時間（`shieldHoldTime`）を入れる／妨害弾のポーズに近い腕（肘と肩の距離が `jammerElbowRadiusRatio` × 1.5 以下など）はシールドの判定から外す／妨害弾のポーズ中はシールドを出さない（優先順位）。
  - (c) 反対の腕がシールドの形になっている → 判定の問題ではなく、ポーズの取り方の問題。
  - どれにするかは、切り分けの結果を見てユーザーと相談して決める。
- 妨害弾：コンパイル確認済み（Unity 外）。**Unity での確認と値の調整はまだ**。腕がカメラを向く姿勢は姿勢推定が苦手なので、特に 2Dモード（YOLO）で関節が取れるかを確認する。シールドの前腕の長さの条件で、今までのシールドが出にくくなっていないかも確認する。
- 見失いの警告：コンパイル確認済み（Unity 外）。**シーンへの配置と Unity での確認はまだ**（レース・Entry・PoseTest）。Entry には Receiver がまだ無い（TrackingInput.prefab は Bootstrapper が RaceInputProvider を必要とするので、Entry には uOscServer＋Receiver だけを置く必要がある）。正式な見た目は別途。
- Entry シーンへの `PoseCalibrator` の組み込み（いつ `StartCalibration()` を呼ぶか、失敗時の演出）はこれから。
