using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 妨害弾の発射台。Playerプレハブに付け、PlayerManagerのJammer Launcherに入れる（PlayerManagerがゴール前のTickごとにTickを呼ぶ）。
// 残弾・発射間隔・再装填を管理し、Qキーで、今の種類（Types のうち選んでいるもの）の弾を標的へ撃つ。
// ・レーサーを狙う種類：自分より順位が高いレーサーのうち、コース上の距離が一番近いレーサー（ゴール済みは除く）
// ・リングを狙う種類：Ring Targetsのうち、自分より前にある一番近いもの（加速リングができるまでの仮の標的）
// 標的がいなければ撃たない（弾は減らない）。弾は1発ずつ回復する（最大数より少ない間だけタイマーが進み、撃ってもリセットしない）。
// 種類の切り替えは、Inspectorのボタン（JammerLauncherEditor）かSelectTypeで行う。
// Qキーはトラッキング入力に割り当てが無いので、IRaceInputには足さず、KeyboardRaceInputと同じくKeyboard.currentを直接読む。
public class JammerLauncher : MonoBehaviour
{
    [SerializeField] private JammerProfile _profile;

    [Tooltip("切り替えられる弾の種類。Inspectorのボタンで選ぶ")]
    [SerializeField] private List<JammerTypeProfile> _types = new List<JammerTypeProfile>();

    [Tooltip("今の種類（Typesの番号）")]
    [SerializeField] private int _currentTypeIndex;

    [Tooltip("リングを狙う種類の標的（加速リングができるまでの仮の標的）。このうち、自分より前にある一番近いものを狙う")]
    [SerializeField] private List<GameObject> _ringTargets = new List<GameObject>();

    [Header("デバッグ")]
    [SerializeField] private bool _showDebugLogs = true;

    private float _fireIntervalTimer;
    private float _reloadElapsed;
    private bool _isInitialized;

    public IReadOnlyList<JammerTypeProfile> Types => _types;

    public int CurrentTypeIndex => _currentTypeIndex;

    public JammerTypeProfile CurrentType => _currentTypeIndex >= 0 && _currentTypeIndex < _types.Count ? _types[_currentTypeIndex] : null;

    public int Ammo { get; private set; }

    public int MaxAmmo => _profile != null ? _profile.MaxAmmo : 0;

    // 次の1発が回復するまでの残り秒数。満タンなら0。
    public float ReloadRemainingTime => _profile != null && Ammo < MaxAmmo ? Mathf.Max(0f, _profile.ReloadDuration - _reloadElapsed) : 0f;

    private void Awake()
    {
        ResetAmmo();
    }

    // 種類を切り替える。残弾はそのまま。
    public void SelectType(int index)
    {
        if (index < 0 || index >= _types.Count)
        {
            return;
        }

        _currentTypeIndex = index;
        Log($"妨害弾の種類を切り替えた（{CurrentType?.DisplayName}）");
    }

    // 満タンの状態に戻す。
    public void ResetAmmo()
    {
        Ammo = MaxAmmo;
        _fireIntervalTimer = 0f;
        _reloadElapsed = 0f;
        _isInitialized = true;
    }

    // owner：撃つレーサー（自分）、racers：参加者全員、ownerGuide：自分のコースのガイド、canFire：撃てる状態か（スタン中はfalse）。
    public void Tick(float deltaTime, IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers, ICourseGuide ownerGuide, bool canFire)
    {
        if (_profile == null)
        {
            return;
        }

        if (!_isInitialized)
        {
            ResetAmmo();
        }

        TickReload(deltaTime);

        Keyboard keyboard = Keyboard.current;
        if (!canFire || keyboard == null || !keyboard.qKey.wasPressedThisFrame)
        {
            return;
        }

        TryFire(owner, racers, ownerGuide);
    }

