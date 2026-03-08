using System;
using UnityEngine;

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
    
    // delete selected scans
    public static event Action OnResetRequested;
    public static void RequestResetScan() => OnResetRequested?.Invoke();

    // reset selected scans
    public static event Action OnDeleteRequested;
    public static void RequestDeleteScan() => OnDeleteRequested?.Invoke();

    // scale selected scans
    public static event Action<float> OnScaleRequested;
    public static void RequestScaleScan(float scaleFactor) => OnScaleRequested?.Invoke(scaleFactor);
}
