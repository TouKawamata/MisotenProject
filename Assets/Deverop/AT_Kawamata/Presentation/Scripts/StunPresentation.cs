using AT_Murayama.CharacterElectricity;
using UnityEngine;

// スタン時の演出の窓口。レーサー（PlayerManager／CPUManager）のStunStarted／StunEndedを購読し、各演出を呼ぶだけ。
// ブーストを当てられたとき・ギミック・妨害弾のどれでスタンしても、同じイベントが出る。
// 未設定の演出は飛ばすので、必要なものだけ割り当てればよい（BoostPresentationと同じ作り）。
// ・画面のノイズ（高橋さんのScreenNoiseEffectController）：画面全体にかかるので、Playerだけに設定する。
//   コントローラーはシーンに置くため、プレハブではなくシーンのPlayerのインスタンス側で割り当てる。
// ・キャラの電流（村山さんのCharacterElectricityPreview）：PlayerとCPUの両方に設定できる。キャラのモデルに付けたものを割り当てる。
public class StunPresentation : MonoBehaviour
{
    [SerializeField] private RacerManagerBase _racer;

    [Header("画面のノイズ（Playerだけ）")]
    [Tooltip("シーンに置いたScreenNoiseEffectController。CPUでは未設定のままにする")]
    [SerializeField] private ScreenNoiseEffectController _screenNoise;

    [Tooltip("スタン中に再生するプリセット名。空なら、ScreenNoiseEffectControllerで選ばれているプリセットを再生する")]
    [SerializeField] private string _screenNoisePresetName;

    [Header("キャラの電流")]
    [Tooltip("キャラのモデルに付けたCharacterElectricityPreview（CharacterElectricityController）")]
    [SerializeField] private CharacterElectricityPreview _electricity;

    [Tooltip("ON：スタンの残り時間に合わせて、電流をだんだん弱める。OFF：スタン中はずっと最大で、解けたらフェードして消える")]
    [SerializeField] private bool _fadeElectricityWithRemainingTime = true;

    [Header("ブーストの炎")]
    [Tooltip("スタン中に足裏の炎を短くし、ブースト中の追加炎を隠すBoostFlameView")]
    [SerializeField] private BoostFlameView _flameView;

    private bool _isStunned;
    private float _stunTotalTime;

    private void Awake()
    {
        if (_racer == null)
        {
            Debug.LogError("StunPresentation: Racer が未設定です", this);
            return;
        }

        _racer.StunStarted += PlayStunEffect;
        _racer.StunEnded += StopStunEffect;
    }

    private void Start()
    {
        // CharacterElectricityPreviewは、初期値でスタン中（放電あり）になっているため、始めに止めておく。
        if (_electricity != null)
        {
            _electricity.SetStun(false, immediate: true);
        }

        if (_flameView != null)
        {
            _flameView.SetStunned(false);
        }
    }

    private void OnDestroy()
    {
        if (_racer == null)
        {
            return;
        }

        _racer.StunStarted -= PlayStunEffect;
        _racer.StunEnded -= StopStunEffect;
    }

    private void Update()
    {
        if (!_isStunned || !_fadeElectricityWithRemainingTime || _electricity == null || _stunTotalTime <= 0f)
        {
            return;
        }

        _electricity.SetStunNormalizedTime(_racer.StunRemainingTime / _stunTotalTime);
    }

    private void PlayStunEffect()
    {
        _isStunned = true;
        // スタンの長さは原因（ブースト・ギミック・妨害弾）で変わるので、始まった瞬間の残り秒数を全体の長さとする。
        _stunTotalTime = _racer.StunRemainingTime;

        if (_screenNoise != null)
        {
            if (string.IsNullOrEmpty(_screenNoisePresetName))
            {
                _screenNoise.Play();
            }
            else
            {
                _screenNoise.PlayPreset(_screenNoisePresetName);
            }
        }

        if (_electricity != null)
        {
            _electricity.SetStun(true);
        }

        if (_flameView != null)
        {
            _flameView.SetStunned(true);
        }
    }

    private void StopStunEffect()
    {
        _isStunned = false;

        if (_screenNoise != null)
        {
            _screenNoise.Stop();
        }

        if (_electricity != null)
        {
            _electricity.SetStun(false);
        }

        if (_flameView != null)
        {
            _flameView.SetStunned(false);
        }
    }
}
