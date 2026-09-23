// ShortcutZoneの発動エリア内で、何をショートカットの入力として扱うか
public enum ShortcutTriggerMethod
{
    // Eキー（IRaceInput.ShortcutPressed）
    KeyPress,

    // Horizontalの絶対値が閾値以上になった瞬間
    HorizontalThreshold
}
