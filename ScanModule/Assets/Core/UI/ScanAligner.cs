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

	// simulate backing a mesh up into a flat wall until it hits
	private float SweepMeshAgainstWall(Transform meshTransform, Mesh mesh, bool backIsNegativeZ)
	{
		Vector3[] vertices = mesh.vertices;
		float wallHitZ = backIsNegativeZ ? float.MaxValue : float.MinValue;

		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 worldPt = meshTransform.TransformPoint(vertices[i]);

			if (backIsNegativeZ)
			{
				if (worldPt.z < wallHitZ) wallHitZ = worldPt.z; // pushing backwards into -Z
			}
			else
			{
				if (worldPt.z > wallHitZ) wallHitZ = worldPt.z; // pushing backwards into +Z
			}
		}

		return wallHitZ;
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

		// get the base children
		Transform lowerBaseT = lower.transform.Find("Ortho_Lower_Final"); // TODO: make this not hardcoded to a string name
		Transform upperBaseT = upper.transform.Find("Ortho_Upper_Final");

		if (lowerBaseT == null || upperBaseT == null)
		{
			Debug.LogWarning("Could not find base children on one or both scans. Make sure they have the expected child objects named 'Ortho_Lower_Final' and 'Ortho_Upper_Final'.");

            return;
        }

        // add convex MeshColliders
        if (!lowerBaseT.TryGetComponent<MeshCollider>(out var lowerCol))
		{
			lowerCol = lowerBaseT.gameObject.AddComponent<MeshCollider>();
			lowerCol.convex = true;
		}

		if (!upperBaseT.TryGetComponent<MeshCollider>(out var upperCol))
		{
			upperCol = upperBaseT.gameObject.AddComponent<MeshCollider>();
			upperCol.convex = true;
		}

		// calculate Y-Stack and X-Center
		Bounds lowerTotalBounds = lower.GetComponent<MeshRenderer>().bounds;
		lowerTotalBounds.Encapsulate(lowerCol.bounds);

		Bounds upperTotalBounds = upper.GetComponent<MeshRenderer>().bounds;
		upperTotalBounds.Encapsulate(upperCol.bounds);

		float offsetX = lowerTotalBounds.center.x - upperTotalBounds.center.x;
		float offsetY = (lowerTotalBounds.max.y - upperTotalBounds.min.y) + Config.Instance.widePadding * 1.5f;

		bool isBackNegativeZ = lowerCol.bounds.center.z < lower.GetComponent<MeshRenderer>().bounds.center.z;

		float lowerWallZ = SweepMeshAgainstWall(lowerBaseT, lowerBaseT.GetComponent<MeshFilter>().sharedMesh, isBackNegativeZ);
		float upperWallZ = SweepMeshAgainstWall(upperBaseT, upperBaseT.GetComponent<MeshFilter>().sharedMesh, isBackNegativeZ);

		float offsetZ = lowerWallZ - upperWallZ;

        // apply the final offset to upper
        upper.transform.position += new Vector3(offsetX, offsetY, offsetZ);

		ScanEvents.RequestUIMessage("Bases Aligned Face-Up!");
	}
}
