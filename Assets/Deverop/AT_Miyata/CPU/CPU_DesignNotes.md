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
- ボタンの決まり（仕様書「CPU入力 データ早見表.md」より）：Pressed は1Tickだけ true、**Pressed の Tick も Held を true にする**、ブーストとシールドは同時に使えない、スタン中は入力が無視される、値は `UpdateInput` の中でだけ変える。
- RacerId は Player が 0、CPU は 1 から。距離・順位は1フレーム前の値。他のレーサーの速度・ゲージは読めない（速度は `DistanceOnCourse` の差から自分で出す）。
- `ICourseGuide`（`owner.CourseGuide`）でコースの中心・左右・半径・先読みの位置が取れる。`GetRight` が Horizontal の＋側。

## 構成（`AT_Miyata/CPU/`）
```
Scripts/
  CpuRaceInput.cs            窓口（MonoBehaviour, ICpuRaceInput）。CPUManager の Input Source に入れる
  CpuDifficultyProfile.cs    難易度＝うまさ（ScriptableObject）
  CpuPersonalityProfile.cs   個性＝好み（0～1の重み、表示名・色）（ScriptableObject）
  CpuDifficultyController.cs 全CPUの難易度を一括で変える窓口（シーンに1つ。static Current）
  CpuSituation.cs            そのTickの状況（CpuRacerView：他のレーサーの前後の差・横ずれ・相対速度）
  CpuPerception.cs           状況をまとめる。前後の差＝DistanceOnCourseの差、横ずれ＝GetRightへの射影
  CpuTargetSelector.cs       狙う相手（Player・CPUを区別しない）。執念（やり返し）
  CpuShieldBrain.cs          シールド（後ろからのブーストの脅威→反応時間→成功率で1回だけ抽選）
  CpuBoostBrain.cs           ブースト（攻撃／加速、追い上げ補正、RankRatio）
  CpuSteeringBrain.cs        左右（目標の横位置をPD制御で追う。壁に触れたら内側へ）
  CpuShortcutBrain.cs        ショートカット（KeyPress／HorizontalThreshold の両方式）
  CpuButtonLatch.cs          「押し続けたい」→ Pressed/Held。始まらなければ一度離して押し直す
  CpuLabelDrawer.cs          頭上の表示（Game：OnGUI、Scene：Handles）
Editor/
  CpuProfilePresetCreator.cs メニュー Tools > CPU > Create Default Profiles
Profiles/                    難易度3つ・個性5つのアセット
Prefabs/                     CPURacer の Prefab Variant（個性ごと。ユーザーが作成）
```
テストシーン：`AT_Miyata/Scenes/CPUTestCourse.unity`（TestCourse を Save As したもの）。

## 1Tickの流れ（CpuRaceInput.UpdateInput）
1. 難易度・個性がなければ入力なし（警告は1回だけ）。`DistanceOnCourse` が20m以上戻ったら置き直しとみなして `ResetState`。
2. `CpuPerception.Update`。コースが取れなければ入力なし＋警告（Course Spline 未設定のとき）。
3. 考え直すTick（難易度の ThinkInterval ごと）かどうかを決める。
4. `CpuTargetSelector.Update`（毎Tick最新化、選び直すのは考え直すTickだけ）
5. `CpuShieldBrain.Update`（毎Tick。反応時間を秒で数えるため）
6. 考え直すTickだけ：`CpuBoostBrain.Think`、`CpuSteeringBrain.Think`（目標の横位置）
7. `CpuSteeringBrain.Evaluate`（毎Tick、PD＋Perlinノイズ）→ `CpuShortcutBrain.Update`（必要なら Horizontal を上書き）
8. 優先順位：**シールド ＞ ブースト**。スタン中は押していない扱い。`CpuButtonLatch` で Pressed/Held を作る。

