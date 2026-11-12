using UnityEngine;

// PoseLandmarkReceiverが受信した姿勢からレース入力を作るIRaceInput実装。
// 判定にはReceiver.NormalizedLandmarks（縦横比をそろえた姿勢空間。2D・3Dモード共通でMediaPipeの並び）を使う。
//
// 移動（Move）
//  - x（左右）：上半身の左右の傾き角度から連続値を作る。
//  - y（上）  ：両手を上に上げる。両手首が鼻より高いほど大きくなる（手首が映らないときは肘で代用）。
//  - y（下）  ：しゃがむ。キャリブレーション（PoseCalibration、Entryシーンで測定）の直立時の肩の高さから、
//               どれだけ肩が下がったかで決める。キャリブレーションしていないときは下は0のまま。
//  - xとyを合わせたベクトルの大きさは1以下に制限する（IRaceInput.Moveの約束）。8方向への丸めは飛行制御側で行う。
//
// ボタン
//  - BoostPressed/ShieldPressedは「ポーズが成立したフレームで1回だけtrue」になるトリガー。
//  - BoostHeld/ShieldHeldはポーズを取り続けている間ずっとtrueになる「プレス（継続状態）」。
//  - Boost  : 空手の構えのように、両手首を腰〜肩の中間あたりの高さで体の横に引く（腰そのものの高さだと、腕を下げただけの姿勢を誤検知するため）
//  - Shield : 片手（左右どちらでも可）を胸の前で水平に構える（手首が肘より体の中心側にある状態）
//  - Jammer : 片手（左右どちらでも可）をカメラに向けて前に突き出す。正面から見ると手首・肘・肩が重なって見えることで判定する。
//             JammerPressedだけ（Heldは無い）。ポーズがjammerHoldTime続いたら1回だけtrue。腕を戻すまで次は撃てない。
//
// トラッキングが途切れた（人物が映らない）ときは、Moveを0、ボタンをすべて離した扱いにする。
// 2026-11-07 川又：IRaceInputのHorizontal→Move（Vector2）変更と、ショートカット廃止への暫定対応。
// 2026-11-11：上下の入力（バンザイ／しゃがみ）、2Dモード・縦横比・トラッキング途切れへの対応。ショートカットの処理は削除。
// 2026-11-12：妨害弾（JammerPressed）を追加。シールドに「前腕が画面上である程度の長さに見える」条件を追加（突き出した腕でシールドが出ないように）。
public class TrackingRaceInput : IRaceInput {
	private const int Nose = PoseLandmarkIndex.Nose;
	private const int LeftShoulder = PoseLandmarkIndex.LeftShoulder;
	private const int RightShoulder = PoseLandmarkIndex.RightShoulder;
	private const int LeftElbow = PoseLandmarkIndex.LeftElbow;
	private const int RightElbow = PoseLandmarkIndex.RightElbow;
	private const int LeftWrist = PoseLandmarkIndex.LeftWrist;
	private const int RightWrist = PoseLandmarkIndex.RightWrist;
	private const int LeftHip = PoseLandmarkIndex.LeftHip;
	private const int RightHip = PoseLandmarkIndex.RightHip;

	// 姿勢データの取得元
	private readonly PoseLandmarkReceiver _receiver;
	// 各種しきい値・調整用パラメータ
	private readonly TrackingRaceInputSettings _settings;

	// 直近でEnsureUpdatedForCurrentFrameを実行したUnityのフレーム番号。同一フレーム内の再計算を防ぐために使う。
	private int _lastEvaluatedFrame = -1;

	// 平滑化用に保持している現在値（毎フレーム目標値へ少しずつ近づける）
	private float _smoothedHorizontal;
	private float _smoothedVertical;
	// Moveプロパティが返す、このフレーム用に計算済みの値
	private Vector2 _cachedMove;

	// 各ポーズの「前フレームでの継続状態」。今フレームの状態と比較してPressed（トリガー）の立ち上がりを検出するために使う。
	private bool _previousBoostActive;
	private bool _previousShieldActive;

	// 各ポーズが成立した瞬間だけtrueになる値（トリガー）。
	private bool _cachedBoostPressed;
	private bool _cachedShieldPressed;

	// 各ポーズを取り続けている間ずっとtrueになる値（プレス）。
	private bool _cachedBoostHeld;
	private bool _cachedShieldHeld;

