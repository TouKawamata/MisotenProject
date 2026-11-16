// Build Settingsに並べるシーンの名前。シーンを読み込むときは文字列を直接書かず、ここの定数を使う（打ち間違いを防ぐため）。
// 遷移の順：Title → Entry（キャリブレーション）→ Game → Result → Title
public static class SceneNames
{
    public const string Title = "TitleScene";

    public const string Entry = "EntryScene";

    public const string Game = "GameScene";

    public const string Result = "ResultScene";
}
