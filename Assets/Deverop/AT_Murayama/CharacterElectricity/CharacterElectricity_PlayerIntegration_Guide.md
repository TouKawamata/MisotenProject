# ⚡ キャラクター帯電・放電エフェクト プレイヤーシステム連携ガイド

> **対象**: プレイヤー制御・ゲームプレイ担当者向け（川俣さん等）  
> **作成者**: 村山 (AT_Murayama)  
> **配置場所**: `Assets/Deverop/AT_Murayama/CharacterElectricity/`

---

## 📌 概要と特徴

キャラクター被弾時・タックル衝突時の「スタン（帯電・放電）演出」を司るコントローラーです。  
プレイヤー側の既存コード（`RacerManagerBase.cs` 等）を汚さず、**外から1行呼び出すだけ**で直感的に連携できるよう設計されています。

- **0 GC Alloc（超軽量）**: `Update()` 内でマネージドヒープGCアロケーションは一切発生しません（60fps / 120fps でも負荷ゼロ）。
- **デザイナー制作マテリアル完全保護**: キャラクター本来のマテリアル（`shiro.mat` / `ao.mat` / `kuro.mat` 等）を自動バックアップし、スタン終了時に100%元通り復元します。
- **色バリエーション自動追従**: 白・青・黒などの衣装バリエーションのテクスチャと地色を動的に引き継ぎ、放電中もキャラ固有の装甲色が白飛び・変色しません。
- **SRP Batcher 100% 整合**: URP 17.3 Forward+ 環境で DrawCall の最適化を維持します。

---

## 🛠️ 基本的な接続手順（3ステップ）

1. **コンポーネントのアタッチ**:
   キャラクタープレハブ（または `born model` 等のメッシュを持つオブジェクト）に `CharacterElectricityPreview` をアタッチします。
   （対象レンダラーやマテリアルスロットは自動認識されます）

2. **参照の取得**:
   プレイヤー制御スクリプト（`RacerManagerBase.cs` や関連コントローラー）の初期化時に取得します。
   ```csharp
   private CharacterElectricityPreview _electricity;

   private void Awake()
   {
       _electricity = GetComponentInChildren<CharacterElectricityPreview>();
   }
   ```

3. **APIの呼び出し（4つの選択肢）**:
   プロジェクトの構成に合わせて、以下のいずれかの方法で1行呼び出してください。

---

## 🎮 呼び出しパターン例

### パターンA: イベント駆動（最も推奨・1行）
`RacerManagerBase` のスタン開始・終了イベントに登録します。
```csharp
private void Start()
{
    if (_electricity != null)
    {
        // スタン開始時に放電ON、終了時にOFF
        StunStarted += () => _electricity.SetStun(true);
        StunEnded   += () => _electricity.SetStun(false);
    }
}
```

---

### パターンB: 毎フレーム同期（Update内呼び出し）
毎フレームのヒープGCアロケーションは0バイトのため、`Update()` 内で直接同期しても安心です。
```csharp
private void Update()
{
    if (_electricity != null)
    {
        _electricity.SetStun(IsStunned);
    }
}
```

---

### パターンC: スタン残時間に応じた減衰フェード（よりリッチな演出）
スタン残時間（`StunRemainingTime`）の比率（1.0〜0.0）を渡すことで、時間経過に伴い放電が自然に弱まっていく演出が可能です。
```csharp
private void Update()
{
    if (_electricity != null && IsStunned)
    {
        float normalizedRemaining = _stun.StunRemainingTime / _boostHitProfile.StunDuration;
        _electricity.SetStunNormalizedTime(normalizedRemaining);
    }
}
```

---

### パターンD: 属性攻撃に応じた電撃色変更
紫の毒電撃、赤の過負荷爆発、黄色のサンダーなど、状況に応じたHDRカラーを指定できます。
```csharp
// 紫プラズマ電撃
_electricity.SetElectricityColor(new Color(1.3f, 0.35f, 2.5f, 1.0f));

// 赤色過負荷サージ
_electricity.SetElectricityColor(new Color(2.5f, 0.3f, 0.2f, 1.0f));

// 規定色に戻す
_electricity.OverrideColor = false;
```

---

## ⚡ 4つの動作モードと「シェーダー＋パーティクル」の自由な組み合わせ（新機能）

