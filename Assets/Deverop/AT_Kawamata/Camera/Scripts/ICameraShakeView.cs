using UnityEngine;

// カメラに揺れを足す窓口。演出側（CameraShakeEffect）はこのinterfaceだけを持ち、カメラのTransformを直接触らない。
// CameraControllerが明示的に実装するので、CameraControllerを参照しただけでは書き換えられない。
public interface ICameraShakeView
{
    // カメラの向きを基準にした位置のずれ（m。xが右、yが上）。CameraControllerが位置を決めた後に足す。
    Vector3 ShakeOffset { get; set; }
}
