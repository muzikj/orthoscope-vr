using System;
using UnityEngine;

public class ScanEvents
{
    public static event Action<string> OnImportScanRequested;

    public static event Action<bool> OnImportScanCompleted;

    public static void RequestImportScan(string path)
    {
        OnImportScanRequested?.Invoke(path);
    }

    public static void NotifyImportScanCompleted(bool success)
    {
        OnImportScanCompleted?.Invoke(success);
    }
}