本コントローラー（`CharacterElectricityPreview` / エイリアス `CharacterElectricityController`）は、**シェーダー放電（P1〜P20）と純粋パーティクル（P1〜P8）を自由に組み合わせて同時再生**できます。

### 動作モード一覧 (`CharacterDischargeMode`)

| モード名 | 列挙値 | 概要 | 推奨用途 |
| :--- | :--- | :--- | :--- |
| **ShaderOnly** | `0` | メッシュシェーダー（P1〜P20）のみ適用。子パーティクル階層ゼロ | 最も軽量・描画負荷を最小限に抑えたい場合 |
| **ParticleOnly** | `1` | デザイナー元マテリアルを100%保持し、外付け純粋パーティクル（P1〜P8）のみ再生 | キャラクターの本来の質感・装甲色を一切変更したくない場合 |
| **Hybrid** | `2` | シェーダー ＋ パーティクル連動。`HybridParticleSource` で軽量/純粋/併用を選択可能 | 従来の軽量スパーク（3エミッター）や柔軟な組み合わせ |
| **🌟 RichHybrid** | `3` | **【新機能】シェーダー放電（P1〜P20）＋ リッチ純粋パーティクル（P1〜P8）の完全連動！** | **最高峰の表現力。メッシュ表面の放電と3D空間の稲妻リボン・針火花が同時に炸裂** |

### ハイブリッドパーティクルソース (`HybridParticleSource`)
`Hybrid` モード時に使用するパーティクルシステムを選択できます：
- `PureParticles (0)`: リッチ純粋パーティクル（6エミッター・P1〜P8）
- `Lightweight (1)`: 軽量放電スパーク（3エミッター）
- `Both (2)`: 両系併用（最高密度・最大ボリューム）

### C# スクリプトからの組み合わせ設定例
```csharp
// 1. リッチハイブリッドモードに切り替え
_electricity.SetDischargeMode(CharacterDischargeMode.RichHybrid);

// 2. シェーダー放電パターン（P1〜P20）を選択（例: P4 超帯電ゴールド）
_electricity.SwitchPattern(CharacterElectricityPreview.ElectricityPattern.Pattern4_Gold);

// 3. 純粋パーティクルパターン（P1〜P8）を選択（例: P1 リボン稲妻）
_electricity.SwitchPureParticlePattern(PureParticlePattern.Pattern1_ArcRibbons);

// 4. カラーや強度、スタンを操作すると両システムへ100%自動同期（0 GC）
_electricity.SetElectricityColor(new Color(0.2f, 0.85f, 2.5f, 1.0f));
_electricity.SetElectricityIntensity(1.5f);
_electricity.TriggerParticleBurst(30);
```

---

## ⚡ 放電パターン一覧（全20種）
インスペクターまたは `SwitchPattern` で選択可能な20種類の放電プリセットです（間欠ショート発展系 P9〜P12、次世代拡張 P13〜P17、およびP7×P9究極進化 P18〜P20 を含む）。

