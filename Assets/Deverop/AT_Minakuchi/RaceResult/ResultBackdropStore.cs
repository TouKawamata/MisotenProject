using UnityEngine;

// 表示用の一枚だけを引き継ぐ。レースの数値とは別に管理する。
public static class ResultBackdropStore
{
    private static Texture2D _texture;
    public static void Set(Texture2D texture)
    {
        Clear();
        _texture = texture;
    }
    public static Texture2D Take()
    {
        Texture2D texture = _texture;
        _texture = null;
        return texture;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        if (_texture != null) Object.Destroy(_texture);
        _texture = null;
    }
}
