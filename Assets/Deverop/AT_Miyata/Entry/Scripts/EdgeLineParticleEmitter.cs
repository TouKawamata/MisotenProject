using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 帯の縁に沿って白い線を流し、線の先端の周りに四角い粒を発生させる
/// 粒はその場で縮小して消える
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EdgeLineParticleEmitter : MonoBehaviour {
	[Header("経路")]
	[SerializeField, Tooltip("帯の縁に沿った始点。画面左外に置く")] private RectTransform _lineStart;
	[SerializeField, Tooltip("帯の縁に沿った終点。画面右端に置く。先端がここに着いたら粒の発生を止める")] private RectTransform _lineEnd;

	[Header("線")]
	[SerializeField, Tooltip("子に置いた GradientLineGraphic")] private GradientLineGraphic _line;
	[SerializeField, Tooltip("線の長さ(px)")] private float _lineLength = 600.0f;
	[SerializeField, Tooltip("線の太さ(px)")] private float _lineThickness = 8.0f;
	[SerializeField, Tooltip("線が流れきるまでの時間(秒)")] private float _moveDuration = 0.5f;
	[SerializeField] private Ease _moveEase = Ease.Linear;

	[Header("粒 発生")]
	[SerializeField, Tooltip("プールする粒の数")] private int _maxCount = 128;
	[SerializeField, Tooltip("発生間隔(秒)")] private float _spawnInterval = 0.01f;
	[SerializeField, Tooltip("1回に発生させる数")] private int _spawnPerTick = 2;
	[SerializeField, Tooltip("先端から後ろ方向への散らばり(px)")] private float _spreadAlong = 80.0f;
	[SerializeField, Tooltip("線に垂直な方向への散らばり(px)")] private float _spreadPerp = 30.0f;
	[SerializeField, Tooltip("大きいほど線の近くに集まる")] private float _falloff = 2.0f;

	[Header("粒 見た目")]
	[SerializeField, Tooltip("初期の幅(min, max)")] private Vector2 _sizeX = new Vector2(8.0f, 40.0f);
	[SerializeField, Tooltip("初期の高さ(min, max)")] private Vector2 _sizeY = new Vector2(6.0f, 20.0f);
	[SerializeField, Tooltip("透明度(min, max)")] private Vector2 _alpha = new Vector2(0.6f, 1.0f);
	[SerializeField, Tooltip("寿命(min, max)(秒)")] private Vector2 _lifetime = new Vector2(0.2f, 0.4f);
	[SerializeField] private Ease _shrinkEase = Ease.InQuad;
	[SerializeField] private Color _particleColor = Color.white;

	private struct Particle {
		public RectTransform rect;
		public Image image;
		public Vector2 baseSize;
		public float lifetime;
		public float age;
		public bool active;
	}

	private RectTransform _rectTransform;
	private Particle[] _particles;
	private bool _isInitialized = false;

	private bool _isPlaying = false;
	private float _moveTimer = 0.0f;
	private float _spawnTimer = 0.0f;

	// 経路(このオブジェクトのローカル座標)
	private Vector2 _pathStart;
	private Vector2 _pathDir;
	private Vector2 _pathPerp;
	private float _pathLength;


	void Awake() {
		EnsureInitialized();
	}

	void Update() {
		if (_isPlaying) {
			UpdateLine();
		}
		UpdateParticles();
	}

	public void Play() {
		EnsureInitialized();
		ResetEffect();

		CalcPath();
		_isPlaying = true;
		_line.gameObject.SetActive(true);
		_line.rectTransform.sizeDelta = new Vector2(_lineLength, _lineThickness);
		_line.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(_pathDir.y, _pathDir.x) * Mathf.Rad2Deg);
		ApplyLinePosition(0.0f);
	}

	public void ResetEffect() {
		EnsureInitialized();

		_isPlaying = false;
		_moveTimer = 0.0f;
		_spawnTimer = 0.0f;
		_line.gameObject.SetActive(false);

		for (int i = 0; i < _particles.Length; i++) {
			Deactivate(i);
		}
	}


	// ---------- 初期化 ----------

	// 他スクリプトの Awake から ResetEffect が呼ばれても動くように遅延初期化する
	private void EnsureInitialized() {
		if (_isInitialized) return;
		_isInitialized = true;

		_rectTransform = GetComponent<RectTransform>();

		// 線は中央アンカー、先端(右端)を支点にする
		var lineRect = _line.rectTransform;
		lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0.5f);
		lineRect.pivot = new Vector2(1.0f, 0.5f);

		_particles = new Particle[_maxCount];
		for (int i = 0; i < _maxCount; i++) {
			var go = new GameObject($"Particle_{i}", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(transform, false);

			var rect = go.GetComponent<RectTransform>();
			rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

			var image = go.GetComponent<Image>();
			image.raycastTarget = false;

			go.SetActive(false);
			_particles[i] = new Particle { rect = rect, image = image };
		}
	}

	private void CalcPath() {
		_pathStart = _rectTransform.InverseTransformPoint(_lineStart.position);
		Vector2 end = _rectTransform.InverseTransformPoint(_lineEnd.position);

		Vector2 diff = end - _pathStart;
		_pathLength = diff.magnitude;
		_pathDir = _pathLength > 0.0f ? diff / _pathLength : Vector2.right;
		_pathPerp = new Vector2(-_pathDir.y, _pathDir.x);
	}


	// ---------- 線 ----------

	private void UpdateLine() {
		_moveTimer += Time.deltaTime;
		float t = _moveDuration <= 0.0f ? 1.0f : Mathf.Clamp01(_moveTimer / _moveDuration);

		// 先端が終点を越え、線の後ろの端まで抜けきる距離を進む
		float headDistance = (_pathLength + _lineLength) * DOVirtual.EasedValue(0.0f, 1.0f, t, _moveEase);
		ApplyLinePosition(headDistance);

		// 先端が終点に着くまでの間だけ粒を出す
		if (headDistance <= _pathLength) {
			_spawnTimer += Time.deltaTime;
			while (_spawnTimer >= _spawnInterval) {
				_spawnTimer -= _spawnInterval;
				for (int i = 0; i < _spawnPerTick; i++) {
					Spawn(headDistance);
				}
				if (_spawnInterval <= 0.0f) break;
			}
		}

		if (t >= 1.0f) {
			_isPlaying = false;
			_line.gameObject.SetActive(false);
		}
	}

	private void ApplyLinePosition(float headDistance) {
		_line.rectTransform.localPosition = _pathStart + _pathDir * headDistance;
	}


	// ---------- 粒 ----------

	private void Spawn(float headDistance) {
		int index = FindInactive();
		if (index < 0) return;

		// 先端から後ろへ、線に垂直な方向は線の近くに集める
		float along = headDistance - Random.Range(0.0f, _spreadAlong);
		float perp = _spreadPerp * Mathf.Pow(Random.value, _falloff) * (Random.value < 0.5f ? -1.0f : 1.0f);
		Vector2 pos = _pathStart + _pathDir * along + _pathPerp * perp;

		ref Particle p = ref _particles[index];
		p.baseSize = new Vector2(Random.Range(_sizeX.x, _sizeX.y), Random.Range(_sizeY.x, _sizeY.y));
		p.lifetime = Random.Range(_lifetime.x, _lifetime.y);
		p.age = 0.0f;
		p.active = true;

		Color c = _particleColor;
		c.a *= Random.Range(_alpha.x, _alpha.y);
		p.image.color = c;

		p.rect.localPosition = pos;
		p.rect.sizeDelta = p.baseSize;
		p.rect.gameObject.SetActive(true);
	}

	private void UpdateParticles() {
		for (int i = 0; i < _particles.Length; i++) {
			ref Particle p = ref _particles[i];
			if (!p.active) continue;

			p.age += Time.deltaTime;
			float t = p.lifetime <= 0.0f ? 1.0f : Mathf.Clamp01(p.age / p.lifetime);
			if (t >= 1.0f) {
				Deactivate(i);
				continue;
			}

			p.rect.sizeDelta = p.baseSize * (1.0f - DOVirtual.EasedValue(0.0f, 1.0f, t, _shrinkEase));
		}
	}

	private int FindInactive() {
		for (int i = 0; i < _particles.Length; i++) {
			if (!_particles[i].active) return i;
		}
		return -1;
	}

	private void Deactivate(int index) {
		_particles[index].active = false;
		_particles[index].rect.gameObject.SetActive(false);
	}
}