	// 妨害弾：このフレームで撃つか（トリガー）
	private bool _cachedJammerPressed;
	// 妨害弾のポーズが今フレームで成立しているか（デバッグ表示用）
	private bool _jammerPoseActive;
	// 妨害弾のポーズが続いている秒数（成立までの待ちに使う）
	private float _jammerPoseTime;
	// 妨害弾のポーズを解いている秒数（次を撃てるようにするまでの待ちに使う）
	private float _jammerReleaseTime;
	// 次の妨害弾を撃てる状態か（撃ったらfalse、腕を戻してjammerReleaseTimeたったらtrue）
	private bool _jammerArmed = true;

	// 妨害弾の判定の途中経過（デバッグ表示用）
	private JammerArmDebug _leftJammerDebug;
	private JammerArmDebug _rightJammerDebug;

	// receiver: 姿勢データの取得元。settings: 判定用パラメータ（nullならデフォルト値で生成する）。
	public TrackingRaceInput(PoseLandmarkReceiver receiver, TrackingRaceInputSettings settings) {
		_receiver = receiver;
		_settings = settings ?? new TrackingRaceInputSettings();
	}

	// 移動の入力。x：左右の傾き、y：バンザイ（＋）／しゃがみ（－）。大きさは0～1。
	public Vector2 Move {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedMove;
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

	// 妨害弾のポーズ（片手を前に突き出す）がjammerHoldTime続いたフレームで1回だけtrue。腕を戻すまで次はtrueにならない。
	public bool JammerPressed {
		get {
			EnsureUpdatedForCurrentFrame();
			return _cachedJammerPressed;
		}
	}

	// 妨害弾のポーズを取っているか（デバッグ表示用。IRaceInputには無い）
	public bool JammerPoseActive {
		get {
			EnsureUpdatedForCurrentFrame();
			return _jammerPoseActive;
		}
	}

	// 妨害弾の判定の途中経過・左腕（デバッグ表示用）
	public JammerArmDebug LeftJammerDebug {
		get {
			EnsureUpdatedForCurrentFrame();
			return _leftJammerDebug;
		}
	}

	// 妨害弾の判定の途中経過・右腕（デバッグ表示用）
	public JammerArmDebug RightJammerDebug {
		get {
			EnsureUpdatedForCurrentFrame();
			return _rightJammerDebug;
		}
	}

	// トラッキング中か（人物が映っているか）。デバッグ表示用。
	public bool IsTracking => _receiver != null && _receiver.IsTracking;

	// 1フレームにつき1回だけ全ポーズ・Moveを評価し、結果をキャッシュする。
	// 同じフレーム内で複数回プロパティを参照しても、判定結果が変わらないようにするための仕組み。
	private void EnsureUpdatedForCurrentFrame() {
		int frame = Time.frameCount;
		// 既にこのフレームで評価済みなら何もしない（キャッシュ済みの値をそのまま使う）
		if (frame == _lastEvaluatedFrame) {
			return;
		}

		_lastEvaluatedFrame = frame;

		// トラッキングが途切れているときは、すべての判定を「入力なし」として扱う
		bool tracking = IsTracking;

		// 移動：左右・上下の目標値を求めて平滑化し、大きさを1以下にする
		float targetHorizontal = tracking ? EvaluateHorizontal() : 0f;
		float targetVertical = tracking ? EvaluateVertical() : 0f;
		_smoothedHorizontal = Smooth(_smoothedHorizontal, targetHorizontal, _settings.horizontalSmoothingTime);
		_smoothedVertical = Smooth(_smoothedVertical, targetVertical, _settings.verticalSmoothingTime);
		_cachedMove = Vector2.ClampMagnitude(new Vector2(_smoothedHorizontal, _smoothedVertical), 1f);

		// ブースト：今フレームの成立状態を求め、トリガー（立ち上がり）とプレス（継続状態）の両方を更新
		bool boostActive = tracking && EvaluateBoost();
		_cachedBoostPressed = boostActive && !_previousBoostActive; // 前フレームは不成立で今フレームだけ成立＝立ち上がり
		_cachedBoostHeld = boostActive;
		_previousBoostActive = boostActive;

		// シールド：ブーストと同様の手順
		bool shieldActive = tracking && EvaluateShield();
		_cachedShieldPressed = shieldActive && !_previousShieldActive;
		_cachedShieldHeld = shieldActive;
		_previousShieldActive = shieldActive;

		// 妨害弾：ポーズが続いた時間・解いた時間で、1回だけ撃つ
		UpdateJammer(tracking);
	}

	// 妨害弾の判定を進める。時間はtimeScaleの影響を受けないようにunscaledDeltaTimeで数える（ヒットストップ中も止まらない）。
	private void UpdateJammer(bool tracking) {
		_cachedJammerPressed = false;
		_jammerPoseActive = tracking && EvaluateJammer();
		float deltaTime = Time.unscaledDeltaTime;

		// 判定：ポーズを取っているか
		if (_jammerPoseActive) {
			_jammerPoseTime += deltaTime;
			_jammerReleaseTime = 0f;

			// 判定：撃てる状態で、ポーズがjammerHoldTime続いたか（続いたら1回だけ撃つ）
			if (_jammerArmed && _jammerPoseTime >= _settings.jammerHoldTime) {
				_cachedJammerPressed = true;
				_jammerArmed = false;
			}
			return;
		}

		_jammerPoseTime = 0f;
		_jammerReleaseTime += deltaTime;
		// 判定：腕を戻してjammerReleaseTimeたったか（たったら次を撃てるようにする）
		if (_jammerReleaseTime >= _settings.jammerReleaseTime) {
			_jammerArmed = true;
		}
	}

	// 妨害弾のポーズ（片手を前に突き出す）が成立しているか。左右どちらかの腕で成立すればよい。
	private bool EvaluateJammer() {
		_leftJammerDebug = default;
		_rightJammerDebug = default;

		// 判定：肩・腰のランドマークが揃っているか（胴の長さに使う）
		if (!IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)) {
			return false;
		}

		float torsoLength = GetTorsoLength();
		// 判定：胴の長さが極端に小さい（＝検出が不安定）場合は未成立扱いにする
		if (torsoLength <= 0.0001f) {
			return false;
		}

		_leftJammerDebug = EvaluateJammerArm(LeftShoulder, LeftElbow, LeftWrist, torsoLength);
		_rightJammerDebug = EvaluateJammerArm(RightShoulder, RightElbow, RightWrist, torsoLength);
		return _leftJammerDebug.Active || _rightJammerDebug.Active;
	}

