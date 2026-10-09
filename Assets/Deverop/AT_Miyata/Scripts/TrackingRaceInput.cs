using UnityEngine;

// PoseLandmarkReceiverが受信した姿勢（MediaPipe Pose準拠の33点）からレース入力を作るIRaceInput実装。
// Horizontalは上半身の左右の傾き角度から連続値を作る。
// BoostPressed/ShieldPressed/ShortcutPressedは「ポーズが成立したフレームで1回だけtrue」になる
// トリガー（KeyboardRaceInputのwasPressedThisFrameと同じ扱い）。
// BoostHeld/ShieldHeldはポーズを取り続けている間ずっとtrueになる「プレス（継続状態）」。
// ショートカットの継続状態はIRaceInputに無いため、IsShortcutPoseActiveとして独自に公開している。
//
// 想定しているポーズ（すべて上半身のみで完結）
//  - Boost   : 空手の構えのように、両手首を腰〜肩の中間あたりの高さで体の横に引く（腰そのものの高さだと、腕を下げただけの姿勢を誤検知するため）
//  - Shield  : 片手（左右どちらでも可）を胸の前で水平に構える（手首が肘より体の中心側にある状態）
//  - Shortcut: バンザイ（両手首を頭より高く上げる）
public class TrackingRaceInput : IRaceInput {
	// MediaPipe Poseのランドマーク番号（PoseLandmarkReceiver.Landmarksのインデックスに対応）
	private const int Nose = 0; // 鼻
	private const int LeftShoulder = 11; // 左肩
	private const int RightShoulder = 12; // 右肩
	private const int LeftElbow = 13; // 左肘
	private const int RightElbow = 14; // 右肘
	private const int LeftWrist = 15; // 左手首
	private const int RightWrist = 16; // 右手首
	private const int LeftHip = 23; // 左腰
	private const int RightHip = 24; // 右腰

	// 姿勢データの取得元
	private readonly PoseLandmarkReceiver _receiver;
	// 各種しきい値・調整用パラメータ
	private readonly TrackingRaceInputSettings _settings;

	// 直近でEnsureUpdatedForCurrentFrameを実行したUnityのフレーム番号。同一フレーム内の再計算を防ぐために使う。
	private int _lastEvaluatedFrame = -1;

	// Horizontalの平滑化用に保持している現在値（毎フレーム目標値へ少しずつ近づける）
	private float _smoothedHorizontal;
	// Horizontalプロパティが返す、このフレーム用に計算済みの値
	private float _cachedHorizontal;

	// 各ポーズの「前フレームでの継続状態」。今フレームの状態と比較してPressed（トリガー）の立ち上がりを検出するために使う。
	private bool _previousBoostActive;
	private bool _previousShieldActive;
	private bool _previousShortcutActive;

	// 各ポーズが成立した瞬間だけtrueになる値（トリガー）。IRaceInputのBoostPressed等が返す。
	private bool _cachedBoostPressed;
	private bool _cachedShieldPressed;
	private bool _cachedShortcutPressed;

	// 各ポーズを取り続けている間ずっとtrueになる値（プレス）。BoostHeld等が返す。
	private bool _cachedBoostHeld;
	private bool _cachedShieldHeld;
	private bool _cachedShortcutHeld;

	// receiver: 姿勢データの取得元。settings: 判定用パラメータ（nullならデフォルト値で生成する）。
	public TrackingRaceInput(PoseLandmarkReceiver receiver, TrackingRaceInputSettings settings) {
		_receiver = receiver;
		_settings = settings ?? new TrackingRaceInputSettings();
	}

