# スマート方向サイン・矢印サイネージ（SmartArrowSignage）実装＆利用ガイド

## 概要
`SmartSignage` をベースに発展させ、**「文字ではなく方向・矢印を示す」**ことに特化した次世代高機能サインシステムです。
レースコースの高速誘導、都市ビルの壁面・高架下・広場案内、空中分岐ルートガイドなど、あらゆるスケール・設置環境に柔軟に対応します。

---

## 🌟 主な特徴・こだわり

### 1. 超巨大拡大でも絶対にボケない・ジャギーが出ない数式SDF（Signed Distance Field）描画
- **課題**: 「結構拡大して使ったりするかも」という要件に対し、ビットマップテクスチャ（2Kや4K）では巨大配置（数十〜数百メートル）や至近距離カメラでピクセルボケ・ジャギーが発生します。
- **解決策**: シェーダー内部で数学的な **SDF（Signed Distance Field）幾何学計算** と `fwidth` による **サブピクセル・アンチエイリアシング（AA）** を実装。
- **効果**: 10cmの至近距離から1,000mの上空、1mの小型看板から100mの超巨大メガスクリーンまで、拡大率に関わらず**常に1ピクセル幅で完璧にアンチエイリアスされた極めて鮮明なベクター描画**を実現します。テクスチャ解像度制限やVRAM消費も一切ありません。

### 2. 多彩な矢印幾何学パターン（全8種）
インスペクターから1クリックで瞬時に切り替え可能：
1. **⚡ 鋭角レーシング (Sharp Racing)**: 高速感を極限まで引き出した鋭角エアロダイナミック・シェブロン。
2. **🛑 太めサイン (Bold Highway)**: 視認性を重視した三角矢頭＋長方形シャフトのハイウェイ・エアポート型太矢印。
3. **⏩ 二重シェブロン (Double Chevron)**: 二重にくさびが重なり、フロー時にズレて流れる先進ナビゲーション。
4. **⏭️ 三重連続シェブロン (Triple Speed)**: 3連続で並ぶハイスピード誘導シェブロン。
5. **💡 ネオン管中空 (Neon Tube)**: 矢印が中空のアウトライン管となり、中心ホットラインと外周グローで発光するサイバーパンク・ネオン。
6. **💠 クアンタム・セグメント (Quantum Lattice)**: 矢印内部が微細デジタルブロック・格子に分割された高密度SFホログラム。
7. **⚠️ ハザード警戒 (Hazard Stripes)**: 矢印内部に45度の安全警戒ストライプ（イエロー＆ブラック／オレンジ＆ホワイト等）が走るコーションサイン。
8. **↩️ カーブ案内アーク (Curved Speed)**: 円弧と矢じりによるコーナリング・ヘアピン・急カーブ誘導アーク。

### 3. 直感的な 8方向クイックコンパス ＆ 任意角度スライダー
- インスペクター上の3×3ボタングリッドで直感的に向きを指定可能：
  - `[ ↖ 135° ] [ ↑ 90° ] [ ↗ 45° ]`
  - `[ ← 180° ] [ 🎯 リセット ] [ → 0°  ]`
  - `[ ↙ -135°] [ ↓ -90°] [ ↘ -45°]`
- スライダー（-180°〜+180°）による1度単位の自由な角度調整にも完全対応。

### 4. 進行方向アニメーション（全5種）
- **🌊 流れる進行方向ウェーブ (Flowing Wave)**: 光のパルスが手前から矢印先端へ向かって流れる、現代レースゲーム特有の直感的な方向誘導。二重・三重・連続シェブロンでは個別に位相がズレて美しく波打ちます。
- **⚡ 点滅フラッシュ (Blinking Flash)**: 急カーブ・危険地帯手前で強く点滅警告。
- **💓 呼吸パルス (Breathing Pulse)**: 穏やかなサイン向けの滑らかな正弦波発光。
- **📏 スキャンライン (Scanline Flow)**: ホログラム走査線が矢印内部を駆け抜けるデジタル演出。
- **⏸️ 静止・常時点灯 (Static)**: 常時点灯する落ち着いた固定案内。

### 5. 多彩な背景表現モード（全9種：空中純浮遊・金具無し対応拡張）
空中への金具無し配置パターンに対応し、背景モードを大幅に拡充しました：
1. **🪟 物理透過フロストガラス (Frosted Glass)**:
   - URP Scene Color 13タップ・ポアソン核背景ブラー、RGB光学的色収差分散、太陽光スペキュラー光沢、動的反射光スイープ、面取りベベル発光を完全搭載。
2. **✨ 純粋空中ホログラム (Pure Hologram)**:
   - ガラス板のない純粋な空中浮遊エネルギー矢印。空中レースウェイのゲートやウェイポイントに最適。
