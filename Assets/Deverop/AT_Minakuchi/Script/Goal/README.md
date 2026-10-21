# ゴール演出

`Assets/Deverop/AT_Minakuchi/Scene/Minakuti_Start_Goal.unity` に接続済み。

1. Player のゴールイベントから自動開始。
2. 3秒かけて速度を落とし、Landing Point に着陸。
3. 0.45秒停止後、2.4秒かけて確定順位の台へ移動。
4. プレイヤーを中心にカメラが周回し、CPUも全員台に着いてからNEXT ボタンを表示。
5. NEXT / Enter / Space / ゲームパッド南ボタンで、既存の星形トランジションを再生して ResultScene へ。

Hierarchy の **RaceGoalDirection** にある同名のコンポーネント で時間・カメラ距離・高さ・周回速度を調整する。
子の **Landing Point** は地面の接地点。**Rank 1/2/3 Point** は台の上面の接地点。
マーカーの高さに Ground Clearance（初期値1m）を加えた場所にプレイヤー原点を置く。
台の位置や高さを変える場合は対応する Rank Point も一緒に動かす。
4位以降は3位地点の横へ追加配置する。

移動対象はプレイヤーとInspectorの **Cpu Racers** 配列へ登録したCPU。
RaceGoalDirection > Cpu Racers にCPU本体をドラッグして登録する。
このシーンではCPURacer・CPURacer (1)を登録済み。配列の順番は順位に影響しない。
CPUはRaceManagerにもレース参加者として登録されている必要がある。空欄の要素は無視する。
先にゴールしていたCPUはプレイヤーの演出開始時から、後着のCPUは自身のゴール後から着陸・順位配置する。
制限時間でRaceFinishedになった時点の未ゴールCPUも、その時点の順位で配置する。レース結果・ゴールタイムは書き換えない。
着陸地点は順位ごとに横へ3mずらして重なりを避ける。
演出中は各レーサーのColliderを無効にし、RigidbodyをKinematicにして接触による妨害を防ぐ。
RaceManagerの手動Tickと競合しないよう、LateUpdateで演出の位置・向きを最終反映する。
CameraControllerを一時停止し、カメラ位置・視野角を演出側で制御する。リセット・無効化時に元に戻す。
ゲーム全体のTime.timeScaleは変更しない。演出・入力待ち・トランジションは実時間で動く。

遷移先は **RaceGoalDirection > Result Scene Name**。ResultSceneは現在のBuild Settingsに登録済み。
遷移先が読み込めない場合は演出画面に留まり、エラーを表示してNEXTを再試行可能にする。

短時間の確認: Play ModeでRaceGoalDirectionのコンポーネントメニューから
**Play Goal (Play Mode)** を実行する。実レースのゴール確認は別途行うこと。
リトライ時は `RaceGoalDirection.ResetDirection()` で演出を解除してからレースを再配置する。

検証: Unity同梱Roslynとプロジェクトの参照DLLでC#コンパイル、およびシーンのfileID重複・接続を確認。
UnityのPlay Modeでの映像・操作の確認は未実施。

ゴール処理はRaceGoalDirection 1コンポーネントへ統合済み。
Prefab/RaceGoalDirection.prefab に着陸地点・Rank Point・台・トランジションCanvasを同梱。
別シーンで使う場合はプレハブを配置し、Race Manager / Player / Cpu Racers / Gameplay CameraをInspectorへ登録する。
内部のLanding Point / Rank Points / フェード参照は設定済み。
