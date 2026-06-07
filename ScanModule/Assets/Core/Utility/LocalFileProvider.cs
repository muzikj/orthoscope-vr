using UnityEngine;

using System.Collections.Generic;
using System.IO;

public struct FileMeta
{
    public string displayName;
    public string filePath;
}

public enum FileFormat
{
    STL,
    PLY,
}

public class LocalFileProvider : MonoBehaviour
{
    [Tooltip("Directory to scan for files, e.g. TestData in the ScanModule project folder.")]
    [SerializeField] private string _directory = "TestData";

    private string GetScanDirectory()
    {
        string path = Application.persistentDataPath;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN

        path = Path.GetFullPath(Path.Combine(Application.dataPath, "../", _directory));

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);

            Debug.Log($"[LocalFileProvider] Created PC/Win directory at: {path}!");
        }

#elif UNITY_ANDROID

        path = Path.Combine(Application.persistentDataPath, _directory);

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);

            Debug.Log($"[LocalFileProvider] Created VR/Android directory at: {path}!");
        }

#endif

        return path;
    }

    public List<FileMeta> GetAvailableFiles(FileFormat fileFormat)
    {
        string targetDirectory = GetScanDirectory();

        List<FileMeta> availableFiles = new();

        if (!Directory.Exists(targetDirectory))
        {
            Debug.Log($"Scan Directory {targetDirectory} does NOT exist!");
            
            return availableFiles;
        }

        string searchPattern = fileFormat.GetSearchPattern();
        string[] files = Directory.GetFiles(targetDirectory, searchPattern);

        foreach (string file in files)
        {
            availableFiles.Add(new FileMeta
            {
                displayName = Path.GetFileName(file),
                filePath = file,
            });
        }

        return availableFiles;
    }
}

public static class FilePatternExtensions
{
    public static string GetSearchPattern(this FileFormat format)
    {
        // prefix format with "*" for all files and make sure it is in lower-case regardless of current culture
        return $"*.{format.ToString().ToLowerInvariant()}";
    }
}
