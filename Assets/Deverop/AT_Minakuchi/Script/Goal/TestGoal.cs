using UnityEngine;

public class TestGoal : MonoBehaviour
{
    [Header("ç∂âEà⁄ìÆÇÃê›íË")]
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 5f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void Update()
    {
        float x = Mathf.PingPong(
            Time.time * moveSpeed,
            moveDistance * 2f
        ) - moveDistance;

        transform.localPosition = startPosition + Vector3.right * x;
    }
}