3. **💎 ミニマル半透明ガラス (Minimal Glass)**:
   - 背景ブラーを省いた軽量なクリアアクリル／ポリカーボネート調。
4. **🌐 サイバーグリッド・ホロ (Cyber Grid Holo)**:
   - 空中投影されたデジタルマトリクス板。微細な走査線、方眼メッシュ、コーナーレティクルが空間に境界を示し、金具なしでも空中に看板領域が明確に映えます。
5. **🕶️ ダークエアロ・バイザー (Dark Aero Visor)**:
   - レーシングバイザー調の偏光ダークスモーク板。直射日光下や白雲・青空の屋外コースでもネオン矢印が強烈なハイコントラストでくっきり視認できます。
6. **🛡️ 反重力エネルギーシールド (Energy Shield)**:
   - 六角形ハニカム格子と脈動するプラズマ波境界によるフォースフィールド。「空中に金具なしでエネルギーシールド展開により浮遊している」設定を視覚的に納得させます。
7. **🌈 オーロラプリズム・光彩ガラス (Aurora Dichroic Prism)**:
   - 見る角度によって虹色に偏光する未来型ダイクロイック光彩ガラス。太陽光スペキュラーとエッジ分散が美しくきらめきます。
8. **✨ ネオンエッジ・ホロフレーム (Neon Wireframe Holo)**:
   - 外周境界のみが多重ネオンパルスで鮮やかに発光し、中央は完全クリア。背景のコース景色を遮ることなく、空中に浮かぶ看板の領域を直感的に伝達します。
9. **✨ スターダスト・微粒子マトリクス (Star Dust Matrix)**:
   - 看板内部の空間に微細な光のナノダスト・データ微粒子がキラキラと漂う高密度SF浮遊フィールド。反重力浮遊の空間存在感を美しく演出します。

### 6. 多彩な設置スタイル・金具（全6種プレハブ完備 ＆ 動的金具自動生成 ＆ HUDコーナー金具切替）
1. `Prefab_SmartArrow_ScreenOnly.prefab`: 余計な金具のない純粋な浮遊サイン板（空中配置用。四隅コーナー金具装飾もデフォルトOFF）。
2. `Prefab_SmartArrow_WallMount.prefab`: ビル外壁やトンネル壁面に吸着するカンチレバー水平アーム金具付き。
3. `Prefab_SmartArrow_CeilingHanger.prefab`: 天井・高架トラス・ゲート構造から吊り下げるツインロッド＆クランプ懸架金具付き。
4. `Prefab_SmartArrow_FloorStand.prefab`: サーキット路面や広場・フロアに設置する重厚エアロ台座＆支持支柱スタンド付き。
5. `Prefab_SmartArrow_Framed.prefab`: スクリーンの周囲を覆う高品位エアロダイナミック外枠ベゼル付き。
6. `Prefab_SmartArrow_FloatingPylons.prefab`: 上下の反重力エネルギーポッドから放射される空中浮遊ホログラム。

※ **金具マウント・HUDコーナー金具の完全制御**：
- **3D金具（Physical Mount）**: ScreenOnly（金具無し）からインスペクター上で壁面マウントやフレームへ切り替えた際、子オブジェクトの金具・外枠およびマテリアルが自動的かつ確実に生成・適用されます。ScreenOnly選択時は3D金具が確実に非表示になります。
- **2D HUDコーナー金具（Screen Graphics）**: インスペクター上の専用トグルボタンからワンクリックで表示・非表示を切り替え可能。空中純浮遊時に画面の留め金具風グラフィックが気になる場合、OFFにすることで完全な純粋浮遊スクリーンとして描画されます。また、アスペクト比補正により、ワイドスクリーンでもブラケット腕の長さ・太さが均等かつ幾何学的に正しく描画されます。

---

## 📁 アセット構成一覧

