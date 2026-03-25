using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScanSpawner : MonoBehaviour
{
    // initial scale (.stl is millimeters, Unity is meters)
    private readonly float _scale = 0.010f;

    public ModelTheme modelThemeVertexColor;
    public ModelTheme modelThemeNoColor;

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
        await SpawnScanAsync(path);
    }

    public async Task SpawnScanAsync(string path)
    {
        var Parser = path.GetParser();
        if (Parser == null) return;

        Mesh mesh = await Parser.ParseMeshAsync(path);

        if (mesh != null)
        {
            GameObject stl = new(Path.GetFileNameWithoutExtension(path));
            stl.transform.SetPositionAndRotation(transform.position, transform.rotation);
            stl.transform.localScale = Vector3.one * _scale;

            // set the mesh geometry
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

            // apply the corect model theme (for materials)
            ScanController scanController = stl.AddComponent<ScanController>();
            if (mesh.HasVertexAttribute(VertexAttribute.Color))
            {
                if (modelThemeVertexColor != null) scanController.modelTheme = modelThemeVertexColor;
                else Debug.LogError("Missing ModelTheme for the VertexColor option!");
            }
            else
            {
                if (modelThemeNoColor != null) scanController.modelTheme = modelThemeNoColor;
                else Debug.LogError("Missing ModelTheme for the non-VertexColor option!");
            }

            ScanEvents.NotifyImportScanCompleted(true);
        }
        else
        {
            Debug.LogError($"Failed to load scan file: {path}");

            ScanEvents.NotifyImportScanCompleted(false);
        }
    }
}

public static class ScanParserFactory
{
    public static readonly Dictionary<string, IScanParser> Parsers = new()
    {
        // Path.GetExtension returns with the ".", so ".stl", instead of just "stl"
        {".stl", new ScanParserSTL()},
        {".ply", new ScanParserPLY()},
    };
}

public static class FileFormatExtensions
{
    public static IScanParser GetParser(this string path)
    {
        // avoid ".STL" x ".stl" capitalized shenanigans with ToLowerInvariant
        string format = Path.GetExtension(path).ToLowerInvariant();

        if (ScanParserFactory.Parsers.TryGetValue(format, out var parser))
        {
            return parser;
        }

        Debug.LogError($"Cannot parse file {path}. Unsupported type {format}!");
        return null;
    }
}
