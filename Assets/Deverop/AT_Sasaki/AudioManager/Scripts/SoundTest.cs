using UnityEngine;

public class AudioTest : MonoBehaviour
{
    private void Update()
    {
        // スペースキーでSE再生
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AudioManager.instance.PlaySE("Click");
        }

        // BキーでBGM1再生
        if (Input.GetKeyDown(KeyCode.B))
        {
            AudioManager.instance.PlayBGM("FieldBGM");
        }

        // SキーでBGM停止
        if (Input.GetKeyDown(KeyCode.S))
        {
            AudioManager.instance.StopBGM(1.0f); // 1秒かけてフェードアウト
        }
    }
}