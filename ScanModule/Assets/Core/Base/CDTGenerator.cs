using UnityEngine;
using System.Collections.Generic;

using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Topology;

public static class CDTGenerator
{
    public static (Vector2[] vertices, int[] triangles) Triangulate2D(List<Vector2> outerBoundary, List<Vector2> innerBoundary = null)
    {
        Polygon polygon = new();

        // outer boundary
        List<Vertex> boundaryVertices = new(outerBoundary.Count);

        foreach (Vector2 pt in outerBoundary)
        {
            boundaryVertices.Add(new Vertex(pt.x, pt.y));
        }
        
        polygon.Add(new Contour(boundaryVertices), false);

        // inner boundary
        if (innerBoundary != null && innerBoundary.Count > 0)
        {
            List<Vertex> innerVertices = new(innerBoundary.Count);

            foreach (Vector2 pt in innerBoundary)
            {
                innerVertices.Add(new Vertex(pt.x, pt.y));
            }

            // do not punch a hole
            polygon.Add(new Contour(innerVertices), false);
        }

        ConstraintOptions constraints = new()
        {
            ConformingDelaunay = false
        };

        IMesh cdtMesh = polygon.Triangulate(constraints);

        return ConvertToUnityData(cdtMesh, innerBoundary);
    }

    private static (Vector2[], int[]) ConvertToUnityData(IMesh cdtMesh, List<Vector2> holePolygon)
    {
        Vector2[] unityVertices = new Vector2[cdtMesh.Vertices.Count];
        Dictionary<int, int> idToIndex = new();

        int index = 0;
        foreach (Vertex v in cdtMesh.Vertices)
        {
            unityVertices[index] = new Vector2((float)v.X, (float)v.Y);
            idToIndex[v.ID] = index;
            index++;
        }

        List<int> unityTriangles = new();
        Vector2[] holePolyArray = holePolygon?.ToArray();

        foreach (Triangle tri in cdtMesh.Triangles)
        {
            Vector2 v0 = unityVertices[idToIndex[tri.GetVertex(0).ID]];
            Vector2 v1 = unityVertices[idToIndex[tri.GetVertex(1).ID]];
            Vector2 v2 = unityVertices[idToIndex[tri.GetVertex(2).ID]];

            // check if the center of this triangle is inside the teeth spline
            if (holePolyArray != null && holePolyArray.Length > 2)
            {
                Vector2 centroid = (v0 + v1 + v2) / 3f;

                if (IsPointInPolygon(centroid, holePolyArray))
                {
                    continue;
                }
            }

            unityTriangles.Add(idToIndex[tri.GetVertex(2).ID]);
            unityTriangles.Add(idToIndex[tri.GetVertex(1).ID]);
            unityTriangles.Add(idToIndex[tri.GetVertex(0).ID]);
        }

        return (unityVertices, unityTriangles.ToArray());
    }

    // mathematical raycast (even-odd rule) to accurately detect if a point is inside a complex 2D shape
    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool isInside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) && (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }
}