| 番号 | パターン名 | 特徴・推奨用途 |
| :--- | :--- | :--- |
| **P1** | **Surface Arc (高密度アーク放電)** | 装甲表面を血管や基板のように鋭く走る細い稲妻。高密度な電撃感 |
| **P2** | **Pulse Overcharge (脈動オーバードライブ)** | 全身を周期的に駆け巡る高圧サージウェーブ ＋ 装甲の励起グロー |
| **P3** | **Plasma Cling (纏わりプラズマ膜)** | 輪郭にまとわりつく電離プラズマの皮膜 ＋ 局所ショート火花 |
| **P4** | **Golden Overload (超帯電ゴールド)** | 白熱する芯を持つ高エネルギー金色の稲妻。パワーアップ・大ダメージ |
| **P5** | **Clean Streak (クリーン・低密度ストリーク)** | **【低密度・推奨】** 密度を落とし太く明瞭な稲妻ラインが走る、うるさくない洗練された表現 |
| **P6** | **Fresnel Rim (フレネル・エッジ放電)** | 表面全体ではなく輪郭・エッジ部分のみに電気が纏わりつき、正面のキャラディテールや顔・装甲を隠さない |
| **P7** | **Short Sparks (間欠的ショートスパーク)** | 常時ではなくバチッ…バチッ…と不規則に激しい火花が局所的に弾け飛ぶショート故障感・麻痺感 |
| **P8** | **Cyber Circuit (電子サーキット・サイバーグリッド)** | メッシュに沿って電子基板の直角ラインとデータサージが走る幾何学・SF放電 |
| **P9** | **Multi-Point Chain Sparks (多点連鎖クラスタースパーク)** | **【短絡連鎖】** 2〜3箇所の部位から時間差で「バチバチッ！…バチッ！」と連鎖的に飛び交う激しい過負荷ショート |
| **P10** | **Micro Spark Jitter / Crackle (微小・高頻度チリチリリーク)** | **【視界クリア・繊細】** 全身各所で小型の鋭い火花がチリチリ弾ける、視界や顔の造形を一切邪魔しない繊細な感電麻痺 |
| **P11** | **Heavy High-Voltage Arc Burst (高圧アーク放電バースト)** | **【一撃重放電・衝撃】** 予兆の微小スパークから一瞬「ドカン！バチィン！」と太い高電圧アークが奔流しイオン余熱を残すインパクト重視の放電 |
| **P12** | **Thermal Afterglow Sparks (余熱赤熱スパーク / Molten Ember)** | **【溶融ダメージ・物理余韻】** 火花が弾けた後、起点の装甲が赤熱・余熱でボワッとオレンジに残りながらゆっくり冷めていくショート |
| **P13** | **Anatomical Grounding (3D骨格階層・解剖学的接地アーク)** | **【次世代案1】** UV継ぎ目を超越し、胸/肩の高電位コアから腰・脚部のアース接地へ3D最短経路で貫通する連鎖放電 |
| **P14** | **Lichtenberg Branch (リーヒテンベルク樹状分岐カスケード)** | **【次世代案2】** 太い主幹アークが走った直後（+18ms）、斜め前方へ鋭い小枝と孫枝がフラクタルに枝分かれして弾ける |
| **P15** | **Stepped Leader Staccato (絶縁破壊ステップトリーダー)** | **【次世代案3】** 「チッ(30%)…パチッ(65%)…」と前駆放電が小刻みに前進した直後、「バチィィン！(100%)」と極太リターンストロークが炸裂 |
| **P16** | **Impact Vector Sparks (被弾点・タックル衝突ベクトル飛散)** | **【次世代案4】** 被弾した衝撃点を起点に、吹っ飛び方向（気流後方）へ扇状に電撃と火花が飛散する |
| **P17** | **Shockwave Refraction (衝撃波ディストーション＆爆圧スパーク)** | **【次世代案5】** 火花炸裂の瞬間、同心円状の衝撃波リングで周囲の装甲テクスチャと法線が物理的にグニャッと歪む |
| **P18** | **Magnetic Arc Whip (双極マグネティック・アークウィップ)** | **【独自追加1】** 2つの電極間でアークが磁場(ローレンツ力)で弓なりにしなり(Whip)、限界でパチン！と千切れて火花を散らす |
| **P19** | **Armor Seam Short (装甲シーム・モールド沿い短絡奔流)** | **【独自追加2】** テクスチャの塗り分け境界・モールドやポリゴン角を自動検出し、パーツの隙間から火花が噴き出すメカニック漏電 |
| **P20** | **Critical Strobe Cascade (臨界ストロボ連鎖・P7×P9究極進化)** | **【★独自追加3・集大成】** P7の「2段ダブルタップ短絡」とP9の「3連跳躍ブリッジ」が非周期リズムでランダムに切り替わる決定版 |

---

## 📋 公開APIリファレンス一覧

