using System;
using UnityEngine;

// 視界反転の向き（妨害弾など）。左右と上下が両方かかっていれば、両方反転する（180度回したのと同じ見え方になる）。
// Playerは画面を反転し（ViewFlipPresentation）、CPUは同じ軸の移動の入力を反転する（RacerManagerBase）。
[Flags]
public enum EViewFlip
{
    [InspectorName("なし")]
    None = 0,

    [InspectorName("左右")]
    Horizontal = 1 << 0,

    [InspectorName("上下")]
    Vertical = 1 << 1
}
