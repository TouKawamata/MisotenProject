# CPU（AI）設計メモ

CPUレーサーの「判断」（入力）を作る機能の仕様・構成・経緯をまとめたもの。次の作業者（人・Claude）が前提をすぐ把握できるようにする。
（他のメンバーの Claude 用の設定とぶつからないように、ファイル名は `CLAUDE.md` にせず `CPU_DesignNotes.md` にしている。CPU の作業を始めるときは、まずこのファイルを読む）

## 作業ルール（重要）
- 新規作業は `Assets/Deverop/AT_Miyata` 内で行う（フォルダ名は `Develop` ではなく `Deverop`）。担当者はAT_Miyata。
- **既存ファイル（AT_Kawamata など他メンバーのスクリプト・シーン・Prefab）を変更する場合は、どのファイルをどう変えるかを提示して、承認を得てから変更する。**
- コードは周りに合わせる：日本語コメント、`_field`、波括弧は省略しない、Tooltip/Headerを付ける、namespaceなし。
- 作業するPCがよく変わるので、このメモにユーザー名入りのパスを書かない（詳しくは「デバッグ」）。
- 数値調整の進め方：変更 → コンパイル確認（「デバッグ」のコマンド）→ ユーザーが Unity で確認 → OK が出たら「現状と残っていること」に日付つきで記録する。

## 前提（既存の仕組み。AT_Kawamata側、変更しない）
- `CPUManager : RacerManagerBase` が CPU 本体。入力だけを `ICpuRaceInput`（`IRaceInput` に `UpdateInput(CPUManager owner, float dt, IReadOnlyRacerData data)` を足したもの）から受け取る。
- `RaceManager` の CPU の欄に入れた CPU は `SetRacers` → `Initialize` → `PlaceAt` → 毎フレーム `Tick` の順に呼ばれる。`UpdateInput` は Tick の最初に1回呼ばれる。カウントダウン中・ゴール後は呼ばれない。
- 入力（`IRaceInput`）：`Move`（Vector2。x＝右、y＝上、大きさ0～1）、ブースト・シールドの Pressed/Held、`JammerPressed`（妨害弾。1Tickだけ true）。2026-11-07 にショートカットは廃止、移動は上下左右の平行移動になった（`FlightTuningProfile` の設定で8方向に丸められることがある）。
- ボタンの決まり（仕様書「CPU入力 データ早見表.md」より）：Pressed は1Tickだけ true、**Pressed の Tick も Held を true にする**、ブーストとシールドは同時に使えない、スタン中は入力が無視される、値は `UpdateInput` の中でだけ変える。
- RacerId は Player が 0、CPU は 1 から。距離・順位は1フレーム前の値。他のレーサーの速度・ゲージは読めない（速度は `DistanceOnCourse` の差から自分で出す）。
- `ICourseGuide`（`owner.CourseGuide`）でコースの中心・左右・上下・半径・先読みの位置が取れる。`GetRight` が Move.x の＋側、`GetUp` が Move.y の＋側。
- 妨害弾：`JammerLauncher`（CPURacer.prefab にも付いている）。標的は発射台が決める（自分より順位が高い中で一番近い相手）。種類は `SelectType` で切り替える。`owner.CanFireJammer` で撃てるか分かる。弾（`JammerProjectile`）の種類は `Type`、狙っているレーサーは `TargetRacer`（リングを狙う弾では null）で読める（2026-11-18 に CPU 用に追加）。
- 効果（`owner.Effects`）：減速・上下左右が遅い・ブースト禁止・視界反転。**視界反転は、CPUでは移動の入力が反転される**（`RacerManagerBase`）。
- ギミック（`CourseGimmickBase` の派生）：加速リング `SpeedRing`（AT_Minakuchi。妨害弾で減速リングに変わる。今減速リングかは `IsSlowing`）、爆弾 `Bomb`、減速エリア `SlowZone`（AT_Takahashi）。

