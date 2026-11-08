using System;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.Serialization;

// プレイヤーの飛行制御（純粋な飛行計算のみ。PlayerとCPUで共通）。自動で前進しつつ、上下左右の入力でコースの断面の中を平行移動する。
// 左右・上下とも旋回ではなく平行移動。入力がニュートラルに近いときだけ、コースの中央へだんだん戻る（入力中は入力を優先する）。
// 姿勢は機首の向きとコースの上方向（バンク込み）から作る。ロールの計算はしない（機体の見た目のバンクはアニメーション側で、公開しているSteeringから付ける）。
// CourseSplineへは基本的に ICourseGuide 経由で問い合わせる。Splineは前進方向（接線）と、移動できる範囲（トンネル）の基準にだけ使う。
// Splineを中心軸とした円筒（その場所のトンネル半径）の外には出られず、境界（壁）にぶつかると
// 外側へ向かう成分を消したうえで、当たった角度に応じて減速する（かすめただけなら、ほぼ減速せずに滑る）。
// 入力・Boostのチューニング値・FlightTuningProfileはRacerManagerBaseからTickの引数で受け取る。
// 速度は「慣性の速度（_velocity：前進と、カーブ・坂で外へ膨らむ分）」と「上下左右の速さ（_planarSpeed：コースの右・上方向）」に分けて持つ。
// 上下左右の速さは移動の入力と中央への戻りから目標を決め、そこへ加速度で近づける（速度方向の遅延追従の対象にしないので、
// 押した・離したときの反応を、加速度の設定だけで短くできる）。
// 最高速度（maxSpeed）は前進（機首の向き）の成分だけにかけ、上下左右の速さの一定割合（LateralSpeedLimitRate）だけ上限を下げる。
public class PlayerFlightController : MonoBehaviour
{
    [FormerlySerializedAs("courseSpline")]
    [SerializeField] private CourseSpline _courseSpline;

    private ICourseGuide _guide;
    private Vector3 _playerForward;
    private Vector3 _velocity;

    // 上下左右の速さ（m/s）。x＝コースの右方向、y＝コースの上方向。
    private Vector2 _planarSpeed;

    // 前のTickの、コースの中心線上の最寄り点。コースに沿って進んだ距離（向きの追従に使う）を測るために持つ。
    private Vector3 _previousCenterPosition;
    private bool _hasPreviousCenterPosition;

    // 入力中の追従の距離をどれだけ使うか（0＝通常の距離～1＝入力中の距離）。入力中は1、入力をやめるとイージングで0へ戻す。
    private float _inputFollowWeight;

    // 入力をやめてからの秒数（イージングで戻すのに使う）。
    private float _inputFollowReleaseTime;
    private Vector3 _steerRight = Vector3.right;
    private Vector3 _steerUp = Vector3.up;
    private readonly LateralPush _lateralPush = new LateralPush();

    // 機首の向きへの押し出し（シールドで防いだときの加速など）。横ずれと同じく「何mを何秒で」位置へ足すため、LateralPushを使い回す。
    private readonly LateralPush _forwardPush = new LateralPush();

    // コースへの問い合わせ口（中心・左右・上下・トンネル半径）。Initialize前はnull。CPUの入力（判断）などが読む。
    public ICourseGuide Guide => _guide;

    // 慣性の速度と上下左右の速さを合わせた、ワールド座標の速度。
    public Vector3 Velocity => _velocity + _steerRight * _planarSpeed.x + _steerUp * _planarSpeed.y;

    // 機首の向きの速さ（最高速度で制限している成分）。押し出し（ApplyForwardPush）の分は含まない。
    public float ForwardSpeed => Vector3.Dot(Velocity, _playerForward);

    // ブースト接触などで横・前後へ押し出している途中か。
    public bool IsBeingPushed => _lateralPush.IsActive || _forwardPush.IsActive;

    // 機体の位置でのコースの上方向（バンク込み）。機体の回転の上方向に使う。カメラの向きの基準にもする。
    public Vector3 ReferenceUp { get; private set; } = Vector3.up;

    // 直近のTickの移動の入力（方向の方式で変換した後。x＝右、y＝上、大きさ0～1）。
    public Vector2 MoveInput { get; private set; }

