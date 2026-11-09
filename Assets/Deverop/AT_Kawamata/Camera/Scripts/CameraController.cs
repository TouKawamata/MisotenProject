using UnityEngine;

// プレイヤーカメラの制御。Playerの子オブジェクトとして置き、向き（LookAhead方向へのブレンド・遅延・コースの上方向への追従・
// 左右入力による傾き）と、1人称の位置（上下左右だけ少し遅れて付いていく）を扱う。上方向の基準はワールドの真上ではなくPlayerのReferenceUp（コースの上方向）。
// 揺れ（位置のずれと横の傾き）は演出側（CameraShakeEffect）がICameraShakeView経由で変えるだけで、カメラのTransformを書き換えるのはこのスクリプトだけにする。
// Splineへは直接問い合わせず、PlayerFlightControllerが公開する値だけを参照する
// （二重に最近傍探索を行うと計算コストが倍増し、Playerと異なる最近傍点を拾って向きがズレる恐れがあるため）。
public class CameraController : MonoBehaviour, ICameraShakeView
{
    [SerializeField] private PlayerFlightController player;

    [Tooltip("向きのうちLookAhead方向をどれだけ混ぜるか（0=Playerの向きそのまま、1=LookAhead方向そのまま）")]
    [SerializeField] private float lookAheadBlendWeight = 0.6f;

    [Tooltip("向きの追従の遅れ（秒）。大きいほど滑らか、小さいほど追従が早い。0なら遅れなし。" +
             "動き出しと止まりがなだらかになる追い方（SmoothDamp）で、だいたいこの秒数で目標の向きに追いつく")]
    [Min(0f)]
    [SerializeField] private float _rotationSmoothTime = 0.3f;

    [Header("1人称の位置")]
    [Tooltip("1人称のとき、機体の上下左右の動きにカメラの位置が遅れて付いていく秒数。0なら遅れなし（機体にぴったり付く）。" +
             "急に止まった・切り返したときの衝撃が和らぐ。前後の動きには遅れない。大きくしすぎると酔いやすいので小さめに")]
    [Min(0f)]
    [SerializeField] private float _firstPersonPlanarSmoothTime = 0.06f;

    [Tooltip("1人称の位置の遅れの上限（m）。これ以上は機体から離れない（コックピットの外に出ないように）")]
    [Min(0f)]
    [SerializeField] private float _firstPersonMaxLagDistance = 0.5f;

    [Header("傾き")]
    [Tooltip("コースの上方向（バンク）への追従の遅れ（秒）。0なら遅れなし（コースの傾きにぴったり合う）")]
    [SerializeField] private float upFollowSmoothTime = 0f;

    [Tooltip("左右入力いっぱいのときのカメラの傾き（度）。右入力で右に傾く。0なら傾かない")]
    [SerializeField] private float steeringTiltAngle = 5f;

    [Tooltip("左右入力による傾きの追従の遅れ（秒）。0なら遅れなし")]
    [SerializeField] private float steeringTiltSmoothTime = 0.25f;

    private Vector3 _smoothedForward;
    private Vector3 _forwardVelocity;
    private Vector3 _smoothedUp;

    // 1人称の位置（上下左右だけ遅れて付いていく）。まだ決めていなければ_hasFirstPersonPositionがfalse。
    private Vector3 _laggedFirstPersonPosition;
    private Vector3 _firstPersonVelocity;
    private bool _hasFirstPersonPosition;
    private float _steeringTilt;
    private float _steeringTiltVelocity;

    // 1人称の位置。プレハブで置いたローカル位置（Playerから見た目の位置）をそのまま使う。
    private Vector3 _firstPersonLocalOffset;

    private Vector3 _shakeOffset;
    private float _shakeTilt;
    private float _shakeTiltWeight;

    // カメラの向きを基準にした揺れ。演出側がICameraShakeView経由でだけ変える。
    Vector3 ICameraShakeView.ShakeOffset
    {
        get => _shakeOffset;
        set => _shakeOffset = value;
    }

    // 揺れによる横の傾き（度。＋で右）。演出側がICameraShakeView経由でだけ変える。
    float ICameraShakeView.ShakeTilt
    {
        get => _shakeTilt;
        set => _shakeTilt = value;
    }

    // 左右入力による傾きの代わりに、揺れの傾きをどれだけ使うか（0～1）。演出側がICameraShakeView経由でだけ変える。
    float ICameraShakeView.ShakeTiltWeight
    {
        get => _shakeTiltWeight;
        set => _shakeTiltWeight = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        _firstPersonLocalOffset = transform.localPosition;
        _smoothedForward = player != null ? player.transform.forward : transform.forward;
        _smoothedUp = player != null ? player.transform.up : transform.up;

        if (player != null)
        {
            player.StateReset += SnapToTarget;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.StateReset -= SnapToTarget;
        }
    }

    // 位置 → 向きの順（向きはカメラの位置からLookAhead位置への方向を使うため）。
    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        transform.position = UpdateFirstPersonPosition(player.transform.TransformPoint(_firstPersonLocalOffset), deltaTime);

        UpdateSmoothedForward(deltaTime);
        UpdateSmoothedUp(deltaTime);

        // 右入力（＋）で右に傾ける（AngleAxisは＋で左に傾くので符号を逆にする）。
        float targetTilt = -player.Steering * steeringTiltAngle;
        _steeringTilt = steeringTiltSmoothTime > 0f
            ? Mathf.SmoothDamp(_steeringTilt, targetTilt, ref _steeringTiltVelocity, steeringTiltSmoothTime, Mathf.Infinity, deltaTime)
            : targetTilt;