## 構成（`AT_Miyata/CPU/`）
```
Scripts/
  CpuRaceInput.cs            窓口（MonoBehaviour, ICpuRaceInput）。CPUManager の Input Source に入れる
  CpuDifficultyProfile.cs    難易度＝うまさ（ScriptableObject）
  CpuPersonalityProfile.cs   個性＝好み（0～1の重み、表示名・色）（ScriptableObject）
  CpuDifficultyController.cs 全CPUの難易度を一括で変える窓口（シーンに1つ。static Current）
  CpuSituation.cs            そのTickの状況（CpuRacerView：前後の差・断面でのずれ(Vector2)・相対速度、CpuGimmickView：前方のギミック）
  CpuPerception.cs           状況をまとめる。前後の差＝DistanceOnCourseの差、ずれ＝GetRight・GetUpへの射影。前方のギミックを近い順に集める
  CpuWorldCache.cs           シーンのギミック・妨害弾を全CPUで共有して探す（ギミックは1秒ごと、弾は1フレーム1回）。ギミックとの当たりの計算
  CpuTargetSelector.cs       狙う相手（Player・CPUを区別しない）。執念（やり返し）
  CpuShieldBrain.cs          シールド（後ろのブースト・飛んでくる妨害弾・爆発しそうな爆弾→反応時間→成功率で1回だけ抽選）
  CpuBoostBrain.cs           ブースト（攻撃／加速、追い上げ補正、RankRatio）
  CpuSteeringBrain.cs        上下左右（断面の候補点にコストを付けて目標の位置を選び、2次元のPD制御で追う）
  CpuJammerBrain.cs          妨害弾（撃つ好機か、どの種類か）
  CpuButtonLatch.cs          「押し続けたい」→ Pressed/Held。始まらなければ一度離して押し直す
  CpuLabelDrawer.cs          頭上の表示（Game：OnGUI、Scene：Handles）
  CpuDebugViewer.cs          デバッグ用：全CPUの一覧・CPUを追う観察用カメラ・狙いの線（テストシーンに置く）
Editor/
  CpuProfilePresetCreator.cs メニュー Tools > CPU > Create Default Profiles
  CpuGimmickTestSceneCreator.cs メニュー Tools > CPU > Create Gimmick Test Scene（CPUTest_2 から背景・UIを省いたシーンを作る）
Profiles/                    難易度3つ・個性5つのアセット
Prefabs/                     CPURacer の Prefab Variant（個性ごと。ユーザーが作成）
```
テストシーン：`AT_Miyata/Scenes/CPUTest_2.unity`（2026-11-18 に `CPUTestCourse.unity` から切り替えた。旧シーンは削除）。
ギミック確認用：`AT_Miyata/Scenes/CPUGimmickTest.unity`（Tools > CPU > Create Gimmick Test Scene で CPUTest_2 から作る）。
- CPUTest_2 をコピーし、背景（ocean・Plane・stadium・tower・building・signboards・Decal_Stadium・Mid_Ring）、UI（Minimap・PauseMenu・PlayerPoseStatusUIController・DebugHud）、AudioManager、スタート・ゴールの演出を消す。コース・Player・エフェクト・CPU・空（ライトがここにある）・Canvas（カウントダウン）は残す。
- 残すオブジェクトから参照されているものは消さない（Console に「〇〇から参照されているため残しました」と出る）。RaceIntroDirection だけは例外（RaceManager の Intro は未設定でよい）。
- コースに沿って 150m から 100m おきに、加速リング → 爆弾 → 加速リング → 減速エリア → 減速リングの順に置く（`------- Gimmicks（自動配置） -------` の下。断面の中のずらす方向は固定の乱数）。
- 既にシーンがあれば何もしない（作り直すときは消してから実行）。

## 1Tickの流れ（CpuRaceInput.UpdateInput）
1. 難易度・個性がなければ入力なし（警告は1回だけ）。`DistanceOnCourse` が20m以上戻ったら置き直しとみなして `ResetState`。
2. `CpuPerception.Update`。コースが取れなければ入力なし＋警告（Course Spline 未設定のとき）。
3. 考え直すTick（難易度の ThinkInterval ごと）かどうかを決める。
4. `CpuTargetSelector.Update`（毎Tick最新化、選び直すのは考え直すTickだけ）
5. `CpuShieldBrain.Update`（毎Tick。反応時間を秒で数えるため）、`CpuJammerBrain.Update`（撃つかは考え直すTickだけ）
6. 考え直すTickだけ：`CpuBoostBrain.Think`、`CpuSteeringBrain.Think`（目標の位置）
7. `CpuSteeringBrain.Evaluate`（毎Tick、2次元のPD＋Perlinノイズ）→ 視界反転に慣れていれば、同じ軸を先に反転して Move にする
8. 優先順位：**シールド ＞ ブースト**。スタン中は押していない扱い。`CpuButtonLatch` で Pressed/Held を作る。JammerPressed はそのTickだけ。

