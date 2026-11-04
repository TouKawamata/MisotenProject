using System.Collections.Generic;
using UnityEngine;

// 妨害弾の発射台。残弾・発射間隔・再装填を管理し、標的を選んで弾を出す。
// 弾は1発ずつ回復する（最大数より少ない間だけタイマーが進み、撃ってもリセットしない）。
public class JammerLauncher
{
    private JammerProfile _profile;
    private float _fireIntervalTimer;
    private float _reloadElapsed;

    public int Ammo { get; private set; }

    public int MaxAmmo => _profile != null ? _profile.MaxAmmo : 0;

    // 次の1発が回復するまでの残り秒数。満タンなら0。
    public float ReloadRemainingTime => _profile != null && Ammo < MaxAmmo ? Mathf.Max(0f, _profile.ReloadDuration - _reloadElapsed) : 0f;

    public bool CanFire => _profile != null && _profile.ProjectilePrefab != null && Ammo > 0 && _fireIntervalTimer <= 0f;

    // 設定を渡して、満タンの状態に戻す。
    public void Reset(JammerProfile profile)
    {
        _profile = profile;
        Ammo = MaxAmmo;
        _fireIntervalTimer = 0f;
        _reloadElapsed = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (_profile == null)
        {
            return;
        }

        _fireIntervalTimer = Mathf.Max(0f, _fireIntervalTimer - deltaTime);
        if (Ammo >= MaxAmmo)
        {
            return;
        }

        _reloadElapsed += deltaTime;
        if (_reloadElapsed >= _profile.ReloadDuration)
        {
            _reloadElapsed = 0f;
            Ammo++;
        }
    }

    // 撃てる状態で標的がいれば、弾を出してtrueを返す。撃てなければ、弾は減らない。
    public bool TryFire(IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers, ICourseGuide guide, Vector3 position, Vector3 forward, out IReadOnlyRacerData target)
    {
        target = null;
        if (!CanFire)
        {
            return false;
        }

        target = FindTarget(owner, racers);
        if (target == null)
        {
            return false;
        }

        JammerProjectile projectile = Object.Instantiate(_profile.ProjectilePrefab, position, Quaternion.LookRotation(forward));
        projectile.Launch(_profile, owner, target, racers, guide, forward);

        Ammo--;
        _fireIntervalTimer = _profile.FireInterval;
        return true;
    }

    // 自分より順位が高いレーサーのうち、コース上の距離が一番近いレーサー。ゴール済みは含めない。いなければnull。
    private static IReadOnlyRacerData FindTarget(IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers)
    {
        IReadOnlyRacerData nearest = null;
        float nearestGap = float.MaxValue;

        foreach (IReadOnlyRacerData racer in racers)
        {
            if (racer.IsFinished || racer.CurrentRank >= owner.CurrentRank)
            {
                continue;
            }

            float gap = Mathf.Abs(racer.DistanceOnCourse - owner.DistanceOnCourse);
            if (gap < nearestGap)
            {
                nearestGap = gap;
                nearest = racer;
            }
        }

        return nearest;
    }
}
