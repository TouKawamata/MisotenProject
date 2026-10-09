using UnityEngine;

// TrackingRaceInputの判定に使う調整用パラメータ。
// 実機（トラッキング＋プレイヤーの旋回性能）を見ながら対面で調整することを想定し、
// 角度やしきい値はInspectorから変更できるようにしている。
[System.Serializable]
public class TrackingRaceInputSettings
{
    [Header("Horizontal（体の左右の傾き）")]
    [Tooltip("この角度（度）の傾きでHorizontalが±1.0に達する")]
    public float maxTiltAngle = 25f;

    [Tooltip("この角度（度）未満の傾きは0として扱う（直立時のノイズ対策）")]
    public float tiltDeadZoneAngle = 2f;

    [Tooltip("1.0で角度に比例（線形）。1より小さいと中間域で早めに値が出やすくなる")]
    [Range(0.3f, 1f)]
    public float tiltResponseCurve = 0.6f;

    [Tooltip("値の変化を滑らかにする時定数（秒）。0で平滑化なし")]
    public float horizontalSmoothingTime = 0.1f;

    [Tooltip("ONで左右の符号を反転する（鏡像設定やカメラ向きに応じた調整用）")]
    public bool invertHorizontal = false;

    [Header("Boost（空手の構え：両手を腰の横で引く）")]
    [Tooltip("判定に使う高さの位置。0で腰（股関節）の高さ、1で肩の高さ（MediaPipeの腰は股関節付近のため、0のままだと腕を下に伸ばしただけで誤検知しやすい）")]
    [Range(0f, 1f)]
    public float boostHeightRatio = 0.5f;

    [Tooltip("手首とboostHeightRatioで決めた高さとの差の許容量（胴の長さに対する比率）")]
    public float boostHeightToleranceRatio = 0.35f;

    [Tooltip("手首と同じ側の腰の水平距離の許容量（胴の長さに対する比率）")]
    public float boostHorizontalToleranceRatio = 0.55f;

    [Header("Shield（片手を胸の前で水平に構える）")]
    [Tooltip("肩からどれだけ下げた高さを胸の高さとするか（胴の長さに対する比率）")]
    [Range(0f, 0.5f)]
    public float chestHeightOffsetRatio = 0.15f;

    [Tooltip("手首と胸の高さの差の許容量（胴の長さに対する比率）")]
    public float shieldHeightToleranceRatio = 0.3f;

    [Tooltip("肘と手首の高さの差の許容量（腕が水平かどうかの判定。胴の長さに対する比率）")]
    public float shieldForearmLevelToleranceRatio = 0.3f;

    [Tooltip("手首が肘よりも体の中心側にどれだけ寄っていれば良いかのマージン（胴の長さに対する比率）")]
    public float shieldInwardMarginRatio = 0.1f;

    [Header("Shortcut（バンザイ：両手を頭上に上げる）")]
    [Tooltip("手首が鼻より高いと判定するための最小マージン（胴の長さに対する比率）")]
    public float shortcutRaiseMarginRatio = 0.15f;

    [Header("共通")]
    [Tooltip("この信頼度を下回るランドマークは未検出として扱う")]
    [Range(0f, 1f)]
    public float minVisibility = 0.5f;
}
