using System.Collections.Generic;
using UnityEngine;

// 足裏・背中のブースト炎エフェクトの見た目と状態優先度をまとめて制御するコンポーネント。
// BoostPresentation（ブースト中）と StunPresentation（スタン中）の両方から状態を受け取り、
//   1. スタン中：P_BoostFlame_Foot をスタン用値で表示、FootBoost / Back を非表示
//   2. ブースト中（非スタン）：P_BoostFlame_Foot を通常値で表示、FootBoost / Back を表示
//   3. 通常時：P_BoostFlame_Foot を通常値で表示、FootBoost / Back を非表示
// の優先順で一括反映する（スタン解除時にまだブースト中なら自動でブースト見た目に復帰する）。
public class BoostFlameView : MonoBehaviour
{
    private struct FlameGroup
    {
        public GameObject Root;
        public ParticleSystem[] Particles;
    }

    private enum EVisualState
    {
        Uninitialized,
        Normal,
        Boosting,
        Stunned
    }

    [Header("通常・スタン中も出し続ける足裏の炎 (P_BoostFlame_Foot)")]
    [Tooltip("左右の足裏ボーンに置いた P_BoostFlame_Foot を割り当てる")]
    [SerializeField] private GameObject[] _footFlames;

    [Header("ブースト中のみ表示する炎 (P_BoostFlame_FootBoost / P_BoostFlame_Back)")]
    [Tooltip("左右の足裏ボーンに置いた P_BoostFlame_FootBoost を割り当てる")]
    [SerializeField] private GameObject[] _footBoostFlames;

    [Tooltip("背中ボーンに置いた P_BoostFlame_Back を割り当てる")]
    [SerializeField] private GameObject[] _backFlames;

    private FlameGroup[] _footGroups;
    private FlameGroup[] _footBoostGroups;
    private FlameGroup[] _backGroups;
    private BoostFlameStunModifier[] _footModifiers;

    private bool _isInitialized;
    private bool _isBoosting;
    private bool _isStunned;
    private EVisualState _currentVisualState = EVisualState.Uninitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (_isInitialized)
        {
            ApplyState(force: true);
        }
    }

    // BoostPresentation からブースト開始／終了を通知する。
    public void SetBoosting(bool isBoosting)
    {
        EnsureInitialized();
        _isBoosting = isBoosting;
        ApplyState();
    }

    // StunPresentation からスタン開始／終了を通知する（ブースト中でもスタンを優先する）。
    public void SetStunned(bool isStunned)
    {
        EnsureInitialized();
        _isStunned = isStunned;
        ApplyState();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _footGroups = BuildFlameGroups(_footFlames);
        _footBoostGroups = BuildFlameGroups(_footBoostFlames);
        _backGroups = BuildFlameGroups(_backFlames);
        _footModifiers = CollectFootModifiers(_footFlames);

        _isBoosting = false;
        _isStunned = false;
        ApplyState(force: true);
    }

    private void ApplyState(bool force = false)
    {
        EVisualState nextState = _isStunned
            ? EVisualState.Stunned
            : (_isBoosting ? EVisualState.Boosting : EVisualState.Normal);

        if (!force && nextState == _currentVisualState)
        {
            return;
        }

        _currentVisualState = nextState;

        bool showBoostExtras = nextState == EVisualState.Boosting;
        bool useStunFootValues = nextState == EVisualState.Stunned;

        SetGroupsVisible(_footGroups, true, clearOnHide: false);
        if (_footModifiers != null)
        {
            for (int i = 0; i < _footModifiers.Length; i++)
            {
                if (_footModifiers[i] != null)
                {
                    _footModifiers[i].SetStunned(useStunFootValues);
                }
            }
        }

        SetGroupsVisible(_footBoostGroups, showBoostExtras, clearOnHide: true);
        SetGroupsVisible(_backGroups, showBoostExtras, clearOnHide: true);
    }

    private static FlameGroup[] BuildFlameGroups(GameObject[] sources)
    {
        if (sources == null || sources.Length == 0)
        {
            return System.Array.Empty<FlameGroup>();
        }

        var groups = new List<FlameGroup>(sources.Length);
        var seenRoots = new HashSet<GameObject>();
        for (int i = 0; i < sources.Length; i++)
        {
            GameObject root = sources[i];
            if (root == null || !seenRoots.Add(root))
            {
                continue;
            }

            groups.Add(new FlameGroup
            {
                Root = root,
                Particles = root.GetComponentsInChildren<ParticleSystem>(true)
            });
        }

        return groups.ToArray();
    }

    private static BoostFlameStunModifier[] CollectFootModifiers(GameObject[] footSources)
    {
        if (footSources == null || footSources.Length == 0)
        {
            return System.Array.Empty<BoostFlameStunModifier>();
        }

        var list = new List<BoostFlameStunModifier>(footSources.Length * 2);
        var seen = new HashSet<BoostFlameStunModifier>();

        for (int i = 0; i < footSources.Length; i++)
        {
            GameObject footRoot = footSources[i];
            if (footRoot == null)
            {
                continue;
            }

            BoostFlameStunModifier[] found = footRoot.GetComponentsInChildren<BoostFlameStunModifier>(true);
            if (found.Length == 0)
            {
                Debug.LogWarning($"BoostFlameView: '{footRoot.name}' の子（Flame_Core / Flame_Outer）に BoostFlameStunModifier が見つかりません。スタン中に足裏の炎が短くなりません。", footRoot);
                continue;
            }

            for (int j = 0; j < found.Length; j++)
            {
                if (found[j] != null && seen.Add(found[j]))
                {
                    list.Add(found[j]);
                }
            }
        }

        return list.ToArray();
    }

    private static void SetGroupsVisible(FlameGroup[] groups, bool visible, bool clearOnHide)
    {
        if (groups == null)
        {
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            GameObject root = groups[i].Root;
            if (root == null)
            {
                continue;
            }

            ParticleSystem[] particles = groups[i].Particles;
            if (visible)
            {
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                }

                if (particles != null)
                {
                    for (int j = 0; j < particles.Length; j++)
                    {
                        ParticleSystem ps = particles[j];
                        if (ps != null && !ps.isPlaying)
                        {
                            ps.Play(false);
                        }
                    }
                }
            }
            else
            {
                if (particles != null)
                {
                    ParticleSystemStopBehavior stopBehavior = clearOnHide
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting;

                    for (int j = 0; j < particles.Length; j++)
                    {
                        ParticleSystem ps = particles[j];
                        if (ps != null)
                        {
                            ps.Stop(false, stopBehavior);
                        }
                    }
                }

                if (root.activeSelf)
                {
                    root.SetActive(false);
                }
            }
        }
    }

    [ContextMenu("テスト：通常")]
    private void DebugTestNormal()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureInitialized();
        _isStunned = false;
        _isBoosting = false;
        ApplyState(force: true);
    }

    [ContextMenu("テスト：ブースト中")]
    private void DebugTestBoosting()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureInitialized();
        _isStunned = false;
        _isBoosting = true;
        ApplyState(force: true);
    }

    [ContextMenu("テスト：スタン中")]
    private void DebugTestStunned()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureInitialized();
        _isStunned = true;
        ApplyState(force: true);
    }
}
