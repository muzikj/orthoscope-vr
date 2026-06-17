using UnityEngine;

[CreateAssetMenu(fileName = "DentalScanTheme", menuName = "(+) ScanModule/DentalScanTheme")]
public class DentalScanTheme : ScriptableObject
{
    public Material defaultMaterial;
    public Material highlightedMaterial;
}
