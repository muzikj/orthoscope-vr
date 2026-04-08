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
    [Tooltip("Radius of the spline.")]
    public float splineRadius = 0.005f;
    [Tooltip("Offset for the spline surface to prevent z-fighting.")]
    public float splineSurfaceOffset = 0.0005f;
    [Tooltip("How far out to start the raycast (in meters).")]
    public float projectionDistance = 0.05f;
    [Tooltip("Number of segments per curve for the spline (how many segments are created per line, between two points that is).")]
    public int splineCurveResolution = 20;
    [Tooltip("Number of segments around the radius of the spline (in essence, how rounded or box-like the tube is).")]
    public int splineRadialResolution = 16;
    [Tooltip("Material for the spline.")]
    public Material splineMaterial;

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