## 判断の中身
- **狙う相手**：点数＝近さ＋断面での位置のそろい具合＋順位ボーナス（自分より上なら0.3）＋執念ボーナス。シールド中・スタン中の相手は除外。狙う距離は攻撃性で 10～45m。今の相手が一番の相手の点数の80%以上あれば変えない。
- **執念**：`BoostHitInfo` に当てた相手が含まれないため、「自分がスタンした瞬間に、前後15m以内でブーストしていた一番近い相手」を当てた相手とみなす（既存ファイルを変えないための代わりの方法）。
- **シールド**：脅威は3種類（一番早く届きそうなもの1つ。爆弾は最優先）。
  - 後ろのブースト：一定距離以内（慎重さで 8～25m）、断面での距離3m以内、ブースト中で近づいてくる相手。自分がブースト中なら張らない（ブースト同士ははじくだけのため）。
  - 妨害弾：自分の方を向いて飛んでくる弾（慎重さで 25～60m 以内、弾の通り道から5m以内）。標的でなくても通り道にいれば当たるため。リングを狙う弾（`TargetRacer` が null）と、シールドで防げない種類（`CanBeBlockedByShield` が false）は無視する。
  - 爆弾：爆風の範囲（BlastRadius の90%）にいて、自分か誰かが爆弾に触れそう（5m以内）なとき。
  - 妨害弾・爆弾は、慎重さ0.5以上なら、ブーストをやめてでも張る。
- **ブースト**：攻撃＝相手が25m以内で断面での位置（上下左右）がそろっている（そろいの許容は難易度の AttackAlignTolerance）。加速＝先のカーブの角度が BoostCurveLimit 以下で、ゲージが閾値以上。閾値は「ブーストの使い方」、追い上げ補正（Playerとの差）、RankRatio（最下位で -0.15）で変わる。相手がシールドを張ったら攻撃をやめる。
- **上下左右**：断面の候補点（中央＋3重の円×12方向＋行きたい位置・リングの中心など）にコストを付け、一番低い点を目標にする。
  - 行きたい位置：狙う相手 ＞ 先頭なら中央 ＞ 走行ライン（方向は RacerId×137.5度でCPUごとにずらし、ライン変更の頻度でときどき変える。中央からの距離は PreferredLine×`LineSpread`(0.5)）。
  - コストを足す：後ろのブーストの通り道（シールドなしのとき）、前の近い相手と重なる位置、爆弾・減速エリア・減速リング・障害物に当たる位置（遠いギミックほど弱く）。
  - コストを引く：加速リングをくぐれる位置（加速リング志向で 1～10）。
  - ギミックに気づくかは、ギミックごとに1回だけ難易度の GimmickAwareness で抽選（見落としたものは無いものとして走る）。見る距離は GimmickLookAhead。
  - トンネル半径の80%以内に収め、壁に触れたら1.5秒間は55%以内にする。
- **妨害弾**：撃てる・前回から個性の間隔（撃ちたがりで 12～2.5秒）が経った・標的（発射台と同じ選び方）が 6m～（撃ちたがりで 60～150m）にいて、シールド中・スタン中でないときに撃つ。種類は点数で選ぶ（減速1、上下左右が遅い0.8、ブースト禁止0.5＋標的がブースト中なら1.5、視界反転0.3＋ひねくれ度×1.5、リングの切り替え0.2＋ひねくれ度×1.5（標的より先に加速リングが見えるときだけ））。難易度の JammerSmartness の確率で一番の種類、それ以外はでたらめ。リングの切り替えが撃てなかったら（発射台に Ring Targets が無い）10秒は選ばない。
- **視界反転**：効果がかかってから難易度の ViewFlipAdaptTime（±20%）の間は、反転されたまま（逆に動く）。慣れたら同じ軸を先に反転して出す。効果が切れたときも同じだけ戸惑う。
- **追い上げ補正**：速度（物理）は変えない。判断（ブーストの閾値）だけを Playerとの前後の差で変える。強さは難易度の CatchUpStrength（0で無効）。ゲーム側の `BoostRankTable`（下位ほどゲージ消費が少ない）とは別の仕組み。
- 個性の重みを実際の距離・閾値に直す換算は、各 Brain の先頭にある `private const` にまとめてある（調整はここで行う）。

