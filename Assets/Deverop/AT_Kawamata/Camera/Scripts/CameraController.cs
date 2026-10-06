using UnityEngine;

// プレイヤーカメラの制御。Playerの子オブジェクトとして置き、向き（LookAhead方向へのブレンド・遅延・ロール減衰）と、
// 1人称／3人称の位置の補間を扱う。3人称の度合いは演出側（BoostCameraEffect）がIThirdPersonView経由で変えるだけで、
// カメラのTransformを書き換えるのはこのスクリプトだけにする。
// Splineへは直接問い合わせず、PlayerFlightControllerが公開する値だけを参照する
// （二重に最近傍探索を行うと計算コストが倍増し、Playerと異なる最近傍点を拾って向きがズレる恐れがあるため）。
public class CameraController : MonoBehaviour, IThirdPersonView
{
    [SerializeField] private PlayerFlightController player;

    [Tooltip("向きのうちLookAhead方向をどれだけ混ぜるか（0=Playerの向きそのまま、1=LookAhead方向そのまま）")]
    [SerializeField] private float lookAheadBlendWeight = 0.6f;

    [Tooltip("カメラ独自の追加の遅延（大きいほど滑らか、小さいほど追従が早い）")]
    [SerializeField] private float rotationSmoothing = 4f;

    [Tooltip("Playerのロールに対するカメラロールの減衰率（仕様書の25°→5°相当なら0.2）")]
    [SerializeField] private float rollDampFactor = 0.2f;

    [Header("3人称")]
    [Tooltip("3人称のときのカメラの位置。カメラの向き（ロールなし）を基準にした、Playerからのオフセット（x=右、y=上、z=前）")]
    [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0f, 3f, -8f);

    [Tooltip("3人称のときに、向きへ足す回転（度）。X＝見下ろす角度（＋で下向き）、Y＝左右、Z＝傾き。3人称の度合いに合わせて混ざる")]
    [SerializeField] private Vector3 _thirdPersonRotationOffset = new Vector3(5f, 0f, 0f);

    [Tooltip("3人称の位置の追従の遅れ（秒）。0なら遅れなし。カーブで外側へ少し振られる感じになる")]
    [SerializeField] private float _thirdPersonPositionSmoothTime = 0.08f;

    private Vector3 _smoothedForward;

    // 1人称の位置。プレハブで置いたローカル位置（Playerから見た目の位置）をそのまま使う。
    private Vector3 _firstPersonLocalOffset;

    private Vector3 _smoothedThirdPersonOffset;
    private Vector3 _thirdPersonOffsetVelocity;
    private float _thirdPersonWeight;

    // 0＝1人称、1＝3人称。演出側がIThirdPersonView経由でだけ変える。
    float IThirdPersonView.ThirdPersonWeight
    {
        get => _thirdPersonWeight;
        set => _thirdPersonWeight = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        _firstPersonLocalOffset = transform.localPosition;
        _smoothedForward = player != null ? player.transform.forward : transform.forward;

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

        UpdatePosition(Time.deltaTime);

        float smoothT = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        _smoothedForward = Vector3.Slerp(_smoothedForward, ComputeTargetForward(), smoothT).normalized;

        float cameraRoll = player.CurrentRoll * rollDampFactor;
        Quaternion thirdPersonRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(_thirdPersonRotationOffset), _thirdPersonWeight);
        transform.rotation = Quaternion.LookRotation(_smoothedForward, Vector3.up) * thirdPersonRotation * Quaternion.AngleAxis(cameraRoll, Vector3.forward);
    }

    // 1人称はPlayerのローカル位置（Playerのロールに合わせて動く）、3人称はカメラの向き（ロールなし）基準で後ろ上に置き、
    // 度合いで補間する。3人称の位置はPlayerのロールで振られないよう、Playerの向きではなくカメラの向きを基準にする。
    private void UpdatePosition(float deltaTime)
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 firstPersonPosition = player.transform.TransformPoint(_firstPersonLocalOffset);

        Quaternion basis = Quaternion.LookRotation(_smoothedForward, Vector3.up);
        Vector3 targetThirdPersonOffset = basis * _thirdPersonOffset;
        _smoothedThirdPersonOffset = _thirdPersonPositionSmoothTime > 0f
            ? Vector3.SmoothDamp(_smoothedThirdPersonOffset, targetThirdPersonOffset, ref _thirdPersonOffsetVelocity, _thirdPersonPositionSmoothTime, Mathf.Infinity, deltaTime)
            : targetThirdPersonOffset;

        if (_thirdPersonWeight <= 0f)
        {
            transform.position = firstPersonPosition;
            return;
        }

        Vector3 thirdPersonPosition = playerPosition + _smoothedThirdPersonOffset;
        transform.position = Vector3.Lerp(firstPersonPosition, thirdPersonPosition, _thirdPersonWeight);
    }

    // Playerが置き直されたとき、遅延なしで目標の向き・位置に合わせる（スタート前にカメラが回り込むのを防ぐ）。
    private void SnapToTarget()
    {
        _smoothedForward = ComputeTargetForward();
        _smoothedThirdPersonOffset = Quaternion.LookRotation(_smoothedForward, Vector3.up) * _thirdPersonOffset;
        _thirdPersonOffsetVelocity = Vector3.zero;
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
