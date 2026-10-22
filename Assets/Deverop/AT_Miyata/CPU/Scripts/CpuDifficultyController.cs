using UnityEngine;

// 全CPUの難易度を一括で決める窓口。シーンに1つだけ置く（置かなければ、各CPUのCpuRaceInputの難易度を使う）。
// CpuRaceInputの「Follow Global Difficulty」がONのCPUは、ここの難易度を使う（OFFのCPUは、自分の難易度のまま）。
// レース前の難易度選択などからSetDifficultyを呼べば、次のTickから全CPUに反映される。
public class CpuDifficultyController : MonoBehaviour
{
    [Tooltip("全CPUに使う難易度。未設定なら、各CPUの難易度を使う")]
    [SerializeField] private CpuDifficultyProfile _difficulty;

    // シーンに置かれている窓口。無ければnull。
    public static CpuDifficultyController Current { get; private set; }

    public CpuDifficultyProfile Difficulty => _difficulty;

    public void SetDifficulty(CpuDifficultyProfile difficulty)
    {
        _difficulty = difficulty;
    }

    private void OnEnable()
    {
        if (Current != null && Current != this)
        {
            Debug.LogWarning($"CpuDifficultyController: シーンに複数あります。後から有効になった {name} を使います", this);
        }

        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}