```
Assets/Deverop/AT_Murayama/SmartArrowSignage/
├── Editor/
│   ├── SmartArrowSignageControllerEditor.cs     # インスペクター拡張UI・アセット生成・ショーケース配置
│   └── SmartArrowSignageControllerEditor.cs.meta
├── Materials/
│   ├── M_SmartArrow_NeonCyan.mat               # レーシングシアン鋭角シェブロン
│   ├── M_SmartArrow_SpeedLime.mat              # スピードライム太めサイン
│   ├── M_SmartArrow_CautionAmber.mat           # コーションアンバー警戒ストライプ
│   ├── M_SmartArrow_HoloGold.mat               # サイバーゴールド空中ホログラムカーブ
│   ├── M_SmartArrow_TripleViolet.mat           # バイオレット三重シェブロン
│   ├── M_SmartArrow_NeonTube.mat               # ネオン管ホワイト中空アウトライン
│   ├── M_SmartArrow_Frame_White.mat            # 外枠・金具用（クリーンホワイト PBR）
│   ├── M_SmartArrow_Frame_Dark.mat             # 外枠・金具用（ダークメタリック PBR）
│   └── 各種.meta
├── Meshes/
│   ├── SmartArrow_Screen_RoundedRect.asset     # 丸角フロストガラススクリーンメッシュ
│   ├── SmartArrow_AeroFrame.asset              # エアロベゼル外枠メッシュ
│   ├── SmartArrow_WallMount.asset              # 壁面カンチレバー金具メッシュ
│   ├── SmartArrow_CeilingHanger.asset          # 天井吊り下げ懸架金具メッシュ
│   ├── SmartArrow_FloorStand.asset             # 地面設置スタンド支柱メッシュ
│   ├── SmartArrow_FloatingPylons.asset         # 反重力上下浮遊ポッドメッシュ
│   └── 各種.meta
├── Prefabs/
│   ├── Prefab_SmartArrow_ScreenOnly.prefab     # スクリーン単体
│   ├── Prefab_SmartArrow_WallMount.prefab      # 壁面マウント型
│   ├── Prefab_SmartArrow_CeilingHanger.prefab  # 天井吊り下げ型
│   ├── Prefab_SmartArrow_FloorStand.prefab     # フロアスタンド型
│   ├── Prefab_SmartArrow_Framed.prefab         # 枠付き型
│   ├── Prefab_SmartArrow_FloatingHolo.prefab   # 浮遊ホログラム型
│   └── 各種.meta
├── Scripts/
│   ├── SmartArrowSignageController.cs          # ランタイム制御コンポーネント (ExecuteAlways)
│   └── SmartArrowSignageController.cs.meta
└── Shaders/
    ├── S_SmartArrowSignage.shader              # 数学SDFベクター矢印シェーダー (URP Forward+)
    └── S_SmartArrowSignage.shader.meta
```

---

## 🎮 検証・実機確認手順

1. **検証シーンを開く**:
   - `Assets/Deverop/AT_Murayama/scene/03.unity` を開きます。
2. **ショーケースの確認**:
   - ヒエラルキーに `--- SmartArrowSignage Showcase ---` が配置されています（位置: `X=0, Y=17, Z=25`）。
   - 左から右へ順に以下の6種が並んでいます：
     - `Showcase_01_RacingWallMount` (壁面マウント / 鋭角レーシング / シアン)
     - `Showcase_02_CeilingHangerGuide` (天井吊り下げ / 21:9 太めサイン / ライム / 45度)
     - `Showcase_03_FloorStandCaution` (フロアスタンド / 警戒ストライプ / アンバー / 90度上向き)
     - `Showcase_04_FloatingHoloCurve` (空中ホログラム / カーブ案内 / ゴールド / パルス)
     - `Showcase_05_FramedTripleSpeed` (エアロ枠 / 32:9 超ワイド / 三重連続シェブロン / バイオレット)
     - `Showcase_06_ScreenOnlyNeonTube` (スクリーン単体 / ネオン管中空 / ホワイト / -45度)
3. **インスペクターUIでの操作**:
   - いずれかのオブジェクトを選択すると、インスペクター最上部に **8方向クイックコンパス**、**8種矢印スタイルボタングリッド**、**アニメーション切替**、**シェブロン連数ボタン（1〜6連）**、**カラーパレット**、**アスペクト比ボタン** が表示されます。
   - ボタンを押すだけでリアルタイムに見た目が変化します。
4. **マテリアル別名保存 (Save As)**:
   - 気に入った見た目ができたら、インスペクターの「💾 別名で新規保存＆適用」を押すだけで、設定済みの `.mat` ファイルが `Materials/` 配下に即時生成され、自動的に割り当てられます。
5. **別シーンへの配置**:
   - `Assets/Deverop/AT_Murayama/SmartArrowSignage/Prefabs/` 配下のプレハブをシーン内にドラッグ＆ドロップするだけで、即座に好みの形態・角度で設置できます。

---

## ⚡ パフォーマンス・設計仕様

- **CPU負荷**: `Update()` メソッドは完全不使用（**0.00ms**）。すべてGPUシェーダー内数学計算（`_Time.y`）でアニメーション。
- **GC Allocation**: **Update内 0 GC Alloc** を完全厳守。
- **DrawCall 最適化**: `MaterialPropertyBlock` によりマテリアルのインスタンス化リークを防止。本採用確定後は「Save As」で固定マテリアルを作成することで URP の **SRP Batcher** が100%適用されます。
- **チーム規約**: `Assets/Deverop/AT_Murayama/` 配下完結、`[MenuItem]` 追加なし、Roslyn C#コンパイル 0エラー0警告。
