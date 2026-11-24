using UnityEngine;

// P_BoostFlame_Foot の各子（Flame_Core / Flame_Outer）に付けて、スタン中だけ炎を短くする設定を持たせるコンポーネント。
// 子オブジェクトごとの Inspector でスタン中の Start Lifetime / Start Speed を個別に調整でき、
// BoostFlameView から SetStunned(bool) でまとめて制御される。
[DisallowMultipleComponent]
public class BoostFlameStunModifier : MonoBehaviour
{
    [Tooltip("設定を変える ParticleSystem。未設定なら同じオブジェクトの ParticleSystem を使う")]
    [SerializeField] private ParticleSystem _particle;

    [Header("スタン中の設定")]
    [Tooltip("ONのとき、スタン中だけ Start Lifetime を下の値に変える")]
    [SerializeField] private bool _overrideLifetime = true;

    [Tooltip("スタン中の Start Lifetime（初期目安: Flame_Core=0.35 / Flame_Outer=0.7）")]
    [SerializeField] private float _stunLifetime = 0.35f;

    [Tooltip("ONのとき、スタン中だけ Start Speed を下の値に変える（Flame_Core は OFF のまま変えない）")]
    [SerializeField] private bool _overrideSpeed = false;

    [Tooltip("スタン中の Start Speed（初期目安: Flame_Outer=1.8）")]
    [SerializeField] private float _stunSpeed = 1.8f;

    private float _defaultLifetime;
    private float _defaultSpeed;
    private bool _isInitialized;
    private bool _isStunned;

    private void Reset()
    {
        _particle = GetComponent<ParticleSystem>();

        // 付けた対象の名前から、仕様の初期値を自動セットする。
        if (gameObject.name.IndexOf("Outer", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            _overrideLifetime = true;
            _stunLifetime = 0.7f;
            _overrideSpeed = true;
            _stunSpeed = 1.8f;
        }
        else
        {
            _overrideLifetime = true;
            _stunLifetime = 0.35f;
            _overrideSpeed = false;
            _stunSpeed = 2f;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_particle == null)
        {
            _particle = GetComponent<ParticleSystem>();
        }

        if (Application.isPlaying && _isInitialized && _particle != null)
        {
            var main = _particle.main;
            main.startLifetimeMultiplier = (_isStunned && _overrideLifetime) ? Mathf.Max(0f, _stunLifetime) : _defaultLifetime;
            main.startSpeedMultiplier = (_isStunned && _overrideSpeed) ? Mathf.Max(0f, _stunSpeed) : _defaultSpeed;
        }
    }
#endif

    // スタン中かどうかを切り替え、通常時はプレハブ初期値・スタン中は指定値へ戻す／変える。
    public void SetStunned(bool isStunned)
    {
        EnsureInitialized();
        _isStunned = isStunned;
        ApplyValues();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        if (_particle == null)
        {
            _particle = GetComponent<ParticleSystem>();
        }

        if (_particle == null)
        {
            Debug.LogError($"BoostFlameStunModifier: {name} に ParticleSystem がありません", this);
            return;
        }

        var main = _particle.main;
        _defaultLifetime = main.startLifetimeMultiplier;
        _defaultSpeed = main.startSpeedMultiplier;
    }

    private void ApplyValues()
    {
        if (_particle == null)
        {
            return;
        }

        var main = _particle.main;
        if (_overrideLifetime)
        {
            main.startLifetimeMultiplier = _isStunned ? Mathf.Max(0f, _stunLifetime) : _defaultLifetime;
        }

        if (_overrideSpeed)
        {
            main.startSpeedMultiplier = _isStunned ? Mathf.Max(0f, _stunSpeed) : _defaultSpeed;
        }
    }
}
