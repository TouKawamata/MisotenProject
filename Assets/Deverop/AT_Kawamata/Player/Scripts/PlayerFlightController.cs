using System;
using UnityEngine;
using UnityEngine.Serialization;

// プレイヤーの飛行制御（純粋な飛行計算のみ）。自由飛行（Velocityベース）で移動しつつ、姿勢は機首の向きとコースの上方向
// （バンク込み）から作る。ロールの計算はしない（機体の見た目のバンクはアニメーション側で、公開しているSteeringから付ける）。
// CourseSplineへは基本的に ICourseGuide 経由で問い合わせる。
// Splineを中心軸とした円筒（その場所のトンネル半径）の外には出られず、境界（壁）にぶつかると
// 外側へ向かう成分を消したうえで、当たった角度に応じて減速する（かすめただけなら、ほぼ減速せずに滑る）。
// 入力・Boostのチューニング値・FlightTuningProfileはPlayerManagerからTickの引数で受け取る。
// 速度は「慣性の速度（_velocity：前進・上下・カーブで外へ膨らむ分）」と「横の速さ（_steerSpeed：コースの左右方向）」に分けて持つ。
// 横の速さは左右の入力とコース中央への補正（左右）から目標を決め、そこへ加速度で近づける（速度方向の遅延追従の対象にしないので、
// 押した・離したときの反応を、加速度の設定だけで短くできる）。
// 最高速度（maxSpeed）は前進（機首の向き）の成分だけにかけ、横の速さの一定割合（LateralSpeedLimitRate）だけ上限を下げる。
public class PlayerFlightController : MonoBehaviour
{
    [FormerlySerializedAs("courseSpline")]
    [SerializeField] private CourseSpline _courseSpline;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _steerSpeed;
    private Vector3 _steerRight = Vector3.right;
    private readonly LateralPush _lateralPush = new LateralPush();

    // 機首の向きへの押し出し（シールドで防いだときの加速など）。横ずれと同じく「何mを何秒で」位置へ足すため、LateralPushを使い回す。
    private readonly LateralPush _forwardPush = new LateralPush();

    // コースへの問い合わせ口（中心・左右・上下・トンネル半径）。Initialize前はnull。CPUの入力（判断）などが読む。
    public ICourseGuide Guide => _guide;

    // 慣性の速度と横の速さを合わせた、ワールド座標の速度。
    public Vector3 Velocity => _velocity + _steerRight * _steerSpeed;

    // 機首の向きの速さ（最高速度で制限している成分）。押し出し（ApplyForwardPush）の分は含まない。
    public float ForwardSpeed => Vector3.Dot(Velocity, _playerForward);

    // ブースト接触などで横・前後へ押し出している途中か。
    public bool IsBeingPushed => _lateralPush.IsActive || _forwardPush.IsActive;

    // 機体の位置でのコースの上方向（バンク込み）。機体の回転の上方向に使う。カメラの向きの基準にもする。
    public Vector3 ReferenceUp { get; private set; } = Vector3.up;

    // 直近のTickの左右入力（−1＝左〜1＝右）。機体のバンクのアニメーションとカメラの傾き用。
    public float Steering { get; private set; }

    public Vector3 LookAheadWorldPosition { get; private set; }

    // 直近のTickで、トンネル境界の外に出ようとして位置を戻したか。
    public bool IsClampedToTunnel { get; private set; }

    // ResetState（スタートグリッドへの配置・リトライ）で状態をリセットしたとき。カメラの向きの即時合わせに使う。
    public event Action StateReset;

    // トンネルの境界（壁）に触れ始めた瞬間。引数は当たった強さ（0＝かすめた～1＝正面から）。
    // カメラの揺れ・SE・エフェクトなどの演出は、後からここにつなぐ。
    public event Action<float> WallHit;

    public void Initialize(FlightTuningProfile initialProfile)
    {
        _guide = _courseSpline;
        ResetState(initialProfile);
    }

