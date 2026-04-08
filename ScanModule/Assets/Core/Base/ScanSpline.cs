using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(TubeRenderer))]
public class ScanSpline : MonoBehaviour
{
    private TubeRenderer _tubeRenderer;
    private List<Vector3> _markLocations = new();
    private List<Vector3> _markNormals = new();
    private List<GameObject> _markObjects = new();

    private void Awake()
    {
        _tubeRenderer = GetComponent<TubeRenderer>();
    }

    public void AddMark(Vector3 position, Vector3 normal)
    {
        Vector3 floatingPosition = position + (normal * Config.Instance.splineSurfaceOffset);

        if (Config.Instance.markPrefab != null)
        {
            GameObject mark = Instantiate(Config.Instance.markPrefab, floatingPosition, Quaternion.LookRotation(normal));
            mark.transform.SetParent(transform, true);
            _markObjects.Add(mark);
        }

        _markLocations.Add(transform.InverseTransformPoint(floatingPosition));
        _markNormals.Add(transform.InverseTransformDirection(normal));

        _tubeRenderer.RenderTube(GenerateProjectedSpline());
    }

    public void ClearMarks()
    {
        _markLocations.Clear();
        _markNormals.Clear();

        foreach (GameObject mark in _markObjects) Destroy(mark);
        _markObjects.Clear();

        _tubeRenderer.RenderTube(_markLocations);
    }

    private List<Vector3> GenerateProjectedSpline()
    {
        List<Vector3> splinePoints = new();
        if (_markLocations.Count < 2) return _markLocations;

        // pad the lists for Catmull-Rom math to work on the first/last points
        List<Vector3> pts = new(_markLocations);
        pts.Insert(0, _markLocations[0]);
        pts.Add(_markLocations[^1]);

        List<Vector3> nrms = new(_markNormals);
        nrms.Insert(0, _markNormals[0]);
        nrms.Add(_markNormals[^1]);

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
}
