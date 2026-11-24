using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AT_Murayama.PauseMenu
{
    /// <summary>
    /// GameScene 用のポーズメニュー制御コンポーネント。
    /// Esc キーでの通常停止と、トラッキング切断等による外部強制停止の両方に対応し、
    /// 再開カウントダウン・ヒットストップ保護・AudioManager 連携・マウス＆キーボード操作を統合管理する。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public class PauseMenu : MonoBehaviour, IPauseMenuController
    {
        public enum MenuButtonType
        {
            Resume = 0,
            Calibrate = 1,
            Restart = 2,
            ReturnToTitle = 3
        }

        public enum EditorPreviewMode
        {
            Hidden = 0,
            UserPause = 1,
            TrackingLostPause = 2,
            Calibrating = 3
        }

        private const int TotalButtonCount = 4;
        private const int PauseCanvasSortingOrder = 200;

        [Header("外部参照（未設定時はシーン内から自動検索）")]
        [SerializeField] private RaceManager _raceManager;

        [Header("UI フォント ＆ アニメーション設定")]
        [Tooltip("UIテキストに使用する TMP_FontAsset（日本語対応フォント推奨。未設定時は TMP 既定フォントを使用）")]
        [SerializeField] private TMP_FontAsset _fontAsset;
        [Tooltip("開閉時のフェード/スケールアニメーション時間（実時間・秒）")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _fadeDuration = 0.16f;
        [Tooltip("シーン上に EventSystem が存在しない単独テスト時のみ自動生成するか")]
        [SerializeField] private bool _createFallbackEventSystemIfMissing = true;

        [Header("AudioManager 連携（任意）")]
        [Tooltip("ポーズ中に AudioManager.Instance.PauseBGM() でBGMを一時停止するか")]
        [SerializeField] private bool _pauseBgmWhileOpen = true;
        [Tooltip("ポーズを開いた時に鳴らす SE クリップ名（空欄なら再生しない）")]
        [SerializeField] private string _openSeName = "";
        [Tooltip("ポーズを閉じた時に鳴らす SE クリップ名（空欄なら再生しない）")]
        [SerializeField] private string _closeSeName = "";
        [Tooltip("キーボード/ホバーで項目選択が動いた時に鳴らす SE クリップ名（空欄なら再生しない）")]
        [SerializeField] private string _selectMoveSeName = "";
        [Tooltip("ボタン決定・キャリブレーション確定時に鳴らす SE クリップ名（空欄なら再生しない）")]
        [SerializeField] private string _decideSeName = "";

        [Header("ビジュアル・カラー設定")]
        [SerializeField] private Color _backdropColor = new Color(0.02f, 0.05f, 0.10f, 0.78f);
        [SerializeField] private Color _panelBgColor = new Color(0.05f, 0.10f, 0.18f, 0.96f);
        [SerializeField] private Color _accentCyanColor = new Color(0.18f, 0.86f, 1.0f, 1.0f);
        [SerializeField] private Color _warningAmberColor = new Color(1.0f, 0.65f, 0.18f, 1.0f);
        [SerializeField] private Color _buttonNormalColor = new Color(0.10f, 0.16f, 0.27f, 0.92f);
        [SerializeField] private Color _buttonSelectedColor = new Color(0.13f, 0.36f, 0.54f, 1.0f);

        [Header("生成済み UI 参照（自動構築またはプレハブで保持）")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _rootPanel;
        [SerializeField] private GameObject _mainMenuContainer;
        [SerializeField] private GameObject _calibrationContainer;
        [SerializeField] private TMP_Text _headerTitleText;
        [SerializeField] private TMP_Text _reasonBannerText;
        [SerializeField] private TMP_Text _footerHintText;
        [SerializeField] private TMP_Text _calibrationStatusText;
        [SerializeField] private TMP_Text _calibrationSubText;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _titleButton;
        [SerializeField] private Button _calibrateButton;
        [SerializeField] private Image _panelBorderImage;
        [SerializeField] private Image[] _buttonBackgrounds = new Image[TotalButtonCount];
        [SerializeField] private Image[] _buttonAccentBars = new Image[TotalButtonCount];
        [SerializeField] private TMP_Text[] _buttonLabels = new TMP_Text[TotalButtonCount];

        // 0 GC Alloc 用の固定配列バッファ
        private readonly Button[] _buttons = new Button[TotalButtonCount];
        private readonly int[] _visibleButtonIndices = new int[TotalButtonCount];
        private int _visibleButtonCount;
        private int _selectedVisiblePosition;

        private bool _isOpen;
        private bool _isCalibrating;
        private bool _isTransitioningScene;
        private bool _didPauseBgm;
        private bool _areCallbacksBound;
        private int _lastStateChangedFrame = -1;
        private EPauseReason _currentReason = EPauseReason.UserRequest;
        private Tween _activeTween;

        public bool IsOpen => _isOpen;
        public bool IsCalibrating => _isCalibrating;
        public EPauseReason CurrentReason => _currentReason;
        public int VisibleButtonCount => _visibleButtonCount;

        public MenuButtonType SelectedButtonType =>
            (_visibleButtonCount > 0 && _selectedVisiblePosition >= 0 && _selectedVisiblePosition < _visibleButtonCount)
                ? (MenuButtonType)_visibleButtonIndices[_selectedVisiblePosition]
                : MenuButtonType.Calibrate;

        /// <summary>
        /// シーン再読み込みやタイトル遷移直前に呼ばれる拡張フック（将来のリザルト/staticリセット差し込み用）
        /// </summary>
        public event Action BeforeSceneTransition;

        private void Awake()
        {
            EnsureRaceManagerReference();
            EnsureUiBuilt();
            if (!_areCallbacksBound)
            {
                BindButtonCallbacks();
            }
            RefreshButtonArray();

            // 初期状態は非表示（シーンに置いた時に画面を塞がないようにする）
            HideUiImmediate();
            _isOpen = false;
            _isCalibrating = false;
        }

        private void OnDestroy()
        {
            KillActiveTween();

            // ポーズを開いたまま他スクリプトからシーン遷移・破棄された場合の安全ガード
            // （再開カウントダウン中は _isOpen == false のため timeScale = 0 を誤って戻さない）
            if (Application.isPlaying && _isOpen)
            {
                Time.timeScale = 1f;
                RestoreBgmIfPaused();
                _isOpen = false;
                _isCalibrating = false;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || _isTransitioningScene)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // 同一フレーム内での連続誤反応（開いた瞬間に閉じる・Enter連打で即決定等）を防ぐ
            if (Time.frameCount == _lastStateChangedFrame)
            {
                return;
            }

            if (_isOpen)
            {
                if (_isCalibrating)
                {
                    if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        TryConfirmCalibration();
                    }
                    return;
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    // TrackingLost 中は Close() 内部ガードにより閉じられない
                    Close();
                    return;
                }

                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                {
                    MoveSelection(-1);
                    return;
                }

                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                {
                    MoveSelection(1);
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ExecuteSelectedButton();
                }

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                TryOpenByEscapeKey();
            }
        }

        /// <summary>
        /// Esc キーからのポーズ要求（ゴール後・再開カウントダウン中は開かない）
        /// </summary>
        public bool TryOpenByEscapeKey()
        {
            if (_isOpen || _isTransitioningScene)
            {
                return false;
            }

            EnsureRaceManagerReference();
            if (_raceManager != null)
            {
                if (_raceManager.State == ERaceState.Finished || _raceManager.IsResumeCountdownRunning)
                {
                    return false;
                }
            }

            Open(EPauseReason.UserRequest);
            return true;
        }

        /// <summary>
        /// ポーズメニューを開く。TrackingLost 等で外から呼ぶ場合はゴール後を除きいつでも受け付ける。
        /// </summary>
        public void Open(EPauseReason reason = EPauseReason.UserRequest)
        {
            if (_isTransitioningScene)
            {
                return;
            }

            EnsureRaceManagerReference();

            // 仕様：ゴールした後（ERaceState.Finished）はポーズさせない
            if (_raceManager != null && _raceManager.State == ERaceState.Finished)
            {
                return;
            }

            // 仕様：再開のカウントダウン中に外から開かれたら、先に CancelResumeCountdown() で止めてから開く
            if (_raceManager != null && _raceManager.IsResumeCountdownRunning)
            {
                _raceManager.CancelResumeCountdown();
            }

            EnsureUiBuilt();
            if (!_areCallbacksBound)
            {
                BindButtonCallbacks();
            }
            RefreshButtonArray();

            if (Application.isPlaying)
            {
                EnsureEventSystemExists();
                ClearEventSystemSelection();
            }

            bool wasAlreadyOpen = _isOpen;

            // すでに UserRequest で開いている最中に TrackingLost が来た場合は TrackingLost へ昇格させる
            // 逆に TrackingLost 中に UserRequest が呼ばれても Esc 復帰可能へは降格させない
            if (!wasAlreadyOpen)
            {
                _currentReason = reason;
            }
            else if (reason == EPauseReason.TrackingLost)
            {
                _currentReason = EPauseReason.TrackingLost;
            }

            _isOpen = true;
            _isCalibrating = false;
            _lastStateChangedFrame = Application.isPlaying ? Time.frameCount : -1;

            if (Application.isPlaying)
            {
                Time.timeScale = 0f;
                PauseBgmIfConfigured();
            }

            ApplyMenuStateToUi();
            ShowUiAnimatedOrImmediate();

            if (!wasAlreadyOpen && Application.isPlaying)
            {
                PlayUiSe(_openSeName);
            }
        }

        /// <summary>
        /// ポーズメニューを閉じてレースを再開する（UserRequest で開いた場合のみ閉じる）。
        /// </summary>
        public void Close()
        {
            if (!_isOpen || _isTransitioningScene)
            {
                return;
            }

            // 仕様：外から開かれた（TrackingLost）ときは Esc / 戻る で閉じられない
            if (_currentReason == EPauseReason.TrackingLost)
            {
                return;
            }

            _isOpen = false;
            _isCalibrating = false;
            _lastStateChangedFrame = Application.isPlaying ? Time.frameCount : -1;

            if (Application.isPlaying)
            {
                ClearEventSystemSelection();
                RestoreBgmIfPaused();
                // 仕様：再開時は前の値を覚えずに必ず 1f に戻す（HitStopEffect 残留防止）
                Time.timeScale = 1f;
                PlayUiSe(_closeSeName);
            }

            HideUiAnimatedOrImmediate();
        }

        /// <summary>
        /// 「キャリブレーション」ボタン押下時：キャリブレーション中画面へ移行し、Enter 待ちに入る。
        /// </summary>
        public void BeginCalibration()
        {
            if (!_isOpen || _isCalibrating || _isTransitioningScene)
            {
                return;
            }

            _isCalibrating = true;
            _lastStateChangedFrame = Application.isPlaying ? Time.frameCount : -1;

            if (Application.isPlaying)
            {
                ClearEventSystemSelection();
                PlayUiSe(_decideSeName);
            }

            ApplyCalibrationStateToUi();
        }

        /// <summary>
        /// キャリブレーション完了（Enter 押下）時：メニューを閉じ、走行中ならカウントダウン再開、イントロ/初期カウントダウン中なら即復帰する。
        /// </summary>
        public bool TryConfirmCalibration()
        {
            if (!_isOpen || !_isCalibrating || _isTransitioningScene)
            {
                return false;
            }

            EnsureRaceManagerReference();

            _isOpen = false;
            _isCalibrating = false;
            _lastStateChangedFrame = Application.isPlaying ? Time.frameCount : -1;

            // 仕様：カウントダウンが見えるように、呼ぶ前にメニューを閉じておく
            HideUiImmediate();

            if (Application.isPlaying)
            {
                ClearEventSystemSelection();
                RestoreBgmIfPaused();
                PlayUiSe(_decideSeName);

                // 仕様：走行中（Racing）のときだけ timeScale = 0 のまま ResumeWithCountdownAsync() を呼ぶ。
                // スタート演出中・最初のカウントダウン中・RaceManager 未配置時は timeScale = 1f に戻す。
                if (_raceManager != null && _raceManager.State == ERaceState.Racing)
                {
                    Time.timeScale = 0f;
                    _raceManager.ResumeWithCountdownAsync().Forget();
                }
                else
                {
                    Time.timeScale = 1f;
                }
            }

            return true;
        }

        /// <summary>
        /// 「リスタート」ボタン：GameScene を最初から読み直す
        /// </summary>
        public bool TriggerRestart()
        {
            return TryLoadSceneSafely(SceneNames.Game);
        }

        /// <summary>
        /// 「タイトルへ」ボタン：TitleScene へ戻る
        /// </summary>
        public bool TriggerReturnToTitle()
        {
            return TryLoadSceneSafely(SceneNames.Title);
        }

        public void MoveSelection(int delta)
        {
            if (!_isOpen || _isCalibrating || _visibleButtonCount <= 0)
            {
                return;
            }

            int nextPos = (_selectedVisiblePosition + delta) % _visibleButtonCount;
            if (nextPos < 0)
            {
                nextPos += _visibleButtonCount;
            }

            if (nextPos != _selectedVisiblePosition)
            {
                _selectedVisiblePosition = nextPos;
                UpdateButtonVisualHighlights();

                if (Application.isPlaying)
                {
                    PlayUiSe(_selectMoveSeName);
                }
            }
        }

        public void ExecuteSelectedButton()
        {
            if (!_isOpen || _isCalibrating || _visibleButtonCount <= 0)
            {
                return;
            }

            ExecuteButtonByType(SelectedButtonType);
        }

        internal void NotifyButtonHovered(int rawButtonIndex)
        {
            if (!_isOpen || _isCalibrating)
            {
                return;
            }

            for (int i = 0; i < _visibleButtonCount; i++)
            {
                if (_visibleButtonIndices[i] == rawButtonIndex)
                {
                    if (_selectedVisiblePosition != i)
                    {
                        _selectedVisiblePosition = i;
                        UpdateButtonVisualHighlights();
                        if (Application.isPlaying)
                        {
                            PlayUiSe(_selectMoveSeName);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// RaceManager を外部から差し替える用（テスト・独自初期化向け）
        /// </summary>
        public void SetRaceManager(RaceManager raceManager)
        {
            _raceManager = raceManager;
        }

        private void ExecuteButtonByType(MenuButtonType buttonType)
        {
            switch (buttonType)
            {
                case MenuButtonType.Resume:
                    Close();
                    break;
                case MenuButtonType.Restart:
                    if (Application.isPlaying)
                    {
                        PlayUiSe(_decideSeName);
                    }
                    TriggerRestart();
                    break;
                case MenuButtonType.ReturnToTitle:
                    if (Application.isPlaying)
                    {
                        PlayUiSe(_decideSeName);
                    }
                    TriggerReturnToTitle();
                    break;
                case MenuButtonType.Calibrate:
                    BeginCalibration();
                    break;
            }
        }

        private bool TryLoadSceneSafely(string sceneName)
        {
            if (_isTransitioningScene)
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[PauseMenu] シーン '{sceneName}' が Build Profiles の Scene List に登録されていません。", this);
                return false;
            }

            _isTransitioningScene = true;
            _isOpen = false;
            _isCalibrating = false;
            _lastStateChangedFrame = Application.isPlaying ? Time.frameCount : -1;

            KillActiveTween();

            if (_raceManager != null && _raceManager.IsResumeCountdownRunning)
            {
                _raceManager.CancelResumeCountdown();
            }

            if (Application.isPlaying)
            {
                ClearEventSystemSelection();
                RestoreBgmIfPaused();
                // 仕様：シーンを移る前に必ず 1f に戻す
                Time.timeScale = 1f;
            }

            BeforeSceneTransition?.Invoke();
            SceneManager.LoadScene(sceneName);
            return true;
        }

        private void EnsureRaceManagerReference()
        {
            if (_raceManager == null)
            {
                _raceManager = FindFirstObjectByType<RaceManager>();
            }
        }

        private void PauseBgmIfConfigured()
        {
            if (!_pauseBgmWhileOpen || _didPauseBgm)
            {
                return;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PauseBGM();
                _didPauseBgm = true;
            }
        }

        private void RestoreBgmIfPaused()
        {
            if (!_didPauseBgm)
            {
                return;
            }

            _didPauseBgm = false;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ResumeBGM();
            }
        }

        private static void PlayUiSe(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySE(clipName);
            }
        }

        private void EnsureEventSystemExists()
        {
            if (!_createFallbackEventSystemIfMissing || EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemGo = new GameObject("PauseMenu_FallbackEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.transform.SetParent(transform, false);
        }

        private static void ClearEventSystemSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void RefreshButtonArray()
        {
            _buttons[(int)MenuButtonType.Resume] = _resumeButton;
            _buttons[(int)MenuButtonType.Restart] = _restartButton;
            _buttons[(int)MenuButtonType.ReturnToTitle] = _titleButton;
            _buttons[(int)MenuButtonType.Calibrate] = _calibrateButton;
        }

        private void BindButtonCallbacks()
        {
            BindSingleButton(_resumeButton, MenuButtonType.Resume);
            BindSingleButton(_restartButton, MenuButtonType.Restart);
            BindSingleButton(_titleButton, MenuButtonType.ReturnToTitle);
            BindSingleButton(_calibrateButton, MenuButtonType.Calibrate);
            _areCallbacksBound = true;
        }

        private void BindSingleButton(Button button, MenuButtonType type)
        {
            if (button == null)
            {
                return;
            }

            // Unity UI 標準の Navigation による上下二重移動・Enter 二重Submitを防ぐため None に固定
            Navigation nav = button.navigation;
            if (nav.mode != Navigation.Mode.None)
            {
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
            }

            PauseMenuHoverRelay relay = button.GetComponent<PauseMenuHoverRelay>();
            if (relay == null)
            {
                relay = button.gameObject.AddComponent<PauseMenuHoverRelay>();
            }
            relay.Initialize(this, (int)type);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || !_isOpen || _isCalibrating)
                {
                    return;
                }

                ClearEventSystemSelection();
                ExecuteButtonByType(type);
            });
        }

        private void ApplyMenuStateToUi()
        {
            bool allowResume = _currentReason == EPauseReason.UserRequest;

            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.SetActive(true);
            }

            if (_calibrationContainer != null)
            {
                _calibrationContainer.SetActive(false);
            }

            if (_resumeButton != null)
            {
                _resumeButton.gameObject.SetActive(allowResume);
            }

            if (_headerTitleText != null)
            {
                _headerTitleText.text = allowResume ? "PAUSED" : "TRACKING LOST";
                _headerTitleText.color = allowResume ? _accentCyanColor : _warningAmberColor;
            }

            if (_reasonBannerText != null)
            {
                _reasonBannerText.text = allowResume
                    ? "レースを一時停止中"
                    : "カメラの接続が切れました — キャリブレーションを行ってください";
                _reasonBannerText.color = allowResume
                    ? new Color(0.82f, 0.93f, 1.0f, 0.92f)
                    : _warningAmberColor;
            }

            if (_footerHintText != null)
            {
                _footerHintText.text = allowResume
                    ? "W / S・↑ / ↓：選択　｜　Enter / クリック：決定　｜　Esc：戻る"
                    : "W / S・↑ / ↓：選択　｜　Enter / クリック：決定　｜　※Escでは閉じられません";
            }

            if (_panelBorderImage != null)
            {
                _panelBorderImage.color = allowResume
                    ? new Color(_accentCyanColor.r, _accentCyanColor.g, _accentCyanColor.b, 0.72f)
                    : new Color(_warningAmberColor.r, _warningAmberColor.g, _warningAmberColor.b, 0.85f);
            }

            RebuildVisibleButtonIndices();
            _selectedVisiblePosition = 0;
            UpdateButtonVisualHighlights();
        }

        private void ApplyCalibrationStateToUi()
        {
            if (_mainMenuContainer != null)
            {
                _mainMenuContainer.SetActive(false);
            }

            if (_calibrationContainer != null)
            {
                _calibrationContainer.SetActive(true);
            }

            if (_headerTitleText != null)
            {
                _headerTitleText.text = "CALIBRATION";
                _headerTitleText.color = _accentCyanColor;
            }

            if (_reasonBannerText != null)
            {
                _reasonBannerText.text = "姿勢トラッキング調整";
                _reasonBannerText.color = new Color(0.82f, 0.93f, 1.0f, 0.92f);
            }

            if (_calibrationStatusText != null)
            {
                _calibrationStatusText.text = "キャリブレーション中";
            }

            if (_calibrationSubText != null)
            {
                _calibrationSubText.text = "カメラの前に正対して姿勢を整えたら\n[ Enter ] キーでその場から再開します";
            }

            if (_footerHintText != null)
            {
                _footerHintText.text = "Enter：カウントダウンしてレース再開";
            }

            if (_panelBorderImage != null)
            {
                _panelBorderImage.color = new Color(_accentCyanColor.r, _accentCyanColor.g, _accentCyanColor.b, 0.85f);
            }
        }

        private void RebuildVisibleButtonIndices()
        {
            _visibleButtonCount = 0;
            for (int i = 0; i < TotalButtonCount; i++)
            {
                Button btn = _buttons[i];
                if (btn != null && btn.gameObject.activeSelf)
                {
                    _visibleButtonIndices[_visibleButtonCount] = i;
                    _visibleButtonCount++;
                }
            }
        }

        private void UpdateButtonVisualHighlights()
        {
            int selectedRawIndex = (_visibleButtonCount > 0 && _selectedVisiblePosition >= 0 && _selectedVisiblePosition < _visibleButtonCount)
                ? _visibleButtonIndices[_selectedVisiblePosition]
                : -1;

            Color activeAccent = _currentReason == EPauseReason.TrackingLost ? _warningAmberColor : _accentCyanColor;

            for (int i = 0; i < TotalButtonCount; i++)
            {
                bool isSelected = i == selectedRawIndex;

                if (_buttonBackgrounds != null && i < _buttonBackgrounds.Length && _buttonBackgrounds[i] != null)
                {
                    _buttonBackgrounds[i].color = isSelected ? _buttonSelectedColor : _buttonNormalColor;
                }

                if (_buttonAccentBars != null && i < _buttonAccentBars.Length && _buttonAccentBars[i] != null)
                {
                    _buttonAccentBars[i].enabled = isSelected;
                    _buttonAccentBars[i].color = activeAccent;
                }

                if (_buttonLabels != null && i < _buttonLabels.Length && _buttonLabels[i] != null)
                {
                    _buttonLabels[i].color = isSelected ? Color.white : new Color(0.78f, 0.87f, 0.96f, 0.92f);
                }

                Button btn = _buttons[i];
                if (btn != null)
                {
                    btn.transform.localScale = isSelected ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
                }
            }
        }

        private void ShowUiAnimatedOrImmediate()
        {
            KillActiveTween();

            if (_canvas != null)
            {
                _canvas.enabled = true;
            }

            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.gameObject.SetActive(true);
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            if (!Application.isPlaying || _fadeDuration <= 0.001f)
            {
                _canvasGroup.alpha = 1f;
                if (_rootPanel != null)
                {
                    _rootPanel.localScale = Vector3.one;
                }
                return;
            }

            _canvasGroup.alpha = 0f;
            if (_rootPanel != null)
            {
                _rootPanel.localScale = new Vector3(0.94f, 0.94f, 1f);
            }

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(_canvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutCubic).SetUpdate(true));
            if (_rootPanel != null)
            {
                seq.Join(_rootPanel.DOScale(Vector3.one, _fadeDuration).SetEase(Ease.OutBack).SetUpdate(true));
            }
            _activeTween = seq;
        }

        private void HideUiAnimatedOrImmediate()
        {
            KillActiveTween();

            if (_canvasGroup == null)
            {
                if (_canvas != null)
                {
                    _canvas.enabled = false;
                }
                return;
            }

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            if (!Application.isPlaying || _fadeDuration <= 0.001f)
            {
                HideUiImmediate();
                return;
            }

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(_canvasGroup.DOFade(0f, _fadeDuration * 0.75f).SetEase(Ease.InCubic).SetUpdate(true));
            if (_rootPanel != null)
            {
                seq.Join(_rootPanel.DOScale(new Vector3(0.96f, 0.96f, 1f), _fadeDuration * 0.75f).SetEase(Ease.InCubic).SetUpdate(true));
            }
            seq.OnComplete(HideUiImmediate);
            _activeTween = seq;
        }

        private void HideUiImmediate()
        {
            KillActiveTween();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.gameObject.SetActive(false);
            }

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
        }

        private void KillActiveTween()
        {
            if (_activeTween != null)
            {
                _activeTween.Kill(false);
                _activeTween = null;
            }
        }

        /// <summary>
        /// UIが未構築の場合に自動構築する
        /// </summary>
        public void EnsureUiBuilt()
        {
            if (_canvas != null && _canvasGroup != null &&
                _resumeButton != null && _restartButton != null &&
                _titleButton != null && _calibrateButton != null &&
                _calibrationContainer != null)
            {
                return;
            }

            BuildOrRebuildUiHierarchy(_fontAsset);
        }

        /// <summary>
        /// エディタ上・ランタイムどちらからでも使えるUI階層構築（サイバーレース仕様）
        /// </summary>
        public void BuildOrRebuildUiHierarchy(TMP_FontAsset overrideFont = null)
        {
            if (overrideFont != null)
            {
                _fontAsset = overrideFont;
            }

            TMP_FontAsset fontToUse = _fontAsset != null ? _fontAsset : TMP_Settings.defaultFontAsset;

            // 既存の生成ルートがあればクリーンに差し替え
            Transform existingRoot = transform.Find("PauseMenu_Canvas");
            if (existingRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existingRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(existingRoot.gameObject);
                }
            }

            // Canvas ルート（既存ミニマップ=100、状態ポーズUI=110 より前面に出すため 200）
            GameObject canvasGo = new GameObject("PauseMenu_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = PauseCanvasSortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 暗転・入力ブロック用オーバーレイ（CanvasGroup 管理）
            GameObject overlayGo = new GameObject("OverlayRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
            StretchFull(overlayRect);
            Image backdropImg = overlayGo.GetComponent<Image>();
            backdropImg.color = _backdropColor;
            backdropImg.raycastTarget = true;
            _canvasGroup = overlayGo.GetComponent<CanvasGroup>();

            // 中央パネル外枠（発光ボーダー）
            GameObject borderGo = new GameObject("MenuFrameBorder", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(overlayGo.transform, false);
            _rootPanel = borderGo.GetComponent<RectTransform>();
            _rootPanel.anchorMin = new Vector2(0.5f, 0.5f);
            _rootPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _rootPanel.sizeDelta = new Vector2(640f, 660f);
            _panelBorderImage = borderGo.GetComponent<Image>();
            _panelBorderImage.color = new Color(_accentCyanColor.r, _accentCyanColor.g, _accentCyanColor.b, 0.72f);

            // 内側ダークパネル本体
            GameObject panelBgGo = new GameObject("MenuPanelBody", typeof(RectTransform), typeof(Image));
            panelBgGo.transform.SetParent(borderGo.transform, false);
            RectTransform panelBgRect = panelBgGo.GetComponent<RectTransform>();
            StretchFull(panelBgRect, 3f);
            Image panelBgImg = panelBgGo.GetComponent<Image>();
            panelBgImg.color = _panelBgColor;

            // 上部ヘッダーライン装飾
            CreateColorBar(panelBgGo.transform, "TopAccentLine", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 0f), _accentCyanColor);

            // タイトルテキスト ("PAUSED")
            _headerTitleText = CreateTmpText(panelBgGo.transform, "HeaderTitle", "PAUSED", 44f, FontStyles.Bold, _accentCyanColor, TextAlignmentOptions.Center, fontToUse);
            SetAnchoredBox(_headerTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -84f), new Vector2(-24f, -22f));

            // 理由・サブバナーテキスト
            _reasonBannerText = CreateTmpText(panelBgGo.transform, "ReasonBanner", "レースを一時停止中", 20f, FontStyles.Normal, new Color(0.82f, 0.93f, 1.0f, 0.92f), TextAlignmentOptions.Center, fontToUse);
            SetAnchoredBox(_reasonBannerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -140f), new Vector2(-24f, -88f));

            // 区切り線
            CreateColorBar(panelBgGo.transform, "HeaderDivider", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), new Vector2(0f, -150f), new Vector2(0f, -148f), new Color(_accentCyanColor.r, _accentCyanColor.g, _accentCyanColor.b, 0.35f));

            // メインボタンコンテナ
            _mainMenuContainer = new GameObject("MainMenuButtons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _mainMenuContainer.transform.SetParent(panelBgGo.transform, false);
            RectTransform menuContRect = _mainMenuContainer.GetComponent<RectTransform>();
            SetAnchoredBox(menuContRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(64f, 74f), new Vector2(-64f, -162f));

            VerticalLayoutGroup vlg = _mainMenuContainer.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(0, 0, 12, 12);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _buttonBackgrounds = new Image[TotalButtonCount];
            _buttonAccentBars = new Image[TotalButtonCount];
            _buttonLabels = new TMP_Text[TotalButtonCount];

            _resumeButton = CreateMenuButton(_mainMenuContainer.transform, "Button_Resume", "戻る（レース再開）", MenuButtonType.Resume, fontToUse);
            _calibrateButton = CreateMenuButton(_mainMenuContainer.transform, "Button_Calibrate", "キャリブレーション", MenuButtonType.Calibrate, fontToUse);
            _restartButton = CreateMenuButton(_mainMenuContainer.transform, "Button_Restart", "リスタート", MenuButtonType.Restart, fontToUse);
            _titleButton = CreateMenuButton(_mainMenuContainer.transform, "Button_Title", "タイトルへ", MenuButtonType.ReturnToTitle, fontToUse);

            // キャリブレーション中コンテナ
            _calibrationContainer = new GameObject("CalibrationPanel", typeof(RectTransform));
            _calibrationContainer.transform.SetParent(panelBgGo.transform, false);
            RectTransform calibRect = _calibrationContainer.GetComponent<RectTransform>();
            SetAnchoredBox(calibRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(48f, 84f), new Vector2(-48f, -170f));

            GameObject calibBox = new GameObject("CalibrationCard", typeof(RectTransform), typeof(Image));
            calibBox.transform.SetParent(_calibrationContainer.transform, false);
            StretchFull(calibBox.GetComponent<RectTransform>());
            calibBox.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.28f, 0.90f);

            _calibrationStatusText = CreateTmpText(calibBox.transform, "CalibrationStatusText", "キャリブレーション中", 38f, FontStyles.Bold, _accentCyanColor, TextAlignmentOptions.Center, fontToUse);
            SetAnchoredBox(_calibrationStatusText.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.90f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

            _calibrationSubText = CreateTmpText(calibBox.transform, "CalibrationSubText", "カメラの前に正対して姿勢を整えたら\n[ Enter ] キーでその場から再開します", 24f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center, fontToUse);
            SetAnchoredBox(_calibrationSubText.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.55f), new Vector2(24f, 0f), new Vector2(-24f, 0f));

            _calibrationContainer.SetActive(false);

            // 下部フッター操作ヒント
            _footerHintText = CreateTmpText(panelBgGo.transform, "FooterHint", "W / S・↑ / ↓：選択　｜　Enter / クリック：決定　｜　Esc：戻る", 16f, FontStyles.Normal, new Color(0.68f, 0.80f, 0.92f, 0.82f), TextAlignmentOptions.Center, fontToUse);
            SetAnchoredBox(_footerHintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 62f));

            RefreshButtonArray();
            _areCallbacksBound = false;
            BindButtonCallbacks();
            ApplyMenuStateToUi();
            HideUiImmediate();
        }

        private Button CreateMenuButton(Transform parent, string objectName, string labelText, MenuButtonType type, TMP_FontAsset fontAsset)
        {
            int idx = (int)type;

            GameObject btnGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(parent, false);
            btnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 76f);

            LayoutElement layout = btnGo.GetComponent<LayoutElement>();
            layout.preferredHeight = 76f;
            layout.minHeight = 76f;

            Image bg = btnGo.GetComponent<Image>();
            bg.color = _buttonNormalColor;
            _buttonBackgrounds[idx] = bg;

            Button btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Navigation nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            // 左側の選択ハイライトアクセントバー
            GameObject barGo = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(btnGo.transform, false);
            RectTransform barRect = barGo.GetComponent<RectTransform>();
            SetAnchoredBox(barRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(8f, -6f));
            Image barImg = barGo.GetComponent<Image>();
            barImg.color = _accentCyanColor;
            barImg.raycastTarget = false;
            _buttonAccentBars[idx] = barImg;

            // ラベルテキスト
            TMP_Text label = CreateTmpText(btnGo.transform, "Label", labelText, 27f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, fontAsset);
            StretchFull(label.rectTransform, 16f);
            label.raycastTarget = false;
            _buttonLabels[idx] = label;

            return btn;
        }

        private static TMP_Text CreateTmpText(Transform parent, string name, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
            }
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static void CreateColorBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            SetAnchoredBox(go.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void StretchFull(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        private static void SetAnchoredBox(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタ非再生中の見た目プレビュー用（Time.timeScale や AudioManager は触らない）
        /// </summary>
        public void PreviewStateInEditor(EditorPreviewMode mode)
        {
            EnsureUiBuilt();
            RefreshButtonArray();

            switch (mode)
            {
                case EditorPreviewMode.Hidden:
                    _isOpen = false;
                    _isCalibrating = false;
                    HideUiImmediate();
                    break;
                case EditorPreviewMode.UserPause:
                    _isOpen = true;
                    _isCalibrating = false;
                    _currentReason = EPauseReason.UserRequest;
                    ApplyMenuStateToUi();
                    ShowUiAnimatedOrImmediate();
                    break;
                case EditorPreviewMode.TrackingLostPause:
                    _isOpen = true;
                    _isCalibrating = false;
                    _currentReason = EPauseReason.TrackingLost;
                    ApplyMenuStateToUi();
                    ShowUiAnimatedOrImmediate();
                    break;
                case EditorPreviewMode.Calibrating:
                    _isOpen = true;
                    _isCalibrating = true;
                    _currentReason = EPauseReason.TrackingLost;
                    ApplyCalibrationStateToUi();
                    ShowUiAnimatedOrImmediate();
                    break;
            }
        }
#endif
    }
}