    // 現在のtransformを基準に、向き・速度などの内部状態をリセットする（スタートグリッドへの配置・リトライ用）。
    // 向きはtransform.forward／upから取るため、transformを設定した後に呼ぶこと。
    public void ResetState(FlightTuningProfile profile)
    {
        _playerForward = transform.forward;
        _velocity = Vector3.zero;
        _steerSpeed = 0f;
        _steerRight = transform.right;
        ReferenceUp = transform.up;
        Steering = 0f;
        _lateralPush.Stop();
        _forwardPush.Stop();
        IsClampedToTunnel = false;
        LookAheadWorldPosition = transform.position + transform.forward * profile.LookAheadDistance;
        StateReset?.Invoke();
    }

    // steeringPower：横の速さを上げるときの加速度（m/s²）。maxLateralSpeed：左右の入力いっぱいのときの横の速さ（m/s）。
    public void Tick(float horizontal, float maxSpeed, float forwardAcceleration, float steeringPower, float maxLateralSpeed, FlightTuningProfile profile, float deltaTime)
    {
        if (_guide == null || profile == null)
        {
            return;
        }

        // 1. 現在位置でのSpline基準値
        Vector3 position = transform.position;
        Vector3 splineForward = _guide.GetForward(position);
        Vector3 splineRight = _guide.GetRight(position);
        Vector3 splineUp = _guide.GetUp(position);
        Vector3 centerPosition = _guide.GetCenterPosition(position);
        float tunnelRadius = _guide.GetTunnelRadius(position);
        Steering = Mathf.Clamp(horizontal, -1f, 1f);

        // 2. 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-profile.HeadingTurnRate * deltaTime));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 3. 加速度計算（前進と、コース中央への上下の補正だけ。左右の入力と左右の補正は、横の速さ（_steerSpeed）で扱う）
        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 centerOffset = centerPosition - position;
        float lateralCenterOffset = Vector3.Dot(centerOffset, splineRight);
        float verticalCenterOffset = Vector3.Dot(centerOffset, splineUp);
        Vector3 verticalCorrectionAccel = splineUp * Mathf.Clamp(verticalCenterOffset * profile.VerticalCorrectionGain, -profile.MaxVerticalCorrectionAccel, profile.MaxVerticalCorrectionAccel);

        // 4. 速度積分
        _velocity += (forwardAccel + verticalCorrectionAccel) * deltaTime;

        // 5. 速度方向の遅延追従（第2段階。カーブで外側へ膨らんでから戻る「滑る」感）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, 1f - Mathf.Exp(-profile.VelocityTurnRate * deltaTime));
            _velocity = velocityDirection.normalized * speed;
        }

        // 6. 横の速さ（速度方向の遅延追従の対象にしないので、押した・離したときの反応は加速度の設定だけで決まる）
        _steerRight = splineRight;
        UpdateSteerSpeed(lateralCenterOffset, steeringPower, maxLateralSpeed, profile, deltaTime);

        // 最高速度は前進の成分だけにかける（向きを回した後にかけ、横の速度が前向きに回り込んでも最高速度を超えないようにする）。
        _velocity = ClampForwardSpeed(_velocity, splineRight, maxSpeed, profile);

        // 7. 位置積分（円筒境界＝壁の外には出られず、当たった角度に応じて減速する）。
        //    横ずれ・前への押し出し（LateralPush）は速度とは別に位置へ足し、慣性や最高速度の制限で打ち消されないようにする。
        Vector3 desiredPosition = position + Velocity * deltaTime
            + splineRight * _lateralPush.Advance(deltaTime)
            + _playerForward * _forwardPush.Advance(deltaTime);
        transform.position = ClampVelocityAndPositionToTunnelRadius(desiredPosition, centerPosition, splineRight, splineUp, tunnelRadius, profile);

        // 8. 回転：ForwardはPlayerForward、Upはコースの上方向（ワールドの真上は使わないので、垂直の区間・逆さの区間でも崩れない）
        ReferenceUp = _guide.GetUp(transform.position);
        transform.rotation = Quaternion.LookRotation(_playerForward, ReferenceUp);

