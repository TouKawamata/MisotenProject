using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Enterキー（テンキーのEnterも含む）で次のシーンを読み込むだけの仮の遷移。Title・Entry・Resultのシーンに置く。
// GameScene→ResultSceneはRaceGoalDirection（ゴール後のEnter）が受け持つので、GameSceneには置かない。
public class EnterKeySceneLoader : MonoBehaviour
{
    [Tooltip("Enterで読み込むシーンの名前（SceneNamesの値）。Build ProfilesのScene Listに入っている必要がある")]
    [SerializeField] private string _nextSceneName = SceneNames.Title;

    private bool _isLoading;

    public string NextSceneName => _nextSceneName;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            LoadNext();
        }
    }

    // UIのButtonのOnClickからも呼べる。
    public void LoadNext()
    {
        // 同じフレームに2回押された・続けて押されたときに、二重に読み込まない。
        if (_isLoading)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(_nextSceneName))
        {
            Debug.LogError($"EnterKeySceneLoader: シーン '{_nextSceneName}' がBuild ProfilesのScene Listにありません", this);
            return;
        }

        _isLoading = true;

        // ポーズ・ヒットストップでtimeScaleが下がったまま抜けても、次のシーンが止まったままにならないようにする。
        Time.timeScale = 1f;
        SceneManager.LoadScene(_nextSceneName);
    }
}
