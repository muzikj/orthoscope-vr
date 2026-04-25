using UnityEngine;

public class Config : MonoBehaviour
{
    public static Config Instance { get; private set; }

    [Header("General Settings")]
    [Tooltip("Default scale factor for the scans (in meters).")]
    public float scanScale = 0.010f;
    [Tooltip("Initial rotation (to have teeth in line with the view, as if looking at a patient)")]
    public Quaternion scanRotation = Quaternion.Euler(0f, 0f, 0f);

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
    [Tooltip("GameObject Prefab for the ghost mark (preview before placing - smaller than a normal Mark).")]
    public GameObject ghostMarkPrefab;
    [Tooltip("GameObject Prefab for the closing ghost mark (preview for closing the loop - bigger than a normal Mark).")]
    public GameObject closingGhostMarkPrefab;
    [Tooltip("Material for the spline.")]
    public Material splineMaterial;
    [Tooltip("Material for the loop spline (the spline that connects the last point to the first).")]
    public Material loopSplineMaterial;
    [Tooltip("Material for the in-progress line/plane.")]
    public Material markProgressMat;
    [Tooltip("Material for the completed line/plane.")]
    public Material markCompleteMat;
    [Tooltip("Radius of the spline.")]
    public float splineRadius = 0.005f;
    [Tooltip("Offset for the spline surface to prevent z-fighting.")]
    public float splineSurfaceOffset = 0.0005f;
    [Tooltip("How far out to start the raycast (in meters).")]
    public float projectionDistance = 0.05f;
    [Tooltip("Number of segments per curve for the spline (how many segments are created per line, between two points that is).")]
    public int splineCurveResolution = 16;
    [Tooltip("Number of segments around the radius of the spline (in essence, how rounded or box-like the tube is).")]
    public int splineRadialResolution = 8;
    [Tooltip("The multiplier for the mark radius that is the threshold for snapping the last point to the first point to create a closed loop.")]
    public float closeLoopSnappingThresholdMult = 0.9f;

    [Header("Base meshing")]
    [Tooltip("How far away from the tube the triangles still get cut (in meters).")]
    public float cutRadius = 0.0075f;
    [Tooltip("How far the skirt (between the n-gon and the teeth) of the base drops (in meters).")]
    public float skirtDepth = 0.01f;
    [Tooltip("How tall the straight n-gon walls are (in meters).")]
    public float baseHeight = 0.075f;
    [Tooltip("How much wider the base n-gon is than the teeth (in meters).")]
    public float widePadding = 0.025f;
    [Tooltip("How much to flare out the skirt, so it droops nicely (in meters).")]
    public float outwardFlare = 0.002f; 
    [Tooltip("Minimum distance between spline points to count them (in meters).")]
    public float minDistance = 0.003f;

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
