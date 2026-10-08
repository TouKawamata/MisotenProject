using System;
using UnityEngine;
using UnityEngine.Serialization;

// プレイヤーの飛行制御（純粋な飛行計算のみ）。自由飛行（Velocityベース）で移動しつつ、姿勢（ロール）はSplineの傾き
// （バンク）に自然に追従する。CourseSplineへは基本的に ICourseGuide 経由で問い合わせる。
// Splineを中心軸とした円筒（CourseSpline.TunnelRadius）の外には出られず、境界（壁）にぶつかると
// 外側へ向かう成分を消したうえで、当たった角度に応じて減速する（かすめただけなら、ほぼ減速せずに滑る）。
// 入力・Boostのチューニング値・FlightTuningProfileはPlayerManagerからTickの引数で受け取る。
// 最高速度（maxSpeed）は前進（機首の向き）の成分だけにかけ、曲がって横に動いている間も前進の速さが落ちないようにする。
// 横・上下の速さは、操作の加速度と速度方向の遅延追従（VelocityTurnRate）の釣り合いで自然に上限が決まる。
public class PlayerFlightController : MonoBehaviour
{
    [FormerlySerializedAs("courseSpline")]
    [SerializeField] private CourseSpline _courseSpline;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;
    private float _currentRoll;
    private float _rollVelocity;
    private readonly LateralPush _lateralPush = new LateralPush();

    public Vector3 Velocity => _velocity;

    // 機首の向きの速さ（最高速度で制限している成分）。
    public float ForwardSpeed => Vector3.Dot(_velocity, _playerForward);

    // ブースト接触などで横へ押し出している途中か。
    public bool IsBeingPushed => _lateralPush.IsActive;

    public float CurrentRoll => _currentRoll;

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

    // 現在のtransformを基準に、向き・速度・ロールなどの内部状態をリセットする（スタートグリッドへの配置・リトライ用）。
    // 向きはtransform.forwardから取るため、transformを設定した後に呼ぶこと。
    public void ResetState(FlightTuningProfile profile)
    {
        _playerForward = transform.forward;
        _velocity = Vector3.zero;
        _currentRoll = 0f;
        _rollVelocity = 0f;
        _lateralPush.Stop();
        IsClampedToTunnel = false;
        LookAheadWorldPosition = transform.position + transform.forward * profile.LookAheadDistance;
        StateReset?.Invoke();
    }

    public void Tick(float horizontal, float maxSpeed, float forwardAcceleration, float steeringPower, FlightTuningProfile profile, float deltaTime)
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

        // 2. 見出し方向の遅延追従（第1段階）。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, 1f - Mathf.Exp(-profile.HeadingTurnRate * deltaTime));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 3. 加速度計算（コース中央への補正は左右・上下で別々の強さを持てるように分けて計算する）
        Vector3 forwardAccel = _playerForward * forwardAcceleration;
        Vector3 lateralAccel = splineRight * (horizontal * steeringPower);
        Vector3 centerOffset = centerPosition - position;
        float lateralCenterOffset = Vector3.Dot(centerOffset, splineRight);
        float verticalCenterOffset = Vector3.Dot(centerOffset, splineUp);
        Vector3 lateralCorrectionAccel = splineRight * Mathf.Clamp(lateralCenterOffset * profile.CenterCorrectionGain, -profile.MaxCorrectionAccel, profile.MaxCorrectionAccel);
        Vector3 verticalCorrectionAccel = splineUp * Mathf.Clamp(verticalCenterOffset * profile.VerticalCorrectionGain, -profile.MaxVerticalCorrectionAccel, profile.MaxVerticalCorrectionAccel);
        Vector3 correctionAccel = lateralCorrectionAccel + verticalCorrectionAccel;

        // 4. 速度積分
        _velocity += (forwardAccel + lateralAccel + correctionAccel) * deltaTime;

