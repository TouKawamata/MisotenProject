using UnityEngine;

/// <summary>
/// 最初のカウントダウンが終わってレースが始まるまで、CanvasGroup のアルファを0にして隠す。
/// 一度表示したら、ポーズ復帰時の再カウントダウン中も表示を続ける。
/// エディタ上（再生していないとき）は調整しやすいよう何もしない。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ShowAfterRaceStart : MonoBehaviour
{
    [SerializeField] private RaceManager _raceManager;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
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
        if (_raceManager == null)
        {
            return;
        }

        ERaceState state = _raceManager.State;
        if (state != ERaceState.Racing && state != ERaceState.Finished)
        {
            return;
        }

        _canvasGroup.alpha = 1f;
        enabled = false;
    }
}
