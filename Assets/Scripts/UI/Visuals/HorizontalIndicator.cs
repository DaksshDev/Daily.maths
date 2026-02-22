using UnityEngine;

public class HorizontalIndicator : MonoBehaviour
{
    [SettingsHeader("Move left-right a lil (jiggle lol)")]
    
    [Space]
    [Header("Movement Settings")]
    [SerializeField] private float moveDistance = 2f;
    [SerializeField] private float speed = 2f;

    private Vector3 startPosition;
    private float time;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        time += Time.deltaTime * speed;
        
        float offset = Mathf.Sin(time) * moveDistance;
        
        transform.position = new Vector3(
            startPosition.x + offset,
            transform.position.y,
            transform.position.z
        );
    }
}