        // 5. 速度方向の遅延追従（第2段階。カーブで外側へ膨らんでから戻る「滑る」感）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, 1f - Mathf.Exp(-profile.VelocityTurnRate * deltaTime));
            _velocity = velocityDirection.normalized * speed;
        }

        // 最高速度は前進の成分だけにかける（向きを回した後にかけ、横の速度が前向きに回り込んでも最高速度を超えないようにする）。
        _velocity = ClampForwardSpeed(_velocity, maxSpeed);

        // 6. 位置積分（円筒境界＝壁の外には出られず、当たった角度に応じて減速する）。
        //    横ずれ（LateralPush）は速度とは別に位置へ足し、慣性や最高速度の制限で打ち消されないようにする。
        Vector3 desiredPosition = position + _velocity * deltaTime + splineRight * _lateralPush.Advance(deltaTime);
        transform.position = ClampVelocityAndPositionToTunnelRadius(desiredPosition, centerPosition, splineRight, splineUp, profile, ref _velocity);

        // 7. 回転：ForwardはPlayerForwardから、Rollは「Splineのバンクへの追従」＋「操作によるロール」の合算
        Vector3 bankUp = _guide.GetUp(transform.position);
        float bankFromCourse = Vector3.SignedAngle(Vector3.up, bankUp, _playerForward);
        float steeringRoll = -horizontal * profile.RollMaxAngle;
        float targetRoll = Mathf.Clamp(bankFromCourse + steeringRoll, -profile.RollMaxAngle, profile.RollMaxAngle);
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, profile.RollSmoothTime, Mathf.Infinity, deltaTime);
        transform.rotation = Quaternion.LookRotation(_playerForward, Vector3.up) * Quaternion.AngleAxis(_currentRoll, Vector3.forward);

        // 8. カメラへ公開する先読み位置
        LookAheadWorldPosition = _guide.GetLookAheadPosition(transform.position, profile.LookAheadDistance);
    }

    // 機首の向きの成分が最高速度を超えていれば、その成分だけを切り詰める（横・上下の成分は変えない）。
    private Vector3 ClampForwardSpeed(Vector3 velocity, float maxSpeed)
    {
        float forwardSpeed = Vector3.Dot(velocity, _playerForward);
        if (forwardSpeed <= maxSpeed)
        {
            return velocity;
        }

        return velocity - _playerForward * (forwardSpeed - maxSpeed);
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

    // 他のレーサーとの通常の接触で、食い込みを押し戻す（RacerContactResolverから、PlayerManager経由で呼ばれる）。
    // blockNormalがゼロでなければ、その逆向き（相手へ向かう向き）の速度を消す。トンネルの境界の制限は次のTickでかかる。
    public void ApplyContactCorrection(Vector3 offset, Vector3 blockNormal)
    {
        transform.position += offset;

        float velocityTowardOther = Vector3.Dot(_velocity, blockNormal);
        if (velocityTowardOther < 0f)
        {
            _velocity -= blockNormal * velocityTowardOther;
        }
    }

    // Splineを中心軸とした円筒（TunnelRadius）の外に出られないようにする。境界（壁）の外側へ向かう速度成分を消し、
    // さらに当たった角度に応じて残りの速度を減らす（正面から当たるほど大きく、かすめただけなら少し）。
    // 減らす割合 ＝ WallHitSpeedLossRate × 壁へ向かっていた速さの割合（＝当たった角度のsin）。
    private Vector3 ClampVelocityAndPositionToTunnelRadius(Vector3 desiredPosition, Vector3 center, Vector3 right, Vector3 up, FlightTuningProfile profile, ref Vector3 velocity)
    {
        bool wasClampedToTunnel = IsClampedToTunnel;
        IsClampedToTunnel = false;
        if (_courseSpline == null || _courseSpline.TunnelRadius <= 0f)
        {
            return desiredPosition;
        }

        float radius = _courseSpline.TunnelRadius;
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
        float velocityAlongRadial = Vector3.Dot(velocity, radialDir);
        float impact = 0f;
        if (velocityAlongRadial > 0f)
        {
            float speedBeforeHit = velocity.magnitude;
            impact = speedBeforeHit > 0.0001f ? Mathf.Clamp01(velocityAlongRadial / speedBeforeHit) : 0f;
            velocity -= radialDir * velocityAlongRadial;
            velocity *= 1f - profile.WallHitSpeedLossRate * impact;
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
