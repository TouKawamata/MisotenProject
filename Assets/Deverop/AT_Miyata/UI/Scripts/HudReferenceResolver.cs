using UnityEngine;

/// <summary>
/// HUDのプレハブはシーン上のオブジェクトを参照として持てないので、
/// インスペクターで未設定のときに実行時に探すための共通処理。
/// </summary>
public static class HudReferenceResolver
{
    public static RaceManager FindRaceManager()
    {
        return Object.FindFirstObjectByType<RaceManager>();
    }

    // プレイヤー（PlayerManager）と同じオブジェクトに付いているコンポーネントを探す。
    // CPUも BoostController / ShieldController を持つので、必ず PlayerManager を経由する。
    public static T FindPlayerComponent<T>() where T : Component
    {
        PlayerManager player = Object.FindFirstObjectByType<PlayerManager>();
        return player != null ? player.GetComponentInChildren<T>(true) : null;
    }
}
