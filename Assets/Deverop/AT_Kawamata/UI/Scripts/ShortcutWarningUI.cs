using UnityEngine;

// ShortcutZoneのWarningイベントから呼ばれ、警告UIの表示/非表示を切り替える。
// 複数のShortcutZoneが同じUIを共有する想定のため、重複してShowされても
// 対応するHideが揃うまで消えないよう参照カウントで管理する。
// promptRootはシーンで表示したまま保存しても、開始時に隠す（警告エリアに入るまで出さない）。
public class ShortcutWarningUI : MonoBehaviour
{
    [SerializeField] private GameObject promptRoot;

    private int _activeCount;

    private void Awake()
    {
        if (promptRoot == null)
        {
            Debug.LogError("ShortcutWarningUI: Prompt Root が未設定です", this);
            return;
        }

        promptRoot.SetActive(false);
    }

    public void Show()
    {
        _activeCount++;
        promptRoot.SetActive(true);
    }

    public void Hide()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        if (_activeCount == 0)
        {
            promptRoot.SetActive(false);
        }
    }
}
