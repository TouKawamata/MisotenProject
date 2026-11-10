// 固いギミック（CourseGimmickBase.IsSolid＝true）に当たったとき、レーサーの体をどう動かすか（CourseGimmickBase.OnRacerHitの戻り値）。
// 体を動かす物理はRacerGimmickSensorが共通で行い、ギミックはどれにするかを返すだけにする。
public enum EGimmickBlockResponse
{
    // 面へ向かう動きだけを消して、面に沿って滑る（摩擦力0）
    Slide,

    // 当たった位置で止める（後ろへ下げるなどは、ギミックがIGimmickRacerで行う）
    Stop,

    // 体を止めずに通す（ブーストで壊した、など）
    PassThrough
}
