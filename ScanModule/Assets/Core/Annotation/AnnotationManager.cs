using UnityEngine;

using System.Collections.Generic;

[RequireComponent(typeof(AnnotationTube))]
public class AnnotationManager : MonoBehaviour
{
    public bool bClosed = false;

    private AnnotationTube _tubeRenderer;

    private List<Vector3> _markLocations = new();
    private List<Vector3> _markNormals = new();
    private List<GameObject> _markObjects = new();

    private void Awake()
    {
        _tubeRenderer = GetComponent<AnnotationTube>();
    }

    private void OnEnable()
    {
        UIEvents.OnScaleRequested += HandleScaleRequested;
    }

    private void OnDisable()
    {
        UIEvents.OnScaleRequested -= HandleScaleRequested;
    }

    public bool WillSnapToStart(Vector3 position, out Vector3 snapPosition, out Vector3 snapNormal)
    {
        snapPosition = position;
        snapNormal = Vector3.up;

        if (_markLocations.Count < 3) return false;

        Vector3 firstPointWorld = transform.TransformPoint(_markLocations[0]); // has to be world-space, so it's not scaled
        
        float distanceToStart = Vector3.Distance(position, firstPointWorld);

        float currentMarkWorldSize = Config.Instance.markPrefab != null ? Config.Instance.markPrefab.transform.localScale.x * transform.lossyScale.x : 1f;
        float scaledSnapDistance = Config.Instance.closeLoopSnappingThresholdMult * currentMarkWorldSize;

        if (distanceToStart <= scaledSnapDistance)
        {
            snapPosition = firstPointWorld;
            snapNormal = transform.TransformDirection(_markNormals[0]);

            return true;
        }

        return false;
    }

    public void AddSplinePoint(Vector3 position, Vector3 normal)
    {
        if (bClosed) return;

        Vector3 localPosition = transform.InverseTransformPoint(position);
        Vector3 localNormal = transform.InverseTransformDirection(normal);

        // closing a loop point
        if (WillSnapToStart(position, out _, out _))
        {
            bClosed = true;

            _tubeRenderer.SetTubeMaterial(Config.Instance.loopSplineMaterial);
            _tubeRenderer.RenderTube(GenerateProjectedSpline(), bClosed);

            return; // we don't want a new mark here
        }

        // regular point addition
        if (Config.Instance.markPrefab != null)
        {
            GameObject mark = Instantiate(Config.Instance.markPrefab, position, Quaternion.LookRotation(normal));
            mark.transform.SetParent(transform, true);
            mark.transform.localScale = Config.Instance.markPrefab.transform.localScale;
            _markObjects.Add(mark);
        }

        _markLocations.Add(localPosition);
        _markNormals.Add(localNormal);

        _tubeRenderer.RenderTube(GenerateProjectedSpline(), bClosed);
    }

    public void RemoveLastSplinePoint()
    {
        if (bClosed)
        {
            bClosed = false;

            _tubeRenderer.SetTubeMaterial(Config.Instance.splineMaterial);
            _tubeRenderer.RenderTube(GenerateProjectedSpline(), bClosed);

            return;
        }

        if (_markLocations.Count > 0)
        {
            _markLocations.RemoveAt(_markLocations.Count - 1);
            _markNormals.RemoveAt(_markNormals.Count - 1);

            if (_markObjects.Count > 0)
            {
                Destroy(_markObjects[^1]);
                _markObjects.RemoveAt(_markObjects.Count - 1);
            }

            _tubeRenderer.RenderTube(GenerateProjectedSpline(), bClosed);
        }
    }

    public void ClearSplineMarks()
    {
        bClosed = false;

        _tubeRenderer.SetTubeMaterial(Config.Instance.splineMaterial);

        _markLocations.Clear();
        _markNormals.Clear();

        foreach (GameObject mark in _markObjects) Destroy(mark);
        _markObjects.Clear();

        _tubeRenderer.RenderTube(_markLocations, bClosed);
    }

    private List<Vector3> GenerateProjectedSpline()
    {
        List<Vector3> splinePoints = new();
        if (_markLocations.Count < 2) return _markLocations;

        List<Vector3> pts = new(_markLocations);
        List<Vector3> nrms = new(_markNormals);

        // pad the lists for Catmull-Rom math to work on the first/last points
        if (bClosed)
        {
            pts.Insert(0, _markLocations[^1]);
            pts.Add(_markLocations[0]);
            pts.Add(_markLocations[1]);

            nrms.Insert(0, _markNormals[^1]);
            nrms.Add(_markNormals[0]);
            nrms.Add(_markNormals[1]);
        }
        else
        {
            pts.Insert(0, _markLocations[0]);
            pts.Add(_markLocations[^1]);

            nrms.Insert(0, _markNormals[0]);
            nrms.Add(_markNormals[^1]);
        }

        for (int i = 1; i < pts.Count - 2; i++)
        {
            for (int j = 0; j <= Config.Instance.splineCurveResolution; j++)
            {
                if (j == 0 && i > 1) continue; // prevent overlapping points at joints

                float t = j / (float)Config.Instance.splineCurveResolution;
                Vector3 rawPoint = GetCatmullRom(t, pts[i - 1], pts[i], pts[i + 1], pts[i + 2]);
                Vector3 rawNormal = Vector3.Lerp(nrms[i], nrms[i + 1], t).normalized;

                // shrink-wrap to stick to the dental scan surface
                Vector3 projectedPoint = ProjectOntoSurface(rawPoint, rawNormal);
                splinePoints.Add(projectedPoint);
            }
        }

        return splinePoints;
    }

    public List<Vector3> GetSplinePoints() => GenerateProjectedSpline();

    private Vector3 ProjectOntoSurface(Vector3 localPoint, Vector3 localNormal)
    {
        Vector3 worldPoint = transform.TransformPoint(localPoint);
        Vector3 worldNormal = transform.TransformDirection(localNormal);

        // start raycast slightly floating in the air, shoot it backwards at the bone; ensuring we aren't inside or too far away from the surface
        Vector3 rayStart = worldPoint + (worldNormal * Config.Instance.projectionDistance);

        if (Physics.Raycast(rayStart, -worldNormal, out RaycastHit hit, Config.Instance.projectionDistance * 2f, Config.Instance.scanRaycastLayer))
        {
            // snap to the surface
            Vector3 surfacePoint = hit.point + (hit.normal * Config.Instance.splineSurfaceOffset);
            return transform.InverseTransformPoint(surfacePoint);
        }

        return localPoint;
    }

    private Vector3 GetCatmullRom(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 a = 2f * p1;
        Vector3 b = p2 - p0;
        Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
        Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

        return 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));
    }

    private void HandleScaleRequested(float scaleFactor)
    {
        _tubeRenderer.RenderTube(GenerateProjectedSpline(), bClosed);
    }
}