    private void TickReload(float deltaTime)
    {
        _fireIntervalTimer = Mathf.Max(0f, _fireIntervalTimer - deltaTime);
        if (Ammo >= MaxAmmo)
        {
            _reloadElapsed = 0f;
            return;
        }

        _reloadElapsed += deltaTime;
        if (_reloadElapsed >= _profile.ReloadDuration)
        {
            _reloadElapsed = 0f;
            Ammo++;
        }
    }

    private void TryFire(IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers, ICourseGuide ownerGuide)
    {
        JammerTypeProfile type = CurrentType;
        if (type == null || _profile.ProjectilePrefab == null)
        {
            Log("妨害弾を撃てない（種類か、弾のプレハブが未設定）");
            return;
        }

        if (Ammo <= 0 || _fireIntervalTimer > 0f)
        {
            Log($"妨害弾を撃てない（残弾 {Ammo}/{MaxAmmo}、次に撃てるまで {_fireIntervalTimer:F1}秒）");
            return;
        }

        Transform ownerTransform = owner.Transform;
        Vector3 forward = ownerTransform.forward;
        Vector3 position = ownerTransform.position + forward * _profile.SpawnForwardOffset;

        if (type.TargetsRing)
        {
            GameObject ring = FindRingTarget(ownerTransform.position, ownerGuide != null ? ownerGuide.GetForward(ownerTransform.position) : forward);
            if (ring == null)
            {
                Log($"妨害弾（{type.DisplayName}）を撃てない（前に標的のリングが無い）");
                return;
            }

            CreateProjectile(position, forward).LaunchAtObject(_profile, type, owner, ring, ownerGuide, forward);
            Log($"妨害弾（{type.DisplayName}）を発射 標的 {ring.name} 残弾 {Ammo - 1}/{MaxAmmo}");
        }
        else
        {
            IReadOnlyRacerData target = FindRacerTarget(owner, racers);
            if (target == null)
            {
                Log($"妨害弾（{type.DisplayName}）を撃てない（自分より順位が高いレーサーがいない）");
                return;
            }

            CreateProjectile(position, forward).LaunchAtRacer(_profile, type, owner, target, racers, ownerGuide, forward);
            Log($"妨害弾（{type.DisplayName}）を発射 標的 {target.DisplayName} 残弾 {Ammo - 1}/{MaxAmmo}");
        }

        Ammo--;
        _fireIntervalTimer = _profile.FireInterval;
    }

    private JammerProjectile CreateProjectile(Vector3 position, Vector3 forward)
    {
        return Instantiate(_profile.ProjectilePrefab, position, Quaternion.LookRotation(forward));
    }

    // 自分より順位が高いレーサーのうち、コース上の距離が一番近いレーサー。ゴール済みは含めない。いなければnull。
    private static IReadOnlyRacerData FindRacerTarget(IReadOnlyRacerData owner, IReadOnlyList<IReadOnlyRacerData> racers)
    {
        IReadOnlyRacerData nearest = null;
        float nearestGap = float.MaxValue;

        foreach (IReadOnlyRacerData racer in racers)
        {
            if (racer.RacerId == owner.RacerId || racer.IsFinished || racer.CurrentRank >= owner.CurrentRank)
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

    // Ring Targetsのうち、自分より前（コースの前向きに対して前方）にあり、一番近い有効なもの。いなければnull。
    private GameObject FindRingTarget(Vector3 position, Vector3 courseForward)
    {
        GameObject nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (GameObject ring in _ringTargets)
        {
            if (ring == null || !ring.activeInHierarchy)
            {
                continue;
            }

            Vector3 toRing = ring.transform.position - position;
            if (Vector3.Dot(toRing, courseForward) <= 0f)
            {
                continue;
            }

            float sqrDistance = toRing.sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = ring;
            }
        }

        return nearest;
    }

    private void Log(string message)
    {
        if (_showDebugLogs)
        {
            Debug.Log($"JammerLauncher: {message}", this);
        }
    }
}
