using UnityEngine;

// 妨害弾の設定の塊。弾数・発射間隔・弾の速度・再装填の秒数と、弾の飛び方を決める。
[CreateAssetMenu(fileName = "JammerProfile", menuName = "Player/JammerProfile")]
public class JammerProfile : ScriptableObject
{
    [Header("弾数")]
    [Tooltip("持てる弾の最大数")]
    [SerializeField] private int _maxAmmo = 3;
    [Tooltip("撃ってから、次に撃てるようになるまでの秒数")]
    [SerializeField] private float _fireInterval = 1f;
    [Tooltip("弾が1発回復するまでの秒数。最大数より少ない間だけ進み、撃ってもリセットしない")]
    [SerializeField] private float _reloadDuration = 5f;

    [Header("弾")]
    [Tooltip("弾のプレハブ（JammerProjectileを付ける）")]
    [SerializeField] private JammerProjectile _projectilePrefab;
    [Tooltip("弾の速度（m/s）。相手の最高速度より遅いと追いつけない")]
    [SerializeField] private float _projectileSpeed = 90f;
    [Tooltip("弾が消えるまでの秒数（当たらなかったとき）")]
    [SerializeField] private float _lifetime = 6f;
    [Tooltip("当たったことにする、レーサーの中心からの距離（m）")]
    [SerializeField] private float _hitRadius = 2.5f;
    [Tooltip("撃ったレーサーの中心から、弾を出す位置までの前方の距離（m）")]
    [SerializeField] private float _spawnForwardOffset = 3f;

    [Header("軌道")]
    [Tooltip("コースに沿って飛ぶときに、どれだけ先のコース上の点へ向かうか（m）")]
    [SerializeField] private float _courseLookAhead = 25f;
    [Tooltip("弾が向きを変えられる速さ（度/秒）")]
    [SerializeField] private float _turnSpeed = 270f;
    [Tooltip("相手までの距離がこれより近くなると、コース沿いから相手へ狙いを寄せ始める（m）")]
    [SerializeField] private float _homingStartDistance = 60f;
    [Tooltip("相手までの距離がこれより近くなると、相手へまっすぐ向かう（m）")]
    [SerializeField] private float _directHomingDistance = 25f;

    public int MaxAmmo => Mathf.Max(0, _maxAmmo);

    public float FireInterval => Mathf.Max(0f, _fireInterval);

    public float ReloadDuration => Mathf.Max(0f, _reloadDuration);

    public JammerProjectile ProjectilePrefab => _projectilePrefab;

    public float ProjectileSpeed => Mathf.Max(0f, _projectileSpeed);

    public float Lifetime => Mathf.Max(0f, _lifetime);

    public float HitRadius => Mathf.Max(0f, _hitRadius);

    public float SpawnForwardOffset => _spawnForwardOffset;

    public float CourseLookAhead => Mathf.Max(0f, _courseLookAhead);

    public float TurnSpeed => Mathf.Max(0f, _turnSpeed);

    public float HomingStartDistance => Mathf.Max(0f, _homingStartDistance);

    public float DirectHomingDistance => Mathf.Max(0f, _directHomingDistance);
}
