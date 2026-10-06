using UnityEngine;

// シールドの状態（Idle→Active→Idle、Active→Broken→Idle）を管理する。
// 押した瞬間にTryActivateで張り、押し続けている間（TickのisHeld）だけ維持する。時間制限は無い。
// ブースト接触などをTryBlockで1回防ぐと消え（Broken）、一定時間使えなくなる。
// 障害物など、ブースト接触以外から防ぐ場合もTryBlockを呼ぶ。
// Initialize/TryActivate/Tick/TryBlock/ForceStopはPlayerManagerから呼び出される。
public class ShieldController : MonoBehaviour
{
    private enum EState
    {
        Idle,
        Active,
        Broken
    }

    [SerializeField] private ShieldProfile _profile;

    private EState _state = EState.Idle;
    private float _stateTimer;

    public bool IsActive => _state == EState.Active;

    public bool IsBroken => _state == EState.Broken;

    public bool CanActivate => _profile != null && _state == EState.Idle;

    // 再び使えるようになるまでの残り秒数。Broken中でなければ0。
    public float BrokenRemainingTime => _state == EState.Broken ? _stateTimer : 0f;

    // 最高速度に掛ける倍率。シールドを張っていなければ1。
    public float MaxSpeedMultiplier => _state == EState.Active ? _profile.MaxSpeedMultiplier : 1f;

    public bool Initialize()
    {
        if (_profile == null)
        {
            Debug.LogError("ShieldController: Shield Profile が未設定です", this);
            return false;
        }

        _state = EState.Idle;
        _stateTimer = 0f;
        return true;
    }

    public bool TryActivate()
    {
        if (!CanActivate)
        {
            return false;
        }

        _state = EState.Active;
        return true;
    }

    // isHeld：シールドの入力を押し続けているか。離したらシールドを解除する。
    public void Tick(float deltaTime, bool isHeld)
    {
        switch (_state)
        {
            case EState.Active:
                if (!isHeld)
                {
                    _state = EState.Idle;
                }

                break;

            case EState.Broken:
                _stateTimer -= deltaTime;
                if (_stateTimer <= 0f)
                {
                    _state = EState.Idle;
                    _stateTimer = 0f;
                }

                break;
        }
    }

    // シールドを張っていれば防いで消える（true）。張っていなければ何もしない（false）。
    public bool TryBlock()
    {
        if (_state != EState.Active)
        {
            return false;
        }

        _state = EState.Broken;
        _stateTimer = _profile.BrokenDuration;
        return true;
    }

    // 入力に関係なくシールドを解除する（ゴール時など）。防いだ扱いにはしない。
    public void ForceStop()
    {
        if (_state == EState.Active)
        {
            _state = EState.Idle;
        }
    }
}
