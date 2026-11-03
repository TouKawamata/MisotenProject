#ifndef COURSE_LINE_ARROW_INCLUDED
#define COURSE_LINE_ARROW_INCLUDED

// 高速で再現性のある2Dハッシュ関数（0.0 〜 1.0）
float CourseLineArrow_Hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// 2Dハッシュ（2つの独立した乱数 0.0 〜 1.0 を返す）
float2 CourseLineArrow_Hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// 1つの矢印（> / => / ー）のマスク（0〜1）を距離フィールド（SDF）で描画する
// tipPos = (0, 0) を矢印の先端とし、+X方向を向く
float CourseLineArrow_DrawSingle(
    float2 p,
    float headSize,
    float tailLength,
    float halfThick,
    float tailFade,
    float aaWidth)
{
    float ay = abs(p.y);
    float mask = 0.0;

    // 1. 先端のV字ヘッド（>）
    if (headSize > 0.0005)
    {
        // 線分 (0, 0) -> (-headSize, headSize) への最短距離
        float t = saturate((-p.x + ay) / max(0.001, 2.0 * headSize));
        float2 closest = float2(-t * headSize, t * headSize);
        float dHead = length(float2(p.x, ay) - closest);
        float headMask = smoothstep(halfThick + aaWidth, max(0.0, halfThick - aaWidth), dHead);
        mask = max(mask, headMask);
    }

    // 2. 後ろに伸びる棒・軌跡（=> の - 部分）
    if (tailLength > 0.0005)
    {
        // 線分 (-tailLength, 0) -> (0, 0) への最短距離
        float cx = clamp(p.x, -tailLength, 0.0);
        float dTail = length(float2(p.x - cx, ay));
        float tailShape = smoothstep(halfThick + aaWidth, max(0.0, halfThick - aaWidth), dTail);

        // 後ろに向かって薄くなるフェード（tailFade = 0 なら均一、1 なら先端1→末端0）
        float tailProgress = saturate((p.x + tailLength) / max(0.001, tailLength));
        float fadeFactor = lerp(1.0, tailProgress, saturate(tailFade));

        mask = max(mask, tailShape * fadeFactor);
    }

    return mask;
}

// 1レイヤー分のレーン分割＆ランダム矢印計算
float CourseLineArrow_EvalLayer(
    float2 posMeters,
    float bandHeight,
    float time,
    float baseSpeed,
    float densityX,
    float laneCount,
    float baseHeadSize,
    float baseTailLength,
    float baseThickness,
    float tailFade,
    float aaWidth,
    float layerSeed)
{
    float lanes = max(1.0, round(laneCount));
    float laneHeight = bandHeight / lanes;
    float laneHalfH = 0.5 * laneHeight;

    // レイヤー2は半レーン分ずらして間の高さを埋める（上下端で矢印が見切れないよう範囲外レーンは描画しない）
    float yOffset = (layerSeed > 1.5) ? laneHalfH : 0.0;
    float activeLanes = (layerSeed > 1.5) ? max(1.0, lanes - 1.0) : lanes;

    float shiftedY = posMeters.y - yOffset;
    float laneId = floor(shiftedY / laneHeight);
    if (laneId < 0.0 || laneId >= activeLanes)
    {
        return 0.0;
    }

    float localY = (frac(shiftedY / laneHeight) - 0.5) * laneHeight;

    // レーン固有の乱数（同じレーン内では速度と周期が一定なので矢印が絶対にちぎれない）
    float2 laneRng = CourseLineArrow_Hash22(float2(laneId, layerSeed * 19.19 + 3.7));
    float speedMul = lerp(0.55, 1.50, laneRng.x);
    float periodMul = lerp(0.80, 1.45, laneRng.y);

    float speed = baseSpeed * speedMul;
    float basePeriodX = 1.0 / max(0.005, densityX);
    float periodX = max(0.5, basePeriodX * periodMul);

    // 進行方向（+UV.x）へスクロール（scrollX は移動する矢印の上で常に一定値になる）
    float scrollX = posMeters.x - time * speed + laneRng.x * 113.0 + layerSeed * 47.3;
    float cellId = floor(scrollX / periodX);
    float cellLocalX = (frac(scrollX / periodX) - 0.5) * periodX;

    // 個別の矢印（cellId × laneId）ごとの乱数（サイズ・高さ微ズレ・横位置ズレ・間引き）
    float2 cellSeed = float2(cellId * 1.17 + layerSeed * 13.0, laneId * 7.31 + layerSeed * 29.0);
    float2 arrowRng = CourseLineArrow_Hash22(cellSeed);
    float spawnRng = CourseLineArrow_Hash21(cellSeed + 51.7);

    // まばら感を出すため、約25%のセルは空欄にする
    if (spawnRng < 0.25)
    {
        return 0.0;
    }

    // ランダムなサイズ倍率（0.6倍 〜 1.35倍）
    float sizeScale = lerp(0.60, 1.35, arrowRng.x);

    float halfThick = max(0.005, baseThickness * 0.5 * sizeScale);
    // レーンの上下境界で矢印が欠けないように最大高さを安全クランプ
    float maxAllowedHead = max(0.0, laneHalfH - halfThick - aaWidth * 1.5);
    float headSize = min(baseHeadSize * sizeScale, maxAllowedHead);

    // セルの左右境界で欠けないように長さを安全クランプ
    float maxAllowedTotalX = max(0.05, periodX * 0.85 - halfThick * 2.0);
    float tailLength = min(baseTailLength * sizeScale, maxAllowedTotalX);
    float totalLength = max(headSize, tailLength);

    // レーン内で上下にランダムに高さをずらす（矢印が欠けない余白の範囲内）
    float yMargin = max(0.0, laneHalfH - headSize - halfThick - aaWidth);
    float yJitter = (arrowRng.y * 2.0 - 1.0) * yMargin;

    // セル内で前後にランダムにずらす（矢印が欠けない余白の範囲内）
    float xMargin = max(0.0, (periodX * 0.5) - (totalLength * 0.5) - halfThick - aaWidth);
    float xJitter = ((spawnRng - 0.25) / 0.75 * 2.0 - 1.0) * xMargin;

    // 矢印の中心をセル中央に合わせつつランダムオフセットを適用
    float2 p = float2(cellLocalX - xJitter - totalLength * 0.5, localY - yJitter);

    return CourseLineArrow_DrawSingle(p, headSize, tailLength, halfThick, tailFade, aaWidth);
}

