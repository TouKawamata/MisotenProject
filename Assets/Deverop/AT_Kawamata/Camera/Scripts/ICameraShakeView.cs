using UnityEngine;

// カメラに揺れを足す窓口。演出側（CameraShakeEffect）はこのinterfaceだけを持ち、カメラのTransformを直接触らない。
// CameraControllerが明示的に実装するので、CameraControllerを参照しただけでは書き換えられない。
public interface ICameraShakeView
{
    // カメラの向きを基準にした位置のずれ（m。xが右、yが上）。CameraControllerが位置を決めた後に足す。
    Vector3 ShakeOffset { get; set; }

    // 揺れによるカメラの横の傾き（度。＋で右に傾く）。左右入力による傾きと同じ軸で傾ける。
    float ShakeTilt { get; set; }

    // 左右入力による傾きの代わりに、揺れの傾きをどれだけ使うか（0＝左右入力の傾きだけ、1＝揺れの傾きだけ）。
    // 揺れている間は揺れを優先し、両方を足して傾き過ぎないようにするために使う。
    float ShakeTiltWeight { get; set; }
}
