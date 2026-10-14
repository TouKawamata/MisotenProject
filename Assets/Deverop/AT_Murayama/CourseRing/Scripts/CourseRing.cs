using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// イベント用クラス（Inspector上で引数付きイベントを表示するため）
/// </summary>
[System.Serializable]
public class RingPassEvent : UnityEvent<Transform> {}

/// <summary>
/// 汎用的なコースリング（チェックポイント、ブーストリング等のベース）。
/// スプラインには依存せず、単体で空間に存在し、通り抜けたことだけを検知する。
/// </summary>
public class CourseRing : MonoBehaviour
{
    [Header("リングサイズ")]
    [Tooltip("リングの半径（この数値を変更すると、自動的にTransformのX・Yスケールが同期されます）")]
    [SerializeField] private float _radius = 10f;

    [Header("判定対象")]
    [Tooltip("通過を監視する対象リスト（Playerや敵などを複数登録可能）")]
    [SerializeField] private List<Transform> _targets = new List<Transform>();

    [Header("イベント")]
    [Tooltip("対象がリングを通過した時に発火するイベント（誰が通過したかを渡します）")]
    public RingPassEvent OnPassed;

    // 各ターゲットの前フレーム位置を独立して記憶する辞書（すり抜けバグ防止用）
    private Dictionary<Transform, Vector3> _previousPositions = new Dictionary<Transform, Vector3>();

    // デフォルトの基本半径（Scale = 1 のとき、半径1とする）
    private const float BaseRadius = 1.0f;

    /// <summary>
    /// 半径を取得・設定します（外部スクリプトから変更用）
    /// </summary>
    public float Radius
    {
        get => _radius;
        set
        {
            _radius = Mathf.Max(0.1f, value); // マイナスや0を防ぐ
            UpdateScale();
        }
    }

    private void UpdateScale()
    {
        // Z軸（厚み）はそのままに、XとYだけを_radiusのサイズに合わせる
        transform.localScale = new Vector3(_radius, _radius, transform.localScale.z);
    }

    // Inspectorで数値をいじった時に自動でScaleを更新する
    private void OnValidate()
    {
        _radius = Mathf.Max(0.1f, _radius);
        UpdateScale();
    }

    /// <summary>
    /// 動的に監視対象を追加する
    /// </summary>
    public void AddTarget(Transform target)
    {
        if (target != null && !_targets.Contains(target))
        {
            _targets.Add(target);
        }
    }

    /// <summary>
    /// 動的に監視対象を削除する
    /// </summary>
    public void RemoveTarget(Transform target)
    {
        if (_targets.Contains(target))
        {
            _targets.Remove(target);
            _previousPositions.Remove(target);
        }
    }

    private void Update()
    {
        // 登録されている全ターゲットをチェック
        for (int i = 0; i < _targets.Count; i++)
        {
            Transform target = _targets[i];
            if (target == null) continue;

            Vector3 currentPosition = target.position;

            // 辞書に未登録（初回フレーム）の場合は記憶だけして終わる
            if (!_previousPositions.TryGetValue(target, out Vector3 previousPosition))
            {
                _previousPositions[target] = currentPosition;
                continue;
            }

            // ワールド座標をリングのローカル座標に変換
            Vector3 localPrev = transform.InverseTransformPoint(previousPosition);
            Vector3 localCur  = transform.InverseTransformPoint(currentPosition);

            // Z軸（前後）をまたいだかチェック
            bool crossedForward  = localPrev.z < 0f && localCur.z >= 0f;
            bool crossedBackward = localPrev.z > 0f && localCur.z <= 0f;

            if (crossedForward || crossedBackward)
            {
                // Z=0（リングの面）と、移動ラインが交差する割合(t)を求める
                float t = -localPrev.z / (localCur.z - localPrev.z);

                // 交差点のローカルX, Y座標を計算
                float crossX = localPrev.x + (localCur.x - localPrev.x) * t;
                float crossY = localPrev.y + (localCur.y - localPrev.y) * t;

                // 中心からの距離の2乗を計算
                float distSq = crossX * crossX + crossY * crossY;

                // リングの半径内を通過していればイベント発火！
                if (distSq <= BaseRadius * BaseRadius)
                {
                    Debug.Log($"<color=lime>[CourseRing]</color> <b>{target.name}</b> が <b>{gameObject.name}</b> を通過しました！");
                    
                    // 「誰が通ったか(target)」をイベントで渡す
                    OnPassed?.Invoke(target);
                }
            }

            // 位置を更新
            _previousPositions[target] = currentPosition;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 親オブジェクトのスケール（拡大縮小）や回転をギズモに完全に反映させる
        UnityEditor.Handles.matrix = transform.localToWorldMatrix;
        UnityEditor.Handles.color = Color.cyan;
        
        // Z=0のローカル平面に半径 1.0 の円を描く（Scaleの値がそのままワールド空間の半径になります）
        UnityEditor.Handles.DrawWireDisc(Vector3.zero, Vector3.forward, BaseRadius);
    }
#endif
}