        // 9. カメラへ公開する先読み位置
        LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, profile.LookAheadDistance);
    }

    // その場で止める（ゴール後の停止用）。速度・横ずれ・左右入力を消すだけなので、この後Tickを呼ばなければ位置も動かない。
    public void Stop()
    {
        _velocity = Vector3.zero;
        _steerSpeed = 0f;
        _lateralPush.Stop();
        _forwardPush.Stop();
        Steering = 0f;
    }

    // 左右の入力（Steering）とコース中央への補正（左右）から目標の横の速さを決め、そこへ加速度で近づける。
    // 補正は入力が小さいほど強くかける（押している間は入力を優先し、離すと中央へ戻る）。
    // 同じ向きに速くするときはsteeringPower、遅くする・離した・逆へ切り返したときはLateralStopDecelerationで近づける。
    // 逆へ切り返したときは、まず0まで止めてから（次のTickから）逆向きに速くする。
    private void UpdateSteerSpeed(float lateralCenterOffset, float steeringPower, float maxLateralSpeed, FlightTuningProfile profile, float deltaTime)
    {
        float correction = Mathf.Clamp(lateralCenterOffset * profile.CenterCorrectionGain, -profile.MaxCorrectionSpeed, profile.MaxCorrectionSpeed);
        float target = Steering * maxLateralSpeed + (1f - Mathf.Abs(Steering)) * correction;

        if (_steerSpeed * target < 0f)
        {
            _steerSpeed = Mathf.MoveTowards(_steerSpeed, 0f, profile.LateralStopDeceleration * deltaTime);
        }
        else if (Mathf.Abs(target) > Mathf.Abs(_steerSpeed))
        {
            _steerSpeed = Mathf.MoveTowards(_steerSpeed, target, steeringPower * deltaTime);
        }
        else
        {
            _steerSpeed = Mathf.MoveTowards(_steerSpeed, target, profile.LateralStopDeceleration * deltaTime);
        }
    }

    // 機首の向きの成分が上限を超えていれば、その成分だけを切り詰める（横・上下の成分は変えない）。
    // 上限は、最高速度から横の速さ（慣性の速度の左右成分＋_steerSpeed）のLateralSpeedLimitRate倍を引いたもの。
    // 横の速さは前進の成分を除いてから測る（カーブで機首の向きがコースからずれていても、前進の速さを横として数えない）。
    private Vector3 ClampForwardSpeed(Vector3 velocity, Vector3 right, float maxSpeed, FlightTuningProfile profile)
    {
        float forwardSpeed = Vector3.Dot(velocity, _playerForward);
        float lateralSpeed = Mathf.Abs(Vector3.Dot(velocity - _playerForward * forwardSpeed, right) + _steerSpeed);
        float forwardLimit = Mathf.Max(0f, maxSpeed - profile.LateralSpeedLimitRate * lateralSpeed);
        if (forwardSpeed <= forwardLimit)
        {
            return velocity;
        }

        return velocity - _playerForward * (forwardSpeed - forwardLimit);
    }

    // ブースト接触などで、コースの左右方向へdistance（m）をduration秒かけて押し出す（最初に大きく動き、だんだん止まる）。
    // directionはコースの左右どちら向きかだけを使う。Vector3.zeroなど向きが決まらないときは、コースの中央側へ押し出す
    // （壁際で外へ押し出されるのを防ぐ）。押し出している途中で呼ばれたら、そこから押し出し直す。
    public void ApplyLateralPush(Vector3 direction, float distance, float duration)
    {
        if (_guide == null || distance <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 right = _guide.GetRight(position);
        float dot = Vector3.Dot(direction, right);
        float side;
        if (Mathf.Abs(dot) > 0.0001f)
        {
            side = Mathf.Sign(dot);
        }
        else
        {
            float offsetToCenter = Vector3.Dot(_guide.GetCenterPosition(position) - position, right);
            side = offsetToCenter >= 0f ? 1f : -1f;
        }

        _lateralPush.Start(side * distance, duration);
    }

    // 機首の向きへdistance（m）をduration秒かけて押し出す（最初に大きく動き、だんだん止まる）。負なら後ろへ下げる。
    // 速度ではなく位置に足すので、最高速度で切り詰められない（シールドで防いだときの加速、障害物で後ろへ下げるときなど）。
    // 押し出している途中で呼ばれたら、そこから押し出し直す。
    public void ApplyForwardPush(float distance, float duration)
    {
        if (Mathf.Approximately(distance, 0f))
        {
            return;
        }

        _forwardPush.Start(distance, duration);
    }

    // 他のレーサーとの通常の接触で、食い込みを押し戻す（RacerContactResolverから、PlayerManager経由で呼ばれる）。
    // blockNormalがゼロでなければ、その逆向き（相手へ向かう向き）の速度を消す（横の速さが相手へ向かっていれば0にする）。
    // トンネルの境界の制限は次のTickでかかる。
    public void ApplyContactCorrection(Vector3 offset, Vector3 blockNormal)
    {
        transform.position += offset;

        float velocityTowardOther = Vector3.Dot(_velocity, blockNormal);
        if (velocityTowardOther < 0f)
        {
            _velocity -= blockNormal * velocityTowardOther;
        }

        if (_steerSpeed * Vector3.Dot(_steerRight, blockNormal) < 0f)
        {
            _steerSpeed = 0f;
        }
    }

    // Splineを中心軸とした円筒（半径radius＝その場所のトンネル半径）の外に出られないようにする。境界（壁）の外側へ向かう速度成分を消し、
    // さらに当たった角度に応じて残りの速度を減らす（正面から当たるほど大きく、かすめただけなら少し）。
    // 減らす割合 ＝ WallHitSpeedLossRate × 壁へ向かっていた速さの割合（＝当たった角度のsin）。
    // 当たった角度は横の速さも含めた速度で測る。横の速さが壁へ向かっていれば0にし（切り返しがすぐ効くように）、慣性の速度を減らす。
    private Vector3 ClampVelocityAndPositionToTunnelRadius(Vector3 desiredPosition, Vector3 center, Vector3 right, Vector3 up, float radius, FlightTuningProfile profile)
    {
        bool wasClampedToTunnel = IsClampedToTunnel;
        IsClampedToTunnel = false;
        if (radius <= 0f)
        {
            return desiredPosition;
        }

        Vector3 offset = desiredPosition - center;
        float lateral = Vector3.Dot(offset, right);
        float vertical = Vector3.Dot(offset, up);
        float distance = Mathf.Sqrt(lateral * lateral + vertical * vertical);

        if (distance <= radius || distance < 0.0001f)
        {
            return desiredPosition;
        }

        float scale = radius / distance;
        Vector3 correctedPosition = desiredPosition + right * (lateral * scale - lateral) + up * (vertical * scale - vertical);

        Vector3 radialDir = (right * lateral + up * vertical) / distance;
        Vector3 velocityBeforeHit = Velocity;
        float velocityAlongRadial = Vector3.Dot(velocityBeforeHit, radialDir);
        float impact = 0f;
        if (velocityAlongRadial > 0f)
        {
            float speedBeforeHit = velocityBeforeHit.magnitude;
            impact = speedBeforeHit > 0.0001f ? Mathf.Clamp01(velocityAlongRadial / speedBeforeHit) : 0f;

            if (_steerSpeed * Vector3.Dot(right, radialDir) > 0f)
            {
                _steerSpeed = 0f;
            }

            float inertiaAlongRadial = Vector3.Dot(_velocity, radialDir);
            if (inertiaAlongRadial > 0f)
            {
                _velocity -= radialDir * inertiaAlongRadial;
            }

            _velocity *= 1f - profile.WallHitSpeedLossRate * impact;
        }

        IsClampedToTunnel = true;

        // 壁に触れ始めた瞬間だけ通知する（擦り続けている間は出さない）。
        if (!wasClampedToTunnel)
        {
            WallHit?.Invoke(impact);
        }

        return correctedPosition;
    }
}
