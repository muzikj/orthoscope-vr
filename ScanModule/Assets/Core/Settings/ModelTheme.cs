using UnityEngine;

[CreateAssetMenu(fileName = "ModelTheme", menuName = "STL Dental Scan/ModelTheme")]
public class ModelTheme : ScriptableObject
{
    [Header("STL Model Materials")]
    public Material defaultMaterial;
    public Material highlightedMaterial;
}
