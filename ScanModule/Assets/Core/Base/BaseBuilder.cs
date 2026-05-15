using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

public class BaseBuilder : MonoBehaviour
{
	[Tooltip("The final location where upper and lower bases will be moved.")]
	public Transform finalAssemblyLocation;

	public static BaseBuilder Instance { get; private set; }

	public enum BuilderState { MarkOcclusal, MarkSagittal, MarkUpperGums, MeshingUpper, MarkLowerGums, MeshingLower, Finished }
	public BuilderState currentState = BuilderState.MarkOcclusal;

	public enum OcclusalPoint { MolarRight = 0, IncisorMiddle = 1, MolarLeft = 2 };
	public enum SagittalPoint { Front = 0, Back = 1 }

	private readonly List<Vector3> _occlusalPoints = new();
	private readonly List<Vector3> _sagittalPoints = new();

	private readonly List<GameObject> _occlusalMarks = new();
	private readonly List<GameObject> _sagittalMarks = new();

	private bool _bUpperJaw = true;

	private Quaternion _baseRotation;

	private float _masterScaleFactor = 1f;
	private float _masterCenterX = 0f;
	private float _masterMinZ = 0f;

	private bool _bProcessing = false;

	private struct PlinthSettings
	{
		public bool isUpperJaw;
		public float skirtDepth;
		public float baseHeight;
		public float widePadding;
		public float outwardFlare;
		public float minDistance;
	}

