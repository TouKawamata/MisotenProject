using System;
using UnityEngine;

// 9分割の移動。コースの断面（Splineに垂直な、動ける範囲の円）を9つのマスに分け、入力の向きで行き先のマスを選ぶ。
// マスの番号（画面から見て）：
//   1 2 3
//   4 5 6
//   7 8 9
// 入力なし＝5（中央）、右上＝3、下＝8 のように、入力の向き（45度刻みの8方向）で周りの8マスを選ぶ。
// 周りの8マスは円に沿って置く（どの向きへも中心から同じ距離。トンネル半径 × OuterRadiusRatio）。
// 実際にマスへ動かすのは PlayerFlightController（SetNineZone で渡す）。ここは「どのマスか」と「目標の位置」を決めるだけ。
// トラッキングの入力は細かく揺れるので、マスの切り替えには幅（ヒステリシス）を持たせ、境目でちらつかないようにする。
[Serializable]
public class NineZoneMovement {
	// 周りの8マスの向きの刻み（度）
	private const float SectorDegrees = 45f;

	// 向きの番号（右＝0から反時計回りに45度ずつ）→ マスの番号
	private static readonly int[] SectorToZone = { 6, 3, 2, 1, 4, 7, 8, 9 };

	// 中央のマスの番号
	public const int CenterZone = 5;

	[Tooltip("周りの8マスの、中心からの距離（トンネル半径に対する比率）。1で壁ぎわ")]
	[Range(0f, 1f)]
	[SerializeField] private float _outerRadiusRatio = 0.7f;

	[Tooltip("中央にいるとき、入力の大きさがこれ以上になったら周りのマスへ移る")]
	[Range(0f, 1f)]
	[SerializeField] private float _enterThreshold = 0.5f;

	[Tooltip("周りのマスにいるとき、入力の大きさがこれを下回ったら中央へ戻る（Enter Threshold より小さくして、境目でのちらつきを防ぐ）")]
	[Range(0f, 1f)]
	[SerializeField] private float _exitThreshold = 0.35f;

	[Tooltip("周りのマスにいるとき、隣のマスへ移るのに、境目（22.5度）からさらに何度回す必要があるか（ちらつき防止）")]
	[Range(0f, 22.5f)]
	[SerializeField] private float _sectorHysteresisDegrees = 7.5f;

	[Tooltip("目標のマスへ寄せる強さ（1/秒）。目標の速さ＝残りの距離 × この値（上限は上下左右の最高速度）。大きいほど速く着くが、止まり際が硬くなる")]
	[Min(0.1f)]
	[SerializeField] private float _approachGain = 5f;

	[Tooltip("目標のマスとの距離（m）がこれ以下なら、着いたとみなして寄せるのをやめる")]
	[Min(0f)]
	[SerializeField] private float _arrivalTolerance = 0.05f;

	// 今の向きの番号（0～7）。中央にいるときは -1。
	[NonSerialized] private int _sector = -1;

	// 今のマスの番号（1～9）。
	public int CurrentZone => _sector < 0 ? CenterZone : SectorToZone[_sector];

	// 目標の位置（コースの中心から、x＝右・y＝上。トンネル半径に対する比率）。
	public Vector2 TargetOffsetRatio {
		get {
			// 判定：中央にいるか（中央ならコースの中心が目標）
			if (_sector < 0) {
				return Vector2.zero;
			}

			float angle = _sector * SectorDegrees * Mathf.Deg2Rad;
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _outerRadiusRatio;
		}
	}

	public float ApproachGain => _approachGain;

	public float ArrivalTolerance => _arrivalTolerance;

	// 中央へ戻す（スタートグリッドへの配置・リトライ用）。
	public void Reset() {
		_sector = -1;
	}

	// 移動の入力（x＝右、y＝上、大きさ0～1）から、今のマスを決め直す。毎Tick呼ぶ。
	public void SelectZone(Vector2 move) {
		float magnitude = move.magnitude;
		// 判定：中央にいるか（中央からは Enter、周りからは Exit のしきい値で比べ、境目でのちらつきを防ぐ）
		float threshold = _sector < 0 ? _enterThreshold : Mathf.Min(_exitThreshold, _enterThreshold);
		// 判定：入力がしきい値より小さいか（小さければ中央）
		if (magnitude < threshold || magnitude < 0.0001f) {
			_sector = -1;
			return;
		}

		float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
		int nearest = ToSector(Mathf.RoundToInt(angle / SectorDegrees));
		// 判定：中央から出たところか（それなら一番近い向きのマスへ）
		if (_sector < 0) {
			_sector = nearest;
			return;
		}

		// 周りのマスにいるときは、今の向きとの差が境目（22.5度）＋幅を超えたときだけ、隣のマスへ移る。
		float difference = Mathf.Abs(Mathf.DeltaAngle(angle, _sector * SectorDegrees));
		// 判定：今の向きから十分に離れたか
		if (difference > SectorDegrees * 0.5f + _sectorHysteresisDegrees) {
			_sector = nearest;
		}
	}

	// 向きの番号を 0～7 にそろえる（-4 → 4 など）。
	private static int ToSector(int index) {
		int count = SectorToZone.Length;
		return ((index % count) + count) % count;
	}
}
