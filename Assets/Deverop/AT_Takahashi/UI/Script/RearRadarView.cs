using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 後方にいるCPUを画面下部全体へ警告表示する。
// 左後方は画面左、真後ろは中央、右後方は画面右。
// 指定距離以内では、回転しない距離テキストを表示する。
public class RearRadarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RaceManager _raceManager;
    [SerializeField] private PlayerFlightController _playerFlight;

    [Header("UI Prefabs")]
    [Tooltip("針のImageプレハブ")]
    [SerializeField] private Image _needlePrefab;

    [Tooltip("距離表示用のTextMeshProUGUIプレハブ")]
    [SerializeField] private TMP_Text _distanceTextPrefab;

    [Tooltip("画面下部全体へStretchさせた親")]
    [SerializeField] private RectTransform _markerRoot;

    [Header("Sprites")]
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _boostSprite;

    [Header("Position")]
    [Tooltip("画面左右から空ける余白")]
    [Min(0f)]
    [SerializeField] private float _horizontalPadding = 80f;

    [Tooltip("針の縦位置")]
    [SerializeField] private float _needleY = 50f;

    [Tooltip("距離テキストの縦位置")]
    [SerializeField] private float _textY = 100f;

    [Header("Distance")]
    [Tooltip("この距離より遠いCPUは表示しない")]
    [Min(0.1f)]
    [SerializeField] private float _maxDistance = 60f;

    [Tooltip("この距離以内で距離テキストを表示する")]
    [Min(0f)]
    [SerializeField] private float _distanceTextThreshold = 10f;

    [Header("Needle Size")]
    [Min(0.01f)]
    [SerializeField] private float _minScale = 0.5f;

    [Min(0.01f)]
    [SerializeField] private float _maxScale = 1f;

    [Header("Optimization")]
    [Tooltip("UIを更新する間隔。0なら毎フレーム")]
    [Min(0f)]
    [SerializeField] private float _updateInterval = 0.05f;

    private readonly Dictionary<int, Marker> _markers =
        new Dictionary<int, Marker>();

    private float _nextUpdateTime;
    private float _cachedRootWidth = -1f;

    private sealed class Marker
    {
        public Image Needle;
        public TMP_Text DistanceText;

        public bool IsVisible;
        public bool IsTextVisible;
        public bool WasBoosting;

        public int LastDistanceMetres = -1;
        public Vector2 LastPosition;
        public float LastScale = -1f;
    }

    private void Awake()
    {
        if (_needlePrefab != null)
        {
            _needlePrefab.gameObject.SetActive(false);
        }

        if (_distanceTextPrefab != null)
        {
            _distanceTextPrefab.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextUpdateTime)
        {
            return;
        }

        _nextUpdateTime =
            Time.unscaledTime + _updateInterval;

        if (_raceManager == null ||
            _playerFlight == null ||
            _needlePrefab == null ||
            _distanceTextPrefab == null ||
            _markerRoot == null)
        {
            return;
        }

        IReadOnlyRacerData player =
            _raceManager.PlayerData;

        bool isRacing =
            player != null &&
            _raceManager.State == ERaceState.Racing &&
            !player.IsFinished;

        if (!isRacing)
        {
            HideAllMarkers();
            return;
        }

        CacheRootWidth();

        foreach (IReadOnlyRacerData racer in _raceManager.Racers)
        {
            if (racer.IsPlayer)
            {
                continue;
            }

            float gap =
                player.DistanceOnCourse -
                racer.DistanceOnCourse;

            bool shouldShow =
                gap > 0f &&
                gap <= _maxDistance;

            // 一度も表示していない遠方CPUには
            // UIオブジェクトを生成しない。
            if (!shouldShow &&
                !_markers.ContainsKey(racer.RacerId))
            {
                continue;
            }

            Marker marker =
                GetOrCreateMarker(racer.RacerId);

            SetMarkerVisible(marker, shouldShow);

            if (!shouldShow)
            {
                continue;
            }

            UpdateMarker(marker, racer, gap);
        }
    }

    private void UpdateMarker(
        Marker marker,
        IReadOnlyRacerData racer,
        float gap)
    {
        Vector3 local =
            _playerFlight.transform.InverseTransformPoint(
                racer.Transform.position
            );

        float angle =
            Mathf.Atan2(local.x, -local.z);

        UpdatePosition(marker, angle);
        UpdateScale(marker, gap);
        UpdateSprite(marker, racer.IsBoosting);
        UpdateDistanceText(marker, gap);
    }

    private void UpdatePosition(
        Marker marker,
        float angle)
    {
        // 左後方=-1、真後ろ=0、右後方=1
        float horizontal =
            Mathf.Clamp(
                angle / (Mathf.PI * 0.5f),
                -1f,
                1f
            );

        float usableHalfWidth =
            Mathf.Max(
                0f,
                (_cachedRootWidth * 0.5f) -
                _horizontalPadding
            );

        float x =
            horizontal * usableHalfWidth;

        Vector2 needlePosition =
            new Vector2(x, _needleY);

        if ((marker.LastPosition - needlePosition)
            .sqrMagnitude > 0.01f)
        {
            marker.Needle.rectTransform.anchoredPosition =
                needlePosition;

            marker.DistanceText.rectTransform.anchoredPosition =
                new Vector2(x, _textY);

            marker.LastPosition = needlePosition;
        }
    }

    private void UpdateScale(
        Marker marker,
        float gap)
    {
        float distance01 =
            Mathf.Clamp01(gap / _maxDistance);

        float scale =
            Mathf.Lerp(
                _maxScale,
                _minScale,
                distance01
            );

        if (Mathf.Abs(marker.LastScale - scale) < 0.001f)
        {
            return;
        }

        marker.Needle.rectTransform.localScale =
            Vector3.one * scale;

        marker.LastScale = scale;
    }

    private void UpdateSprite(
        Marker marker,
        bool isBoosting)
    {
        if (marker.WasBoosting == isBoosting &&
            marker.Needle.sprite != null)
        {
            return;
        }

        marker.Needle.sprite =
            isBoosting
                ? _boostSprite
                : _normalSprite;

        marker.WasBoosting = isBoosting;
    }

    private void UpdateDistanceText(
        Marker marker,
        float gap)
    {
        bool shouldShowText =
            gap <= _distanceTextThreshold;

        if (marker.IsTextVisible != shouldShowText)
        {
            marker.DistanceText.gameObject.SetActive(
                shouldShowText
            );

            marker.IsTextVisible = shouldShowText;
        }

        if (!shouldShowText)
        {
            return;
        }

        // 毎フレーム小数文字列を作らず、
        // 表示する整数値が変わったときだけ更新する。
        int distanceMetres =
            Mathf.CeilToInt(Mathf.Max(0f, gap));

        if (marker.LastDistanceMetres == distanceMetres)
        {
            return;
        }

        marker.DistanceText.SetText(
            "{0} m",
            distanceMetres
        );

        marker.LastDistanceMetres = distanceMetres;
    }

    private Marker GetOrCreateMarker(int racerId)
    {
        if (_markers.TryGetValue(
            racerId,
            out Marker marker))
        {
            return marker;
        }

        Image needle =
            Instantiate(_needlePrefab, _markerRoot);

        TMP_Text distanceText =
            Instantiate(_distanceTextPrefab, _markerRoot);

        needle.sprite = _normalSprite;
        needle.raycastTarget = false;
        distanceText.raycastTarget = false;

        needle.gameObject.SetActive(false);
        distanceText.gameObject.SetActive(false);

        marker = new Marker
        {
            Needle = needle,
            DistanceText = distanceText
        };

        _markers.Add(racerId, marker);

        return marker;
    }

    private void SetMarkerVisible(
        Marker marker,
        bool visible)
    {
        if (marker.IsVisible == visible)
        {
            return;
        }

        marker.Needle.gameObject.SetActive(visible);

        if (!visible)
        {
            marker.DistanceText.gameObject.SetActive(false);
            marker.IsTextVisible = false;
        }

        marker.IsVisible = visible;
    }

    private void HideAllMarkers()
    {
        foreach (Marker marker in _markers.Values)
        {
            SetMarkerVisible(marker, false);
        }
    }

    private void CacheRootWidth()
    {
        float width = _markerRoot.rect.width;

        if (!Mathf.Approximately(
            _cachedRootWidth,
            width))
        {
            _cachedRootWidth = width;
        }
    }
}
