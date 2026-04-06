using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScanSpawner : MonoBehaviour
{
    // initial scale (.stl,.ply are millimeters, Unity is meters)
    private readonly Vector3 _scale = 0.010f * Vector3.one;
    // initial rotation (to have teeth in line with the view, as if looking at a patient)
    private readonly Quaternion _rotation = Quaternion.Euler(-120f, 0f, 0f);

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
            GameObject scan = new(Path.GetFileNameWithoutExtension(path));
            scan.transform.SetPositionAndRotation(transform.position, _rotation);
            scan.transform.localScale = _scale;
            scan.layer = LayerMask.NameToLayer("ScanGrab");

            // set the mesh geometry
            MeshFilter filter = scan.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            // add a dummy renderer
            scan.AddComponent<MeshRenderer>();

            // give it a simpole box collider for interactions
            BoxCollider collider = scan.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;

            // make a child object for raycast interactions (adding points, lines, etc.)
            GameObject scanRaycast = new("RaycastHitBox");
            scanRaycast.transform.SetParent(scan.transform, false);
            scanRaycast.layer = LayerMask.NameToLayer("ScanRaycast");

            // offload the expensive mesh baking to a background thread
            MeshCollider raycastCollider = scanRaycast.AddComponent<MeshCollider>();
            EntityId meshEntityId = mesh.GetEntityId(); // we have to cache the EntityId before moving onto a new thread with Task.Run
            await Task.Run(() => Physics.BakeMesh(meshEntityId, false));
            raycastCollider.sharedMesh = mesh;

            // make it grabbable in VR (with snap-to-hand behavior off)
            XRGrabInteractable grabInteractable = scan.AddComponent<XRGrabInteractable>();
            grabInteractable.useDynamicAttach = true;

            // disable collisions and disable gravity
            if (!scan.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody = scan.AddComponent<Rigidbody>();
            }
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // apply the corect model theme (for materials)
            ScanController scanController = scan.AddComponent<ScanController>();
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

            // allow drawing marks and lines
            scan.AddComponent<ScanSpline>();
            if (!scan.TryGetComponent<LineRenderer>(out var lineRenderer))
            {
                lineRenderer = scan.AddComponent<LineRenderer>();
            }
            lineRenderer.startWidth = lineRenderer.endWidth = Config.Instance.lineWidth;
            lineRenderer.positionCount = 0; // delete the default 0,0,0 to 0,0,1 line
            if (Config.Instance.lineMaterial != null)
            {
                lineRenderer.material = Config.Instance.lineMaterial;
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

public static class FileParserExtensions
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