	// 左右の傾きから求めた操舵値（-1～1）。直立時はできるだけ0に近くなる。
	public float Horizontal {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedHorizontal;
		}
	}

	// ブーストのポーズ（両手首を腰の横で引く）が成立した瞬間だけtrue。
	public bool BoostPressed {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedBoostPressed;
		}
	}

	// シールドのポーズ（片手を胸の前で水平に構える）が成立した瞬間だけtrue。
	public bool ShieldPressed {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedShieldPressed;
		}
	}

	// ショートカットのポーズ（バンザイ）が成立した瞬間だけtrue。
	public bool ShortcutPressed {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedShortcutPressed;
		}
	}

	// ブーストのポーズを取り続けている間ずっとtrue（プレス）。ポーズを解くとfalseになりブーストが終了する。
	public bool BoostHeld {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedBoostHeld;
		}
	}

	// シールドのポーズを取り続けている間ずっとtrue（プレス）。ポーズを解くとfalseになりシールドが終了する。
	public bool ShieldHeld {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedShieldHeld;
		}
	}

	// ショートカット（バンザイ）のポーズを取り続けている間ずっとtrue（プレス）。IRaceInputには無い独自の拡張。
	public bool IsShortcutPoseActive {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedShortcutHeld;
		}
	}

	// 1フレームにつき1回だけ全ポーズ・Horizontalを評価し、結果をキャッシュする。
	// 同じフレーム内で複数回プロパティを参照しても、判定結果が変わらないようにするための仕組み。
	private void EnsureUpdatedForCurrentFrame() {
		int frame = Time.frameCount;
		// 既にこのフレームで評価済みなら何もしない（キャッシュ済みの値をそのまま使う）
		if (frame == _lastEvaluatedFrame) {
			return;
		}

		_lastEvaluatedFrame = frame;

		// Horizontalを計算
		_cachedHorizontal = EvaluateHorizontal();

		// ブースト：今フレームの成立状態を求め、トリガー（立ち上がり）とプレス（継続状態）の両方を更新
		bool boostActive = EvaluateBoost();
		_cachedBoostPressed = boostActive && !_previousBoostActive; // 前フレームは不成立で今フレームだけ成立＝立ち上がり
		_cachedBoostHeld = boostActive;
		_previousBoostActive = boostActive;

		// シールド：ブーストと同様の手順
		bool shieldActive = EvaluateShield();
		_cachedShieldPressed = shieldActive && !_previousShieldActive;
		_cachedShieldHeld = shieldActive;
		_previousShieldActive = shieldActive;

		// ショートカット：ブーストと同様の手順
		bool shortcutActive = EvaluateShortcut();
		_cachedShortcutPressed = shortcutActive && !_previousShortcutActive;
		_cachedShortcutHeld = shortcutActive;
		_previousShortcutActive = shortcutActive;
	}

	// 上半身の左右の傾き角度から、Horizontal（-1～1）を計算する。
	private float EvaluateHorizontal() {
		// 傾きが取得できない場合の初期値（0＝直立扱い）
		float targetHorizontal = 0f;

		// 判定：肩・腰の4点すべてが十分な信頼度で検出できているか（できていなければ傾き計算をスキップし0のまま扱う）
		if (_receiver != null
			&& IsVisible(LeftShoulder) && IsVisible(RightShoulder)
			&& IsVisible(LeftHip) && IsVisible(RightHip)) {
			// 腰の中点から肩の中点へ向かうベクトル（背骨の向き）
			Vector3 spine = GetMidShoulder() - GetMidHip();

			// 直立時は0度。左右に傾くとspine.x（左右方向）が出てくるので、そこから傾き角度を求める。
			float tiltDeg = Mathf.Atan2(spine.x, spine.y) * Mathf.Rad2Deg;
			float absDeg = Mathf.Abs(tiltDeg);

			// 判定：デッドゾーンより大きく傾いているか（デッドゾーン以下ならHorizontalは0のまま＝直立時のノイズ対策）
			if (absDeg > _settings.tiltDeadZoneAngle) {
				// デッドゾーン〜最大角度の範囲を0～1に正規化
				float range = Mathf.Max(0.01f, _settings.maxTiltAngle - _settings.tiltDeadZoneAngle);
				float t = Mathf.Clamp01((absDeg - _settings.tiltDeadZoneAngle) / range);
				// 0～1の値にカーブ（べき乗）をかけ、中間域での出やすさを調整
				float curved = Mathf.Pow(t, _settings.tiltResponseCurve);
				// 元の傾きの符号（左右どちらに傾いたか）を掛け戻す
				targetHorizontal = Mathf.Sign(tiltDeg) * curved;
			}

			// 判定：左右反転設定がONか（鏡像設定やカメラ向きの都合で符号を反転したい場合用）
			if (_settings.invertHorizontal) {
				targetHorizontal = -targetHorizontal;
			}
		}

		// 判定：平滑化を行うかどうか（0以下なら平滑化なしで目標値をそのまま採用）
		if (_settings.horizontalSmoothingTime <= 0f) {
			_smoothedHorizontal = targetHorizontal;
		} else {
			// 指数平滑化：時定数(horizontalSmoothingTime)に応じて目標値へ滑らかに近づける
			float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, _settings.horizontalSmoothingTime));
			_smoothedHorizontal = Mathf.Lerp(_smoothedHorizontal, targetHorizontal, t);
		}

		// 念のため-1～1にクランプして返す
		return Mathf.Clamp(_smoothedHorizontal, -1f, 1f);
	}

	// ブースト（空手の構え：両手首を腰〜肩の中間あたりの高さで体の横に引く）が成立しているかどうかを判定する。
	private bool EvaluateBoost() {
		// 判定：手首・腰・肩のランドマークが揃っているか（欠けていれば未成立扱い）
		if (_receiver == null
			|| !IsVisible(LeftWrist) || !IsVisible(RightWrist)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)
			|| !IsVisible(LeftShoulder) || !IsVisible(RightShoulder)) {
			return false;
		}

		// しきい値の基準となる胴の長さ
		float torsoLength = GetTorsoLength();
		// 判定：胴の長さが極端に小さい（＝検出が不安定）場合は未成立扱いにする
		if (torsoLength <= 0.0001f) {
			return false;
		}

		// 判定：左右どちらの手も「腰〜肩の中間あたりの高さで体の横に構えている」状態になっているか（両方成立していればブースト成立）
		return IsHandChamberedAtWaist(LeftWrist, LeftHip, LeftShoulder, torsoLength)
			&& IsHandChamberedAtWaist(RightWrist, RightHip, RightShoulder, torsoLength);
	}

	// 片手が「腰〜肩の中間あたりの高さで体の横に構えている」状態かどうかを判定する（ブースト判定の左右共通処理）。
	// MediaPipeの腰ランドマークは股関節付近のため、腰の高さそのものを基準にすると腕を下げただけの姿勢を誤検知してしまう。
	// そのためboostHeightRatioで腰〜肩の間を補間した高さを基準点として使う。
	private bool IsHandChamberedAtWaist(int wristIndex, int hipIndex, int shoulderIndex, float torsoLength) {
		Vector3 wrist = GetLandmark(wristIndex);
		Vector3 hip = GetLandmark(hipIndex);
		Vector3 shoulder = GetLandmark(shoulderIndex);
		// 腰〜肩の間をboostHeightRatioで補間した基準点（0で腰、1で肩の位置）
		Vector3 target = Vector3.Lerp(hip, shoulder, _settings.boostHeightRatio);

		// 手首と基準点の高さの差（小さいほど基準の高さに近い）
		float heightDiff = Mathf.Abs(wrist.y - target.y);
		// 手首と基準点の水平方向の差（小さいほど「体の横に引き寄せている」状態に近い）
		float horizontalDiff = Mathf.Abs(wrist.x - target.x);

		// 判定：高さ・水平位置の両方が許容範囲内に収まっているか
		return heightDiff <= torsoLength * _settings.boostHeightToleranceRatio
			&& horizontalDiff <= torsoLength * _settings.boostHorizontalToleranceRatio;
	}

	// シールド（片手を胸の前で水平に構える）が成立しているかどうかを判定する。
	private bool EvaluateShield() {
		// 判定：肩・腰のランドマークが揃っているか（欠けていれば未成立扱い）
		if (_receiver == null || !IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)) {
			return false;
		}

		// しきい値の基準となる胴の長さ
		float torsoLength = GetTorsoLength();
		// 判定：胴の長さが極端に小さい（＝検出が不安定）場合は未成立扱いにする
		if (torsoLength <= 0.0001f) {
			return false;
		}

		// 胸の高さ（肩の高さから少し下げた位置）
		float chestHeight = GetChestHeight(torsoLength);
		// 体の左右中心のX座標（肩の中点）
		float centerX = GetMidShoulder().x;

		// 判定：左右どちらか一方の腕でもシールド姿勢になっていれば成立（両手同時である必要はない）
		return IsArmHeldAsShield(LeftWrist, LeftElbow, chestHeight, centerX, torsoLength)
			|| IsArmHeldAsShield(RightWrist, RightElbow, chestHeight, centerX, torsoLength);
	}

	// 片方の腕が「胸の前で水平に構えている」状態かどうかを判定する（シールド判定の左右共通処理）。
	private bool IsArmHeldAsShield(int wristIndex, int elbowIndex, float chestHeight, float centerX, float torsoLength) {
		// 判定：手首・肘のランドマークが検出できているか
		if (!IsVisible(wristIndex) || !IsVisible(elbowIndex)) {
			return false;
		}

		Vector3 wrist = GetLandmark(wristIndex);
		Vector3 elbow = GetLandmark(elbowIndex);

		// 条件1：手首が胸の高さに近いか
		bool atChestHeight = Mathf.Abs(wrist.y - chestHeight) <= torsoLength * _settings.shieldHeightToleranceRatio;
		// 条件2：肘と手首の高さがほぼ同じか（腕が水平に近いかどうかの判定）
		bool forearmLevel = Mathf.Abs(wrist.y - elbow.y) <= torsoLength * _settings.shieldForearmLevelToleranceRatio;

		// 条件3：手首が肘よりも体の中心側にあるか（盾を構えるように、肘を軸に手首を体の内側へ寄せているかの判定）
		float wristDistanceFromCenter = Mathf.Abs(wrist.x - centerX);
		float elbowDistanceFromCenter = Mathf.Abs(elbow.x - centerX);
		bool isInwardOfElbow = elbowDistanceFromCenter - wristDistanceFromCenter >= torsoLength * _settings.shieldInwardMarginRatio;

		// 判定：3条件すべてを満たしたときだけ「シールド姿勢」とみなす
		return atChestHeight && forearmLevel && isInwardOfElbow;
	}

	// ショートカット（バンザイ：両手首を頭より高く上げる）が成立しているかどうかを判定する。
	private bool EvaluateShortcut() {
		// 判定：鼻・手首・肩・腰のランドマークが揃っているか（欠けていれば未成立扱い）
		if (_receiver == null
			|| !IsVisible(Nose) || !IsVisible(LeftWrist) || !IsVisible(RightWrist)
			|| !IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)) {
			return false;
		}

		// しきい値の基準となる胴の長さ
		float torsoLength = GetTorsoLength();
		// 判定：胴の長さが極端に小さい（＝検出が不安定）場合は未成立扱いにする
		if (torsoLength <= 0.0001f) {
			return false;
		}

		// 鼻の高さと、そこからどれだけ上なら「上げている」とみなすかのマージン
		float noseY = GetLandmark(Nose).y;
		float margin = torsoLength * _settings.shortcutRaiseMarginRatio;

		// 判定：両方の手首が「鼻の高さ＋マージン」より高い位置にあるか（両手とも満たして初めて成立）
		return GetLandmark(LeftWrist).y >= noseY + margin
			&& GetLandmark(RightWrist).y >= noseY + margin;
	}

	// 左肩・右肩の中点座標を返す。
	private Vector3 GetMidShoulder() {
		return (GetLandmark(LeftShoulder) + GetLandmark(RightShoulder)) * 0.5f;
	}

	// 左腰・右腰の中点座標を返す。
	private Vector3 GetMidHip() {
		return (GetLandmark(LeftHip) + GetLandmark(RightHip)) * 0.5f;
	}

	// 肩の中点から腰の中点までの距離（胴の長さ）。各種しきい値をスケールに依存しない比率で扱うための基準値。
	private float GetTorsoLength() {
		return Vector3.Distance(GetMidShoulder(), GetMidHip());
	}

	// 「胸の高さ」を、肩の高さから胴の長さに比例した分だけ下げた位置として計算する。
	private float GetChestHeight(float torsoLength) {
		return GetMidShoulder().y - torsoLength * _settings.chestHeightOffsetRatio;
	}

	// 指定インデックスのランドマークの座標を取得する。範囲外や未受信の場合はVector3.zeroを返す。
	private Vector3 GetLandmark(int index) {
		PoseLandmark[] landmarks = _receiver.Landmarks;
		// 判定：ランドマーク配列が存在し、かつインデックスが範囲内か
		if (landmarks == null || index < 0 || index >= landmarks.Length) {
			return Vector3.zero;
		}

		return landmarks[index].Position;
	}

	// 指定インデックスのランドマークが、設定された信頼度（minVisibility）以上で検出されているかを判定する。
	private bool IsVisible(int index) {
		PoseLandmark[] landmarks = _receiver.Landmarks;
		// 判定：ランドマーク配列が存在し、かつインデックスが範囲内か（範囲外なら未検出扱い）
		if (landmarks == null || index < 0 || index >= landmarks.Length) {
			return false;
		}

		// 判定：信頼度が設定値以上あるか
		return landmarks[index].Visibility >= _settings.minVisibility;
	}
}