## 難易度と個性
- 難易度アセット：`CpuDifficulty_Easy/Normal/Hard`（考え直す間隔、反応時間、操作の強さとぶれ、攻撃のそろい、カーブの限界、ゲージの下限、シールドの成功率、ギミックに気づく確率・見る距離、妨害弾の選び方、視界反転に慣れる秒数、追い上げ補正）。
  - 2026-11-18 追加分（Easy/Normal/Hard）：ギミックに気づく確率 0.4/0.7/1（旧ショートカット成功率の値を引き継ぎ）、見る距離 35/55/80m、妨害弾の選び方 0.2/0.6/1、視界反転に慣れる秒数 1.5/0.8/0.3。
- 個性の項目：攻撃性・慎重さ・ブーストの使い方・走行ライン・ライン変更の頻度・執念・加速リング志向（旧ショートカット志向）・妨害弾の撃ちたがり・ひねくれ度。
  - 2026-11-18 の値（加速リング志向／撃ちたがり／ひねくれ度）：アタッカー 0.4/0.9/0.3、ガーディアン 0.5/0.3/0.1、スピードスター 1/0.2/0.1、トリックスター 0.6/0.7/1、バランス 0.6/0.5/0.4。
- 個性アセット：`CpuPersonality_Attacker`（赤）／`Guardian`（青）／`Speedster`（黄）／`Trickster`（紫）／`Balance`（緑）。
- **CPUごと**：CpuRaceInput の Difficulty。**一括**：`CpuDifficultyController` を置く。Follow Global Difficulty が ON（初期値）の CPU は一括の設定に従う。実行中は `SetDifficulty()` で切り替えられ、次の Tick から反映される。
- プリセット作成メニューは、既存のアセットを上書きしない（表示名が空のときだけ、表示名・色を入れる）。

## セットアップ（案B：既存ファイルは変えない）
1. CPURacer.prefab の Prefab Variant を `AT_Miyata/CPU/Prefabs/` に作り、ルートに CpuRaceInput を付けて Difficulty・Personality を設定し、`CPUManager.Input Source` に入れる。
2. 自分用のシーン（CPUTest_2）に Variant を置き、RaceManager の CPU の欄に入れる。
3. **シーンに置いた各 CPU の `PlayerFlightController.Course Spline` に、シーンの CourseSpline を設定する。** シーンのオブジェクトなので Prefab には保存できない。未設定だと CPU は全く動かない（実際に起きた。今は CpuRaceInput が警告を出す）。

## デバッグ
- Show Debug Logs：狙う相手の変更、脅威に気づいた、ブースト（攻撃／加速）、シールド、妨害弾を Console に出す。
- Gizmos（選択中）：黄＝目標の位置、赤＝狙う相手、水色＝脅威、緑＝前方の加速リング、橙＝前方の避けるギミック。
- 頭上の表示（Show Label / Label Height）：「個性 / 難易度」の下に、1行ずつ次を出す（2026-11-19 に行を分けた）。
  - 移動：前に進む速さ（`DistanceOnCourse` の差。1m/s 未満は「止まっている」）と、上下左右の入力の向き（矢印。入力なしは「・」）。
  - 狙い：ブーストで狙っている相手と前後の距離（＋＝前）。執念の相手なら（執念）。
  - 妨害弾：撃ってから1.5秒は「★撃った！ 種類→標的」。それ以外は「発射台なし／撃てない（弾・発射間隔）／次まで○秒（個性の間隔）／好機を待っている」。
  - 今していること（あれば）：スタン、シールド：脅威の種類、攻撃・加速ブースト、リングへ、ギミック回避、反転で混乱。
  - 判断していないときは「待機中」（カウントダウン中・ゴール後）、「停止：Course Spline が未設定」「停止：難易度・個性が未設定」。
- **CpuDebugViewer**（2026-11-19）：テストシーンに空のオブジェクトを置いて付ける。本番のシーンには置かない。
  - 画面の左上に全CPUの頭上の表示と同じ内容を1行ずつ出す（カメラから遠くても見える）。F5 で表示・非表示。
  - 観察用カメラ：選んだCPUの後ろに付いていく（位置は遅らせずにぴったり付けるので、高速でも離れない。向きはコースの前・上が基準）。F2 で 小窓（右下）→ 全画面 → 切る、F3/F4 で次・前のCPU。Player のカメラはそのまま。
  - 線（Gizmos）：狙っている相手へ赤、妨害弾を撃った直後の標的へ紫。選択していなくても全CPU分。Game ビューでは Gizmos ボタンが ON のときだけ（ユーザーの希望で Gizmos だけにした。2026-11-19）。
  - Player のカメラは1人称で、先を走るCPUは画面外か遠い（頭上の表示は300mより遠いと出さない）ため、Gizmos の線や頭上の表示だけでは見えなかった（2026-11-19 にユーザーが確認）。
