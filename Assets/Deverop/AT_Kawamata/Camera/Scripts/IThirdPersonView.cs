// カメラの1人称／3人称の度合いを変える窓口。演出側（BoostCameraEffect）はこのinterfaceだけを持ち、
// カメラのTransformを直接触らない。CameraControllerが明示的に実装するので、CameraControllerを参照しただけでは書き換えられない。
public interface IThirdPersonView
{
    // 0＝1人称、1＝3人称。
    float ThirdPersonWeight { get; set; }
}
