using UnityEngine;

public class ScanAligner : MonoBehaviour
{
    private void OnEnable()
    {
        ScanEvents.OnAlignBasesRequested += HandleAlignRequested;
    }

    private void OnDisable()
    {
        ScanEvents.OnAlignBasesRequested -= HandleAlignRequested;
    }

    private void HandleAlignRequested()
    {
        ScanController[] scans = FindObjectsByType<ScanController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        ScanController upper = null;
        ScanController lower = null;

        int selectedCount = 0;

        foreach (var scan in scans)
        {
            if (scan.Selected)
            {
                selectedCount++;

                if (scan.jawType == ScanController.JawType.Upper)
                {
                    upper = scan;
                }
                else if (scan.jawType == ScanController.JawType.Lower)
                {
                    lower = scan;
                }
            }
        }

        if (selectedCount != 2 || upper == null || lower == null)
        {
            ScanEvents.RequestUIMessage("Please select exactly one Upper Base and one Lower Base to align.");

            return;
        }

        // rotate lower as Top and upper as Bot and apply rotation First for the AABB
        Quaternion lowerRot = Quaternion.Euler(90f, 0f, 0f) * lower.OriginalRotation;
        Quaternion upperRot = Quaternion.Euler(-90f, 0f, 0f) * upper.OriginalRotation;

        lower.transform.rotation = lowerRot;
        upper.transform.rotation = upperRot;

        // leave lower in place and move upper to match it
        Vector3 lowerPos = lower.transform.position;
        upper.transform.position = lowerPos;

        // ScanController requires a MeshRenderer, so we can safely get bounds from it
        Bounds lowerBounds = lower.GetComponent<MeshRenderer>().bounds;
        Bounds upperBounds = upper.GetComponent<MeshRenderer>().bounds;

        // calculate the centering and upper offset
        float offsetX = lowerBounds.center.x - upperBounds.center.x;
        float offsetY = lowerBounds.max.y - upperBounds.min.y;

        // calculate the depth offset, get the bases, fin back wall, transform to WS
        Transform lowerBaseT = lower.transform.Find("Ortho_Lower_Final"); //TODO: make names not hardcoded
        Transform upperBaseT = upper.transform.Find("Ortho_Upper_Final");

        Vector3 lowerBackLocal = new(0f, 0f, lowerBaseT.GetComponent<MeshFilter>().sharedMesh.bounds.max.z);
        Vector3 upperBackLocal = new(0f, 0f, upperBaseT.GetComponent<MeshFilter>().sharedMesh.bounds.min.z);

        Vector3 lowerBackWorld = lowerBaseT.TransformPoint(lowerBackLocal);
        Vector3 upperBackWorld = upperBaseT.TransformPoint(upperBackLocal);

        float offsetZ = lowerBackWorld.z - upperBackWorld.z;

        // apply the final offset to upper
        Vector3 upperPos = lowerPos + new Vector3(offsetX, offsetY, offsetZ);

        upper.transform.position = upperPos;

        ScanEvents.RequestUIMessage("Bases Aligned Face-Up!");
    }
}