| メソッド / プロパティ | 引数 / 戻り値 | 説明 |
| :--- | :--- | :--- |
| `SetDischargeMode(CharacterDischargeMode mode)` | `CharacterDischargeMode` | 動作モード（ShaderOnly / ParticleOnly / Hybrid / RichHybrid）の切り替え |
| `SetHybridParticleSource(HybridParticleSource source)` | `HybridParticleSource` | ハイブリッド時のパーティクル系統（PureParticles / Lightweight / Both）の切り替え |
| `SwitchPattern(ElectricityPattern pattern)` | `ElectricityPattern` | シェーダー放電パターン（全20種類：P1〜P20）の切替 |
| `SwitchPureParticlePattern(PureParticlePattern pattern)` | `PureParticlePattern` | 純粋パーティクル放電パターン（全8種類：P1〜P8）の切替 |
| `SetStun(bool isStunned, bool immediate = false)` | `bool`, `bool` | スタン状態の開始/解除を切り替え。解除時（false）は自動で元のデザイナーマテリアルへ完全復帰 |
| `SetStunNormalizedTime(float normalizedTime)` | `float (0.0〜1.0)` | スタン残時間比率に応じた減衰表現（1.0=最大放電、0.0=終了・元マテリアルへ自動復帰） |
| `SetElectricityIntensity(float multiplier)` | `float` | 放電発光強度・パーティクル放出量の倍率設定（通常1.0、0.0で消灯） |
| `SetElectricityColor(Color color, bool override = true)` | `Color`, `bool` | 放電のHDRカスタムカラー設定（シェーダー・パーティクル両方に即時同期） |
| `TriggerParticleBurst(int count = 20)` | `int` | タックル衝突や被弾衝撃時の瞬間的な火花バーストをトリガー |
| `SetImpactVector(Vector2 centerUV, Vector2 dirUV)` | `Vector2`, `Vector2` | タックル被弾点UVと吹っ飛び方向ベクトルの設定（Pattern 16 連動） |
| `RestoreOriginalMaterial()` | なし | 元のデザイナー制作マテリアルへの即時完全復元（MPB解除・SRP Batcher復帰） |
| `ResetToOriginalAndCleanup()` | なし | パーティクル破棄とマテリアル復元の完全初期化（安全解除用） |
| `DeleteParticlesAndSetShaderOnly()` | なし | 生成済みパーティクルを一括消去し、軽量シェーダー単体モードへ移行 |
| `DischargeMode` | `CharacterDischargeMode (get/set)` | 現在の動作モード |
| `HybridSource` | `HybridParticleSource (get/set)` | 現在のハイブリッドパーティクル系統 |
| `CurrentPattern` | `ElectricityPattern (get)` | 現在設定されているシェーダー放電パターン |
| `PurePattern` | `PureParticlePattern (get/set)` | 現在設定されている純粋パーティクルパターン |
| `IsStunned` | `bool (get)` | 現在スタン中かどうか |
| `CurrentStunWeight` / `StunNormalizedTime` | `float (get)` | 現在のスタン重み・正規化時間（0.0〜1.0） |
| `IntensityMultiplier` | `float (get)` | 現在の放電強度倍率 |
| `OriginalMaterialBackup` | `Material (get)` | 退避されているデザイナー元マテリアルの参照 |

---

## 🌟 もう一つの選択肢：完全パーティクル版 (`CharacterPureParticleElectricity`)

マテリアル差し替えを一切行わず、**キャラクター本来のLitマテリアル（shiro/ao/kuro）を100%保持したまま外付けで電撃を表現したい場合**は、本コンポーネントの代わりに `CharacterPureParticleElectricity` をアタッチして使用できます。

### 特徴
- **マテリアル変更ゼロ**: SkinnedMeshRenderer の表面から直接6系統のエミッター（3Dリボン稲妻、高速針、オーラ、樹状フォーク、這い回りチリチリ、被弾バースト）が噴出。
- **全8パターンの差別化**: リボン稲妻、ニードル放射、高圧ストロボ、樹状フォーク、這い回りプラズマ、臨界メルトダウン、磁場旋回、間欠短絡。
- **同一の直感的API**:
  ```csharp
  var pureElectricity = GetComponentInChildren<CharacterPureParticleElectricity>();
  
  // スタン開始 / 終了
  pureElectricity.SetStun(true);
  pureElectricity.SetStun(false);
  
  // パターン切り替え
  pureElectricity.SetPattern(PureParticlePattern.Pattern1_ArcRibbons);
  
  // 被弾バースト（衝突ベクトル付き）
  pureElectricity.TriggerBurst(30, 1.2f, null, impactVector);
  ```

---

## 🛡️ スクリプトの安全な外し方（Remove Component）とパーティクル不要時の単体運用