// Shader Graph Custom Function エントリポイント (Float精度)
// Name: CourseLineArrow
void CourseLineArrow_float(
    float2 UV,
    float BandHeight,
    float Time,
    float ArrowSpeed,
    float2 ArrowDensity,
    float HeadSize,
    float TailLength,
    float LineThickness,
    float TailFade,
    out float Out)
{
    float safeHeight = max(0.01, BandHeight);
    float2 posMeters = float2(UV.x, UV.y * safeHeight);

    // ピクセル微分から適切なアンチエイリアス幅を算出（遠方でのジャギ防止）
    float aaWidth = clamp(fwidth(posMeters.y) * 1.25, 0.015, 0.25);

    // 高さと密度が異なる2レイヤーを合成し、ランダムな高さ・サイズ・速度のまばらな流れを作る
    float layer1 = CourseLineArrow_EvalLayer(
        posMeters, safeHeight, Time,
        ArrowSpeed, ArrowDensity.x, ArrowDensity.y,
        HeadSize, TailLength, LineThickness, TailFade, aaWidth, 1.0);

    float layer2 = CourseLineArrow_EvalLayer(
        posMeters, safeHeight, Time,
        ArrowSpeed * 1.18, ArrowDensity.x * 0.82, ArrowDensity.y,
        HeadSize * 0.85, TailLength * 0.85, LineThickness * 0.9, TailFade, aaWidth, 2.0);

    // 矢印同士が追い越しざまに重なっても白飛びしないよう max で頭打ち合成
    Out = max(layer1, layer2);
}

// Shader Graph Custom Function エントリポイント (Half精度)
void CourseLineArrow_half(
    half2 UV,
    half BandHeight,
    half Time,
    half ArrowSpeed,
    half2 ArrowDensity,
    half HeadSize,
    half TailLength,
    half LineThickness,
    half TailFade,
    out half Out)
{
    float outFloat = 0.0;
    CourseLineArrow_float(
        float2(UV),
        float(BandHeight),
        float(Time),
        float(ArrowSpeed),
        float2(ArrowDensity),
        float(HeadSize),
        float(TailLength),
        float(LineThickness),
        float(TailFade),
        outFloat);
    Out = half(outFloat);
}

#endif // COURSE_LINE_ARROW_INCLUDED
