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

    private void OnEnable()
    {
        UIEvents.OnUIMessageRequested += HandleUIMessageRequested;
    }

    private void OnDisable()
    {
        UIEvents.OnUIMessageRequested -= HandleUIMessageRequested;
    }

    private void OnDestroy()
    {
        // we have to disable the non-persistent event listener if destroyed
        if (formatDropdown != null) formatDropdown.onValueChanged.RemoveListener(OnFormatChanged);
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

	private void PopulateScans(bool bNotify = false)
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

		if (bNotify)
		{
			UIEvents.RequestUIMessage("Scan list refreshed!");
        }
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

    private void HandleUIMessageRequested(string message)
    {
        ChangeText(message);
        EnableText();

        CancelInvoke(nameof(DisableText));
        Invoke(nameof(DisableText), 3.5f);
    }

	private string GetSelectedScanPath()
	{
		int index = scanDropdown.value;

		if (_availableScans == null || _availableScans.Count == 0 || index < 0 || index >= _availableScans.Count)
		{
			return string.Empty;
		}
		
		return _availableScans[index].filePath;
	}

    public void ClickRefreshDropdown() => PopulateScans(true);
    public void ClickImportSelectedScan() => UIEvents.RequestImportScan(GetSelectedScanPath());

    public void ClickDeleteScan() => UIEvents.RequestDeleteScan();
	public void ClickResetScan() => UIEvents.RequestResetScan();

	public void ClickChangeScale(float scaleFactor) => UIEvents.RequestScaleScan(scaleFactor);

	public void ClickAdvanceBuilder() => UIEvents.RequestAdvanceBuilder();
	public void ClickResetBuilder() => UIEvents.RequestResetBuilder();

	public void ClickViewFront() => UIEvents.RequestSnapView(OrthoView.Front);
	public void ClickViewBack() => UIEvents.RequestSnapView(OrthoView.Back);
	public void ClickViewLeft() => UIEvents.RequestSnapView(OrthoView.Left);
	public void ClickViewRight() => UIEvents.RequestSnapView(OrthoView.Right);
	public void ClickViewTop() => UIEvents.RequestSnapView(OrthoView.Top);
	public void ClickViewBottom() => UIEvents.RequestSnapView(OrthoView.Bottom);

	public void ClickAlignBases() => UIEvents.RequestAlignBases();

	public void ClickInjectDebugData(int setIndex) => UIEvents.RequestInjectDebugData(setIndex);
}
