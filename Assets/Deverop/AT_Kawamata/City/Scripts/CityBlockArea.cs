using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 区画の範囲（BoxCollider）と配置ルールを持つ。
/// 実際の生成はEditor（CityBlockAreaEditor）から行う。実行時には何もしない。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class CityBlockArea : MonoBehaviour
{
    public enum RotationMode
    {
        Fixed,           // すべて同じ向き（Yaw Offset）
        Random90,        // 0/90/180/270°からランダム
        FaceNearestEdge, // 一番近い辺（道路側）へ正面を向ける
    }

    [Serializable]
    public sealed class BuildingEntry
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0f)] private float _weight = 1f;

        public GameObject Prefab => _prefab;
        public float Weight => _weight;
    }

    public readonly struct Placement
    {
        public readonly GameObject Prefab;
        public readonly Vector3 LocalPosition;
        public readonly float LocalYaw;

        public Placement(GameObject prefab, Vector3 localPosition, float localYaw)
        {
            Prefab = prefab;
            LocalPosition = localPosition;
            LocalYaw = localYaw;
        }
    }

    [Header("建物（重みで出やすさを調整）")]
    [SerializeField] private BuildingEntry[] _buildings = new BuildingEntry[3];

    [Header("グリッド")]
    [Tooltip("1マスの大きさ（X / Z、m）")]
    [SerializeField] private Vector2 _cellSize = new Vector2(10f, 10f);
    [Tooltip("区画の外周から内側へ空ける幅（m）")]
    [SerializeField, Min(0f)] private float _margin = 2f;
    [Tooltip("1マスに建物を置く確率（1で全マス）")]
    [SerializeField, Range(0f, 1f)] private float _fillRate = 1f;
    [Tooltip("マスの中心からずらす最大量（m）")]
    [SerializeField, Min(0f)] private float _positionJitter = 0f;

    [Header("向き")]
    [SerializeField] private RotationMode _rotationMode = RotationMode.FaceNearestEdge;
    [Tooltip("Fixedのときの角度。他のモードでは、モデルの正面が+Zでない場合の補正として足す")]
    [SerializeField] private float _yawOffset = 0f;

    [Header("乱数")]
    [SerializeField] private int _seed = 0;

    [Header("出力")]
    [SerializeField] private string _containerName = "_Generated";

    public string ContainerName => _containerName;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        // 範囲指定にだけ使う。レーサーのTrigger判定に引っかからないよう無効にしておく
        box.isTrigger = true;
        box.enabled = false;
        box.center = new Vector3(0f, 5f, 0f);
        box.size = new Vector3(50f, 10f, 50f);
    }

    /// <summary>配置する位置・向き・Prefabを計算する（ローカル座標、Transformのスケールは1前提）。</summary>
    public List<Placement> BuildPlacements()
    {
        var result = new List<Placement>();
        var box = GetComponent<BoxCollider>();
        if (_cellSize.x <= 0f || _cellSize.y <= 0f) return result;

        float halfW = box.size.x * 0.5f;
        float halfD = box.size.z * 0.5f;
        int countX = Mathf.FloorToInt((box.size.x - _margin * 2f) / _cellSize.x);
        int countZ = Mathf.FloorToInt((box.size.z - _margin * 2f) / _cellSize.y);
        if (countX <= 0 || countZ <= 0) return result;

        // グリッドを区画の中央に寄せる
        float startX = -countX * _cellSize.x * 0.5f + _cellSize.x * 0.5f;
        float startZ = -countZ * _cellSize.y * 0.5f + _cellSize.y * 0.5f;

        var rng = new System.Random(_seed);

        for (int z = 0; z < countZ; z++)
        {
            for (int x = 0; x < countX; x++)
            {
                // 乱数の消費順を固定するため、置かないマスでも同じ回数だけ引く
                double fill = rng.NextDouble();
                float jx = RandomRange(rng, -_positionJitter, _positionJitter);
                float jz = RandomRange(rng, -_positionJitter, _positionJitter);
                GameObject prefab = PickPrefab(rng);
                int rand90 = rng.Next(4);

                if (fill > _fillRate || prefab == null) continue;

                float px = startX + x * _cellSize.x + jx;
                float pz = startZ + z * _cellSize.y + jz;
                float yaw = DecideYaw(px, pz, halfW, halfD, rand90);

                // Yはプレハブのルートに設定されている値をそのまま使う
                float y = prefab.transform.localPosition.y;
                var local = new Vector3(box.center.x + px, y, box.center.z + pz);
                result.Add(new Placement(prefab, local, yaw));
            }
        }
        return result;
    }

    private GameObject PickPrefab(System.Random rng)
    {
        if (_buildings == null) return null;

        float total = 0f;
        foreach (var e in _buildings)
        {
            if (e != null && e.Prefab != null) total += e.Weight;
        }
        if (total <= 0f) return null;

        double r = rng.NextDouble() * total;
        foreach (var e in _buildings)
        {
            if (e == null || e.Prefab == null) continue;
            r -= e.Weight;
            if (r <= 0d) return e.Prefab;
        }
        return null;
    }

    private float DecideYaw(float x, float z, float halfW, float halfD, int rand90)
    {
        switch (_rotationMode)
        {
            case RotationMode.Random90:
                return rand90 * 90f + _yawOffset;

            case RotationMode.FaceNearestEdge:
                {
                    float right = halfW - x;
                    float left = halfW + x;
                    float front = halfD - z;
                    float back = halfD + z;
                    float min = Mathf.Min(Mathf.Min(right, left), Mathf.Min(front, back));

                    float yaw;
                    if (min == front) yaw = 0f;
                    else if (min == right) yaw = 90f;
                    else if (min == back) yaw = 180f;
                    else yaw = -90f;
                    return yaw + _yawOffset;
                }

            default:
                return _yawOffset;
        }
    }

    private static float RandomRange(System.Random rng, float min, float max)
    {
        return (float)(min + rng.NextDouble() * (max - min));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;

        // 区画
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(box.center, box.size);

        // 配置予定のマスと正面の向き
        var cell = new Vector3(_cellSize.x * 0.9f, 0.1f, _cellSize.y * 0.9f);
        foreach (var p in BuildPlacements())
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(p.LocalPosition, cell);

            Gizmos.color = Color.red;
            Vector3 forward = Quaternion.Euler(0f, p.LocalYaw, 0f) * Vector3.forward;
            Gizmos.DrawLine(p.LocalPosition, p.LocalPosition + forward * Mathf.Min(_cellSize.x, _cellSize.y) * 0.5f);
        }
    }
#endif
}