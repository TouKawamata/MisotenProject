// ShortcutZoneTriggerが担う役割。
public enum ShortcutZoneRole
{
    // 警告用の外側エリア。UI表示（「上昇しろ」等）のフックにのみ使う。
    Warning,

    // 吸着発動の対象となる内側エリア。ここに入っている間だけPlayer側の入力で吸着が有効になる。
    Activation
}
