using UnityEngine;

// 1人称視点のカメラ制御。位置はPlayerの子オブジェクトとしてローカルオフセットで追従させ、
// このスクリプトは向き（LookAhead方向へのブレンド・遅延・ロール減衰）のみを扱う。
// Splineへは直接問い合わせず、PlayerFlightControllerが公開する値だけを参照する
// （二重に最近傍探索を行うと計算コストが倍増し、Playerと異なる最近傍点を拾って向きがズレる恐れがあるため）。
public class CameraController : MonoBehaviour
{
    [SerializeField] private PlayerFlightController player;

    [Tooltip("向きのうちLookAhead方向をどれだけ混ぜるか（0=Playerの向きそのまま、1=LookAhead方向そのまま）")]
    [SerializeField] private float lookAheadBlendWeight = 0.6f;

    [Tooltip("カメラ独自の追加の遅延（大きいほど滑らか、小さいほど追従が早い）")]
    [SerializeField] private float rotationSmoothing = 4f;

    [Tooltip("Playerのロールに対するカメラロールの減衰率（仕様書の25°→5°相当なら0.2）")]
    [SerializeField] private float rollDampFactor = 0.2f;

    private Vector3 _smoothedForward;

    private void Awake()
    {
        _smoothedForward = player != null ? player.transform.forward : transform.forward;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        Vector3 toLookAhead = player.LookAheadWorldPosition - transform.position;
        Vector3 lookDirection = toLookAhead.sqrMagnitude > 0.0001f ? toLookAhead.normalized : player.transform.forward;

        Vector3 blendedForward = Vector3.Slerp(player.transform.forward, lookDirection, lookAheadBlendWeight);
        if (blendedForward.sqrMagnitude < 0.0001f)
        {
            blendedForward = player.transform.forward;
        }

        float smoothT = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        _smoothedForward = Vector3.Slerp(_smoothedForward, blendedForward.normalized, smoothT).normalized;

        float cameraRoll = player.CurrentRoll * rollDampFactor;
        transform.rotation = Quaternion.LookRotation(_smoothedForward, Vector3.up) * Quaternion.AngleAxis(cameraRoll, Vector3.forward);
    }
}
