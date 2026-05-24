using UnityEngine;

using TMPro;
using System.Collections.Generic;
using System;

public class UIManager : MonoBehaviour
{
    [Header("Feedback Infographics")]
    public TextMeshProUGUI text;

    [Header("Format Selection")]
    public TMP_Dropdown formatDropdown;

    [Header("Dental Scan Selection")]
    public TMP_Dropdown scanDropdown;
    public LocalFileProvider scanProvider;

    private FileFormat _fileFormat;
    private List<FileMeta> _availableScans = new();

    private void Awake()
    {
        DisableText();
    }

    private void Start()
    {
        PopulateFormatOptions();
        PopulateScans();
    }

    private void PopulateFormatOptions()
    {
        if (formatDropdown == null) return;

        formatDropdown.ClearOptions();

        string[] formats = Enum.GetNames(typeof(FileFormat));
        formatDropdown.AddOptions(new List<string>(formats));

        formatDropdown.onValueChanged.AddListener(OnFormatChanged);
    }

    private void OnFormatChanged(int index)
    {
        FileFormat newFormat = (FileFormat)index;

        if (_fileFormat != newFormat)
        {
            _fileFormat = newFormat;

            PopulateScans();
        }
    }

    private void PopulateScans()
    {
        if (scanDropdown == null || scanProvider == null) return;

        scanDropdown.ClearOptions();

        _availableScans.Clear();
        _availableScans = scanProvider.GetAvailableFiles(_fileFormat);

        List<string> scanOptions = new();
        foreach (FileMeta scan in _availableScans)
        {
            scanOptions.Add(scan.displayName);
        }
        scanDropdown.AddOptions(scanOptions);
    }

    public void ClickRefreshDropdown()
    {
        PopulateScans();

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
        ScanEvents.OnUIMessageRequested += HandleUIMessageRequested;
    }

    private void OnDisable()
    {
        ScanEvents.OnImportScanRequested -= HandleImportScanRequested;
        ScanEvents.OnImportScanCompleted -= HandleImportScanCompleted;
        ScanEvents.OnUIMessageRequested -= HandleUIMessageRequested;
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

    public void ClickDeleteScan() => ScanEvents.RequestDeleteScan();

    public void ClickResetScan() => ScanEvents.RequestResetScan();

    public void ClickChangeScale(float scaleFactor) => ScanEvents.RequestScaleScan(scaleFactor);

    public void ClickAdvanceBuilder() => ScanEvents.RequestAdvanceBuilder();

    public void ClickViewFront() => ScanEvents.RequestSnapView(OrthoView.Front);
    public void ClickViewBack() => ScanEvents.RequestSnapView(OrthoView.Back);
    public void ClickViewLeft() => ScanEvents.RequestSnapView(OrthoView.Left);
    public void ClickViewRight() => ScanEvents.RequestSnapView(OrthoView.Right);
    public void ClickViewTop() => ScanEvents.RequestSnapView(OrthoView.Top);
    public void ClickViewBottom() => ScanEvents.RequestSnapView(OrthoView.Bottom);

    private void HandleUIMessageRequested(string message)
    {
        ChangeText(message);
        EnableText();

        CancelInvoke(nameof(DisableText));
        Invoke(nameof(DisableText), 3.5f);
    }

    private void OnDestroy()
    {
        // we have to disable the non-persistent event listener if destroyed
        if (formatDropdown != null) formatDropdown.onValueChanged.RemoveListener(OnFormatChanged);
    }
}
