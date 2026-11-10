using UnityEngine;

// コースギミックが自分（レーサー）に効果を出したときの、画面の演出（カメラの揺れ・集中線など）の継承元。
// RacerManagerBase.GimmickActivatedを購読し、TGimmickの型のギミックのときだけPlayを呼ぶ。
// 画面全体にかかる演出なので、Playerプレハブにだけ置く（CPURacerには置かない）。
// BoostPresentationと同じく、Playerプレハブの中で完結するため、VContainerには登録せずAwake／OnDestroyで購読・解除する。
// ギミック自身の演出（壊れるエフェクト・SEなど）は、こちらではなくCourseGimmickBase.Activatedにつなぐ。
public abstract class GimmickPresentationBase<TGimmick> : MonoBehaviour where TGimmick : CourseGimmickBase
{
    [SerializeField] private RacerManagerBase _racerManager;

    protected RacerManagerBase RacerManager => _racerManager;

    protected virtual void Awake()
    {
        if (_racerManager == null)
        {
            Debug.LogError($"{GetType().Name}: Racer Manager が未設定です", this);
            return;
        }

        _racerManager.GimmickActivated += OnGimmickActivated;
    }

    protected virtual void OnDestroy()
    {
        if (_racerManager != null)
        {
            _racerManager.GimmickActivated -= OnGimmickActivated;
        }
    }

    // TGimmickのギミックが、自分に効果を出した瞬間。
    protected abstract void Play(TGimmick gimmick);

    private void OnGimmickActivated(CourseGimmickBase gimmick)
    {
        if (gimmick is TGimmick target)
        {
            Play(target);
        }
    }
}
