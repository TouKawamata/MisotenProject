using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer.Unity;

// RaceManagerのCountdownChangedを購読し、3, 2, 1, GO をテキストで表示するだけの簡易UI（演出なし）。
// GOは一定時間表示したあと非表示にする。Initialize/DisposeはGameLifetimeScope（VContainer）から呼び出される。
public class CountdownUI : MonoBehaviour, IInitializable, IDisposable
{
    [SerializeField] private RaceManager _raceManager;
    [SerializeField] private TMP_Text _countdownText;

    [Tooltip("GOを表示し続ける秒数")]
    [SerializeField] private float _goDisplayDuration = 1f;

    private CancellationTokenSource _cancellationTokenSource;

    public void Initialize()
    {
        if (_raceManager == null || _countdownText == null)
        {
            Debug.LogError("CountdownUI: RaceManager / Countdown Text が未設定です", this);
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _countdownText.gameObject.SetActive(false);
        _raceManager.CountdownChanged += OnCountdownChanged;
    }

    public void Dispose()
    {
        if (_raceManager != null)
        {
            _raceManager.CountdownChanged -= OnCountdownChanged;
        }

        if (_cancellationTokenSource == null)
        {
            return;
        }

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    private void OnCountdownChanged(int count)
    {
        _countdownText.gameObject.SetActive(true);

        if (count > 0)
        {
            _countdownText.text = count.ToString();
            return;
        }

        _countdownText.text = "GO!";
        HideAfterDelayAsync(_cancellationTokenSource.Token).Forget();
    }

    private async UniTaskVoid HideAfterDelayAsync(CancellationToken token)
    {
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_goDisplayDuration), cancellationToken: token).SuppressCancellationThrow();
        if (isCanceled)
        {
            return;
        }

        _countdownText.gameObject.SetActive(false);
    }
}