    // 直近のTickの左右入力（−1＝左〜1＝右。MoveInputのx）。機体のバンクのアニメーションとカメラの傾き用。
    public float Steering { get; private set; }

    // カメラの狙い先。今の移動の入力をFlightTuningProfileのCameraPredictionTime秒続けたときの、予測位置。
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
        _planarSpeed = Vector2.zero;
        _hasPreviousCenterPosition = false;
        _inputFollowWeight = 0f;
        _inputFollowReleaseTime = 0f;
        _steerRight = transform.right;
        _steerUp = transform.up;
        ReferenceUp = transform.up;
        MoveInput = Vector2.zero;
        Steering = 0f;
        _lateralPush.Stop();
        _forwardPush.Stop();
        IsClampedToTunnel = false;
        LookAheadWorldPosition = transform.position + transform.forward * profile.LookAheadDistance;
        StateReset?.Invoke();
    }

    // move：移動の入力（x＝右、y＝上。大きさ0～1に制限済みの値。IRaceInput.Move）。
    // steeringPower：上下左右の速さを上げるときの加速度（m/s²）。maxLateralSpeed：入力いっぱいのときの上下左右の速さ（m/s）。
    public void Tick(Vector2 move, float maxSpeed, float forwardAcceleration, float steeringPower, float maxLateralSpeed, FlightTuningProfile profile, float deltaTime)
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

        // 移動の方向の方式（8方向固定／任意角度）だけをここで切り替える。速さ・壁の計算は方式によらず共通。
        // そのあと、最高速度が出る入力の大きさ（FullSpeedInputMagnitude）に合わせて、入力の強さを広げる。
        MoveInput = ApplyFullSpeedInputMagnitude(MoveDirectionResolver.Resolve(move, profile.MoveDirectionMode), profile.FullSpeedInputMagnitude);
        Steering = MoveInput.x;

        // 前のTickからコースの中心線に沿って進んだ距離（m）。向きの追従は時間ではなく、この距離で進める。
        // 時間で追従させると、回転半径の小さいインコースほどコースの向きが速く変わるのに追いつけず、
        // 機首と速度が曲がる前の向き（新しい区間から見て真横）へ残って、前へ進んでいないように見えていた。
        // 中心線に沿った距離なら、コースの向きの変わり方（1mあたり何度）はどこを走っても同じなので、遅れ方がそろう。
        float courseProgress = _hasPreviousCenterPosition ? Vector3.Distance(centerPosition, _previousCenterPosition) : 0f;
        _previousCenterPosition = centerPosition;
        _hasPreviousCenterPosition = true;

        // 入力中かどうかで、向きの追従の距離を切り替える（「入力中の追従」がONのときだけ）。
        UpdateInputFollowWeight(profile, deltaTime);
        float headingFollowDistance = Mathf.Lerp(profile.HeadingFollowDistance, profile.InputHeadingFollowDistance, _inputFollowWeight);
        float velocityFollowDistance = Mathf.Lerp(profile.VelocityFollowDistance, profile.InputVelocityFollowDistance, _inputFollowWeight);

        // 2. 見出し方向の遅延追従（第1段階）。前進方向はSplineの接線を基準にする。垂直成分も含むためPitchも兼ねる。
        _playerForward = Vector3.Slerp(_playerForward, splineForward, GetFollowFactor(headingFollowDistance, courseProgress));
        if (_playerForward.sqrMagnitude < 0.0001f)
        {
            _playerForward = splineForward;
        }

        // 3〜4. 前進の加速度を速度へ積分する（上下左右の移動は、上下左右の速さ（_planarSpeed）で扱う）
        _velocity += _playerForward * (forwardAcceleration * deltaTime);

        // 5. 速度方向の遅延追従（第2段階。カーブ・坂で外側へ膨らむ「滑る」感。膨らんだ分は、ニュートラルのときに中央への戻りで戻る）
        float speed = _velocity.magnitude;
        if (speed > 0.0001f)
        {
            Vector3 velocityDirection = Vector3.Slerp(_velocity / speed, _playerForward, GetFollowFactor(velocityFollowDistance, courseProgress));
            _velocity = velocityDirection.normalized * speed;
        }

        // 6. 上下左右の速さ（速度方向の遅延追従の対象にしないので、押した・離したときの反応は加速度の設定だけで決まる）
        _steerRight = splineRight;
        _steerUp = splineUp;
        Vector3 offsetToCenter = centerPosition - position;
        Vector2 planarOffsetToCenter = new Vector2(Vector3.Dot(offsetToCenter, splineRight), Vector3.Dot(offsetToCenter, splineUp));
        UpdatePlanarSpeed(planarOffsetToCenter, steeringPower, maxLateralSpeed, profile, deltaTime);

        // 最高速度は前進の成分だけにかける（向きを回した後にかけ、上下左右の速度が前向きに回り込んでも最高速度を超えないようにする）。
        _velocity = ClampForwardSpeed(_velocity, splineRight, splineUp, maxSpeed, profile);

        // 7. 位置積分（円筒境界＝壁の外には出られず、当たった角度に応じて減速する）。
        //    横ずれ・前への押し出し（LateralPush）は速度とは別に位置へ足し、慣性や最高速度の制限で打ち消されないようにする。
        Vector3 desiredPosition = position + Velocity * deltaTime
            + splineRight * _lateralPush.Advance(deltaTime)
            + _playerForward * _forwardPush.Advance(deltaTime);
        transform.position = ClampVelocityAndPositionToTunnelRadius(desiredPosition, centerPosition, splineRight, splineUp, tunnelRadius, profile);

        // 8. 回転：ForwardはPlayerForward、Upはコースの上方向（ワールドの真上は使わないので、垂直の区間・逆さの区間でも崩れない）
        ReferenceUp = _guide.GetUp(transform.position);
        transform.rotation = Quaternion.LookRotation(_playerForward, ReferenceUp);

        // 9. カメラへ公開する予測位置（今の移動の入力を一定時間続けたときの位置）
        LookAheadWorldPosition = PredictPosition(transform.position, centerPosition, splineRight, splineUp, profile);
    }

    // その場で止める（ゴール後の停止用）。速度・横ずれ・移動の入力を消すだけなので、この後Tickを呼ばなければ位置も動かない。
    public void Stop()
    {
        _velocity = Vector3.zero;
        _planarSpeed = Vector2.zero;
        _lateralPush.Stop();
        _forwardPush.Stop();
        MoveInput = Vector2.zero;
        Steering = 0f;
    }

    // 入力の大きさを、fullSpeedMagnitudeで1（最高速度）になるように広げる。向きは変えず、1を超えた分は1にする。
    // fullSpeedMagnitudeが1なら何もしない。ここで変えた後の値を、上下左右の速さ・中央への戻り・Steeringのすべてに使う。
    private static Vector2 ApplyFullSpeedInputMagnitude(Vector2 move, float fullSpeedMagnitude)
    {
        float magnitude = move.magnitude;
        if (magnitude < 0.0001f || fullSpeedMagnitude >= 1f)
        {
            return move;
        }

        float scaledMagnitude = Mathf.Min(magnitude / fullSpeedMagnitude, 1f);
        return move / magnitude * scaledMagnitude;
    }

    // コースの中心線に沿ってprogress（m）進んだときに、目標の向きへ近づける割合（0～1）。
    // 追従の距離が0（1mあたりの割合が無限大）なら、すぐに合わせる（無限大×0でNaNにならないよう、先に分ける）。
    private static float GetFollowFactor(float followDistance, float progress)
    {
        float ratePerMeter = FlightTuningProfile.ToFollowRatePerMeter(followDistance);
        if (float.IsPositiveInfinity(ratePerMeter))
        {
            return 1f;
        }

        return 1f - Mathf.Exp(-ratePerMeter * progress);
    }

    // 入力中の追従の距離をどれだけ使うか（_inputFollowWeight）を決める。
    // 入力中はすぐに1（入力中の距離）。入力をやめたら、InputFollowReturnDuration秒かけてイージングで0（通常の距離）へ戻す
    // （すぐに戻すと、離した瞬間に向きの遅れ方が急に変わって違和感が出るため）。戻している途中で入力したら、またすぐに1にする。
    // 「入力中の追従」がOFFなら、常に0。
    private void UpdateInputFollowWeight(FlightTuningProfile profile, float deltaTime)
    {
        if (!profile.UseInputFollowDistance)
        {
            _inputFollowWeight = 0f;
            _inputFollowReleaseTime = 0f;
            return;
        }

        if (MoveInput.sqrMagnitude > 0f)
        {
            _inputFollowWeight = 1f;
            _inputFollowReleaseTime = 0f;
            return;
        }

        // 一度も入力していない（すでに戻し切った）なら、何もしない。
        if (_inputFollowWeight <= 0f)
        {
            return;
        }

        float duration = profile.InputFollowReturnDuration;
        _inputFollowReleaseTime += deltaTime;
        if (duration <= 0f || _inputFollowReleaseTime >= duration)
        {
            _inputFollowWeight = 0f;
            return;
        }

        float eased = EaseManager.Evaluate(profile.InputFollowReturnEase, null, _inputFollowReleaseTime, duration, 1.70158f, 0f);
        _inputFollowWeight = 1f - eased;
    }

    // 移動の入力（MoveInput）とコース中央への戻りから目標の上下左右の速さを決め、そこへ加速度で近づける。
    // 中央への戻りは、ずれ（offsetToCenter：中央へ向かう向き、x＝右・y＝上のm）に比例する速さで、入力が小さいほど強くかける
    // （入力いっぱいなら0、ニュートラルなら全部。押している間は入力を優先し、離すとだんだん中央へ戻る）。
    // 目標の向きの成分は、同じ向きに速くするときはsteeringPower、遅くするときはLateralStopDecelerationで近づける。
    // 目標と逆向きに動いているとき（切り返し）は、まず0まで止めてから（次のTickから）目標の向きに速くする。
    // 目標の向きと直交する成分（右から上へ切り替えたときの右の成分など）は、LateralStopDecelerationで0へ近づける。
    // 入力の大きさは0～1なので、入力の目標は斜めでもmaxLateralSpeedを超えない。切り替えの途中に合計の速さが増えないよう、上限もかける。
    private void UpdatePlanarSpeed(Vector2 offsetToCenter, float steeringPower, float maxLateralSpeed, FlightTuningProfile profile, float deltaTime)
    {
        float stopStep = profile.LateralStopDeceleration * deltaTime;
        Vector2 correction = Vector2.ClampMagnitude(offsetToCenter * profile.CenterCorrectionGain, profile.MaxCorrectionSpeed);
        Vector2 target = MoveInput * maxLateralSpeed + (1f - MoveInput.magnitude) * correction;
        float targetSpeed = target.magnitude;

        if (targetSpeed < 0.0001f)
        {
            _planarSpeed = Vector2.MoveTowards(_planarSpeed, Vector2.zero, stopStep);
            return;
        }

        float previousSpeed = _planarSpeed.magnitude;
        Vector2 direction = target / targetSpeed;
        float along = Vector2.Dot(_planarSpeed, direction);
        Vector2 perpendicular = Vector2.MoveTowards(_planarSpeed - direction * along, Vector2.zero, stopStep);

        if (along < 0f)
        {
            along = Mathf.MoveTowards(along, 0f, stopStep);
        }
        else if (targetSpeed > along)
        {
            along = Mathf.MoveTowards(along, targetSpeed, steeringPower * deltaTime);
        }
        else
        {
            along = Mathf.MoveTowards(along, targetSpeed, stopStep);
        }

        _planarSpeed = Vector2.ClampMagnitude(direction * along + perpendicular, Mathf.Max(targetSpeed, previousSpeed));
    }

    // 機首の向きの成分が上限を超えていれば、その成分だけを切り詰める（上下左右の成分は変えない）。
    // 上限は、最高速度から上下左右の速さ（慣性の速度の上下左右の成分＋_planarSpeed）のLateralSpeedLimitRate倍を引いたもの。
    // 上下左右の速さは前進の成分を除いてから測る（カーブで機首の向きがコースからずれていても、前進の速さを横として数えない）。
    private Vector3 ClampForwardSpeed(Vector3 velocity, Vector3 right, Vector3 up, float maxSpeed, FlightTuningProfile profile)
    {
        float forwardSpeed = Vector3.Dot(velocity, _playerForward);
        Vector3 offForward = velocity - _playerForward * forwardSpeed;
        Vector2 inertiaPlanar = new Vector2(Vector3.Dot(offForward, right), Vector3.Dot(offForward, up));
        float planarSpeed = (inertiaPlanar + _planarSpeed).magnitude;
        float forwardLimit = Mathf.Max(0f, maxSpeed - profile.LateralSpeedLimitRate * planarSpeed);
        if (forwardSpeed <= forwardLimit)
        {
            return velocity;
        }

        return velocity - _playerForward * (forwardSpeed - forwardLimit);
    }

    // カメラの狙い先を、今の移動の入力をCameraPredictionTime秒続けたときの位置として予測する。
    // 前進：コースに沿って「前進の速さ×予測時間」先（最低でもLookAheadDistance先）の中心。カーブでもコースの先を向く。
    // 上下左右：今の中心からのずれに「今の上下左右の速さ×予測時間」を足し、その地点のトンネル半径に収める（壁の外は狙わない）。
    // 入力の目標ではなく実際の速さを使う（実際の速さは加速度でだんだん変わるので、押した・離した瞬間に狙い先が跳ばず、カメラが急に振られない）。
    // 上下左右の移動とカメラの旋回は別の処理で、カメラ側（CameraController）はこの位置へ向くだけ。
    private Vector3 PredictPosition(Vector3 position, Vector3 center, Vector3 right, Vector3 up, FlightTuningProfile profile)
    {
        float predictionTime = profile.CameraPredictionTime;
        float forwardDistance = Mathf.Max(Mathf.Max(0f, ForwardSpeed) * predictionTime, profile.LookAheadDistance);

        // centerはコースの中心線上の点なので、GetLookAheadPositionは中心線上の先の点を返す。
        Vector3 futureCenter = _guide.GetLookAheadPosition(center, forwardDistance);

        Vector3 offset = position - center;
        Vector2 planarOffset = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up))
            + _planarSpeed * predictionTime;

        float futureRadius = _guide.GetTunnelRadius(futureCenter);
        if (futureRadius > 0f)
        {
            planarOffset = Vector2.ClampMagnitude(planarOffset, futureRadius);
        }

        return futureCenter + _guide.GetRight(futureCenter) * planarOffset.x + _guide.GetUp(futureCenter) * planarOffset.y;
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
    // blockNormalがゼロでなければ、その逆向き（相手へ向かう向き）の速度を消す（上下左右の速さも、相手へ向かう成分だけ消す）。
    // トンネルの境界の制限は次のTickでかかる。
    public void ApplyContactCorrection(Vector3 offset, Vector3 blockNormal)
    {
        transform.position += offset;

        float velocityTowardOther = Vector3.Dot(_velocity, blockNormal);
        if (velocityTowardOther < 0f)
        {
            _velocity -= blockNormal * velocityTowardOther;
        }

        Vector2 planarNormal = new Vector2(Vector3.Dot(blockNormal, _steerRight), Vector3.Dot(blockNormal, _steerUp));
        if (planarNormal.sqrMagnitude > 0.0001f)
        {
            planarNormal.Normalize();
            float planarTowardOther = Vector2.Dot(_planarSpeed, planarNormal);
            if (planarTowardOther < 0f)
            {
                _planarSpeed -= planarNormal * planarTowardOther;
            }
        }
    }

    // Splineを中心軸とした円筒（半径radius＝その場所のトンネル半径）の外に出られないようにする。境界（壁）の外側へ向かう速度成分を消し、
    // さらに当たった角度に応じて残りの速度を減らす（正面から当たるほど大きく、かすめただけなら少し）。
    // 減らす割合 ＝ WallHitSpeedLossRate × 壁へ向かっていた速さの割合（＝当たった角度のsin）。
    // 当たった角度は上下左右の速さも含めた速度で測る。上下左右の速さは外へ向かう成分だけを消し（壁に沿う向きには滑れる）、慣性の速度を減らす。
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

            Vector2 planarRadial = new Vector2(lateral, vertical) / distance;
            float planarAlongRadial = Vector2.Dot(_planarSpeed, planarRadial);
            if (planarAlongRadial > 0f)
            {
                _planarSpeed -= planarRadial * planarAlongRadial;
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
