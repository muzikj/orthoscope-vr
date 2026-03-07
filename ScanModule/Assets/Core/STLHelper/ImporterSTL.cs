using UnityEngine;

using UnityEngine.Rendering;

using System.IO;
using System.Threading.Tasks;

public static class ImporterSTL
{
    private struct MeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public int[] vertexOrder;
    }

    public static async Task<Mesh> LoadSTLAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Path is null or empty.");
            return null;
        }

        if (!File.Exists(path))
        {
            Debug.LogError($"File not found at path: {path}");
            return null;
        }

        // offload parsing to a different thread for peformance (mesh building; however, has to be done on the main thread)
        MeshData meshData = await Task.Run(() => ParseSTL(path));

        Mesh mesh = new()
        {
            name = Path.GetFileNameWithoutExtension(path),

            // if we have more than 2^16 (65K) vertices (almost always), we need to use 32-bit indices instead
            indexFormat = meshData.vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
        };

        // assign the geometry data to the mesh using the optimized set methods
        mesh.SetVertices(meshData.vertices);
        mesh.SetNormals(meshData.normals);
        mesh.SetTriangles(meshData.vertexOrder, 0, false);

        // make sure we have the correct bounding box (also, no need to recalculate the normals, as we already have them and need to save performance)
        mesh.RecalculateBounds();

        return mesh;
    }

    private static MeshData ParseSTL(string path)
    {
        using BinaryReader br = new(File.Open(path, FileMode.Open, FileAccess.Read));

        // skip the .stl header
        br.BaseStream.Seek(80, SeekOrigin.Begin);

        uint triangleCount = br.ReadUInt32();

        Vector3[] vertices = new Vector3[triangleCount * 3];
        Vector3[] normals = new Vector3[triangleCount * 3];
        int[] vertexOrder = new int[triangleCount * 3];

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

            // the vertex order has to be changed from 0, 1, 2 to 0, 2, 1 because we flipped the Y and Z values, which would otherwise incorrectly change the winding order
            vertexOrder[i * 3] = i * 3;
            vertexOrder[i * 3 + 1] = i * 3 + 2;
            vertexOrder[i * 3 + 2] = i * 3 + 1;

            // skip the .stl attribute byte count
            br.BaseStream.Seek(2, SeekOrigin.Current);
        }

        return new MeshData
        {
            vertices = vertices,
            normals = normals,
            vertexOrder = vertexOrder,
        };
    }
}
