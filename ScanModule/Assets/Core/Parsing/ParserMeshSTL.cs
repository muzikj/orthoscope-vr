using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ParserMeshSTL : ParserMesh
{
    protected override MeshData ParseScan(string path)
    {
        using BinaryReader br = new(File.Open(path, FileMode.Open, FileAccess.Read));

        // skip the .stl header
        br.BaseStream.Seek(80, SeekOrigin.Begin);

        uint triangleCount = br.ReadUInt32();

        List<Vector3> vertices = new();
        int[] triangles = new int[triangleCount * 3];

        Dictionary<Vector3Int, int> spatialMap = new();

        for (int i = 0; i < triangleCount; i++)
        {
            // skip face normals
            br.BaseStream.Seek(12, SeekOrigin.Current);

            int[] triIndices = new int[3];

            // read all 3 verices of the triangle
            for (int j = 0; j < 3; j++)
            {
                float triangleX = br.ReadSingle();
                float triangleY = br.ReadSingle();
                float triangleZ = br.ReadSingle();

                // .stl is Z-UP, but Unity is Y-UP, so we need to swap the Y and Z values
                Vector3 v = new(triangleX, triangleZ, triangleY);

                // hash precision to 3 decimal places (1000f) to neutralize floating-point drift
                Vector3Int hash = new
                (
                    Mathf.RoundToInt(v.x * 1000f),
                    Mathf.RoundToInt(v.y * 1000f),
                    Mathf.RoundToInt(v.z * 1000f)
                );

                if (!spatialMap.TryGetValue(hash, out int index))
                {
                    index = vertices.Count;
                    spatialMap[hash] = index;

                    vertices.Add(v);
                }

                triIndices[j] = index;
            }

            // the vertex order has to be changed from 0-1-2 to 0-2-1 because we flipped the Y and Z values
            triangles[i * 3 + 0] = triIndices[0];
            triangles[i * 3 + 1] = triIndices[2];
            triangles[i * 3 + 2] = triIndices[1];

            // skip the .stl attribute byte count
            br.BaseStream.Seek(2, SeekOrigin.Current);
        }

        return new MeshData
        {
            vertices = vertices.ToArray(),
            triangles = triangles,
        };
    }
}
