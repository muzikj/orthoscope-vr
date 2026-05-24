using System;
using UnityEngine;

public enum OrthoView
{
    Front,
    Back,
    Left,
    Right,
    Top,
    Bottom
}

public class ScanEvents // TODO: add sound effects
{
    // importing .stl scans
    public static event Action<string> OnImportScanRequested;
    public static event Action<bool> OnImportScanCompleted;

    public static void RequestImportScan(string path) => OnImportScanRequested?.Invoke(path);
    public static void NotifyImportScanCompleted(bool success) => OnImportScanCompleted?.Invoke(success);

    // selecting scans
    public static event Action<GameObject> OnScanSelected;
    public static event Action<GameObject> OnScanDeselected;

    public static void NotifyScanSelected(GameObject scan) => OnScanSelected?.Invoke(scan);
    public static void NotifyScanDeselected(GameObject scan) => OnScanDeselected?.Invoke(scan);
    
    // deleting selected scans
    public static event Action OnResetRequested;
    public static void RequestResetScan() => OnResetRequested?.Invoke();

    // resetting selected scans
    public static event Action OnDeleteRequested;
    public static void RequestDeleteScan() => OnDeleteRequested?.Invoke();

    // scaling selected scans
    public static event Action<float> OnScaleRequested;
    public static void RequestScaleScan(float scaleFactor) => OnScaleRequested?.Invoke(scaleFactor);

    // advancing the base builder
    public static event Action OnAdvanceBuilderRequested;
    public static void RequestAdvanceBuilder() => OnAdvanceBuilderRequested?.Invoke();

    // logging messages to the UI banner
    public static event Action<string> OnUIMessageRequested;
    public static void RequestUIMessage(string message) => OnUIMessageRequested?.Invoke(message);

    // rotating selected scans to orthographic views
    public static event Action<OrthoView> OnSnapViewRequested;
    public static void RequestSnapView(OrthoView view) => OnSnapViewRequested?.Invoke(view);

    // multi-grabbing mvoement
    public static event Action<Transform> OnGroupGrabStarted;
    public static event Action<Transform> OnGroupMoved;
    public static event Action OnGroupGrabEnded;

    public static void RequestGroupGrabStart(Transform leaderTransform) => OnGroupGrabStarted?.Invoke(leaderTransform);
    public static void RequestGroupMove(Transform leaderTransform) => OnGroupMoved?.Invoke(leaderTransform);
    public static void RequestGroupGrabEnd() => OnGroupGrabEnded?.Invoke();
}
