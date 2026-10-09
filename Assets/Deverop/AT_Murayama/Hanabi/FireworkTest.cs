using UnityEngine;
using UnityEngine.VFX;

public class FireworkTest : MonoBehaviour
{
    [SerializeField] private VisualEffect vfx;

    private VFXEventAttribute eventAttribute;

    private float timer;
    private int patternID;

    private void Start()
    {
        eventAttribute = vfx.CreateVFXEventAttribute();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 1.0f)
        {
            timer = 0f;

            eventAttribute.SetInt("PatternID", patternID);

            Debug.Log(
                $"Pattern ID = {patternID}, " +
                $"EventAttribute = {eventAttribute.GetInt("PatternID")}"
            );

            vfx.SendEvent("HanabiFire", eventAttribute);

            patternID++;

            if (patternID > 3)
                patternID = 0;
        }
    }
}