	private struct Edge : IEquatable<Edge>
	{
		public int v1, v2;
		public Edge(int a, int b) { v1 = Mathf.Min(a, b); v2 = Mathf.Max(a, b); }
		public readonly bool Equals(Edge other) => v1 == other.v1 && v2 == other.v2;
		public override readonly int GetHashCode() => (v1 * 397) ^ v2;
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
		}
		else
		{
			Instance = this;
		}
	}

	private void OnEnable()
	{
		ScanEvents.OnAdvanceBuilderRequested += HandleAdvanceRequested;
	}

	private void OnDisable()
	{
		ScanEvents.OnAdvanceBuilderRequested -= HandleAdvanceRequested;
	}

	private async void HandleAdvanceRequested()
	{
		if (_bProcessing)
		{
			return;
		}

		_bProcessing = true;

		await AdvanceStateAsync();

		_bProcessing = false;
	}

	private async Task AdvanceStateAsync()
	{
		if (currentState == BuilderState.MarkOcclusal)
		{
			if (_occlusalPoints.Count < 3)
			{
				ScanEvents.RequestUIMessage($"Please, place 3 Occlusal points first! ({_occlusalPoints.Count}/3)");

				return;
			}

			currentState = BuilderState.MarkSagittal;
			ScanEvents.RequestUIMessage("State Advanced\nNow marking Sagittal Plane (2 points on upper palate).");
		}
		else if (currentState == BuilderState.MarkSagittal)
		{
			if (_sagittalPoints.Count < 2)
			{
				ScanEvents.RequestUIMessage($"Please, place 2 Sagittal points first! ({_sagittalPoints.Count}/2)");

				return;
			}

			currentState = BuilderState.MarkUpperGums;
			ScanEvents.RequestUIMessage("State Advanced\nNow marking Gum Splines. TubeRenderer active!");
		}
		else if (currentState == BuilderState.MarkUpperGums)
		{
			ScanSpline upperSpline = null;
			ScanSpline[] activeSplines = FindObjectsByType<ScanSpline>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (var spline in activeSplines)
			{
				if (spline.bClosed)
				{
					upperSpline = spline;
					break;
				}
			}

			if (upperSpline == null)
			{
				ScanEvents.RequestUIMessage("Missing a closed spline!\nClose the spline before advancing!");

				return;
			}

			currentState = BuilderState.MeshingUpper;
			ScanEvents.RequestUIMessage("State Advanced:\nNow processing the Upper Base...");

			_baseRotation = CalculateRotation();

			await TrimmingAndBasePipelineAsync(upperSpline);
			upperSpline.gameObject.SetActive(false);

			_bUpperJaw = false;
			
			currentState = BuilderState.MarkLowerGums;
			ScanEvents.RequestUIMessage("Success!\nUpper Base Complete!\nMoving onto the Lower Gum Splines.");
		}
		else if (currentState == BuilderState.MarkLowerGums)
		{
			ScanSpline lowerSpline = null;
			ScanSpline[] activeSplines = FindObjectsByType<ScanSpline>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (var spline in activeSplines)
			{
				if (spline.bClosed)
				{
					lowerSpline = spline;
					break;
				}
			}

			if (lowerSpline == null)
			{
				ScanEvents.RequestUIMessage("Missing a closed spline!\nClose the spline before advancing!");

				return;
			}

			currentState = BuilderState.MeshingLower;
			ScanEvents.RequestUIMessage("State Advanced:\nNow processing the Lower Base...");

			await TrimmingAndBasePipelineAsync(lowerSpline);
			lowerSpline.gameObject.SetActive(false);

			currentState = BuilderState.Finished;
			ScanEvents.RequestUIMessage("Success!\nLower Base Complete!\nABO Base Finished!");
		}
	}

	private async Task TrimmingAndBasePipelineAsync(ScanSpline activeSpline)
	{
		if (activeSpline != null)
		{
			activeSpline.transform.parent.localScale = Vector3.one * Config.Instance.scanScale;
			MeshFilter scanMeshFilter = activeSpline.transform.parent.GetComponentInParent<MeshFilter>();

			if (scanMeshFilter != null)
			{
				Debug.Log($"Trimming scan debris on mesh: {scanMeshFilter.gameObject.name}...");
				await TrimScanAsync(scanMeshFilter, activeSpline);

				Debug.Log("Zipping the gap between the scan and the spline...");
				await ZipScanToSkirtAsync(scanMeshFilter, activeSpline);

				Debug.Log("Trimming & Zipping complete! Building the ABO Base...");
			}
			else
			{
				Debug.LogWarning("Could not find MeshFilter to trim. Skipping trim step.");
			}

			await BaseGenerationPipelineAsync(activeSpline);
		}
		else
		{
			Debug.LogError("Cannot generate base: Gum spline is missing.");
		}
	}

	public async Task ZipScanToSkirtAsync(MeshFilter scanMeshFilter, ScanSpline skirtSpline)
	{
		// raw data on Main
		Vector3[] scanVerts = scanMeshFilter.mesh.vertices;
		int[] scanTris = scanMeshFilter.mesh.triangles;

		// convert spline points to scan local space
		List<Vector3> localSkirtPoints = new();
		foreach (Vector3 pt in skirtSpline.GetSplinePoints())
		{
			Vector3 worldPt = skirtSpline.transform.TransformPoint(pt);
			localSkirtPoints.Add(scanMeshFilter.transform.InverseTransformPoint(worldPt));
		}

		// offload calcualtions to a Thread
		var (zipperVerts, zipperTris) = await Task.Run(() =>
		{
			return ProcessZipper(scanVerts, scanTris, localSkirtPoints);
		});

		if (zipperVerts.Length == 0)
		{
			Debug.LogWarning("Zipper failed to find a continuous boundary. Skipping zip.");
			return;
		}

		// build the zipper mesh on Main
		Mesh zipperMesh = new()
		{
			name = "ABO_Zipper_Bridge",
			vertices = zipperVerts,
			triangles = zipperTris
		};
		zipperMesh.RecalculateNormals();
		zipperMesh.RecalculateBounds();

		// spawn where the scan is
		GameObject zipperObj = new("Zipper_Bridge");
		zipperObj.transform.SetPositionAndRotation(scanMeshFilter.transform.position, scanMeshFilter.transform.rotation);
		zipperObj.transform.SetParent(scanMeshFilter.transform, true);
		zipperObj.transform.localScale = Vector3.one;

		MeshFilter filter = zipperObj.AddComponent<MeshFilter>();
		filter.sharedMesh = zipperMesh;

		MeshRenderer renderer = zipperObj.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = Config.Instance.baseMaterial;
	}

	private (Vector3[], int[]) ProcessZipper(Vector3[] scanVerts, int[] scanTris, List<Vector3> localSkirtPoints)
	{
		List<int> boundaryIndices = GetLongestBoundaryLoop(scanTris);
		if (boundaryIndices.Count == 0) return (new Vector3[0], new int[0]);

		List<Vector3> scanRimPts = new();
		foreach (int idx in boundaryIndices) scanRimPts.Add(scanVerts[idx]);

		// auto-align, i.e. finding the absolute closest pair to start
		int bestScanIdx = 0, bestSkirtIdx = 0;
		float minDst = float.MaxValue;
		for (int i = 0; i < scanRimPts.Count; i++)
		{
			for (int j = 0; j < localSkirtPoints.Count; j++)
			{
				float d = (scanRimPts[i] - localSkirtPoints[j]).sqrMagnitude;
				if (d < minDst)
				{
					minDst = d;
					bestScanIdx = i;
					bestSkirtIdx = j;
				}
			}
		}

		// test, which direction on the skirt loop keeps us closer to the scan loop
		int testNextScan = (bestScanIdx + 1) % scanRimPts.Count;
		int testNextSkirtFwd = (bestSkirtIdx + 1) % localSkirtPoints.Count;
		int testNextSkirtRev = (bestSkirtIdx - 1 + localSkirtPoints.Count) % localSkirtPoints.Count;

		float distFwd = (scanRimPts[testNextScan] - localSkirtPoints[testNextSkirtFwd]).sqrMagnitude;
		float distRev = (scanRimPts[testNextScan] - localSkirtPoints[testNextSkirtRev]).sqrMagnitude;

		// if stepping backwards is a shorter distance, arrays are crossing, so will reverse them so they are running in parallel
		if (distRev < distFwd)
		{
			localSkirtPoints.Reverse();
			bestSkirtIdx = (localSkirtPoints.Count - 1) - bestSkirtIdx; // reversing completely flips the optimal starting point
		}

		List<Vector3> zipperVerts = new();
		zipperVerts.AddRange(scanRimPts);
		int skirtOffset = zipperVerts.Count;
		zipperVerts.AddRange(localSkirtPoints);

		List<int> zipperTris = GreedilyZipLoops(scanRimPts, 0, localSkirtPoints, skirtOffset, bestScanIdx, bestSkirtIdx);

		return (zipperVerts.ToArray(), zipperTris.ToArray());
	}

	private List<int> GreedilyZipLoops(List<Vector3> scanRimPts, int scanOffset, List<Vector3> skirtPts, int skirtOffset, int startScanIdx, int startSkirtIdx)
	{
		List<int> zipperTris = new();
		int scanLen = scanRimPts.Count;
		int skirtLen = skirtPts.Count;

		int scanWalk = 0, skirtWalk = 0;

		while (scanWalk < scanLen || skirtWalk < skirtLen)
		{
			int currScan = (startScanIdx + scanWalk) % scanLen;
			int currSkirt = (startSkirtIdx + skirtWalk) % skirtLen;

			int nextScan = (startScanIdx + scanWalk + 1) % scanLen;
			int nextSkirt = (startSkirtIdx + skirtWalk + 1) % skirtLen;

			bool stepScan;
			if (scanWalk >= scanLen)
			{
				stepScan = false;
			}
			else if (skirtWalk >= skirtLen)
			{
				stepScan = true;
			}
			else
			{
				float distIfScanSteps = (scanRimPts[nextScan] - skirtPts[currSkirt]).sqrMagnitude;
				float distIfSkirtSteps = (scanRimPts[currScan] - skirtPts[nextSkirt]).sqrMagnitude;
				stepScan = distIfScanSteps < distIfSkirtSteps;
			}

			int v1 = currScan + scanOffset;
			int v2, v3;

			if (stepScan)
			{
				v2 = nextScan + scanOffset;
				v3 = currSkirt + skirtOffset;
				scanWalk++;
			}
			else
			{
				v2 = nextSkirt + skirtOffset;
				v3 = currSkirt + skirtOffset;
				skirtWalk++;

			}

			// the winding order will always be correct
			zipperTris.Add(v1);
			zipperTris.Add(v3);
			zipperTris.Add(v2);
		}

		return zipperTris;
	}

	private List<int> GetLongestBoundaryLoop(int[] triangles)
	{
		Dictionary<Edge, int> edgeCounts = new();
		Dictionary<Edge, (int from, int to)> directedEdgeMap = new();

		// count edges, preserving the original winding directions
		for (int i = 0; i < triangles.Length; i += 3)
		{
			for (int j = 0; j < 3; j++)
			{
				int vA = triangles[i + j];
				int vB = triangles[i + ((j + 1) % 3)];
				Edge e = new(vA, vB);

				if (!edgeCounts.ContainsKey(e))
				{
					edgeCounts[e] = 1;
					directedEdgeMap[e] = (vA, vB);
				}
				else
				{
					edgeCounts[e]++;
				}
			}
		}

		// isolate the boundary (if an edge is not shared by any other triangles, i.e. Count == 1, it is exposed)
		Dictionary<int, int> boundaryLinks = new Dictionary<int, int>();
		foreach (var kvp in edgeCounts)
		{
			if (kvp.Value == 1)
			{
				var (from, to) = directedEdgeMap[kvp.Key];
				boundaryLinks[from] = to;
			}
		}

		// chain edges into continuous loops
		List<List<int>> loops = new();
		HashSet<int> visited = new();

		foreach (int startNode in boundaryLinks.Keys)
		{
			if (visited.Contains(startNode)) continue;

			List<int> currentLoop = new List<int>();
			int curr = startNode;

			while (!visited.Contains(curr))
			{
				visited.Add(curr);
				currentLoop.Add(curr);

				if (boundaryLinks.TryGetValue(curr, out int nextNode))
				{
					curr = nextNode;
				}
				else
				{
					break;
				}
			}
			loops.Add(currentLoop);
		}

		// we are only interested in the longest loop, which should be the jaw cut loop, thus ignoring any micro-holes in teeth topology
		List<int> longestLoop = new();
		foreach (var loop in loops)
		{
			if (loop.Count > longestLoop.Count) longestLoop = loop;
		}

		return longestLoop;
	}

	private async Task BaseGenerationPipelineAsync(ScanSpline spline)
	{
		Debug.Log("Extracting spline points.");
		List<Vector3> splinePoints = spline.GetSplinePoints();

		Debug.Log("Generating watertight ABO Base...");
		PlinthSettings settings = new()
		{
			isUpperJaw = _bUpperJaw,
			skirtDepth = Config.Instance.skirtDepth / Config.Instance.scanScale,
			baseHeight = Config.Instance.baseHeight / Config.Instance.scanScale,
			widePadding = Config.Instance.widePadding / Config.Instance.scanScale,
			outwardFlare = Config.Instance.outwardFlare / Config.Instance.scanScale,
			minDistance = Config.Instance.minDistance,
		};

		var (vertices, triangles) = await Task.Run(() =>
		{
			return ProcessPlinthBaseMath(splinePoints, _baseRotation, settings);
		});

		BuildFinalBaseObject(vertices, triangles, spline);
	}

	private void BuildFinalBaseObject(Vector3[] vertices, int[] triangles, ScanSpline spline)
	{
		Mesh finalBaseMesh = new()
		{
			name = _bUpperJaw ? "ABO_Upper_Base" : "ABO_Lower_Base",
			vertices = vertices,
			triangles = triangles
		};
		MakeMeshFlatShaded(finalBaseMesh);

		GameObject orthoBase = new(_bUpperJaw ? "Ortho_Upper_Final" : "Ortho_Lower_Final");
		orthoBase.transform.SetPositionAndRotation(spline.transform.position, spline.transform.rotation);
		orthoBase.transform.SetParent(spline.transform.parent, true);
		orthoBase.transform.localScale = Vector3.one;

		MeshFilter filter = orthoBase.AddComponent<MeshFilter>();
		filter.sharedMesh = finalBaseMesh;

		MeshRenderer renderer = orthoBase.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = Config.Instance.baseMaterial;

		Quaternion leveledRotation = Quaternion.Inverse(_baseRotation);
		Quaternion flippedRotation = Quaternion.Euler(180f, 0f, 0f) * leveledRotation;
		spline.transform.parent.transform.rotation = flippedRotation;

		if (finalAssemblyLocation != null)
		{
			spline.transform.parent.SetPositionAndRotation(finalAssemblyLocation.position, finalAssemblyLocation.rotation * flippedRotation);
		}
		else
		{
			spline.transform.parent.position = Vector3.one;
		}
	}

	public async Task TrimScanAsync(MeshFilter scanMeshFilter, ScanSpline cutSpline)
	{
		Mesh scanMesh = scanMeshFilter.mesh;

		Vector3[] rawVerts = scanMesh.vertices;
		int[] rawTris = scanMesh.triangles;

		Color[] rawColors = scanMesh.colors;
		bool hasColors = rawColors != null && rawColors.Length > 0;

		// coordinate translation
		List<Vector3> localTubePoints = new();
		foreach (Vector3 pt in cutSpline.GetSplinePoints())
		{
			Vector3 worldPt = cutSpline.transform.TransformPoint(pt); // local spline to World-Space
			Vector3 meshLocalPt = scanMeshFilter.transform.InverseTransformPoint(worldPt); // World-Space to Mesh's local space

			localTubePoints.Add(meshLocalPt);
		}

		var (cleanedVerts, cleanedTris, cleanedColors) = await Task.Run(() =>
		{
			return ProcessMeshTrimmingAndBFS(rawVerts, rawTris, rawColors, localTubePoints, hasColors);
		});

		scanMesh.Clear();
		scanMesh.vertices = cleanedVerts;

		if (hasColors)
		{
			scanMesh.colors = cleanedColors;
		}

		scanMesh.triangles = cleanedTris;
		scanMesh.RecalculateNormals();
		scanMesh.RecalculateBounds();

		if (scanMeshFilter.TryGetComponent<MeshCollider>(out var collider))
		{
			collider.sharedMesh = scanMesh;
		}
	}

	private (Vector3[], int[], Color[]) ProcessMeshTrimmingAndBFS(Vector3[] verts, int[] tris, Color[] colors, List<Vector3> tubePoints, bool hasColors)
	{
		float cutRadius = Config.Instance.cutRadius / Config.Instance.scanScale;
		float cutRadiusSq = cutRadius * cutRadius;

		int numVerts = verts.Length;
		int numTris = tris.Length / 3;

		// find the vertices that need be trimmed based on the tube
		bool[] isBadVertex = new bool[numVerts];
		for (int i = 0; i < numVerts; i++)
		{
			Vector3 v = verts[i];
			foreach (Vector3 tp in tubePoints)
			{
				if ((v - tp).sqrMagnitude <= cutRadiusSq)
				{
					isBadVertex[i] = true;
					break;
				}
			}
		}

		// delete the tri's and build an adjacency map
		List<int> keptTriIndices = new();
		List<int>[] vertexToTris = new List<int>[numVerts];

		for (int i = 0; i < numTris; i++)
		{
			int v1 = tris[i * 3];
			int v2 = tris[i * 3 + 1];
			int v3 = tris[i * 3 + 2];

			if (!isBadVertex[v1] && !isBadVertex[v2] && !isBadVertex[v3])
			{
				keptTriIndices.Add(i);

				if (vertexToTris[v1] == null)
				{
					vertexToTris[v1] = new List<int>();
				}

				if (vertexToTris[v2] == null)
				{
					vertexToTris[v2] = new List<int>();
				}

				if (vertexToTris[v3] == null)
				{
					vertexToTris[v3] = new List<int>();
				}

				vertexToTris[v1].Add(i);
				vertexToTris[v2].Add(i);
				vertexToTris[v3].Add(i);
			}
		}

		// breadth-first search to find the islands of good and bad vertices (the largest one should be our desired remainding teeth scan)
		bool[] visitedTris = new bool[numTris];
		List<int> largestIsland = new();

		foreach (int startTri in keptTriIndices)
		{
			if (visitedTris[startTri]) continue;

			List<int> currentIsland = new List<int>();
			Queue<int> queue = new Queue<int>();

			queue.Enqueue(startTri);
			visitedTris[startTri] = true;

			while (queue.Count > 0)
			{
				int currTri = queue.Dequeue();
				currentIsland.Add(currTri);

				int v1 = tris[currTri * 3];
				int v2 = tris[currTri * 3 + 1];
				int v3 = tris[currTri * 3 + 2];

				void CheckNeighbors(int v)
				{
					if (vertexToTris[v] != null)
					{
						foreach (int neighborTri in vertexToTris[v])
						{
							if (!visitedTris[neighborTri])
							{
								visitedTris[neighborTri] = true;
								queue.Enqueue(neighborTri);
							}
						}
					}
				}

				CheckNeighbors(v1);
				CheckNeighbors(v2);
				CheckNeighbors(v3);
			}

			if (currentIsland.Count > largestIsland.Count)
			{
				largestIsland = currentIsland;
			}
		}

		// rebuild the mesh data, preserving color (if present) from the .ply
		Dictionary<int, int> oldToNewVertexMap = new();
		List<Vector3> finalVerts = new();
		List<int> finalTris = new();
		List<Color> finalColors = new();

		foreach (int triIndex in largestIsland)
		{
			for (int j = 0; j < 3; j++)
			{
				int oldVertIndex = tris[triIndex * 3 + j];

				if (!oldToNewVertexMap.TryGetValue(oldVertIndex, out int newVertIndex))
				{
					newVertIndex = finalVerts.Count;
					oldToNewVertexMap[oldVertIndex] = newVertIndex;

					finalVerts.Add(verts[oldVertIndex]);

					if (hasColors)
					{
						finalColors.Add(colors[oldVertIndex]);
					}
				}

				finalTris.Add(newVertIndex);
			}
		}

		return (finalVerts.ToArray(), finalTris.ToArray(), finalColors.ToArray());
	}

	private (Vector3[], int[]) ProcessPlinthBaseMath(List<Vector3> splinePoints, Quaternion baseRotation, PlinthSettings settings)
	{
		// clean the spline up
		List<Vector3> filteredPoints = new()
		{
			splinePoints[0]
		};

		for (int i = 1; i < splinePoints.Count; i++)
		{
			if (Vector3.Distance(splinePoints[i], filteredPoints[^1]) >= settings.minDistance)
			{
				filteredPoints.Add(splinePoints[i]);
			}
		}
		splinePoints = filteredPoints;

		int splineCount = splinePoints.Count;

		Quaternion inverseRotation = Quaternion.Inverse(baseRotation);
		Vector3 upDir = baseRotation * Vector3.up;

		// figure out the bounding box
		float minX = float.MaxValue, maxX = float.MinValue;
		float minZ = float.MaxValue, maxZ = float.MinValue;
		float lowestY = float.MaxValue, highestY = float.MinValue;
		List<Vector3> localPoints = new();
		List<Vector2> bottomPoints2D = new();

		for (int i = 0; i < splineCount; i++)
		{
			Vector3 localPt = inverseRotation * splinePoints[i];
			localPoints.Add(localPt);
			if (localPt.y < lowestY) lowestY = localPt.y;
			if (localPt.y > highestY) highestY = localPt.y;
			if (localPt.x < minX) minX = localPt.x;
			if (localPt.x > maxX) maxX = localPt.x;
			if (localPt.z < minZ) minZ = localPt.z;
			if (localPt.z > maxZ) maxZ = localPt.z;
		}

		float midY = settings.isUpperJaw ? (lowestY - settings.skirtDepth) : (highestY + settings.skirtDepth);
		float topY = settings.isUpperJaw ? (midY - settings.baseHeight) : (midY + settings.baseHeight);

		int pedSides = settings.isUpperJaw ? 7 : 8;
		int pedVerts = (pedSides * 2) + 2; // top/bottom ring + center point

		Vector3[] vertices = new Vector3[(splineCount * 2) + pedVerts];

		for (int i = 0; i < splineCount; i++)
		{
			vertices[i] = splinePoints[i];

			Vector3 prev = localPoints[(i - 1 + splineCount) % splineCount];
			Vector3 next = localPoints[(i + 1) % splineCount];
			Vector3 tangent = (next - prev).normalized;
			Vector3 outwardNormal = new Vector3(tangent.z, 0, -tangent.x).normalized;

			Vector3 localBottom = new(
				localPoints[i].x + (outwardNormal.x * settings.outwardFlare),
				midY,
				localPoints[i].z + (outwardNormal.z * settings.outwardFlare)
			);

			bottomPoints2D.Add(new Vector2(localBottom.x, localBottom.z));
			vertices[i + splineCount] = baseRotation * localBottom;
		}

		// seal the skirt with the Triangulator
		Triangulator triangulator = new(bottomPoints2D);
		int[] capIndices = triangulator.Triangulate();
		int capTriCount = capIndices.Length;

		// apply padding to the ABO pedestal
		minX -= settings.widePadding;
		maxX += settings.widePadding;

		minZ -= settings.widePadding;
		maxZ += settings.widePadding;

		float W = maxX - minX;
		float D = maxZ - minZ;
		float currentCenterX = (minX + maxX) / 2f;

		Vector2[] aboPolygon = new Vector2[pedSides];
		BaseShapeGenerator shapeGen = new();

		float activeScaleFactor;
		float activeCenterX;
		float activeMinZ;

		if (settings.isUpperJaw)
		{
			var (verts, _) = shapeGen.CalculateUpperBaseShape();

			float shapeWidth = verts[1].x - verts[7].x; // C.x - CC.x
			float shapeDepth = verts[6].z - verts[0].z; // T.z - B.z

			activeScaleFactor = Mathf.Max(W / shapeWidth, D / shapeDepth);
			activeCenterX = currentCenterX;
			activeMinZ = minZ; // anchor to the back heel

			_masterScaleFactor = activeScaleFactor;
			_masterCenterX = activeCenterX;
			_masterMinZ = activeMinZ;

			// grow the template forward from the back heel (Z = activeMinZ)
			aboPolygon[0] = new Vector2(activeCenterX + verts[6].x * activeScaleFactor, activeMinZ + verts[6].z * activeScaleFactor); // T
			aboPolygon[1] = new Vector2(activeCenterX + verts[5].x * activeScaleFactor, activeMinZ + verts[5].z * activeScaleFactor); // U
			aboPolygon[2] = new Vector2(activeCenterX + verts[2].x * activeScaleFactor, activeMinZ + verts[2].z * activeScaleFactor); // E
			aboPolygon[3] = new Vector2(activeCenterX + verts[1].x * activeScaleFactor, activeMinZ + verts[1].z * activeScaleFactor); // C
			aboPolygon[4] = new Vector2(activeCenterX + verts[7].x * activeScaleFactor, activeMinZ + verts[7].z * activeScaleFactor); // CC
			aboPolygon[5] = new Vector2(activeCenterX + verts[8].x * activeScaleFactor, activeMinZ + verts[8].z * activeScaleFactor); // EE
			aboPolygon[6] = new Vector2(activeCenterX + verts[9].x * activeScaleFactor, activeMinZ + verts[9].z * activeScaleFactor); // UU
		}
		else
		{
			var (verts, _) = shapeGen.CalculateLowerBaseShape();

			activeScaleFactor = _masterScaleFactor;
			activeCenterX = _masterCenterX;
			activeMinZ = _masterMinZ;

			// map the perimeter clockwise using the Upper Jaw's scale and heel placement
			aboPolygon[0] = new Vector2(activeCenterX + verts[7].x * activeScaleFactor, activeMinZ + verts[7].z * activeScaleFactor);  // A
			aboPolygon[1] = new Vector2(activeCenterX + verts[5].x * activeScaleFactor, activeMinZ + verts[5].z * activeScaleFactor);  // U
			aboPolygon[2] = new Vector2(activeCenterX + verts[2].x * activeScaleFactor, activeMinZ + verts[2].z * activeScaleFactor);  // E
			aboPolygon[3] = new Vector2(activeCenterX + verts[1].x * activeScaleFactor, activeMinZ + verts[1].z * activeScaleFactor);  // C
			aboPolygon[4] = new Vector2(activeCenterX + verts[8].x * activeScaleFactor, activeMinZ + verts[8].z * activeScaleFactor);  // CC
			aboPolygon[5] = new Vector2(activeCenterX + verts[9].x * activeScaleFactor, activeMinZ + verts[9].z * activeScaleFactor);  // EE
			aboPolygon[6] = new Vector2(activeCenterX + verts[10].x * activeScaleFactor, activeMinZ + verts[10].z * activeScaleFactor); // UU
			aboPolygon[7] = new Vector2(activeCenterX + verts[11].x * activeScaleFactor, activeMinZ + verts[11].z * activeScaleFactor); // AA
		}

		float polyMinZ = float.MaxValue, polyMaxZ = float.MinValue;
		foreach (var p in aboPolygon)
		{
			if (p.y < polyMinZ) polyMinZ = p.y;
			if (p.y > polyMaxZ) polyMaxZ = p.y;
		}
		float finalCenterZ = (polyMinZ + polyMaxZ) / 2f;

		int pedStart = splineCount * 2;

		for (int i = 0; i < pedSides; i++) vertices[pedStart + i] = baseRotation * new Vector3(aboPolygon[i].x, midY, aboPolygon[i].y);
		for (int i = 0; i < pedSides; i++) vertices[pedStart + pedSides + i] = baseRotation * new Vector3(aboPolygon[i].x, topY, aboPolygon[i].y);

		int pedTopCenter = pedStart + (pedSides * 2);
		int pedBotCenter = pedStart + (pedSides * 2) + 1;
		vertices[pedTopCenter] = baseRotation * new Vector3(activeCenterX, midY, finalCenterZ);
		vertices[pedBotCenter] = baseRotation * new Vector3(activeCenterX, topY, finalCenterZ);

		// stich everything together
		int[] triangles = new int[(splineCount * 6) + capTriCount + (pedSides * 12)];
		int t = 0;

		bool skirtReverseWinding = RequiresReversedWinding(splinePoints, upDir);
		if (settings.isUpperJaw) skirtReverseWinding = !skirtReverseWinding;

		// stitch the skirt walls
		for (int i = 0; i < splineCount; i++)
		{
			int top1 = i, top2 = (i + 1) % splineCount;
			int bot1 = i + splineCount, bot2 = top2 + splineCount;

			if (skirtReverseWinding)
			{
				triangles[t++] = top1;
				triangles[t++] = bot1;
				triangles[t++] = top2;

				triangles[t++] = top2;
				triangles[t++] = bot1;
				triangles[t++] = bot2;
			}
			else
			{
				triangles[t++] = top1;
				triangles[t++] = top2;
				triangles[t++] = bot1;

				triangles[t++] = top2;
				triangles[t++] = bot2;
				triangles[t++] = bot1;
			}
		}

		// stitch the skirt bottom cap
		for (int i = 0; i < capTriCount; i += 3)
		{
			if (skirtReverseWinding)
			{
				triangles[t++] = capIndices[i] + splineCount;
				triangles[t++] = capIndices[i + 1] + splineCount;
				triangles[t++] = capIndices[i + 2] + splineCount;
			}
			else
			{
				triangles[t++] = capIndices[i + 2] + splineCount;
				triangles[t++] = capIndices[i + 1] + splineCount;
				triangles[t++] = capIndices[i] + splineCount;
			}
		}

		// stitch the pedestal walls
		for (int i = 0; i < pedSides; i++)
		{
			int pTop1 = pedStart + i, pTop2 = pedStart + ((i + 1) % pedSides);
			int pBot1 = pTop1 + pedSides, pBot2 = pTop2 + pedSides;

			if (settings.isUpperJaw)
			{
				triangles[t++] = pTop1; triangles[t++] = pBot1; triangles[t++] = pTop2;
				triangles[t++] = pTop2; triangles[t++] = pBot1; triangles[t++] = pBot2;
			}
			else
			{
				triangles[t++] = pTop1; triangles[t++] = pTop2; triangles[t++] = pBot1;
				triangles[t++] = pTop2; triangles[t++] = pBot2; triangles[t++] = pBot1;
			}
		}

		// stitch the pedestal top cap 
		for (int i = 0; i < pedSides; i++)
		{
			int pTop1 = pedStart + i, pTop2 = pedStart + ((i + 1) % pedSides);
			triangles[t++] = pedTopCenter;

			if (settings.isUpperJaw)
			{
				triangles[t++] = pTop1; triangles[t++] = pTop2;
			}
			else
			{
				triangles[t++] = pTop2; triangles[t++] = pTop1;
			}
		}

		// stitch the pedestal bottom cap
		for (int i = 0; i < pedSides; i++)
		{
			int pBot1 = pedStart + pedSides + i, pBot2 = pedStart + pedSides + ((i + 1) % pedSides);
			triangles[t++] = pedBotCenter;

			if (settings.isUpperJaw)
			{
				triangles[t++] = pBot2; triangles[t++] = pBot1;
			}
			else
			{
				triangles[t++] = pBot1; triangles[t++] = pBot2;
			}
		}

		return (vertices, triangles);
	}

	private void MakeMeshFlatShaded(Mesh mesh)
	{
		Vector3[] oldVerts = mesh.vertices;
		int[] oldTris = mesh.triangles;

		Vector3[] newVerts = new Vector3[oldTris.Length];
		int[] newTris = new int[oldTris.Length];

		for (int i = 0; i < oldTris.Length; i++)
		{
			newVerts[i] = oldVerts[oldTris[i]];
			newTris[i] = i; // every vertex gets a unique index
		}

		mesh.vertices = newVerts;
		mesh.triangles = newTris;

		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
	}

	public void AddPoint(Vector3 worldPosition, Vector3 normal, Transform scanTransform)
	{
		if (currentState == BuilderState.MarkOcclusal && _occlusalPoints.Count >= 3) return;
		if (currentState == BuilderState.MarkSagittal && _sagittalPoints.Count >= 2) return;

		GameObject mark = null;
		if (Config.Instance.markPrefab != null)
		{
			mark = Instantiate(Config.Instance.markPrefab, worldPosition, Quaternion.LookRotation(normal));
			mark.transform.SetParent(scanTransform, true);
			mark.transform.localScale = Config.Instance.markPrefab.transform.localScale;
		}

		Vector3 localPosition = scanTransform.InverseTransformPoint(worldPosition);
		if (currentState == BuilderState.MarkOcclusal)
		{
			_occlusalPoints.Add(localPosition);
			if (mark != null) _occlusalMarks.Add(mark);

			UpdateMarkColors(_occlusalMarks, 3);
			ScanEvents.RequestUIMessage($"Occlusal point added. ({_occlusalPoints.Count}/3)");
		}
		else if (currentState == BuilderState.MarkSagittal)
		{
			_sagittalPoints.Add(localPosition);
			if (mark != null) _sagittalMarks.Add(mark);

			UpdateMarkColors(_sagittalMarks, 2);
			ScanEvents.RequestUIMessage($"Sagittal point added. ({_sagittalPoints.Count}/2)");
		}
	}

	public void RemoveLastPoint()
	{
		if (currentState == BuilderState.MarkOcclusal && _occlusalPoints.Count > 0)
		{
			_occlusalPoints.RemoveAt(_occlusalPoints.Count - 1);
			RemoveLastMark(_occlusalMarks);

			UpdateMarkColors(_occlusalMarks, 3);
			ScanEvents.RequestUIMessage($"Last occlusal point removed. ({_occlusalPoints.Count}/3)");
		}
		else if (currentState == BuilderState.MarkSagittal && _sagittalPoints.Count > 0)
		{
			_sagittalPoints.RemoveAt(_sagittalPoints.Count - 1);
			RemoveLastMark(_sagittalMarks);

			UpdateMarkColors(_sagittalMarks, 2);
			ScanEvents.RequestUIMessage($"Last sagittal point removed. ({_sagittalPoints.Count}/2)");
		}
	}

	private void RemoveLastMark(List<GameObject> list)
	{
		if (list.Count > 0)
		{
			Destroy(list[^1]);
			list.RemoveAt(list.Count - 1);
		}
	}

	private void UpdateMarkColors(List<GameObject> marks, int requiredCount)
	{
		Material material = (marks.Count == requiredCount) ? Config.Instance.markCompleteMat : Config.Instance.markProgressMat;

		foreach (GameObject mark in marks)
		{
			if (mark.TryGetComponent<MeshRenderer>(out var renderer) || mark.GetComponentInChildren<MeshRenderer>() is MeshRenderer childRenderer && (renderer = childRenderer) != null)
			{ 
				renderer.sharedMaterial = material;
			}
		}
	}

	private Quaternion CalculateRotation()
	{
		if (_occlusalPoints.Count < 3 || _sagittalPoints.Count < 2)
		{
			Debug.LogWarning("Cannot calculate rotation, not enough points!");
			return Quaternion.identity;
		}

		Vector3 occlusalMolarRight = _occlusalPoints[(int)OcclusalPoint.MolarRight];
		Vector3 occlusalIncisorMiddle = _occlusalPoints[(int)OcclusalPoint.IncisorMiddle];
		Vector3 occlusalMolarLeft = _occlusalPoints[(int)OcclusalPoint.MolarLeft];

		Vector3 sagittalFront = _sagittalPoints[(int)SagittalPoint.Front];
		Vector3 sagittalBack = _sagittalPoints[(int)SagittalPoint.Back];

		// get the top (up) axis
		Vector3 v1 = occlusalIncisorMiddle - occlusalMolarRight;
		Vector3 v2 = occlusalMolarLeft - occlusalMolarRight;
		Vector3 upAxis = Vector3.Cross(v1, v2).normalized;

		// get the forward (front) axis, so that it points away from the mouth
		Vector3 sagittalDir = sagittalFront - sagittalBack;
		Vector3 forwardAxis = Vector3.ProjectOnPlane(sagittalDir, upAxis).normalized;

		return Quaternion.LookRotation(forwardAxis, upAxis);
	}

	private bool RequiresReversedWinding(List<Vector3> points, Vector3 upDir)
	{
		Vector3 normal = Vector3.zero;

		for (int i = 0; i < points.Count; i++)
		{
			Vector3 current = points[i];
			Vector3 next = points[(i + 1) % points.Count];

			normal.x += (current.y - next.y) * (current.z + next.z);
			normal.y += (current.z - next.z) * (current.x + next.x);
			normal.z += (current.x - next.x) * (current.y + next.y);
		}

		return Vector3.Dot(normal, upDir) < 0;
	}
}
