// レーサーにかかる、時間付きの効果（減速・左右移動が遅い・ブースト禁止など）の窓口。コースギミック・妨害弾から使う。
// sourceは「誰がかけた効果か」の目印（ふつうはギミック・妨害弾の種類の設定など、かけた側のオブジェクト自身）。
// ・同じsourceが同じ種類の効果をかけ直したら、上書きする（重ねがけしない。秒数も最初から数え直す）
// ・別のsourceの効果が重なったら、一番強いもの（倍率なら一番小さいもの）を使う
// ・durationにUntilRemovedを渡すと、Removeするまで続く（減速エリアのように「入っている間」の効果）
// RacerEffectsが実装する。Tick・Resetはレーサー（RacerManagerBase）だけが呼ぶので、ここには出さない。
public interface IRacerEffects
{
    // 最高速度に掛ける倍率（0～1）。効果が無ければ1。
    float MaxSpeedMultiplier { get; }

    // 左右（上下左右）の移動の速さ・加速度に掛ける倍率（0～1）。効果が無ければ1。
    float LateralMultiplier { get; }

    // ブーストを発動できない間か（発動中なら、レーサーがブーストを止める）。
    bool IsBoostLocked { get; }

    // multiplier：最高速度の倍率。fadeDuration：倍率を切り替える秒数（急に速度が変わらないようにする。外れたときも同じ秒数で戻す）。
    void SetMaxSpeedMultiplier(object source, float multiplier, float duration, float fadeDuration = 0f);

    void SetLateralMultiplier(object source, float multiplier, float duration, float fadeDuration = 0f);

    void LockBoost(object source, float duration);

    // sourceがかけた効果を、種類に関係なくすべて外す。
    void Remove(object source);
}
