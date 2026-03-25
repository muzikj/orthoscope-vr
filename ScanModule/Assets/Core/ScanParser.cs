using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using System.Threading.Tasks;

public interface IScanParser
{
    Task<Mesh> ParseMeshAsync(string path);
}

public struct MeshData
{
    public Vector3[] vertices;
    public Vector3[] normals;
    public int[] triangles;
    public Color32[] colors;
}

public abstract class ScanParser : IScanParser
{
    protected abstract MeshData ParseScan(string path);

    public async Task<Mesh> ParseMeshAsync(string path)
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
        MeshData meshData = await Task.Run(() => ParseScan(path));

        Mesh mesh = new()
        {
            name = Path.GetFileNameWithoutExtension(path),

            // if we have more than 2^16 (65K) vertices (almost always), we need to use 32-bit indices instead
            indexFormat = meshData.vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
        };

        // assign the geometry data to the mesh using the optimized set methods
        mesh.SetVertices(meshData.vertices);
        mesh.SetTriangles(meshData.triangles, 0, false);

        // try setting vertex colors, if present
        if (meshData.colors != null)
        {
            mesh.SetColors(meshData.colors);
        }

        // try setting the normals, recalculate if not present
        if (meshData.normals != null)
        {
            mesh.SetNormals(meshData.normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        // make sure we have the correct bounding box
        mesh.RecalculateBounds();

        return mesh;
    }
}
