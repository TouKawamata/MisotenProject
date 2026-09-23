// ショートカット判定エリア（ShortcutZone）から、発動エリア内にいるかどうかを通知される側のインターフェース。
public interface IShortcutZoneReceiver
{
    void SetInShortcutZone(ShortcutZone zone);
}
