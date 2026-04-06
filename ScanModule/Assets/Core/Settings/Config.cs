using UnityEngine;

public class Config : MonoBehaviour
{
    public static Config Instance { get; private set; }

    [Header("Interaction Settings")]
    [Tooltip("Time in seconds to hold before triggering a hold action.")]
    public float holdThreshold = 0.35f;
    [Tooltip("Maximum distance for raycasting when placing spline marks.")]
    public float raycastMaxDistance = 3.0f;

    [Header("Spline Marking Settings")]
    [Tooltip("LayerMask for raycasting when placing spline markers.")]
    public LayerMask scanRaycastLayer;
    [Tooltip("GameObject Prefab for the 3D mark.")]
    public GameObject markPrefab;
    [Tooltip("Width of the spline.")]
    public float lineWidth = 0.001f;
    [Tooltip("Material for the spline.")]
    public Material lineMaterial;

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