### Q1. スクリプトを外す時（Remove Component）はどうすればいい？
**A. どちらの方法で外しても100%安全・ゼロ残留です：**

- **方法A: インスペクター最上部の赤色ボタンを押してから外す（推奨・Undo対応）**
  1. インスペクター最上部「🛡️ スクリプト安全解除・初期化」セクションの **「⚪ 完全初期化・安全クリーンアップ」** ボタンを1クリックします。
     - `CharacterElectricityPreview`: 元のデザイナー制作マテリアル（`shiro.mat` / `ao.mat` / `kuro.mat` 等）へ即座に100%復元し、生成された子パーティクル階層（`ElectricityParticles`）を安全に全削除（Undo対応）。
     - `CharacterPureParticleElectricity`: 全6系統の子パーティクル階層（`PS_Pure_*`）を安全に全削除し、放電ステートを初期化（Undo対応）。
  2. インスペクター右上の「︙」メニューから通常通り **「Remove Component」** を実行します。
- **方法B: 事前ボタンを押さずに直接「Remove Component」を実行した場合（自動安全ガード）**
  - 両スクリプトとも `OnDestroy()` 内に完全自動クリーンアップガードを実装しています。
  - 事前にボタンを押し忘れて直接 Remove Component した場合でも、**`OnDestroy()` が自動でマテリアルを元に戻し、生成された全子パーティクルオブジェクト（`ElectricityParticles` または `PS_Pure_*` 全6系統）を即座に破棄**します。
  - 孤立したパーティクルオブジェクトがヒエラルキーに取り残されることは一切ありません。

---

### Q2. パーティクルが全くいらない時にスクリプトを外したりしたら、キャラクターの構成が壊れたりしない？
**A. 絶対に壊れません。キャラクター本来のボーン・メッシュ階層は100%保護されています。**

- 本システムが生成・削除するのは、自身が生成した子オブジェクト（`ElectricityParticles` および `PS_Pure_*`）**のみ**です。
- キャラクターのボーン階層（`Armature`、`Root`、`Hips`、各種ボーン）やメッシュオブジェクト（`born model`、`SkinnedMeshRenderer`）には一切触れず、改変・削除・並び替えを行いません。
- 自動テスト（Test 15 / Test 16 / Test 13）にて、クリーンアップ前後および直接 Remove Component 前後でキャラクターのボーン・子オブジェクト構成・名前が1ミリも破壊されず100%一致することを機械的に検証・保証しています。

---

### Q3. 元のシェーダー版スクリプトの方もパーティクルはいらない場合、どうすればいい？
**A. インスペクター最上部またはパーティクル欄の「🗑️ パーティクル全削除・完全シェーダー単体化」を1クリックするだけでOKです！**

- **ワンクリック完全シェーダー単体化**:
  - `CharacterElectricityPreview` の「🛡️ スクリプト安全解除・初期化」または「✨ 3D放電パーティクル演出連動」セクションにある **「🗑️ パーティクル全削除・完全シェーダー単体化 (Shader Only)」** ボタンを押します。
  - 子オブジェクト（`ElectricityParticles`）が即座に完全消去され、`_enableParticles` が OFF になります。
  - 現在選択中のシェーダー放電パターン（P1〜P20）の見た目はそのまま維持されます。
- **ヒエラルキー構成汚染ゼロの保証**:
  - `_enableParticles` が OFF の間は、`OnEnable()` や `Start()`、再コンパイル、シーン再読み込みを行っても、**子パーティクルオブジェクトが勝手に自動生成されることは一切ありません**。
  - 万一チェックボックスで無効化したことで子オブジェクトが残存している場合でも、インスペクター上に「⚠️ 残存パーティクル検出」アラートと「🗑️ 残存パーティクルを完全削除」ボタンが自動表示され、1クリックで即時一掃できます。
  - 余計なGameObjectがキャラクター階層に一切存在しない状態で、メッシュシェーダー（`S_CharacterElectricity`）単体による超軽量・美麗な放電エフェクトをお楽しみいただけます。
- **後からパーティクルが必要になった場合**:
  - インスペクターに表示される **「⚡ パーティクル演出を有効化・自動生成 (Enable & Setup Particles)」** ボタンを1クリックするだけで、いつでもワンタッチで3Dパーティクル階層を復元できます。
