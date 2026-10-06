using UnityEngine;

// ブースト接触を受けたときのスタン（入力不可・減速）と、その後の無敵時間、はじかれた後の間隔を管理する。
// 秒数はBoostHitProfileから呼び出し側が渡す。PlayerManagerとDummyRacerで共通に使うため、MonoBehaviourにはしない。
public class StunController
{
    private enum EState
    {
        None,
        Stunned,
        Invincible
    }

    private EState _state = EState.None;
    private float _stateTimer;
    private float _invincibleDurationAfterStun;
    private float _bounceTimer;

    // 入力を受け付けない間。
    public bool IsStunned => _state == EState.Stunned;

    // スタン中と、その後の無敵時間。この間はブースト接触の効果を受けない。
    public bool IsInvincible => _state != EState.None;

    // はじかれてから一定時間は、重なり続けていても再びはじかない。
    public bool CanBounce => _bounceTimer <= 0f;

    public float StunRemainingTime => _state == EState.Stunned ? _stateTimer : 0f;

    public void Reset()
    {
        _state = EState.None;
        _stateTimer = 0f;
        _invincibleDurationAfterStun = 0f;
        _bounceTimer = 0f;
    }

    // スタンを始める。stunDurationが過ぎたら、続けてinvincibleDurationの間だけ無敵にする。
    public void BeginStun(float stunDuration, float invincibleDuration)
    {
        _state = EState.Stunned;
        _stateTimer = stunDuration;
        _invincibleDurationAfterStun = invincibleDuration;
    }

    // スタンさせずに無敵にする（シールドで防いだ直後に、重なったままの相手から続けて当てられないようにする）。
    public void BeginInvincible(float duration)
    {
        if (_state == EState.Stunned)
        {
            return;
        }

        _state = EState.Invincible;
        _stateTimer = Mathf.Max(_stateTimer, duration);
    }

    public void BeginBounceInterval(float duration)
    {
        _bounceTimer = duration;
    }

    public void Tick(float deltaTime)
    {
        if (_bounceTimer > 0f)
        {
            _bounceTimer -= deltaTime;
        }

        if (_state == EState.None)
        {
            return;
        }

        _stateTimer -= deltaTime;
        if (_stateTimer > 0f)
        {
            return;
        }

        if (_state == EState.Stunned && _invincibleDurationAfterStun > 0f)
        {
            _state = EState.Invincible;
            _stateTimer = _invincibleDurationAfterStun;
            return;
        }

        _state = EState.None;
        _stateTimer = 0f;
    }
}
