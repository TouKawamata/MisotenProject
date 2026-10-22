# バックミラー 設計メモ

後ろから他のレーサー（CPU）がブーストで近づいて来たら、画面上部にバックミラーを出す機能。
作業ルール（作業場所・既存ファイルを変えるときは承認を得る・コードの書き方・パスの書き方）は `AT_Miyata/CPU/CPU_DesignNotes.md` と同じ。

## 方式
後ろ向きのカメラ → RenderTexture → UI の RawImage（案A）。
- 選ばなかった案：カメラの Viewport Rect で直接描く（RT を使わないカメラには AT_Murayama の `ConditionalFullScreenFeature` のブーストのエフェクトがかかってしまう）、カメラを使わず矢印などで出す（ミラーの見た目にならない）。
- 負荷対策：カメラは表示している間だけ有効。影・ポストエフェクト・アンチエイリアスなし、Far Clip 150m、RT は 640×200。
- RT は `BackMirrorCamera` が起動時に作る（アセットなし）。
- 既存ファイルは変えていない。`RaceManager.Racers` / `PlayerData`（`IReadOnlyRacerData`）を読むだけ。

## 構成（`AT_Miyata/BackMirror/`）
```
Scripts/
  BackMirrorTrigger.cs   出すかどうかの判定（ShouldShow / Threat）
  BackMirrorCamera.cs    Playerの位置＋オフセットに置き、後ろを向くカメラ。RTを作る。SetRendering でON/OFF
  BackMirrorView.cs      RawImage＋CanvasGroup。Triggerを見てフェードし、見えている間だけカメラに描かせる。左右反転（鏡と同じ向き）
Editor/
  BackMirrorSceneCreator.cs メニュー Tools > BackMirror > Create In Scene
```

## 表示の条件
Player 以外のレーサーのうち、次をすべて満たす相手がいたら出す。
1. `IsBoosting`
2. Player の後ろ：`0 < Playerの DistanceOnCourse − 相手の値 ≤ Detect Distance`（40m）
3. 近づいている：差が前のフレームより縮んでいる（Require Approaching で切り替え）

条件が外れても Min Show Time（1.5秒）は出したまま。走行中（`ERaceState.Racing`）で、Player がゴールする前だけ。

## セットアップ
1. シーン（RaceManager があるもの）を開き、メニュー Tools > BackMirror > Create In Scene。
   `BackMirror`（Trigger）の下に `BackMirrorCamera` と `BackMirrorCanvas`（画面上部中央の枠＋映像）ができ、参照もつながる。
2. Player 自身の機体が映るときは、`BackMirrorCamera` の Offset（前・上へずらす）か Near Clip を調整する。

## デバッグ
- `BackMirrorTrigger` の Show Debug Logs：接近してきた相手の名前を Console に出す。

## 現状と残っていること
- 2026-10-22 作成。コンパイル確認済み。CPUTestCourse での動作確認はこれから。
- 調整する候補：検知距離、最低表示時間、カメラの位置・画角、ミラーの大きさ・位置・枠のデザイン。
