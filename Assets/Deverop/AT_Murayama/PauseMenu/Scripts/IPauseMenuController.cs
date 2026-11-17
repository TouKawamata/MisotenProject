namespace AT_Murayama.PauseMenu
{
    /// <summary>
    /// ポーズメニューの外部操作インターフェース（トラッキング側やテストからの呼び出し口）
    /// </summary>
    public interface IPauseMenuController
    {
        bool IsOpen { get; }
        bool IsCalibrating { get; }
        EPauseReason CurrentReason { get; }

        void Open(EPauseReason reason = EPauseReason.UserRequest);
        void Close();
    }
}
