using UnityEngine;
using UnityEngine.EventSystems;

namespace AT_Murayama.PauseMenu
{
    /// <summary>
    /// ボタン上へのマウスホバーを親 PauseMenu に通知するための軽量コンポーネント（0 GC Alloc）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuHoverRelay : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private PauseMenu _owner;
        [SerializeField] private int _buttonIndex;

        public void Initialize(PauseMenu owner, int buttonIndex)
        {
            _owner = owner;
            _buttonIndex = buttonIndex;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.NotifyButtonHovered(_buttonIndex);
            }
        }
    }
}
