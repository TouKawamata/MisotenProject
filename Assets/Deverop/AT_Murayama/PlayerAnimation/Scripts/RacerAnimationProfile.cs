using System;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation
{
    /// <summary>
    /// 左右・上下移動のアニメーション制御方式。
    /// インスペクターから切り替えて AnimatorController を再構築（GUID維持）し、実機で比較選定できます。
    /// </summary>
    public enum FlightTurnAnimationMode
    {
        [InspectorName("案A：ステート遷移（閾値で Fly ⇄ Left / Right をクロスフェード切替）")]
        DiscreteStates = 0,

        [InspectorName("案B：BlendTree（MoveX / MoveY 入力量で連続ブレンド）")]
        BlendTree = 1
    }

    /// <summary>
    /// アニメーションの左右・上下パラメータへ反映する移動データの取得元。
    /// 入力だけでなく、コース中央への自動戻り補間やコースカーブでの自動旋回を含めるかを選べます。
    /// </summary>
    public enum FlightMotionSourceMode
    {
        [InspectorName("① 入力のみ (Input Only)")]
        InputOnly = 0,

        [InspectorName("② 入力 ＋ コース中央への自動戻り補間")]
        InputAndCenterReturn = 1,

        [InspectorName("③ 入力 ＋ コースカーブ自動旋回")]
        InputAndCourseCurve = 2,

        [InspectorName("④ 全部合成（入力 ＋ 中央戻り ＋ コースカーブ）")]
        FullCombined = 3
    }

    /// <summary>
    /// Player / CPU 共通のアニメーション設定プロファイル（ScriptableObject）。
    /// 必須基本モーション（SetUp / TakeOff / Fly / RightCurve / LeftCurve / Stun / Fire）に加え、
    /// 将来追加予定の拡張モーション（MoveUp / MoveDown / Boost / Shield）もスロットとして保持します。
    /// </summary>
    [CreateAssetMenu(
        fileName = "RacerAnimationProfile",
        menuName = "AT_Murayama/PlayerAnimation/Racer Animation Profile")]
    public sealed class RacerAnimationProfile : ScriptableObject
    {
        // ============================================================
        // Animator パラメータ名・ステート名の共通定数（Runtime / Editor 共有）
        // ============================================================
        public const string ParamIsRacing = "IsRacing";
        public const string ParamStartPlay = "StartPlay";
        public const string ParamMoveX = "MoveX";
        public const string ParamMoveY = "MoveY";
        public const string ParamTurnDir = "TurnDir";
        public const string ParamVerticalDir = "VerticalDir";
        public const string ParamIsStunned = "IsStunned";
        public const string ParamIsBoosting = "IsBoosting";
        public const string ParamIsShielding = "IsShielding";
        public const string ParamFire = "Fire";

        public const string StateSetUp = "SetUp";
        public const string StateTakeOff = "TakeOff";
        public const string StateFly = "Fly";
        public const string StateFlightBlend = "FlightBlend";
        public const string StateLeftCurve = "LeftCurve";
        public const string StateRightCurve = "RightCurve";
        public const string StateMoveUp = "MoveUp";
        public const string StateMoveDown = "MoveDown";
        public const string StateStun = "Stun";
        public const string StateFire = "Fire";
        public const string StateBoost = "Boost";
        public const string StateShield = "Shield";

        // ============================================================
        // 1. 生成・出力先設定
        // ============================================================
        [Header("1. モデル参照・生成先設定")]
        [Tooltip("ボーン対称性の基準となるキャラクターモデル（例: Assets/TeamResources/Art/Models/born model.fbx）")]
        [SerializeField] private GameObject _referenceModelPrefab;

        [Tooltip("補正済み .anim / 自動生成 .controller の保存先フォルダ（Assets/... 形式）")]
        [SerializeField] private string _generatedAssetFolder = "Assets/Deverop/AT_Murayama/PlayerAnimation/Generated";

        [Tooltip("生成・更新対象の AnimatorController アセット名（拡張子不要）")]
        [SerializeField] private string _controllerAssetName = "AC_RacerFlight";

        [Tooltip("自動生成・更新される AnimatorController への参照（GUIDを維持したまま再構築されます）")]
        [SerializeField] private RuntimeAnimatorController _generatedController;

        // ============================================================
        // 2. ソースクリップ設定（基本フロー）
        // ============================================================
        [Header("2. 基本モーション（予定遷移図）")]
        [Tooltip("スタジアムから離陸＆ホバリング（Set up.fbx）")]
        [SerializeField] private AnimationClip _setUpClip;

        [Tooltip("プレイ開始→飛行まで補間（Take off.fbx）")]
        [SerializeField] private AnimationClip _takeOffClip;

        [Tooltip("基本の直線飛行（fly.fbx）")]
        [SerializeField] private AnimationClip _flyClip;

        [Tooltip("右に旋回（Right Curve.fbx または mainRig 補正済み RightCurve_Fixed.anim）")]
        [SerializeField] private AnimationClip _rightCurveClip;

        [Tooltip("左に旋回（Left Curve.fbx または鏡像生成した LeftCurve_Mirrored.anim）")]
        [SerializeField] private AnimationClip _leftCurveClip;

        [Tooltip("スタン（未着の間は空のままでOK。空でも枠とパラメータは先行生成されます）")]
        [SerializeField] private AnimationClip _stunClip;

        [Tooltip("弾発射（未着の間は空のままでOK。空でも枠とパラメータは先行生成されます）")]
        [SerializeField] private AnimationClip _fireClip;

        // ============================================================
        // 3. 将来拡張スロット（未設定なら自動的にスキップ）
        // ============================================================
        [Header("3. 将来拡張モーション（追加したら再構築ボタン1発で自動組み込み）")]
        [Tooltip("上昇移動（設定すると、案Aでは MoveUp ステート、案Bでは 2D BlendTree に自動拡張されます）")]
        [SerializeField] private AnimationClip _moveUpClip;

        [Tooltip("下降移動（設定すると、案Aでは MoveDown ステート、案Bでは 2D BlendTree に自動拡張されます）")]
        [SerializeField] private AnimationClip _moveDownClip;

        [Tooltip("ブースト専用モーション（任意。未設定なら通常飛行のまま炎演出のみ）")]
        [SerializeField] private AnimationClip _boostClip;

        [Tooltip("シールド専用モーション（任意。未設定なら通常飛行のままシールド演出のみ）")]
        [SerializeField] private AnimationClip _shieldClip;

        // ============================================================
        // 4. 切り替え方式・遷移チューニング
        // ============================================================
        [Header("4. 旋回方式・遷移チューニング")]
        [Tooltip("案A（ステート遷移）と案B（BlendTree）を切り替えて再生成できます")]
        [SerializeField] private FlightTurnAnimationMode _turnMode = FlightTurnAnimationMode.DiscreteStates;

        [Tooltip("SetUp 終了後にそのまま最後のホバリング姿勢を維持するか（OFFで SetUp をループ扱いする場合はFBX側のLoop設定と併用）")]
        [SerializeField] private bool _loopSetUpClip = false;

        [Tooltip("Fly（直線飛行）クリップをループ再生にする（自動生成する複製クリップやFBX最適化時に適用）")]
        [SerializeField] private bool _loopFlyClip = true;

        [Tooltip("RightCurve / LeftCurve をループ再生にする（案B: BlendTree比較時や旋回中ループさせたい場合にON）")]
        [SerializeField] private bool _loopCurveClips = false;

        [Tooltip("SetUp → TakeOff のクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _setUpToTakeOffDuration = 0.08f;

        [Tooltip("TakeOff がどこまで再生されたら Fly へブレンド開始するか（正規化時間 0.5〜1.0）")]
        [SerializeField, Range(0.5f, 1.0f)] private float _takeOffExitTime = 0.90f;

        [Tooltip("TakeOff → Fly のクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _takeOffToFlyDuration = 0.12f;

        [Tooltip("Fly ⇄ Left / Right / Up / Down 間のクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _curveTransitionDuration = 0.12f;

        [Tooltip("AnyState → Stun / Stun → Fly のクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _stunTransitionDuration = 0.08f;

        [Tooltip("Fly → Fire / Fire → Fly のクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _fireTransitionDuration = 0.06f;

        [Tooltip("Fire モーションがどこまで再生されたら Fly へ戻るか（正規化時間 0.5〜1.0）")]
        [SerializeField, Range(0.5f, 1.0f)] private float _fireExitTime = 0.90f;

        [Tooltip("Boost / Shield 拡張ステートとのクロスフェード時間（秒）")]
        [SerializeField, Range(0f, 0.5f)] private float _optionalStateTransitionDuration = 0.10f;

        // ============================================================
        // 5. 入力しきい値・ヒステリシス・スムージング
        // ============================================================
        [Header("5. 入力判定・スムージング")]
        [Tooltip("案A（ステート遷移）で左右旋回へ入る Steering の絶対値しきい値")]
        [SerializeField, Range(0.05f, 0.95f)] private float _turnEnterThreshold = 0.25f;

        [Tooltip("案A（ステート遷移）で直線飛行へ戻る Steering の絶対値しきい値（Enterより小さくしてチャタリング防止）")]
        [SerializeField, Range(0.01f, 0.90f)] private float _turnExitThreshold = 0.12f;

        [Tooltip("拡張・上下移動へ入る MoveInput.y の絶対値しきい値")]
        [SerializeField, Range(0.05f, 0.95f)] private float _verticalEnterThreshold = 0.30f;

        [Tooltip("拡張・上下移動から戻る MoveInput.y の絶対値しきい値")]
        [SerializeField, Range(0.01f, 0.90f)] private float _verticalExitThreshold = 0.15f;

        [Tooltip("MoveX / MoveY パラメータの補間応答速度（大きいほど機敏、0で即時反映）")]
        [SerializeField, Range(0f, 30f)] private float _moveParameterSmoothSpeed = 12f;

        // ============================================================
        // 5.5 コース自動移動（中央戻り補間・カーブ自動旋回）の反映
        // ============================================================
        [Header("5.5 コース自動移動（中央戻り補間・カーブ自動旋回）の反映")]
        [Tooltip("どの移動情報をアニメ傾きに反映するか（①入力のみ / ②+中央戻り / ③+カーブ / ④全部合成）")]
        [SerializeField] private FlightMotionSourceMode _motionSourceMode = FlightMotionSourceMode.InputOnly;

        [Tooltip("プレイヤー自身の操作入力（MoveInput）の反映倍率")]
        [SerializeField, Range(0f, 1.5f)] private float _inputMotionWeight = 1.0f;

        [Tooltip("コース中央への自動戻り補間速度の反映倍率（例: 0.65 で自然な軽い傾き）")]
        [SerializeField, Range(0f, 2.0f)] private float _centerReturnWeight = 0.65f;

        [Tooltip("中央戻り横速度（m/s）が何 m/s のときに傾き強さ 1.0 基準とみなすか")]
        [Min(0.5f)]
        [SerializeField] private float _centerReturnReferenceSpeed = 8.0f;

        [Tooltip("中央戻り補間だけで生じる傾きの最大寄与上限（0〜1）")]
        [SerializeField, Range(0f, 1.0f)] private float _centerReturnMaxContribution = 0.70f;

        [Tooltip("コースカーブでの機首自動旋回の反映倍率")]
        [SerializeField, Range(0f, 2.0f)] private float _courseCurveWeight = 0.70f;

        [Tooltip("コースカーブ角速度（deg/s）が何 deg/s のときに傾き強さ 1.0 基準とみなすか")]
        [Min(1.0f)]
        [SerializeField] private float _courseCurveReferenceYawRate = 35.0f;

        [Tooltip("コースカーブ自動旋回だけで生じる傾きの最大寄与上限（0〜1）")]
        [SerializeField, Range(0f, 1.0f)] private float _courseCurveMaxContribution = 0.85f;

        [Tooltip("ON: 上下方向の中央戻り・坂道カーブも MoveY / VerticalDir に反映する / OFF: 左右のみ反映")]
        [SerializeField] private bool _applyAutoMotionToVertical = true;

        [Tooltip("自動移動計測信号（断面速度・角速度）のローパス平滑化応答速度（0で平滑化なし）")]
        [SerializeField, Range(0f, 30f)] private float _autoMotionSignalSmoothSpeed = 8.0f;

        [Header("6. 鏡像・補正ツール設定（Editor生成用）")]
        [Tooltip("Right Curve 鏡像生成時のサンプリングフレームレート（0以下で元クリップのframeRateを使用）")]
        [SerializeField, Range(0f, 120f)] private float _mirrorBakeFrameRate = 60f;

        [Tooltip("Right Curve の生FBXクリップ（再生成元として保持）")]
        [SerializeField] private AnimationClip _rawRightCurveSourceClip;

        // ============================================================
        // 読み取りプロパティ
        // ============================================================
        public GameObject ReferenceModelPrefab
        {
            get => _referenceModelPrefab;
            set => _referenceModelPrefab = value;
        }

        public string GeneratedAssetFolder
        {
            get => string.IsNullOrWhiteSpace(_generatedAssetFolder)
                ? "Assets/Deverop/AT_Murayama/PlayerAnimation/Generated"
                : _generatedAssetFolder.TrimEnd('/', '\\');
            set => _generatedAssetFolder = value;
        }

        public string ControllerAssetName
        {
            get => string.IsNullOrWhiteSpace(_controllerAssetName) ? "AC_RacerFlight" : _controllerAssetName.Trim();
            set => _controllerAssetName = value;
        }

        public RuntimeAnimatorController GeneratedController
        {
            get => _generatedController;
            set => _generatedController = value;
        }

        public AnimationClip SetUpClip { get => _setUpClip; set => _setUpClip = value; }
        public AnimationClip TakeOffClip { get => _takeOffClip; set => _takeOffClip = value; }
        public AnimationClip FlyClip { get => _flyClip; set => _flyClip = value; }
        public AnimationClip RightCurveClip { get => _rightCurveClip; set => _rightCurveClip = value; }
        public AnimationClip LeftCurveClip { get => _leftCurveClip; set => _leftCurveClip = value; }
        public AnimationClip StunClip { get => _stunClip; set => _stunClip = value; }
        public AnimationClip FireClip { get => _fireClip; set => _fireClip = value; }
        public AnimationClip MoveUpClip { get => _moveUpClip; set => _moveUpClip = value; }
        public AnimationClip MoveDownClip { get => _moveDownClip; set => _moveDownClip = value; }
        public AnimationClip BoostClip { get => _boostClip; set => _boostClip = value; }
        public AnimationClip ShieldClip { get => _shieldClip; set => _shieldClip = value; }
        public AnimationClip RawRightCurveSourceClip { get => _rawRightCurveSourceClip; set => _rawRightCurveSourceClip = value; }

        public FlightTurnAnimationMode TurnMode { get => _turnMode; set => _turnMode = value; }
        public bool LoopSetUpClip { get => _loopSetUpClip; set => _loopSetUpClip = value; }
        public bool LoopFlyClip { get => _loopFlyClip; set => _loopFlyClip = value; }
        public bool LoopCurveClips { get => _loopCurveClips; set => _loopCurveClips = value; }

        public float SetUpToTakeOffDuration => _setUpToTakeOffDuration;
        public float TakeOffExitTime => _takeOffExitTime;
        public float TakeOffToFlyDuration => _takeOffToFlyDuration;
        public float CurveTransitionDuration => _curveTransitionDuration;
        public float StunTransitionDuration => _stunTransitionDuration;
        public float FireTransitionDuration => _fireTransitionDuration;
        public float FireExitTime => _fireExitTime;
        public float OptionalStateTransitionDuration => _optionalStateTransitionDuration;

        public float TurnEnterThreshold => _turnEnterThreshold;
        public float TurnExitThreshold => Mathf.Min(_turnExitThreshold, _turnEnterThreshold);
        public float VerticalEnterThreshold => _verticalEnterThreshold;
        public float VerticalExitThreshold => Mathf.Min(_verticalExitThreshold, _verticalEnterThreshold);
        public float MoveParameterSmoothSpeed => _moveParameterSmoothSpeed;
        public float MirrorBakeFrameRate => _mirrorBakeFrameRate;

        public FlightMotionSourceMode MotionSourceMode { get => _motionSourceMode; set => _motionSourceMode = value; }
        public float InputMotionWeight => _inputMotionWeight;
        public float CenterReturnWeight => _centerReturnWeight;
        public float CenterReturnReferenceSpeed => Mathf.Max(0.5f, _centerReturnReferenceSpeed);
        public float CenterReturnMaxContribution => _centerReturnMaxContribution;
        public float CourseCurveWeight => _courseCurveWeight;
        public float CourseCurveReferenceYawRate => Mathf.Max(1f, _courseCurveReferenceYawRate);
        public float CourseCurveMaxContribution => _courseCurveMaxContribution;
        public bool ApplyAutoMotionToVertical => _applyAutoMotionToVertical;
        public float AutoMotionSignalSmoothSpeed => _autoMotionSignalSmoothSpeed;

        public bool HasStunClip => _stunClip != null;
        public bool HasFireClip => _fireClip != null;
        public bool HasMoveUpClip => _moveUpClip != null;
        public bool HasMoveDownClip => _moveDownClip != null;
        public bool HasVerticalClips => _moveUpClip != null || _moveDownClip != null;
        public bool HasBoostClip => _boostClip != null;
        public bool HasShieldClip => _shieldClip != null;

        private void OnValidate()
        {
            if (_turnExitThreshold > _turnEnterThreshold)
            {
                _turnExitThreshold = _turnEnterThreshold;
            }

            if (_verticalExitThreshold > _verticalEnterThreshold)
            {
                _verticalExitThreshold = _verticalEnterThreshold;
            }
        }
    }
}
