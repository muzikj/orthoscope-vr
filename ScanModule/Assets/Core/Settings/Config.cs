using UnityEngine;

public class Config : MonoBehaviour
{
    public static Config Instance { get; private set; }

    [Header("Interaction Settings")]
    [Tooltip("Time in seconds to hold before triggering a hold action.")]
    public float holdThreshold = 0.35f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
