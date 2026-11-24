using System.Collections.Generic;
using UnityEngine;

// レーサーがトンネルの壁に当たったとき、そのあたりのトンネルを濃くする。CourseTunnelMeshと同じGameObjectに付ける。
// 毎フレーム、各レーサーの IsTouchingWall（RacerManagerBase）を見て、触れている間はその位置を強さ1でシェーダーへ渡し、
// 離れたら Fade Out Time かけて薄くしていく。濃さ・範囲・色はマテリアル（Misoten/Course Tunnel）の「Wall Hit」で決める。
// レーサーは最大8人（シェーダーのMAX_TUNNEL_HITSと同じ）。9人目からは無視する。
[RequireComponent(typeof(MeshRenderer))]
public sealed class CourseTunnelHitGlow : MonoBehaviour
{
    // シェーダー（S_CourseTunnel）のMAX_TUNNEL_HITSと同じにする。
    public const int MaxRacers = 8;

    private static readonly int HitsId = Shader.PropertyToID("_TunnelHits");

    [Header("レーサー")]
    [Tooltip("ONなら、シーンのレーサー（RacerManagerBase）を自動で探す。OFFなら下のRacersに入れたものだけ")]
    [SerializeField] private bool _autoFindRacers = true;

    [Tooltip("自動で探すとき、探し直す間隔（秒）。途中で出てくるレーサーにも対応するため")]
    [Min(0.1f)]
    [SerializeField] private float _refreshInterval = 2f;

    [Tooltip("Auto Find RacersがOFFのときに使うレーサー（最大8人）")]
    [SerializeField] private RacerManagerBase[] _racers = new RacerManagerBase[0];

    [Header("濃くする時間")]
    [Tooltip("壁に触れてから、いちばん濃くなるまでの時間（秒）")]
    [Min(0f)]
    [SerializeField] private float _fadeInTime = 0.05f;

    [Tooltip("壁から離れてから、元の薄さに戻るまでの時間（秒）")]
    [Min(0.01f)]
    [SerializeField] private float _fadeOutTime = 0.8f;

    [Tooltip("壁に触れたときの強さ（0〜1）。擦り続けている間もこの強さ")]
    [Range(0f, 1f)]
    [SerializeField] private float _strength = 1f;

    private readonly List<RacerManagerBase> _activeRacers = new List<RacerManagerBase>();
    private readonly Vector4[] _hits = new Vector4[MaxRacers];
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private float _nextRefreshTime;

    private void OnEnable()
    {
        _renderer = GetComponent<MeshRenderer>();
        _nextRefreshTime = 0f;
        System.Array.Clear(_hits, 0, _hits.Length);
        Apply();
    }

    private void OnDisable()
    {
        System.Array.Clear(_hits, 0, _hits.Length);
        Apply();
    }

    private void LateUpdate()
    {
        RefreshRacersIfNeeded();

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < MaxRacers; i++)
        {
            RacerManagerBase racer = i < _activeRacers.Count ? _activeRacers[i] : null;
            bool touching = racer != null && racer.isActiveAndEnabled && racer.IsTouchingWall;
            Vector4 hit = _hits[i];

            if (touching)
            {
                Vector3 position = racer.transform.position;
                float step = _fadeInTime > 0f ? deltaTime / _fadeInTime : 1f;
                hit = new Vector4(position.x, position.y, position.z, Mathf.MoveTowards(hit.w, _strength, step));
            }
            else
            {
                // 離れたら、当たった位置のまま薄くしていく。
                hit.w = Mathf.MoveTowards(hit.w, 0f, deltaTime / _fadeOutTime);
            }

            _hits[i] = hit;
        }

        Apply();
    }

    private void RefreshRacersIfNeeded()
    {
        if (!_autoFindRacers)
        {
            _activeRacers.Clear();
            foreach (RacerManagerBase racer in _racers)
            {
                if (racer != null && _activeRacers.Count < MaxRacers)
                {
                    _activeRacers.Add(racer);
                }
            }

            return;
        }

        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + _refreshInterval;

        // 前からいるレーサーは同じ枠のままにする（探し直しで枠が入れ替わると、薄くなっている途中の光が別の位置へ飛ぶため）。
        // いなくなったレーサーの枠は空け（null）、新しいレーサーは空いた枠に入れる。
        RacerManagerBase[] found = FindObjectsByType<RacerManagerBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < _activeRacers.Count; i++)
        {
            if (_activeRacers[i] != null && System.Array.IndexOf(found, _activeRacers[i]) < 0)
            {
                _activeRacers[i] = null;
            }
        }

        foreach (RacerManagerBase racer in found)
        {
            if (_activeRacers.Contains(racer))
            {
                continue;
            }

            int empty = _activeRacers.IndexOf(null);
            if (empty >= 0)
            {
                _activeRacers[empty] = racer;
            }
            else if (_activeRacers.Count < MaxRacers)
            {
                _activeRacers.Add(racer);
            }
        }
    }

    private void Apply()
    {
        if (_renderer == null)
        {
            return;
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        // CourseTunnelMeshが入れた区画の値を消さないよう、今の値を読んでから書き足す。
        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetVectorArray(HitsId, _hits);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
