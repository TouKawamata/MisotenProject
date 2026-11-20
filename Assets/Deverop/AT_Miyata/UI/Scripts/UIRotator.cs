using UnityEngine;

/// <summary>
/// UIをZ軸まわりに回し続ける（リングの演出用）。
/// </summary>
public class UIRotator : MonoBehaviour
{
    [Tooltip("1秒あたりの回転角度（度）。プラスで反時計回り、マイナスで時計回り")]
    [SerializeField] private float _degreesPerSecond = -20f;

    [Tooltip("ONならポーズ中（Time.timeScale = 0）も回す")]
    [SerializeField] private bool _useUnscaledTime = false;

    private void Update()
    {
        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(0f, 0f, _degreesPerSecond * deltaTime);
    }
}
