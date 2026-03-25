using UnityEngine;
using System.IO;

public class ScanParserSTL : ScanParser
{
    protected override MeshData ParseScan(string path)
    {
        using BinaryReader br = new(File.Open(path, FileMode.Open, FileAccess.Read));

        // skip the .stl header
        br.BaseStream.Seek(80, SeekOrigin.Begin);

        uint triangleCount = br.ReadUInt32();

        Vector3[] vertices = new Vector3[triangleCount * 3];
        Vector3[] normals = new Vector3[triangleCount * 3];
        int[] triangles = new int[triangleCount * 3];

        for (int i = 0; i < triangleCount; i++)
        {
            float normalX = br.ReadSingle();
            float normalY = br.ReadSingle();
            float normalZ = br.ReadSingle();

            // read all 3 verices of the triangle
            for (int j = 0; j < 3; j++)
            {
                float triangleX = br.ReadSingle();
                float triangleY = br.ReadSingle();
                float triangleZ = br.ReadSingle();

                int vertexIndex = i * 3 + j;

                // .stl is Z-UP, but Unity is Y-UP, so we need to swap the Y and Z values
                vertices[vertexIndex] = new Vector3(triangleX, triangleZ, triangleY);
                normals[vertexIndex] = new Vector3(normalX, normalZ, normalY);
            }

            // the vertex order has to be changed from 0-1-2 to 0-2-1 because we flipped the Y and Z values, which would otherwise incorrectly change the winding order
            triangles[i * 3] = i * 3;
            triangles[i * 3 + 1] = i * 3 + 2;
            triangles[i * 3 + 2] = i * 3 + 1;

            // skip the .stl attribute byte count
            br.BaseStream.Seek(2, SeekOrigin.Current);
        }

        return new MeshData
        {
            vertices = vertices,
            normals = normals,
            triangles = triangles,
        };
    }
}