	// 片腕が前に突き出されているか。腕がカメラを向くと、正面からは手首・肘・肩が重なって見えることを使う。
	private JammerArmDebug EvaluateJammerArm(int shoulderIndex, int elbowIndex, int wristIndex, float torsoLength) {
		JammerArmDebug result = default;

		// 条件1：肩・肘・手首が見えているか。腕がカメラを向くと関節が重なって信頼度が下がるため、このポーズだけ低めのしきい値を使う
		if (!IsVisible(shoulderIndex, _settings.jammerMinVisibility)
			|| !IsVisible(elbowIndex, _settings.jammerMinVisibility)
			|| !IsVisible(wristIndex, _settings.jammerMinVisibility)) {
			return result;
		}

		Vector3 shoulder = GetLandmark(shoulderIndex);
		Vector3 elbow = GetLandmark(elbowIndex);
		Vector3 wrist = GetLandmark(wristIndex);

		result.Valid = true;
		// 画面上（x, y）の距離。胴の長さに対する比率
		result.WristRatio = Vector2.Distance(wrist, shoulder) / torsoLength;
		result.ElbowRatio = Vector2.Distance(elbow, shoulder) / torsoLength;

		// 条件2：手首が肩の近くに重なって見えるか
		bool wristNear = result.WristRatio <= _settings.jammerWristRadiusRatio;
		// 条件3：肘も肩の近くに重なって見えるか（手で顔や肩を触ったときは肘が下がっているので、ここで区別する）
		bool elbowNear = result.ElbowRatio <= _settings.jammerElbowRadiusRatio;

		// 条件4（3Dモードのみ）：手首が肩より手前（カメラ側）に出ているか。2Dモードにはzが無いので見ない
		bool forward = true;
		// 判定：奥行きを使う設定で、3Dモードか
		if (_settings.jammerUseDepth && !_receiver.Is2DMode) {
			result.HasDepth = true;
			// zは手前が正。MediaPipeのzは映像の幅の単位なので、縦横比を掛けてx, yと同じ単位（映像の高さ）にそろえる
			result.DepthRatio = (wrist.z - shoulder.z) * _receiver.Aspect / torsoLength;
			forward = result.DepthRatio >= _settings.jammerDepthRatio;
		}

		result.Active = wristNear && elbowNear && forward;
		return result;
	}