- **パスについて**：作業するPCがよく変わるため、このメモにはユーザー名入りのパス（`C:/Users/<名前>/...`）を書かない。プロジェクトの場所は `git rev-parse --show-toplevel`、Unity のバージョンは `ProjectSettings/ProjectVersion.txt`、ユーザーのフォルダは環境変数（`$LOCALAPPDATA` など）から、その場で求める。
- Unity を使わないコンパイル確認（プロジェクトの Unity バージョンの Roslyn を使う。Git Bash で、リポジトリ内のどこからでも実行できる）。
  **注意**：`Library/ScriptAssemblies/Assembly-CSharp.dll` は最後に Unity を開いた時点のもの。他のメンバーの変更を pull した後や、他のメンバーのファイルを変えたときは古いので、Assets の全スクリプト（Editor・Plugins以外）をまとめてコンパイルする。参照に `NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll`、Assets 内の DLL（DOTween など）、`Library/ScriptAssemblies` の Assembly-CSharp 以外の DLL を足し、ソースに `DOTween/Modules/*.cs` も足す（無いと PauseMenu の DOFade がエラーになる）。ファイルが多くてコマンドが長すぎる（exit 126）ので、引数は応答ファイル（`@csc.rsp`、パスは `cygpath -w` で Windows 形式）に書いて渡す。CPU だけを変えたときは下のコマンドでよい：
  ```bash
  P=$(git rev-parse --show-toplevel)
  V=$(sed -n 's/^m_EditorVersion: //p' "$P/ProjectSettings/ProjectVersion.txt" | tr -d '\r')
  U="/c/Program Files/Unity/Hub/Editor/$V/Editor/Data"
  REFS=(); for f in "$U"/Managed/UnityEngine/*.dll; do REFS+=("-r:$f"); done
  "$U/NetCoreRuntime/dotnet.exe" "$U/DotNetSdkRoslyn/csc.dll" -nologo -langversion:9 -target:library -nostdlib -define:UNITY_EDITOR -nowarn:1701,1702,0649,0436 -out:"<scratchpad>/cpu.dll" -r:"$U/NetStandard/ref/2.1.0/netstandard.dll" -r:"$P/Library/ScriptAssemblies/Assembly-CSharp.dll" "${REFS[@]}" "$P"/Assets/Deverop/AT_Miyata/CPU/Scripts/*.cs "$P"/Assets/Deverop/AT_Miyata/CPU/Editor/*.cs
  ```
  （`<scratchpad>` は一時フォルダに置き換える。Unity Hub を標準以外の場所に入れている PC では `U` を直す。`UnityEditor.dll` は追加しない。UnityEngine フォルダの UnityEditor.CoreModule と重複してエラーになる）
- 実行時のログは `$LOCALAPPDATA/Unity/Editor/Editor.log`（Windows では `%LOCALAPPDATA%\Unity\Editor\Editor.log`）で読める。

## 現状と残っていること

### 引き継ぎ（2026-11-18 時点）
- **新しいゲーム性への対応（2026-11-18）**：上下左右の移動、ショートカット廃止、妨害弾、加速リング・爆弾・減速エリア、視界反転に対応した。
  - 状態：作業ブランチ `Miyata_NewCPU` にコミット済み（`いったん`）。コンパイルは確認済み。**Unity での動作確認はまだ**。
  - 変更したファイル：`CpuRaceInput` `CpuSituation` `CpuPerception` `CpuSteeringBrain`（候補点のコスト方式に作り直し）`CpuShieldBrain`（脅威3種類）`CpuBoostBrain` `CpuTargetSelector`（2次元のそろい）`CpuDifficultyProfile` `CpuPersonalityProfile` `CpuProfilePresetCreator`、Profiles の8アセット（新しい項目の値を直接書き込んだ）。
  - 新しく作ったファイル：`CpuJammerBrain.cs` `CpuWorldCache.cs`。
  - 削除したファイル：`CpuShortcutBrain.cs`（と `.meta`）。
  - 項目の名前を変えたもの：難易度 `_shortcutSuccessRate` → `_gimmickAwareness`、個性 `_shortcutPreference` → `_ringPreference`（どちらも `FormerlySerializedAs` 付き。アセットの YAML も新しい名前に書き換えた）。
  - CPU の Prefab Variant（`Prefabs/`）とシーンは変えていない。`CpuRaceInput` のシリアライズされる項目は変わっていないので、そのまま使えるはず。

