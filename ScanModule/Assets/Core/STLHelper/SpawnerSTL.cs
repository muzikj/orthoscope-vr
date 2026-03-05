using UnityEngine;

using System.Threading.Tasks;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SpawnerSTL : MonoBehaviour
{
    public string initialPath = "C:/Users/Martin/Desktop/FBMI/projekt2/pacient_01e.stl";
    public Material material;

    private async void Start()
    {         
        await SpawnSTLAsync(initialPath);
    }

    public async Task SpawnSTLAsync(string path)
    {
        Mesh mesh = await ImporterSTL.LoadSTLAsync(path);

        if (mesh != null)
        {
            GetComponent<MeshFilter>().mesh = mesh;

            if (material == null)
            {
                material = new(Shader.Find("Universal Render Pipeline/Lit"));
            }

            GetComponent<MeshRenderer>().material = material;
        }
        else
        {
            Debug.LogError($"Failed to load STL file at: {path}");
        }
    }
}
