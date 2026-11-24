using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// レース中のプレイヤー順位を数字画像（UI_number1～8）で表示する。
/// RankView（テキスト表示）の画像版。
/// </summary>
public class RankImageView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RaceManager _raceManager;

    [Header("UI")]
    [Tooltip("現在順位を表示するImage")]
    [SerializeField] private Image _rankImage;

    [Tooltip("全体の人数を表示するImage（未設定なら人数は表示しない）")]
    [SerializeField] private Image _totalImage;

    [Tooltip("順位と人数の間の区切り（「/」の画像やテキストなど）。順位表示中のみ有効化される。任意。")]
    [SerializeField] private GameObject _separator;

    [Tooltip("数字のスプライト。要素0 = 1, 要素1 = 2 ...（順位と人数で共用）")]
    [SerializeField] private Sprite[] _numberSprites = new Sprite[8];

    [Tooltip("ONならスプライト切替時にImageをスプライトの元サイズに合わせる。\n" +
             "OFFならRectTransformのサイズ内でアスペクト比を保って表示する。")]
    [SerializeField] private bool _useNativeSize = false;

    private int _previousRank = -1;
    private int _previousRacerCount = -1;

    // 最初のカウントダウンが終わってレースが始まったか。
    // 一度trueになったら、ポーズ復帰時の再カウントダウン中も表示を続ける。
    private bool _hasRaceStarted;

    private void Awake()
    {
        // レース開始（カウントダウン終了）までは何も表示しない
        InitImage(_rankImage);
        InitImage(_totalImage);
        if (_separator != null)
        {
            _separator.SetActive(false);
        }
    }

    private void Start()
    {
        // プレハブから置いたときなど、未設定なら探す
        if (_raceManager == null)
        {
            _raceManager = HudReferenceResolver.FindRaceManager();
        }
    }

    private void LateUpdate()
    {
        // RaceManagerの初期化前はPlayerDataがnullになるため更新しない
        if (_raceManager == null || _raceManager.PlayerData == null)
        {
            return;
        }

        if (!_hasRaceStarted)
        {
            ERaceState state = _raceManager.State;
            if (state != ERaceState.Racing && state != ERaceState.Finished)
            {
                return;
            }

            _hasRaceStarted = true;
        }

        int currentRank = _raceManager.PlayerData.CurrentRank;
        int racerCount = _raceManager.Racers.Count;
        if (currentRank == _previousRank && racerCount == _previousRacerCount)
        {
            return;
        }

        _previousRank = currentRank;
        _previousRacerCount = racerCount;

        bool rankShown = SetNumber(_rankImage, currentRank);
        bool totalShown = SetNumber(_totalImage, racerCount);

        // 順位と人数の両方が出ているときだけ区切りを出す
        if (_separator != null)
        {
            _separator.SetActive(rankShown && totalShown);
        }

        // 人数だけ出ていると不自然なので、順位が未確定なら人数も隠す
        if (!rankShown && _totalImage != null)
        {
            _totalImage.enabled = false;
        }
    }

    private static void InitImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.preserveAspect = true;
        image.enabled = false;
    }

    /// <summary>
    /// 指定の数字スプライトをImageに設定する。表示できたらtrue。
    /// </summary>
    private bool SetNumber(Image image, int number)
    {
        if (image == null)
        {
            return false;
        }

        int index = number - 1;
        bool hasSprite = _numberSprites != null &&
                         index >= 0 && index < _numberSprites.Length &&
                         _numberSprites[index] != null;

        image.enabled = hasSprite;
        if (!hasSprite)
        {
            return false;
        }

        image.sprite = _numberSprites[index];
        if (_useNativeSize)
        {
            image.SetNativeSize();
        }

        return true;
    }
}
