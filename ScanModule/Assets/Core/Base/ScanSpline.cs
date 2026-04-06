using UnityEngine;

using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ScanSpline : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    private List<Vector3> _markLocations = new();
    private List<GameObject> _markObjects = new();

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();

        // make the line renered local-space, so moving the scan also moves the spline
        _lineRenderer.useWorldSpace = false;
    }

    public void AddMark(Vector3 position, Vector3 normal)
    {
        GameObject markPrefab = Config.Instance.markPrefab;
        if (markPrefab != null)
        {
            GameObject mark = Instantiate(markPrefab, position, Quaternion.LookRotation(normal));
            mark.transform.SetParent(transform, true);
            _markObjects.Add(mark);
        }
        
        // make sure that the mark is local-space with respect to the scan
        Vector3 localPosisition = transform.InverseTransformPoint(position);
        _markLocations.Add(localPosisition);

        UpdateLineRenderer();
    }

    private void UpdateLineRenderer()
    {
        _lineRenderer.positionCount = _markLocations.Count;
        _lineRenderer.SetPositions(_markLocations.ToArray());
    }

    public  void ClearMarks()
    {
        _markLocations.Clear();

        foreach (GameObject mark in _markObjects)
        {
            Destroy(mark);
        }

        _markObjects.Clear();
        UpdateLineRenderer();
    }
}

