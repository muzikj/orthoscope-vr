using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TubeRenderer : MonoBehaviour
{
    private Mesh _mesh;

    // helper struct to hold the exact position and rotation for each slice of the tube
    private struct OrientedPoint
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
    }

    private void Awake()
    {
        _mesh = new() { name = "ProceduralTube" };
        _mesh.MarkDynamic(); // since it's generated, make it dynamic for better performance

        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    public void RenderTube(List<Vector3> points, bool bClosed)
    {
        if (points == null || points.Count < 2)
        {
            _mesh.Clear();

            return;
        }

        OrientedPoint[] path = CalculateParallelTransportPath(points);
        GenerateMeshGeometry(path, bClosed);
    }

    private OrientedPoint[] CalculateParallelTransportPath(List<Vector3> points)
    {
        OrientedPoint[] path = new OrientedPoint[points.Count];

        // initial point setup
        path[0].Position = points[0];
        path[0].Forward = (points[1] - points[0]).normalized;
        if (path[0].Forward == Vector3.zero) path[0].Forward = Vector3.forward;

        path[0].Up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(path[0].Forward, path[0].Up)) > 0.99f) path[0].Up = Vector3.right;
        path[0].Up = Vector3.Cross(path[0].Forward, Vector3.Cross(path[0].Up, path[0].Forward)).normalized;

        // transport frame down the path to prevent twisting
        for (int i = 1; i < points.Count; i++)
        {
            path[i].Position = points[i];

            // forward
            if (i < points.Count - 1)
                path[i].Forward = (points[i + 1] - points[i - 1]).normalized;
            else
                path[i].Forward = (points[i] - points[i - 1]).normalized;

            // up (hinge rotation)
            Vector3 axis = Vector3.Cross(path[i - 1].Forward, path[i].Forward);
            if (axis.sqrMagnitude > 1e-06f)
            {
                float angle = Vector3.Angle(path[i - 1].Forward, path[i].Forward);
                Quaternion rot = Quaternion.AngleAxis(angle, axis);

                path[i].Up = rot * path[i - 1].Up;
            }
            else
            {
                path[i].Up = path[i - 1].Up;
            }
        }

        return path;
    }

    private void GenerateMeshGeometry(OrientedPoint[] path, bool bClosed)
    {
        int ringCount = bClosed ? path.Length - 1: path.Length;
        int verticesCount = ringCount * Config.Instance.splineRadialResolution;

        int stitchCount = bClosed ? ringCount : ringCount - 1;
        int trianglesCount = stitchCount * Config.Instance.splineRadialResolution * 6;

        Vector3[] vertices = new Vector3[verticesCount];
        int[] triangles = new int[trianglesCount];

        float actualRadius = Config.Instance.splineRadius / Config.Instance.scanScale;

        // generate 3D rings
        for (int i = 0; i < ringCount; i++)
        {
            Vector3 right = Vector3.Cross(path[i].Up, path[i].Forward).normalized;

            for (int j = 0; j < Config.Instance.splineRadialResolution; j++)
            {
                float angle = (float)j / Config.Instance.splineRadialResolution * Mathf.PI * 2f;
                Vector3 localPos = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * path[i].Up) * actualRadius;

                vertices[i * Config.Instance.splineRadialResolution + j] = path[i].Position + localPos;
            }
        }

        // stitch the tri's
        int triIndex = 0;
        for (int i = 0; i < stitchCount; i++)
        {
            for (int j = 0; j < Config.Instance.splineRadialResolution; j++)
            {
                int current = i * Config.Instance.splineRadialResolution + j;
                int currentPlus1 = i * Config.Instance.splineRadialResolution + ((j + 1) % Config.Instance.splineRadialResolution);
                
                
                int next, nextPlus1;

                if (bClosed && i == ringCount - 1) // if it's closed and we're at the last ring, wrap around to the first ring for stitching
                {
                    next = j;
                    nextPlus1 = (j + 1) % Config.Instance.splineRadialResolution;
                }
                else
                {
                    next = current + Config.Instance.splineRadialResolution;
                    nextPlus1 = next - j + ((j + 1) % Config.Instance.splineRadialResolution);
                }

                triangles[triIndex++] = current;
                triangles[triIndex++] = next;
                triangles[triIndex++] = currentPlus1;

                triangles[triIndex++] = currentPlus1;
                triangles[triIndex++] = next;
                triangles[triIndex++] = nextPlus1;
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    public void SetMaterial(Material material)
    {
        GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
