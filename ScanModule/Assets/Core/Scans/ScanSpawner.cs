using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScanSpawner : MonoBehaviour
{
    public DentalScanTheme modelThemeVertexColor;
    public DentalScanTheme modelThemeNoColor;

    private void OnEnable()
    {
        UIEvents.OnImportScanRequested += HandleImportScanRequested;
    }

    private void OnDisable()
    {
        UIEvents.OnImportScanRequested -= HandleImportScanRequested;
    }

    private async void HandleImportScanRequested(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            UIEvents.RequestUIMessage("Path is invalid. Aborting import!");

            return;
        }

        UIEvents.RequestUIMessage($"Importing scan from:\n{path}");

        await SpawnScanAsync(path);
    }

    public async Task SpawnScanAsync(string path)
    {
        var Parser = path.GetParser();

        if (Parser == null)
        {
            return;
        }

        Mesh mesh = await Parser.ParseMeshAsync(path);
        
        if (mesh != null)
        {
            GameObject scan = new(Path.GetFileNameWithoutExtension(path));
            scan.transform.SetPositionAndRotation(transform.position, Config.Instance.scanRotation);
            scan.transform.localScale = Config.Instance.scanScale * Vector3.one;
            scan.layer = LayerMask.NameToLayer("ScanGrab");

            // set the mesh geometry
            MeshFilter filter = scan.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            // add a dummy renderer
            scan.AddComponent<MeshRenderer>();

            // give it a simple box collider for interactions
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

            // make it grabbable in VR (with snap-to-hand behavior off, throwing off, and with the simple BoxCollider)
            XRGrabInteractable grabInteractable = scan.AddComponent<XRGrabInteractable>();
            grabInteractable.useDynamicAttach = true;
            grabInteractable.throwOnDetach = false;
            grabInteractable.colliders.Clear();
            grabInteractable.colliders.Add(collider);

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
                else Debug.LogError("Missing DentalScanTheme for the VertexColor option!");
            }
            else
            {
                if (modelThemeNoColor != null) scanController.modelTheme = modelThemeNoColor;
                else Debug.LogError("Missing DentalScanTheme for the non-VertexColor option!");
            }

            // allow drawing marks and tube splines
            GameObject splineCanvas = new("SplineCanvas");
            splineCanvas.transform.SetParent(scan.transform, false);

            AnnotationManager scanSpline = splineCanvas.AddComponent<AnnotationManager>();
            if (!splineCanvas.TryGetComponent<AnnotationTube>(out var tubeRenderer))
            {
                tubeRenderer = splineCanvas.AddComponent<AnnotationTube>();
            }

            if (!splineCanvas.TryGetComponent<MeshRenderer>(out var tubeMeshRenderer))
            {
                tubeMeshRenderer = splineCanvas.AddComponent<MeshRenderer>();
            }

            if (Config.Instance.splineMaterial != null)
            {
                tubeMeshRenderer.sharedMaterial = Config.Instance.splineMaterial;
            }

            UIEvents.RequestUIMessage("Scan imported successfully!");
        }
        else
        {
            Debug.LogError($"Failed to load scan file: {path}");

            UIEvents.RequestUIMessage("Failed to import scan.");
        }
    }
}

public static class ScanParserFactory
{
    public static readonly Dictionary<string, IParserMesh> Parsers = new()
    {
        {".stl", new ParserMeshSTL()}, // Path.GetExtension returns with the ".", so ".stl", instead of just "stl"
        {".ply", new ParserMeshPLY()},
    };
}

public static class FileParserExtensions
{
    public static IParserMesh GetParser(this string path)
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
