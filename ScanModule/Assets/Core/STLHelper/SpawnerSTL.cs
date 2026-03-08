using UnityEngine;

using System.Threading.Tasks;
using System.IO;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SpawnerSTL : MonoBehaviour
{
    // initial scale (.stl is millimeters, Unity is meters)
    private readonly float _scale = 0.010f;

    public ModelTheme modelTheme;

    private void OnEnable()
    {
        ScanEvents.OnImportScanRequested += HandleImportScanRequested;
    }

    private void OnDisable()
    {
        ScanEvents.OnImportScanRequested -= HandleImportScanRequested;
    }

    private async void HandleImportScanRequested(string path)
    {
        await SpawnSTLAsync(path);
    }

    public async Task SpawnSTLAsync(string path)
    {
        Mesh mesh = await ImporterSTL.LoadSTLAsync(path);

        if (mesh != null)
        {
            GameObject stl = new(Path.GetFileNameWithoutExtension(path));
            stl.transform.SetPositionAndRotation(transform.position, transform.rotation);
            stl.transform.localScale = Vector3.one * _scale;

            // set the .stl mesh (geometry)
            MeshFilter filter = stl.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            // add a dummy renderer
            MeshRenderer _ = stl.AddComponent<MeshRenderer>();

            // give it a collider for interactions (a mesh collider is out of the question for performance reasons, hence a simple box collider instead)
            BoxCollider collider = stl.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;

            // make it grabbable in VR (with snap-to-hand behavior off)
            XRGrabInteractable grabInteractable = stl.AddComponent<XRGrabInteractable>();
            grabInteractable.useDynamicAttach = true;

            // disable collisions and disable gravity
            if (!stl.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody = stl.AddComponent<Rigidbody>();
            }
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // apply the default model theme
            ScanController scanController = stl.AddComponent<ScanController>();
            if (modelTheme != null)
            {
                scanController.modelTheme = modelTheme;
            }

            ScanEvents.NotifyImportScanCompleted(true);
        }
        else
        {
            Debug.LogError($"Failed to load STL file at: {path}");

            ScanEvents.NotifyImportScanCompleted(false);
        }
    }
}
