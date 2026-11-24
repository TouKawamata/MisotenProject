namespace AT_Murayama.PauseMenu
{
    /// <summary>
    /// ポーズメニューが開かれた理由
    /// </summary>
    public enum EPauseReason
    {
        /// <summary>プレイヤーのEsc操作等による通常一時停止（Esc／戻るで復帰可能）</summary>
        UserRequest = 0,

        /// <summary>カメラ接続切れ等による外部からの強制停止（Esc復帰不可、キャリブレーション経由で再開）</summary>
        TrackingLost = 1
    }
}