### 次にやること（この順で）
1. Unity を開いてコンパイルされることを確かめる。
2. CPUTest_2 に加速リング（`SpeedRing`）・爆弾（`Bomb`）・減速エリア（`SlowZone`）を置いて動きを確認する。テストシーンの CPU は Course Spline の設定を忘れずに。
   - 確かめる点：上下にも散らばって走るか／リングをくぐるか／爆弾を避けるか、近くで爆発しそうならシールドを張るか／妨害弾を撃つか（Show Debug Logs を ON）、飛んでくる弾にシールドを張るか／視界反転の弾を受けると一時的に逆に動き、慣れて戻るか。
   - 頭上の表示と Gizmos（黄＝目標の位置、緑＝リング、橙＝避けるギミック、水色＝脅威）で判断の様子が見える。
3. ユーザーの確認で直すところが出たら、各 Brain 先頭の `const` と、難易度・個性のアセットで調整する。OK が出たら下の「調整した内容」に日付つきで書く。

- **他のメンバーのファイルに読み取り用の口を追加（2026-11-18、承認済み・最小限）**：
  - `AT_Minakuchi/SpeedRing/SpeedRing.cs`：`public bool IsSlowing => _isSlowing;`（`CpuWorldCache` のリフレクションをやめてこれを使う）。
  - `AT_Kawamata/Jammer/Scripts/JammerProjectile.cs`：`public JammerTypeProfile Type => _type;` と `public IReadOnlyRacerData TargetRacer => _targetRacer;`（`CpuShieldBrain` が使う）。
  - 状態：未コミット。全スクリプトをまとめてコンパイルしてエラーなし。

### 気をつけること・分かっていること
- 視界反転・減速リングへの切り替え・妨害弾の種類の選択は `JammerLauncher.SelectType` を CPU から呼んでいる（公開されている口なので既存ファイルは変えていない）。
- リングの切り替えの弾は、発射台の Ring Targets が空だと撃てない。CPU は撃てなかったら10秒間その種類を選ばない。
- 移動が8方向に丸められる設定（`FlightTuningProfile` の `EightWay`）だと、PD 制御が目標のまわりで少しジグザグする可能性がある。動作確認で見る。
- 爆弾の爆風は半径25mとトンネルより大きいことが多いので、避けるだけでは巻き込まれる。そのためにシールドの判断を入れている。

### これまでの経緯
- 2026-10-20 CPUTestCourse で動作し、頭上の表示まで確認済み（旧仕様：左右だけの移動・ショートカットあり）。
- 2026-11-07 川又さんが移動の Vector2 化・ショートカット廃止に合わせて、CPU のショートカット部分をコメントアウトし、Move の x だけに対応させていた。2026-11-18 にそれを作り直した。
- 調整した内容（ユーザーが動作を確認して OK を出したもの）：
  - 2026-10-21 左右：走行ラインを中央寄りに（`CpuSteeringBrain.LineSpread` = 0.5）、先頭のCPUは中央を走る。（2026-11-18 の作り直しでも同じ値と考え方を引き継いだ。方向が上下左右に広がった）
- まだ調整していない：ブーストの頻度・閾値、シールドの成功率、狙う相手の選び方、ギミックの避け方・リングのくぐり方の強さ、妨害弾の頻度、難易度ごとの差など。ユーザーの要望を聞いてから進める。

### その他（要承認のまま残っているもの）
- ブランチの作成・コミットはユーザーが自分で行う（Claude は git の操作をしない。`git rm` などの削除のステージもしない）。
- `DeveropGameScene.unity` の CPU は Prefab とのつながりが切れた（Unpack された）オブジェクト。本番シーンへ入れるには置き直しが必要で、既存シーンの変更になるので要承認。
- 執念を正確にするには、`RacerManagerBase` / `BoostHitInfo` に当てた相手の情報を足す必要がある（既存ファイルの変更なので要承認）。