        // 揺れている間は、左右入力による傾きの代わりに揺れの傾きを使う（両方を足して傾き過ぎないように）。
        // 左右入力による傾きは裏で追従し続けるので、揺れが終わればそのまま戻る。揺れの傾きも＋で右なので、符号を逆にする。
        float tilt = Mathf.Lerp(_steeringTilt, -_shakeTilt, _shakeTiltWeight);

        transform.rotation = Quaternion.LookRotation(_smoothedForward, _smoothedUp) * Quaternion.AngleAxis(tilt, Vector3.forward);

        // 揺れは最後に足す（位置は毎フレームUpdatePositionで決め直すので、揺れが積み重ならない）。
        transform.position += transform.rotation * _shakeOffset;
    }

    // 目標の向きへ、動き出しと止まりがなだらかになるように追従させる（SmoothDamp。臨界減衰のばねと同じで、行き過ぎない）。
    // 以前の「差に比例して近づける」追い方（指数補間）は、目標が変わった瞬間に一番速く回り始めるため、カクっと感じやすかった。
    // 向き（単位ベクトル）をそのままSmoothDampして、毎回正規化する（逆向きへ一気に回るような大きな変化は、この用途では起きない）。
    private void UpdateSmoothedForward(float deltaTime)
    {
        Vector3 targetForward = ComputeTargetForward();
        if (_rotationSmoothTime <= 0f || deltaTime <= 0f)
        {
            _smoothedForward = targetForward;
            _forwardVelocity = Vector3.zero;
            return;
        }

        Vector3 forward = Vector3.SmoothDamp(_smoothedForward, targetForward, ref _forwardVelocity, _rotationSmoothTime, Mathf.Infinity, deltaTime);
        _smoothedForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : targetForward;
    }

    // コースの上方向（Player.ReferenceUp）へ遅れて追従させ、カメラの向きに直交させる。
    // 遅れで上方向が向きとほぼ平行になったとき（垂直の区間など）は、遅れなしの上方向に合わせる。
    private void UpdateSmoothedUp(float deltaTime)
    {
        Vector3 targetUp = player.ReferenceUp;
        Vector3 up = upFollowSmoothTime > 0f
            ? Vector3.Slerp(_smoothedUp, targetUp, 1f - Mathf.Exp(-deltaTime / upFollowSmoothTime))
            : targetUp;

        up = Vector3.ProjectOnPlane(up, _smoothedForward);
        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.ProjectOnPlane(targetUp, _smoothedForward);
        }

        if (up.sqrMagnitude > 0.0001f)
        {
            _smoothedUp = up.normalized;
        }
    }

    // 1人称の位置（目標はPlayerのローカル位置。Playerの姿勢に合わせて動く）を、機体の前後の動きにはそのまま、上下左右の動きにだけ遅れて付いていかせる。
    // 前後まで遅らせると、速いときにカメラが機体の後ろへ大きく取り残されるため、前後の成分はすぐに合わせる。
    // 遅れは_firstPersonMaxLagDistance（m）までに抑える。
    private Vector3 UpdateFirstPersonPosition(Vector3 targetPosition, float deltaTime)
    {
        if (_firstPersonPlanarSmoothTime <= 0f || !_hasFirstPersonPosition || deltaTime <= 0f)
        {
            _laggedFirstPersonPosition = targetPosition;
            _firstPersonVelocity = Vector3.zero;
            _hasFirstPersonPosition = true;
            return targetPosition;
        }

        // 前後の差は、すぐに詰める（SmoothDampの速度からも前後の成分を除く）。
        Vector3 playerForward = player.transform.forward;
        _laggedFirstPersonPosition += Vector3.Project(targetPosition - _laggedFirstPersonPosition, playerForward);
        _firstPersonVelocity = Vector3.ProjectOnPlane(_firstPersonVelocity, playerForward);

        // 残った上下左右の差だけを、遅れて詰める。
        _laggedFirstPersonPosition = Vector3.SmoothDamp(_laggedFirstPersonPosition, targetPosition, ref _firstPersonVelocity, _firstPersonPlanarSmoothTime, Mathf.Infinity, deltaTime);

        Vector3 lag = _laggedFirstPersonPosition - targetPosition;
        if (lag.sqrMagnitude > _firstPersonMaxLagDistance * _firstPersonMaxLagDistance)
        {
            _laggedFirstPersonPosition = targetPosition + lag.normalized * _firstPersonMaxLagDistance;
        }

        return _laggedFirstPersonPosition;
    }

    // Playerが置き直されたとき、遅延なしで目標の向き・位置に合わせる（スタート前にカメラが回り込むのを防ぐ）。
    private void SnapToTarget()
    {
        _hasFirstPersonPosition = false;
        _firstPersonVelocity = Vector3.zero;
        _forwardVelocity = Vector3.zero;
        _smoothedForward = ComputeTargetForward();
        _smoothedUp = player.ReferenceUp;
        UpdateSmoothedUp(0f);
        _steeringTilt = 0f;
        _steeringTiltVelocity = 0f;
    }

    // Playerの向きとLookAhead方向をブレンドした、カメラが向くべき方向。
    private Vector3 ComputeTargetForward()
    {
        Vector3 toLookAhead = player.LookAheadWorldPosition - transform.position;
        Vector3 lookDirection = toLookAhead.sqrMagnitude > 0.0001f ? toLookAhead.normalized : player.transform.forward;

        Vector3 blendedForward = Vector3.Slerp(player.transform.forward, lookDirection, lookAheadBlendWeight);
        if (blendedForward.sqrMagnitude < 0.0001f)
        {
            blendedForward = player.transform.forward;
        }

        return blendedForward.normalized;
    }
}