	// 指数平滑化：時定数(smoothingTime)に応じて目標値へ滑らかに近づける。0以下なら平滑化なしで目標値をそのまま使う。
	private static float Smooth(float current, float target, float smoothingTime) {
		// 判定：平滑化を行うかどうか
		if (smoothingTime <= 0f) {
			return target;
		}

		float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smoothingTime));
		return Mathf.Lerp(current, target, t);
	}

	// デッドゾーン〜最大値の範囲を0～1に正規化し、カーブ（べき乗）をかける。
	private static float ApplyResponse(float value, float deadZone, float max, float curve) {
		// 判定：デッドゾーン以下か（以下なら0＝ノイズ対策）
		if (value <= deadZone) {
			return 0f;
		}

		float range = Mathf.Max(0.0001f, max - deadZone);
		float t = Mathf.Clamp01((value - deadZone) / range);
		return Mathf.Pow(t, curve);
	}

	// 上半身の左右の傾き角度から、左右の入力（-1～1）を計算する。
	private float EvaluateHorizontal() {
		// 判定：肩・腰の4点すべてが十分な信頼度で検出できているか（できていなければ0＝直立扱い）
		if (!IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)) {
			return 0f;
		}

		// 腰の中点から肩の中点へ向かうベクトル（背骨の向き）
		Vector3 spine = GetMidShoulder() - GetMidHip();

		// 直立時は0度。左右に傾くとspine.x（左右方向）が出てくるので、そこから傾き角度を求める。
		float tiltDeg = Mathf.Atan2(spine.x, spine.y) * Mathf.Rad2Deg;
		float value = Mathf.Sign(tiltDeg) * ApplyResponse(Mathf.Abs(tiltDeg), _settings.tiltDeadZoneAngle, _settings.maxTiltAngle, _settings.tiltResponseCurve);

		// 判定：左右反転設定がONか（鏡像設定やカメラ向きの都合で符号を反転したい場合用）
		return _settings.invertHorizontal ? -value : value;
	}

	// 上下の入力（-1～1）を計算する。上＝バンザイ、下＝しゃがみ。両方成立したときは差し引きする。
	private float EvaluateVertical() {
		float value = EvaluateRaiseHands() - EvaluateCrouch();
		// 判定：上下反転設定がONか
		return _settings.invertVertical ? -value : value;
	}

	// 上（0～1）：両手を上げているほど大きい。低い方の手の高さで決める（両手とも上げて初めて値が出る）。
	private float EvaluateRaiseHands() {
		// 判定：鼻・肩・腰が見えているか（胴の長さと基準の高さに使う）
		if (!IsVisible(Nose) || !IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
			|| !IsVisible(LeftHip) || !IsVisible(RightHip)) {
			return 0f;
		}

		float torsoLength = GetTorsoLength();
		// 判定：胴の長さが極端に小さい（＝検出が不安定）場合は0扱いにする
		if (torsoLength <= 0.0001f) {
			return 0f;
		}

		// 判定：左右の腕の高さが取れるか（手首も肘も見えていない腕があれば0）
		if (!TryGetArmHeight(LeftWrist, LeftElbow, out float leftHeight) || !TryGetArmHeight(RightWrist, RightElbow, out float rightHeight)) {
			return 0f;
		}

		// 低い方の手が、鼻の高さからどれだけ上にあるか（胴の長さに対する比率）
		float raise = (Mathf.Min(leftHeight, rightHeight) - GetLandmark(Nose).y) / torsoLength;
		return ApplyResponse(raise, _settings.raiseStartRatio, _settings.raiseFullRatio, _settings.verticalResponseCurve);
	}

	// 腕の高さ。手首が見えていれば手首、手を上げすぎて手首が映らないときは肘で代用する。
	private bool TryGetArmHeight(int wristIndex, int elbowIndex, out float height) {
		// 判定：手首が見えているか
		if (IsVisible(wristIndex)) {
			height = GetLandmark(wristIndex).y;
			return true;
		}

		// 判定：肘が見えているか
		if (IsVisible(elbowIndex)) {
			height = GetLandmark(elbowIndex).y;
			return true;
		}

		height = 0f;
		return false;
	}

	// 下（0～1）：キャリブレーションした直立時の肩の高さから、どれだけ肩が下がったか（直立時の胴の長さに対する比率）。
	private float EvaluateCrouch() {
		// 判定：キャリブレーション済みか（未測定なら基準が無いので0）
		if (!PoseCalibration.HasValue || PoseCalibration.TorsoLength <= 0.0001f) {
			return 0f;
		}

		// 判定：肩が見えているか
		if (!IsVisible(LeftShoulder) || !IsVisible(RightShoulder)) {
			return 0f;
		}

		float drop = (PoseCalibration.ShoulderHeight - GetMidShoulder().y) / PoseCalibration.TorsoLength;
		return ApplyResponse(drop, _settings.crouchDeadZoneRatio, _settings.crouchFullRatio, _settings.verticalResponseCurve);
	}

	// ブースト（空手の構え：両手首を腰〜肩の中間あたりの高さで体の横に引く）が成立しているかどうかを判定する。
	private bool EvaluateBoost() {
		// 判定：手首・腰・肩のランドマークが揃っているか（欠けていれば未成立扱い）
		if (!IsVisible(LeftWrist) || !IsVisible(RightWrist)
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
	// 腰ランドマークは股関節付近のため、腰の高さそのものを基準にすると腕を下げただけの姿勢を誤検知してしまう。
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
		if (!IsVisible(LeftShoulder) || !IsVisible(RightShoulder)
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

		// 条件4：前腕が画面上である程度の長さに見えるか（腕を前に突き出すと前腕が縮んで見えて条件2を満たしてしまうため、妨害弾のポーズと区別する）
		bool forearmVisibleLength = Vector2.Distance(wrist, elbow) >= torsoLength * _settings.shieldMinForearmRatio;

		// 判定：4条件すべてを満たしたときだけ「シールド姿勢」とみなす
		return atChestHeight && forearmLevel && isInwardOfElbow && forearmVisibleLength;
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
	// 2Dモードにはzが無いため、どちらのモードでも画面上（x, y）の距離で測る（PoseCalibratorと同じ測り方）。
	private float GetTorsoLength() {
		return Vector2.Distance(GetMidShoulder(), GetMidHip());
	}

	// 「胸の高さ」を、肩の高さから胴の長さに比例した分だけ下げた位置として計算する。
	private float GetChestHeight(float torsoLength) {
		return GetMidShoulder().y - torsoLength * _settings.chestHeightOffsetRatio;
	}

	// 指定インデックスのランドマークの座標（姿勢空間）を取得する。範囲外や未受信の場合はVector3.zeroを返す。
	private Vector3 GetLandmark(int index) {
		PoseLandmark[] landmarks = _receiver != null ? _receiver.NormalizedLandmarks : null;
		// 判定：ランドマーク配列が存在し、かつインデックスが範囲内か
		if (landmarks == null || index < 0 || index >= landmarks.Length) {
			return Vector3.zero;
		}

		return landmarks[index].Position;
	}

	// 指定インデックスのランドマークが、設定された信頼度（minVisibility）以上で検出されているかを判定する。
	private bool IsVisible(int index) {
		return IsVisible(index, _settings.minVisibility);
	}

	// 指定インデックスのランドマークが、指定した信頼度以上で検出されているかを判定する。
	private bool IsVisible(int index, float minVisibility) {
		PoseLandmark[] landmarks = _receiver != null ? _receiver.NormalizedLandmarks : null;
		// 判定：ランドマーク配列が存在し、かつインデックスが範囲内か（範囲外なら未検出扱い）
		if (landmarks == null || index < 0 || index >= landmarks.Length) {
			return false;
		}

		// 判定：信頼度が設定値以上あるか
		return landmarks[index].Visibility >= minVisibility;
	}
}

// 妨害弾の判定の途中経過（片腕分。デバッグ表示用）。
public struct JammerArmDebug {
	public bool Valid;       // 肩・肘・手首が見えていて、距離を測れたか
	public float WristRatio; // 手首と肩の画面上の距離（胴の長さに対する比率）
	public float ElbowRatio; // 肘と肩の画面上の距離（胴の長さに対する比率）
	public bool HasDepth;    // 奥行き（z）を見たか（3Dモードで、jammerUseDepthがONのとき）
	public float DepthRatio; // 手首が肩よりどれだけ手前か（胴の長さに対する比率）
	public bool Active;      // この腕でポーズが成立しているか
}