## 判断の中身
- **狙う相手**：点数＝近さ＋横位置のそろい具合＋順位ボーナス（自分より上なら0.3）＋執念ボーナス。シールド中・スタン中の相手は除外。狙う距離は攻撃性で 10～45m。今の相手が一番の相手の点数の80%以上あれば変えない。
- **執念**：`BoostHitInfo` に当てた相手が含まれないため、「自分がスタンした瞬間に、前後15m以内でブーストしていた一番近い相手」を当てた相手とみなす（既存ファイルを変えないための代わりの方法）。
- **シールド**：後ろの一定距離以内（慎重さで 8～25m）、横位置の差3m以内、ブースト中で近づいてくる相手を脅威とみなす。自分がブースト中なら張らない（ブースト同士ははじくだけのため）。
- **ブースト**：攻撃＝相手が25m以内で横位置がそろっている（そろいの許容は難易度の AttackAlignTolerance）。加速＝先のカーブの角度が BoostCurveLimit 以下で、ゲージが閾値以上。閾値は「ブーストの使い方」、追い上げ補正（Playerとの差）、RankRatio（最下位で -0.15）で変わる。相手がシールドを張ったら攻撃をやめる。
- **左右**：優先順位は 脅威を避ける ＞ 狙う相手 ＞ 先頭なら中央 ＞ 前の相手をよける ＞ 走行ライン（RacerId の偶奇で左右に分け、ライン変更の頻度でときどき変える）。走行ラインは `LineSpread`（0.5）を掛けて中央寄りにしている（全体の寄り具合はこの値で、個性ごとの差は PreferredLine で調整）。トンネル半径の80%以内に収め、壁に触れたら1.5秒間は55%以内にする。
- **追い上げ補正**：速度（物理）は変えない。判断（ブーストの閾値）だけを Playerとの前後の差で変える。強さは難易度の CatchUpStrength（0で無効）。ゲーム側の `BoostRankTable`（下位ほどゲージ消費が少ない）とは別の仕組み。
- 個性の重みを実際の距離・閾値に直す換算は、各 Brain の先頭にある `private const` にまとめてある（調整はここで行う）。

## 難易度と個性
- 難易度アセット：`CpuDifficulty_Easy/Normal/Hard`（考え直す間隔、反応時間、操作の強さとぶれ、攻撃のそろい、カーブの限界、ゲージの下限、シールド／ショートカットの成功率、追い上げ補正）。
- 個性アセット：`CpuPersonality_Attacker`（赤）／`Guardian`（青）／`Speedster`（黄）／`Trickster`（紫）／`Balance`（緑）。
- **CPUごと**：CpuRaceInput の Difficulty。**一括**：`CpuDifficultyController` を置く。Follow Global Difficulty が ON（初期値）の CPU は一括の設定に従う。実行中は `SetDifficulty()` で切り替えられ、次の Tick から反映される。
- プリセット作成メニューは、既存のアセットを上書きしない（表示名が空のときだけ、表示名・色を入れる）。

## セットアップ（案B：既存ファイルは変えない）
1. CPURacer.prefab の Prefab Variant を `AT_Miyata/CPU/Prefabs/` に作り、ルートに CpuRaceInput を付けて Difficulty・Personality を設定し、`CPUManager.Input Source` に入れる。
2. 自分用のシーン（CPUTestCourse）に Variant を置き、RaceManager の CPU の欄に入れる。
3. **シーンに置いた各 CPU の `PlayerFlightController.Course Spline` に、シーンの CourseSpline を設定する。** シーンのオブジェクトなので Prefab には保存できない。未設定だと CPU は全く動かない（実際に起きた。今は CpuRaceInput が警告を出す）。

## デバッグ
- Show Debug Logs：狙う相手の変更、ブースト（攻撃／加速）、シールド、ショートカットを Console に出す。
- Gizmos（選択中）：黄＝目標の横位置、赤＝狙う相手、水色＝脅威。
- 頭上の表示（Show Label / Label Height）：「個性 / 難易度」と今していること。
- **パスについて**：作業するPCがよく変わるため、このメモにはユーザー名入りのパス（`C:/Users/<名前>/...`）を書かない。プロジェクトの場所は `git rev-parse --show-toplevel`、Unity のバージョンは `ProjectSettings/ProjectVersion.txt`、ユーザーのフォルダは環境変数（`$LOCALAPPDATA` など）から、その場で求める。
- Unity を使わないコンパイル確認（プロジェクトの Unity バージョンの Roslyn を使う。Git Bash で、リポジトリ内のどこからでも実行できる）：
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
- CPUTestCourse で動作し、頭上の表示まで確認済み。
- 数値の調整を始めた。調整した内容（ユーザーが動作を確認して OK を出したもの）：
  - 2026-10-21 左右：走行ラインを中央寄りに（`CpuSteeringBrain.LineSpread` = 0.5）、先頭のCPUは中央を走る。
- まだ調整していない：ブーストの頻度・閾値、シールドの成功率、狙う相手の選び方、難易度ごとの差など。ユーザーの要望を聞いてから進める。
- ブランチの作成・コミットはユーザーが自分で行う（Claude は git の操作をしない）。
- `DeveropGameScene.unity` の CPU は Prefab とのつながりが切れた（Unpack された）オブジェクト。本番シーンへ入れるには置き直しが必要で、既存シーンの変更になるので要承認。
- 執念を正確にするには、`RacerManagerBase` / `BoostHitInfo` に当てた相手の情報を足す必要がある（既存ファイルの変更なので要承認）。
