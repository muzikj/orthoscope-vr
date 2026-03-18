using UnityEngine;

using System.Collections.Generic;
using System.IO;

public class LocalDentalScanProvider : MonoBehaviour
{
    [SerializeField] private string _directoryPath = @"C:\Users\Martin\Desktop\FBMI\projekt2";
    private const string _fileExtension = "*.stl";

    public List<DentalScanFileMeta> GetAvailableScans()
    {
        List<DentalScanFileMeta> scans = new();

        if (!Directory.Exists(_directoryPath))
        {
            Debug.Log($"Scan Directory {_directoryPath} does NOT exist!");
            return scans;
        }

        string[] files = Directory.GetFiles(_directoryPath, _fileExtension);

        foreach (string file in files)
        {
            scans.Add(new DentalScanFileMeta
            {
                displayName = Path.GetFileName(file),
                filePath = file,
            });
        }

        return scans;
    }
}
