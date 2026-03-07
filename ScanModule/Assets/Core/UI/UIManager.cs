using UnityEngine;

using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI text;

    private void Awake()
    {
        DisableText();
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
}
