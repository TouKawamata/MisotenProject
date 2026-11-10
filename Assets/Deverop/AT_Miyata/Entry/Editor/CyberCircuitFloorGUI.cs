using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Entry/CyberCircuitFloor シェーダー用のインスペクター。
/// 各パラメータを日本語名で表示し、マウスオーバーで効果の説明（ツールチップ）を出す。
/// </summary>
public class CyberCircuitFloorGUI : ShaderGUI
{
    // (プロパティ名, 表示名, ツールチップ)
    private struct Item
    {
        public string Name, Label, Tooltip;
        public Item(string name, string label, string tooltip) { Name = name; Label = label; Tooltip = tooltip; }
    }

    private struct Section
    {
        public string Title, Description;
        public Item[] Items;
        public Section(string title, string description, params Item[] items) { Title = title; Description = description; Items = items; }
    }

    private static readonly Section[] Sections =
    {
        new Section("模様の貼り方",
            "タイル模様を床にどう敷き詰めるか。",
            new Item("_UseWorldSpace", "ワールド座標で敷く",
                "ON：ワールドの XZ 座標でタイルを敷きます。オブジェクトを拡大縮小・移動してもタイルの大きさは変わらず、複数の床オブジェクトの模様が継ぎ目なくつながります。\n" +
                "OFF：メッシュの UV で敷きます。床が傾いている・曲がっている場合や、オブジェクトと一緒に模様を動かしたい場合に使います。"),
            new Item("_TileSize", "タイルの大きさ",
                "タイル1枚の一辺の長さ。ワールド座標モードではメートル、UV モードでは UV 単位です。\n" +
                "大きくするとタイルも回路も全体に大きくなります。溝の幅などの「タイル比」の値も一緒に拡大されます。")),

        new Section("タイルの表面",
            "タイル本体の色と質感。",
            new Item("_BaseColor", "タイルの色",
                "タイル表面の基本色。暗い色ほど映り込みと発光が際立ちます。"),
            new Item("_TileVariation", "タイルごとの明るさのばらつき",
                "タイル1枚ごとに明るさと光沢をランダムに変える量。\n0：全タイル同じ　大きいほど：パッチワーク状に濃淡が出ます。"),
            new Item("_Metallic", "金属感",
                "0：非金属　1：金属。上げるほど映り込みにタイル色が乗り、金属らしくなります。"),
            new Item("_Smoothness", "なめらかさ（光沢）",
                "上げるほど鏡のようにくっきり映り込みます。下げるとぼやけたつや消しになります。\n映り込みを出すにはシーンに Reflection Probe が必要です。"),
            new Item("_GrainScale", "粒状ノイズの細かさ",
                "表面の細かいざらつき（光沢のムラ）の模様の細かさ。タイル1枚あたりの模様の繰り返し回数です。\n大きいほど細かい粒になります。"),
            new Item("_GrainStrength", "粒状ノイズの強さ",
                "ざらつきによる光沢・色のムラの強さ。\n0：均一なツルツル面　上げるほど：使い込まれた金属のようなムラが出ます。")),

        new Section("目地（タイルの継ぎ目）",
            "タイル同士の境目の溝。",
            new Item("_SeamWidth", "目地の幅",
                "タイルの境目の暗い溝の幅（タイルの一辺に対する割合）。0 で目地なし。"),
            new Item("_BevelWidth", "面取りの幅",
                "目地のふちの斜面（面取り）の幅（タイル比）。広いほど角が丸く、ハイライトが太く出ます。"),
            new Item("_BevelStrength", "面取りの強さ",
                "面取り部分の傾きの強さ。上げるほどふちにライトや映り込みのハイライトが乗り、タイルが立体的に見えます。"),
            new Item("_SeamColor", "目地の色",
                "目地の溝の底の色。"),
            new Item("_SeamGlowColor", "目地の発光色",
                "目地のふちがほんのり光るときの色（HDR）。"),
            new Item("_SeamGlow", "目地の発光の強さ",
                "目地のふちの発光量。0 で発光なし。上げるとタイルの輪郭が光るグリッドになります。")),

        new Section("回路の溝",
            "全タイルがつながった回路状の溝と、その溝の中の常時点灯の光。",
            new Item("_CircuitColor", "溝の光の色",
                "溝の底で常に光っている色（HDR）。流れる光とは別です。"),
            new Item("_CircuitIntensity", "溝の光の強さ",
                "溝の底の常時点灯の明るさ。0 にすると溝は暗く、流れる光だけが見えます。"),
            new Item("_GrooveWidth", "溝の底の幅（半分）",
                "光る溝の底の、中心から端までの幅（タイル比）。光る線の太さになります。"),
            new Item("_GrooveBevel", "溝の壁の幅",
                "溝の底から床面へ立ち上がる斜面の幅（タイル比）。広いほど溝全体が太く見えます。"),
            new Item("_GrooveDepth", "溝の深さ",
                "溝の壁の傾きの強さ（法線の強さ）。上げるほど深く彫られて見え、壁にハイライトが乗ります。0 で平らな線になります。"),
            new Item("_GrooveColor", "溝の壁の色",
                "溝の壁の色。暗いほど彫り込みが深く見えます。"),
            new Item("_GrooveWallGlow", "壁への光の漏れ",
                "溝の底の光が壁を照らす量。\n0：底だけが細く光る　1：溝全体が光る（線が太く見える）"),
            new Item("_HubJitter", "ハブ位置のばらつき",
                "各タイル内の配線の中継点（ハブ）を、タイル中心からずらす量。\n0：全ハブがタイル中心で規則的　大きいほど：不規則な配線になります。"),
            new Item("_PortJitter", "接続点のばらつき",
                "タイルの境目で隣のタイルと線がつながる位置を、辺の中央からずらす量。\n大きいほど配線が斜めに大きく曲がり、回路らしくなります。"),
            new Item("_VerticalKeepChance", "縦方向の接続の割合",
                "上下のタイル同士をつなぐ線を残す確率。\n1：全方向につながる格子状　下げると：横方向の線が主体になります。\n（4列に1本は必ず残るので、ネットワークが途切れることはありません）"),
            new Item("_SpurChance", "枝の出やすさ",
                "ハブから伸びる行き止まりの枝が出る確率（1タイルあたり最大3本）。\n枝の先には丸い端子が付き、光が届くと光ります。0 で枝なし。"),
            new Item("_PadRadius", "端子の大きさ",
                "枝の先とハブにある丸い端子（リング）の半径（タイル比）。0 で端子なし。")),

        new Section("流れる光",
            "溝に沿って走る光の粒。",
            new Item("_PulseColor", "流れる光の色",
                "溝を走る光の粒の色（HDR）。"),
            new Item("_PulseIntensity", "流れる光の強さ",
                "光の粒の明るさ。Bloom を使う場合、上げるほど強くにじみます。"),
            new Item("_PulseSpeed", "速さ",
                "光の粒の移動速度（ワールド単位/秒）。"),
            new Item("_PulseSpeedVariation", "速さのばらつき",
                "経路ごとに速さをランダムに変える量。\n0：全部同じ速さ　1：0〜2倍の範囲でばらつきます。"),
            new Item("_PulseChance", "光の出現率",
                "光の粒1つ1つが実際に出現する確率。下げるほど光の数が減ります。\n粒ごとに抽選するので、同じ経路でも走る時と走らない時があります。"),
            new Item("_PulseSpacing", "光の間隔",
                "同じ経路上の光の粒同士の間隔（ワールド単位）。\n経路より長くすると、1つ通過した後しばらく何も走らない時間ができ、光の数が減ります。"),
            new Item("_SpawnRandomness", "発生点のランダムさ",
                "光が経路のどこから湧き出るか。\n0：必ず経路の始点（ハブ）から出る　1：経路のどこからでも湧き出る"),
            new Item("_DespawnRandomness", "消滅点のランダムさ",
                "光が経路のどこで消えるか。\n0：必ず終点まで走り切る　1：途中のどこででも消える\n（終点まで届かない光は枝の先の端子を光らせません）"),
            new Item("_SpawnFade", "出現・消滅のフェード",
                "光が湧き出る・消えるときにフェードする長さ（ワールド単位）。小さいとパッと出てパッと消えます。"),
            new Item("_PulseLength", "尾の長さ",
                "光の粒の後ろに伸びる尾の長さ（ワールド単位）。長いほど流れ星のようになります。"),
            new Item("_PulseHead", "先端のやわらかさ",
                "光の先端のぼかし幅（ワールド単位）。0 に近いほど先端がくっきりします。遠くでちらつく場合は少し上げてください。")),

        new Section("タイルの発光",
            "一部のタイル全体がほのかに光る演出。",
            new Item("_TileGlowColor", "タイル発光の色",
                "光るタイルの色（HDR）。"),
            new Item("_TileGlowChance", "光るタイルの割合",
                "タイル全体が光る確率。0 で光るタイルなし。"),
            new Item("_TileGlowIntensity", "タイル発光の強さ",
                "光るタイルの明るさ。")),
    };

    private static readonly Dictionary<string, bool> Foldouts = new Dictionary<string, bool>();

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.HelpBox(
            "各項目にマウスを乗せると説明が表示されます。\n" +
            "・床の映り込みには Reflection Probe（Bake）が必要です。\n" +
            "・発光を光らせるには Volume の Bloom を有効にしてください。",
            MessageType.Info);

        foreach (var section in Sections)
        {
            if (!Foldouts.TryGetValue(section.Title, out bool open)) open = true;

            EditorGUILayout.Space(4);
            open = EditorGUILayout.BeginFoldoutHeaderGroup(open, new GUIContent(section.Title, section.Description));
            Foldouts[section.Title] = open;

            if (open)
            {
                EditorGUI.indentLevel++;
                foreach (var item in section.Items)
                {
                    MaterialProperty prop = FindProperty(item.Name, properties, false);
                    if (prop == null) continue;
                    materialEditor.ShaderProperty(prop, new GUIContent(item.Label, item.Tooltip));
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("詳細設定", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }
}
