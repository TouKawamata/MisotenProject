using UnityEngine;
using UnityEngine.VFX;

public class FireworkPerformanceTest : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private VisualEffect baseVFX;
    [SerializeField] private VisualEffect tex2DVFX;
    [SerializeField] private VisualEffect meshVFX;

    [Header("Event Names")]
    [SerializeField] private string baseEventName = "HanabiFire";
    [SerializeField] private string tex2DEventName = "HanabiFire2D";
    [SerializeField] private string meshEventName = "HanabiFireMesh";

    [Header("Test Settings")]
    [SerializeField] private float fireInterval = 0.5f;

    // 1回の発射タイミングで各VFXから何発出すか
    [SerializeField] private int fireworksPerVFX = 50;

    private VFXEventAttribute baseAttribute;
    private VFXEventAttribute tex2DAttribute;
    private VFXEventAttribute meshAttribute;

    private float timer;
    private int patternID;

    private void Start()
    {
        baseAttribute = baseVFX.CreateVFXEventAttribute();
        tex2DAttribute = tex2DVFX.CreateVFXEventAttribute();
        meshAttribute = meshVFX.CreateVFXEventAttribute();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer < fireInterval)
            return;

        timer -= fireInterval;

        // 0 → 1 → 2 → 3 → 0 ...
        patternID++;
        if (patternID > 3)
            patternID = 0;

        baseAttribute.SetInt("PatternID", patternID);
        tex2DAttribute.SetInt("PatternID", patternID);
        meshAttribute.SetInt("PatternID", patternID);

        for (int i = 0; i < fireworksPerVFX; i++)
        {
            baseVFX.SendEvent(baseEventName, baseAttribute);
            tex2DVFX.SendEvent(tex2DEventName, tex2DAttribute);
            meshVFX.SendEvent(meshEventName, meshAttribute);
        }
    }
}