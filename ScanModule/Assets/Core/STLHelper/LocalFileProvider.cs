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
    [Header("Source Settings")]
    [SerializeField] private string _directoryPath = @"C:\Users\Martin\Desktop\FBMI\projekt2";

    public List<FileMeta> GetAvailableFiles(FileFormat fileFormat)
    {
        List<FileMeta> availableFiles = new();

        if (!Directory.Exists(_directoryPath))
        {
            Debug.Log($"Scan Directory {_directoryPath} does NOT exist!");
            
            return availableFiles;
        }

        string searchPattern = fileFormat.GetSearchPattern();
        string[] files = Directory.GetFiles(_directoryPath, searchPattern);

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
