using UnityEngine;

using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI text;

    [Header("Dental Scan Selection")]
    public TMP_Dropdown scanDropdown;
    public LocalDentalScanProvider scanProvider;

    private List<DentalScanFileMeta> _availableScans = new();

    private void Awake()
    {
        DisableText();
    }

    private void Start()
    {
        PopulateDropdown();
    }

    private void PopulateDropdown()
    {
        if (scanDropdown == null || scanProvider == null) return;

        scanDropdown.ClearOptions();

        _availableScans.Clear();
        _availableScans = scanProvider.GetAvailableScans();

        List<string> scanOptions = new();
        foreach (DentalScanFileMeta scan in _availableScans)
        {
            scanOptions.Add(scan.displayName);
        }

        scanDropdown.AddOptions(scanOptions);
    }

    public void ClickRefreshDropdown()
    {
        PopulateDropdown();

        ChangeText("Scan list refreshed!");
        EnableText();

        CancelInvoke(nameof(DisableText));
        Invoke(nameof(DisableText), 2f);
    }    

    public void ClickImportSelectedScan()
    {
        if (_availableScans.Count == 0)
        {
            Debug.Log("No scans available!");
            return;
        }

        int index = scanDropdown.value;
        string selectedScanPath = _availableScans[index].filePath;

        ClickImportScan(selectedScanPath);
    }

    public void ClickImportScan(string path)
    {
         ScanEvents.RequestImportScan(path);
    }    

    private void OnEnable()
    {
        ScanEvents.OnImportScanRequested += HandleImportScanRequested;
        ScanEvents.OnImportScanCompleted += HandleImportScanCompleted;
    }

    private void OnDisable()
    {
        ScanEvents.OnImportScanRequested -= HandleImportScanRequested;
        ScanEvents.OnImportScanCompleted -= HandleImportScanCompleted;
    }

    private void HandleImportScanRequested(string path)
    {
        ChangeText($"Importing scan from:\n{path}");
        EnableText();
    }

    private void HandleImportScanCompleted(bool success)
    {
        ChangeText(success ? "Scan imported successfully!" : "Failed to import scan.");
        Invoke(nameof(DisableText), 3f);
    }

    private void DisableText() => ChangeTextVisibility(false);
    private void EnableText() => ChangeTextVisibility(true);

    private void ChangeTextVisibility(bool visible)
    {
        if (text != null)
        {
            text.gameObject.SetActive(visible);
        }
    }

    private void ChangeText(string newText)
    {
        if (text != null)
        {
            text.text = newText;
        }
    }

    public void ClickDeleteScan()
    {
        ScanEvents.RequestDeleteScan();
    }

    public void ClickResetScan()
    {
        ScanEvents.RequestResetScan();
    }

    public void ClickChangeScale(float scaleFactor)
    {
        ScanEvents.RequestScaleScan(scaleFactor);
    }
}
