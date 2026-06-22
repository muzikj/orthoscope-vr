using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

// necessary Unity Triangle .NET library
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Topology;

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

	private GameObject _occlusalPlanePreview;
	private GameObject _sagittalPlanePreview;

	private readonly List<GameObject> _occlusalMarks = new();
	private readonly List<GameObject> _sagittalMarks = new();

	private bool _bUpperJaw = true;

	private Quaternion _baseRotation;

	private float _masterMinX = 0f;
	private float _masterMaxX = 0f;
	private float _masterMinZ = 0f;
	private float _masterMaxZ = 0f;

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
		UIEvents.OnAdvanceBuilderRequested += HandleAdvanceRequested;
		UIEvents.OnResetBuilderRequested += HandleResetBuilderRequested;
	}

	private void OnDisable()
	{
		UIEvents.OnAdvanceBuilderRequested -= HandleAdvanceRequested;
		UIEvents.OnResetBuilderRequested -= HandleResetBuilderRequested;
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

	private void HandleResetBuilderRequested()
	{
		if (_bProcessing)
		{
			return;
		}

		ResetBuilderState();
	}

	private async Task AdvanceStateAsync()
	{
		if (currentState == BuilderState.MarkOcclusal)
		{
			if (_occlusalPoints.Count < 3)
			{
				UIEvents.RequestUIMessage($"Please, place 3 Occlusal points first! ({_occlusalPoints.Count}/3)");

				return;
			}

			currentState = BuilderState.MarkSagittal;

            if (_occlusalPlanePreview != null)
            {
                _occlusalPlanePreview.SetActive(false);
            }

            UIEvents.RequestUIMessage("State Advanced\nNow marking Sagittal Plane (2 points on upper palate).");
		}
		else if (currentState == BuilderState.MarkSagittal)
		{
			if (_sagittalPoints.Count < 2)
			{
				UIEvents.RequestUIMessage($"Please, place 2 Sagittal points first! ({_sagittalPoints.Count}/2)");

				return;
			}

			DestroyPlanePreviews();

			_baseRotation = CalculateRotation();

			DestroyPlanePoints();

			currentState = BuilderState.MarkUpperGums;
			UIEvents.RequestUIMessage("State Advanced\nNow marking Gum Splines. AnnotationTube active!");
		}
		else if (currentState == BuilderState.MarkUpperGums)
		{
			AnnotationManager upperSpline = null;
			AnnotationManager[] activeSplines = FindObjectsByType<AnnotationManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
				UIEvents.RequestUIMessage("Missing a closed spline!\nClose the spline before advancing!");

				return;
			}

			currentState = BuilderState.MeshingUpper;
			UIEvents.RequestUIMessage("State Advanced:\nNow processing the Upper Base...");

			if (upperSpline.transform.parent.TryGetComponent<MeshRenderer>(out var upperRenderer))
			{
				upperRenderer.material.SetFloat("_Cull", 2f);
			}

			await TrimmingAndBasePipelineAsync(upperSpline);

			_bUpperJaw = false;

			currentState = BuilderState.MarkLowerGums;
			UIEvents.RequestUIMessage("Success!\nUpper Base Complete!\nMoving onto the Lower Gum Splines.");
		}
		else if (currentState == BuilderState.MarkLowerGums)
		{
			AnnotationManager lowerSpline = null;
			AnnotationManager[] activeSplines = FindObjectsByType<AnnotationManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
				UIEvents.RequestUIMessage("Missing a closed spline!\nClose the spline before advancing!");

				return;
			}

			currentState = BuilderState.MeshingLower;
			UIEvents.RequestUIMessage("State Advanced:\nNow processing the Lower Base...");

			if (lowerSpline.transform.parent.TryGetComponent<MeshRenderer>(out var lowerRenderer))
			{
				lowerRenderer.material.SetFloat("_Cull", 2f);
			}

			await TrimmingAndBasePipelineAsync(lowerSpline);

			currentState = BuilderState.Finished;
			UIEvents.RequestUIMessage("Success!\nLower Base Complete!\nABO Base Finished!");

			await Task.Delay(3500);

			ResetBuilderState();
		}
	}

	private async Task TrimmingAndBasePipelineAsync(AnnotationManager activeSpline)
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

	private async Task ZipScanToSkirtAsync(MeshFilter scanMeshFilter, AnnotationManager skirtSpline)
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

        // step ahead by 5% of the total loops to escape microscopic mesh noise
        int scanStride = Mathf.Max(1, scanRimPts.Count / 20);
		int skirtStride = Mathf.Max(1, localSkirtPoints.Count / 20);

		int testNextScan = (bestScanIdx + scanStride) % scanRimPts.Count;
		int testNextSkirtFwd = (bestSkirtIdx + skirtStride) % localSkirtPoints.Count;
		int testNextSkirtRev = (bestSkirtIdx - skirtStride + localSkirtPoints.Count) % localSkirtPoints.Count;

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

		// isolate the boundary (if an edge is not shared by any other triangles, it is exposed)
		Dictionary<int, List<int>> boundaryLinks = new();
		foreach (var kvp in edgeCounts)
		{
			if (kvp.Value == 1)
			{
				var (from, to) = directedEdgeMap[kvp.Key];

				if (!boundaryLinks.ContainsKey(from))
				{
					boundaryLinks[from] = new List<int>();
				}

				boundaryLinks[from].Add(to);
			}
		}

		// chain edges into continuous loops
		List<List<int>> loops = new();
		HashSet<int> visitedEdges = new();

		foreach (int startNode in boundaryLinks.Keys)
		{
			// try starting a new loop from each available outgoing edge
			foreach (int initialNextNode in boundaryLinks[startNode])
			{
				// unique hash for the directed edge to ensure we don't walk the same path twice
				int initialEdgeHash = (startNode * 397) ^ initialNextNode;
				if (visitedEdges.Contains(initialEdgeHash)) continue;

				List<int> currentLoop = new();
				int curr = startNode;
				int next = initialNextNode;

				while (true)
				{
					int edgeHash = (curr * 397) ^ next;
					if (visitedEdges.Contains(edgeHash)) break; // we have closed the loop

					visitedEdges.Add(edgeHash);
					currentLoop.Add(curr);

					curr = next;

					// find the next unvisited outgoing edge from the current node
					bool foundNext = false;
					if (boundaryLinks.TryGetValue(curr, out List<int> nextNodes))
					{
						foreach (int potentialNext in nextNodes)
						{
							int nextEdgeHash = (curr * 397) ^ potentialNext;
							if (!visitedEdges.Contains(nextEdgeHash))
							{
								next = potentialNext;
								foundNext = true;
								break; // take the first untraversed path
							}
						}
					}

					if (!foundNext) break; // dead end
				}

				if (currentLoop.Count > 0)
				{
					loops.Add(currentLoop);
				}
			}
		}

		// we are only interested in the longest loop, which should be the jaw cut loop, thus ignoring any micro-holes in teeth topology
		List<int> longestLoop = new();
		foreach (var loop in loops)
		{
			if (loop.Count > longestLoop.Count) longestLoop = loop;
		}

		return longestLoop;
	}

	private async Task BaseGenerationPipelineAsync(AnnotationManager spline)
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
			return ProcessProfessionalBaseMath(splinePoints, _baseRotation, settings);
		});

		BuildFinalBaseObject(vertices, triangles, spline);
	}

	private void BuildFinalBaseObject(Vector3[] vertices, int[] triangles, AnnotationManager spline)
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

		if (spline.transform.parent.TryGetComponent<ScanController>(out var controller))
		{
			controller.jawType = _bUpperJaw ? ScanController.JawType.Upper : ScanController.JawType.Lower;

			controller.UpdateOriginalState();
		}

		spline.ClearSplineMarks();

		spline.gameObject.SetActive(false);
	}

	private async Task TrimScanAsync(MeshFilter scanMeshFilter, AnnotationManager cutSpline)
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

		Debug.Log($"Original Verts: {rawVerts.Length} | Trimmed Verts: {cleanedVerts.Length}");

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

	public void AddPlanarPoint(Vector3 worldPosition, Vector3 normal, Transform scanTransform)
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
			UIEvents.RequestUIMessage($"Occlusal point added. ({_occlusalPoints.Count}/3)");
		}
		else if (currentState == BuilderState.MarkSagittal)
		{
			_sagittalPoints.Add(localPosition);
			if (mark != null) _sagittalMarks.Add(mark);

			UpdateMarkColors(_sagittalMarks, 2);
			UIEvents.RequestUIMessage($"Sagittal point added. ({_sagittalPoints.Count}/2)");
		}

		UpdatePlanePreviews(scanTransform);
	}

	public void RemoveLastPlanarPoint()
	{
		if (currentState == BuilderState.MarkOcclusal && _occlusalPoints.Count > 0)
		{
			_occlusalPoints.RemoveAt(_occlusalPoints.Count - 1);
			RemoveLastMark(_occlusalMarks);

			UpdateMarkColors(_occlusalMarks, 3);
			UIEvents.RequestUIMessage($"Last occlusal point removed. ({_occlusalPoints.Count}/3)");
		}
		else if (currentState == BuilderState.MarkSagittal && _sagittalPoints.Count > 0)
		{
			_sagittalPoints.RemoveAt(_sagittalPoints.Count - 1);
			RemoveLastMark(_sagittalMarks);

			UpdateMarkColors(_sagittalMarks, 2);
			UIEvents.RequestUIMessage($"Last sagittal point removed. ({_sagittalPoints.Count}/2)");
		}

		Transform parentTransform = null;
		if (_occlusalMarks.Count > 0)
		{
			parentTransform = _occlusalMarks[0].transform.parent;
		}
		else if (_sagittalMarks.Count > 0)
		{
			parentTransform = _sagittalMarks[0].transform.parent;
		}

		if (parentTransform != null)
		{
			UpdatePlanePreviews(parentTransform);
		}
		else
		{
			DestroyPlanePreviews();
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

		Vector3 upAxis = CalculateUpAxis();

		Vector3 sagittalFront = _sagittalPoints[(int)SagittalPoint.Front];
		Vector3 sagittalBack = _sagittalPoints[(int)SagittalPoint.Back];

		// get the forward (front) axis, so that it points away from the mouth
		Vector3 sagittalDir = sagittalFront - sagittalBack;
		Vector3 forwardAxis = Vector3.ProjectOnPlane(sagittalDir, upAxis).normalized;

		return Quaternion.LookRotation(forwardAxis, upAxis);
	}

	private Vector3 CalculateUpAxis()
	{
		if (_occlusalPoints.Count < 3)
		{
			return Vector3.up;
		}

		Vector3 occlusalMolarRight = _occlusalPoints[(int)OcclusalPoint.MolarRight];
		Vector3 occlusalIncisorMiddle = _occlusalPoints[(int)OcclusalPoint.IncisorMiddle];
		Vector3 occlusalMolarLeft = _occlusalPoints[(int)OcclusalPoint.MolarLeft];

		return Vector3.Cross(occlusalIncisorMiddle - occlusalMolarRight, occlusalMolarLeft - occlusalMolarRight).normalized;
	}

	private (Vector3[], int[]) ProcessProfessionalBaseMath(List<Vector3> splinePoints, Quaternion baseRotation, PlinthSettings settings)
	{
		// clean the spline
		List<Vector3> filteredPoints = new() { splinePoints[0] };
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

		// map limits and local points
		float minX = float.MaxValue, maxX = float.MinValue;
		float minZ = float.MaxValue, maxZ = float.MinValue;
		float lowestY = float.MaxValue, highestY = float.MinValue;

		List<Vector3> localPoints = new();
		List<Vector2> spline2D = new();

		for (int i = 0; i < splineCount; i++)
		{
			Vector3 localPt = inverseRotation * splinePoints[i];
			localPoints.Add(localPt);
			spline2D.Add(new Vector2(localPt.x, localPt.z));

			if (localPt.y < lowestY) lowestY = localPt.y;
			if (localPt.y > highestY) highestY = localPt.y;
			if (localPt.x < minX) minX = localPt.x;
			if (localPt.x > maxX) maxX = localPt.x;
			if (localPt.z < minZ) minZ = localPt.z;
			if (localPt.z > maxZ) maxZ = localPt.z;
		}

		float midY = settings.isUpperJaw ? (lowestY - settings.skirtDepth) : (highestY + settings.skirtDepth);
		float topY = settings.isUpperJaw ? (midY - settings.baseHeight) : (midY + settings.baseHeight);

		// ABO base proportions on unpadded true dimensions
		float W = maxX - minX;
		float D = maxZ - minZ;
		float MidX = (minX + maxX) / 2f;

		float canineZ = maxZ - (D * 0.35f);	// canines taper in 35% from the front
		float heelZ = minZ + (D * 0.15f);	// heel cut is 15% deep
		float heelInsetX = W * 0.15f;		// heel cut is 15% wide
		float lowerFrontInsetX = W * 0.12f;	// lower jaw flat front width

		// dynamic dilation
		float dynamicInflation = 0f;
		Vector2[] aboPolygon = null;

		bool isSafe = false;

		int safetyCounter = 0;
		int maxExpansions = 50;

		float totalBackPadding = settings.widePadding * 2f;

		while (!isSafe && safetyCounter < maxExpansions)
		{
			// lock the back wall to the static padding so heels remain perfectly flush
			float pMinZ = minZ - totalBackPadding;
			float pHeelZ = heelZ - totalBackPadding;

			// dynamically inflate the sides and the front
			float pMinX = minX - (settings.widePadding + dynamicInflation);
			float pMaxX = maxX + (settings.widePadding + dynamicInflation);
			float pMaxZ = maxZ + (settings.widePadding + dynamicInflation);
			float pCanineZ = canineZ + (settings.widePadding + dynamicInflation);

			// master footprint synchronization
			if (settings.isUpperJaw)
			{
				_masterMinX = pMinX;
				_masterMaxX = pMaxX;
				_masterMinZ = pMinZ;
				_masterMaxZ = pMaxZ;
			}
			else
			{
				pMinZ = _masterMinZ; // only lock the Z (back)
			}

			// build candidate polygon
			if (settings.isUpperJaw)
			{
				aboPolygon = new Vector2[7];

				aboPolygon[0] = new Vector2(pMinX + heelInsetX, pMinZ);	// Back Left
				aboPolygon[1] = new Vector2(pMinX, pHeelZ);				// Side Back Left
				aboPolygon[2] = new Vector2(pMinX, pCanineZ);			// Canine Left
				aboPolygon[3] = new Vector2(MidX, pMaxZ);				// Front Point
				aboPolygon[4] = new Vector2(pMaxX, pCanineZ);			// Canine Right
				aboPolygon[5] = new Vector2(pMaxX, pHeelZ);				// Side Back Right
				aboPolygon[6] = new Vector2(pMaxX - heelInsetX, pMinZ);	// Back Right
			}
			else
			{
				aboPolygon = new Vector2[8];

				aboPolygon[0] = new Vector2(pMinX + heelInsetX, pMinZ);			// Back Left
				aboPolygon[1] = new Vector2(pMinX, pHeelZ);						// Side Back Left
				aboPolygon[2] = new Vector2(pMinX, pCanineZ);					// Canine Left
				aboPolygon[3] = new Vector2(MidX - lowerFrontInsetX, pMaxZ);	// Front Left Flat
				aboPolygon[4] = new Vector2(MidX + lowerFrontInsetX, pMaxZ);	// Front Right Flat
				aboPolygon[5] = new Vector2(pMaxX, pCanineZ);					// Canine Right
				aboPolygon[6] = new Vector2(pMaxX, pHeelZ);						// Side Back Right
				aboPolygon[7] = new Vector2(pMaxX - heelInsetX, pMinZ);			// Back Right
			}

			isSafe = IsPolygonSafe(aboPolygon, spline2D, 0.0005f);

			if (!isSafe)
			{
				dynamicInflation += 0.1f * settings.widePadding;
			}

			safetyCounter++;
		}

		if (safetyCounter >= maxExpansions)
		{
			Debug.LogWarning("BaseBuilder has hit max expansions! The dental arch shape is extremely irregular.");
		}

		// chop the edges into hundreds of tiny segments for the wavy wall
		Vector2[] denseAboPolygon = DensifyPolygon(aboPolygon, Config.Instance.densificationDistance);
		List<Vector2> outerBound = new(denseAboPolygon);

		// generate flat meshes via Constrained Delaunay Triangulation
		var (skirtVerts2D, skirtTris) = Triangulate2D(outerBound, spline2D);
		var (botVerts2D, botTris) = Triangulate2D(outerBound);

		List<Vector3> finalVerts = new();
		List<int> finalTris = new();

		Vector2[] spline2DArray = spline2D.ToArray();

		// calculate wavy Y-heights for all the new dense points
		float[] aboWavyY = new float[denseAboPolygon.Length];

		for (int i = 0; i < denseAboPolygon.Length; i++)
		{
			float closestSqrDist = float.MaxValue;
			float closestY = 0f;

			for (int j = 0; j < spline2D.Count; j++)
			{
				float d = (denseAboPolygon[i] - spline2D[j]).sqrMagnitude;

				if (d < closestSqrDist)
				{
					closestSqrDist = d;
					closestY = localPoints[j].y;
				}
			}

			float fixedDrop = settings.skirtDepth;

			aboWavyY[i] = settings.isUpperJaw ? Mathf.Max(closestY - fixedDrop, topY) : Mathf.Min(closestY + fixedDrop, topY);
		}

		// Laplacian array smoothing, i.e. geometric AA
		int polyLen = denseAboPolygon.Length;

		for (int pass = 0; pass < Config.Instance.smoothingPasses; pass++)
		{
			float[] smoothedY = new float[polyLen];

			for (int i = 0; i < polyLen; i++)
			{
				float sum = 0f;
				int count = 0;

				for (int w = -Config.Instance.smoothingWindowSize; w <= Config.Instance.smoothingWindowSize; w++)
				{
					// wrap around the closed polygon
					int idx = (i + w + polyLen) % polyLen;
					sum += aboWavyY[idx];

					count++;
				}

				smoothedY[i] = sum / count;
			}

			// overwrite the jagged array with the newly smoothed array
			aboWavyY = smoothedY;
		}

		// lift the skirt to 3D
		int skirtOffset = finalVerts.Count;

		for (int i = 0; i < skirtVerts2D.Length; i++)
		{
			Vector2 v2 = skirtVerts2D[i];
			float finalY = topY;

			int splineIdx = FindExactIndex(spline2DArray, v2);

			if (splineIdx != -1)
			{
				finalY = localPoints[splineIdx].y;
			}
			else
			{
				int aboIdx = FindExactIndex(denseAboPolygon, v2);

				if (aboIdx != -1)
				{
					finalY = aboWavyY[aboIdx];
				}
				else
				{
					float cSqrDist = float.MaxValue;
					float cY = 0f;

					for (int j = 0; j < spline2D.Count; j++)
					{
						float d = (v2 - spline2D[j]).sqrMagnitude;

						if (d < cSqrDist)
						{
							cSqrDist = d;
							cY = localPoints[j].y;
						}
					}

					finalY = settings.isUpperJaw ? Mathf.Max(cY - settings.skirtDepth, topY) : Mathf.Min(cY + settings.skirtDepth, topY);
				}
			}

			finalVerts.Add(baseRotation * new Vector3(v2.x, finalY, v2.y));
		}

		// map the skirt triangles
		bool reverseSkirt = !settings.isUpperJaw;

		for (int i = 0; i < skirtTris.Length; i += 3)
		{
			if (reverseSkirt)
			{
				finalTris.Add(skirtTris[i + 2] + skirtOffset);
				finalTris.Add(skirtTris[i + 1] + skirtOffset);
				finalTris.Add(skirtTris[i] + skirtOffset);
			}
			else
			{
				finalTris.Add(skirtTris[i] + skirtOffset);
				finalTris.Add(skirtTris[i + 1] + skirtOffset);
				finalTris.Add(skirtTris[i + 2] + skirtOffset);
			}
		}

		// lift the bottom cap to 3D
		int botOffset = finalVerts.Count;

		for (int i = 0; i < botVerts2D.Length; i++)
		{
			finalVerts.Add(baseRotation * new Vector3(botVerts2D[i].x, topY, botVerts2D[i].y));
		}

		bool reverseBot = settings.isUpperJaw;

		for (int i = 0; i < botTris.Length; i += 3)
		{
			if (reverseBot)
			{
				finalTris.Add(botTris[i + 2] + botOffset);
				finalTris.Add(botTris[i + 1] + botOffset);
				finalTris.Add(botTris[i] + botOffset);
			}
			else
			{
				finalTris.Add(botTris[i] + botOffset);
				finalTris.Add(botTris[i + 1] + botOffset);
				finalTris.Add(botTris[i + 2] + botOffset);
			}
		}

		// stitch the vertical walls together
		for (int i = 0; i < denseAboPolygon.Length; i++)
		{
			Vector2 current2D = denseAboPolygon[i];
			Vector2 next2D = denseAboPolygon[(i + 1) % denseAboPolygon.Length];

			int sCurr = FindClosestIndex(skirtVerts2D, current2D) + skirtOffset;
			int sNext = FindClosestIndex(skirtVerts2D, next2D) + skirtOffset;

			int bCurr = FindClosestIndex(botVerts2D, current2D) + botOffset;
			int bNext = FindClosestIndex(botVerts2D, next2D) + botOffset;

			if (settings.isUpperJaw)
			{
				finalTris.Add(sCurr);
				finalTris.Add(bCurr);
				finalTris.Add(sNext);

				finalTris.Add(sNext);
				finalTris.Add(bCurr);
				finalTris.Add(bNext);
			}
			else
			{
				finalTris.Add(sCurr);
				finalTris.Add(sNext);
				finalTris.Add(bCurr);

				finalTris.Add(sNext);
				finalTris.Add(bNext);
				finalTris.Add(bCurr);
			}
		}

		return (finalVerts.ToArray(), finalTris.ToArray());
	}

    private static (Vector2[] vertices, int[] triangles) Triangulate2D(List<Vector2> outerBoundary, List<Vector2> innerBoundary = null)
    {
        Polygon polygon = new();

        // outer boundary
        List<Vertex> boundaryVertices = new(outerBoundary.Count);

        foreach (Vector2 pt in outerBoundary)
        {
            boundaryVertices.Add(new Vertex(pt.x, pt.y));
        }

        polygon.Add(new Contour(boundaryVertices), false);

        // inner boundary
        if (innerBoundary != null && innerBoundary.Count > 0)
        {
            List<Vertex> innerVertices = new(innerBoundary.Count);

            foreach (Vector2 pt in innerBoundary)
            {
                innerVertices.Add(new Vertex(pt.x, pt.y));
            }

            // do not punch a hole
            polygon.Add(new Contour(innerVertices), false);
        }

        ConstraintOptions constraints = new()
        {
            ConformingDelaunay = false
        };

        IMesh cdtMesh = polygon.Triangulate(constraints);

        return ConvertToUnityData(cdtMesh, innerBoundary);
    }

    private static (Vector2[], int[]) ConvertToUnityData(IMesh cdtMesh, List<Vector2> holePolygon)
    {
        Vector2[] unityVertices = new Vector2[cdtMesh.Vertices.Count];
        Dictionary<int, int> idToIndex = new();

        int index = 0;
        foreach (Vertex v in cdtMesh.Vertices)
        {
            unityVertices[index] = new Vector2((float)v.X, (float)v.Y);
            idToIndex[v.ID] = index;
            index++;
        }

        List<int> unityTriangles = new();
        Vector2[] holePolyArray = holePolygon?.ToArray();

        foreach (Triangle tri in cdtMesh.Triangles)
        {
            Vector2 v0 = unityVertices[idToIndex[tri.GetVertex(0).ID]];
            Vector2 v1 = unityVertices[idToIndex[tri.GetVertex(1).ID]];
            Vector2 v2 = unityVertices[idToIndex[tri.GetVertex(2).ID]];

            // check if the center of this triangle is inside the teeth spline
            if (holePolyArray != null && holePolyArray.Length > 2)
            {
                Vector2 centroid = (v0 + v1 + v2) / 3f;

                if (IsPointInPolygon(centroid, holePolyArray))
                {
                    continue;
                }
            }

            unityTriangles.Add(idToIndex[tri.GetVertex(2).ID]);
            unityTriangles.Add(idToIndex[tri.GetVertex(1).ID]);
            unityTriangles.Add(idToIndex[tri.GetVertex(0).ID]);
        }

        return (unityVertices, unityTriangles.ToArray());
    }

    // mathematical raycast (even-odd rule) to accurately detect if a point is inside a complex 2D shape
    private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool isInside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) && (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
            {
                isInside = !isInside;
            }
        }

        return isInside;
    }

    private bool IsPolygonSafe(Vector2[] polygon, List<Vector2> spline, float safeMargin)
	{
		float safeMarginSq = safeMargin * safeMargin;

		foreach (Vector2 pt in spline)
		{
			bool isInside = false;

			// point-in-polygon check (raycast even-odd rule)
			for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
			{
				if (((polygon[i].y > pt.y) != (polygon[j].y > pt.y)) && (pt.x < (polygon[j].x - polygon[i].x) * (pt.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x))
				{
					isInside = !isInside;
				}
			}

			// the spline escaped the polygon!
			if (!isInside)
			{
				return false;
			}

			// proximity check (prevents CDT precision crashes)
			for (int i = 0; i < polygon.Length; i++)
			{
				Vector2 p1 = polygon[i];
				Vector2 p2 = polygon[(i + 1) % polygon.Length];

				// spline is too close to the wall
				if (SqrDistancePointToSegment(pt, p1, p2) < safeMarginSq)
				{
					return false; 
				}
			}
		}

		return true;
	}

	private float SqrDistancePointToSegment(Vector2 pt, Vector2 p1, Vector2 p2)
	{
		float l2 = (p1 - p2).sqrMagnitude;

		if (l2 == 0)
		{
			return (pt - p1).sqrMagnitude;
		}

		float t = Mathf.Max(0f, Mathf.Min(1f, Vector2.Dot(pt - p1, p2 - p1) / l2));
		Vector2 projection = p1 + t * (p2 - p1);

		return (pt - projection).sqrMagnitude;
	}

	// a mathematically strict index search, which prevents scrambled Y-heights
	private int FindExactIndex(Vector2[] array, Vector2 target)
	{
		int bestIdx = -1;
		float bestDist = float.MaxValue;

		for (int i = 0; i < array.Length; i++)
		{
			float d = Vector2.SqrMagnitude(array[i] - target);

			if (d < bestDist)
			{
				bestDist = d;
				bestIdx = i;
			}
		}

		return bestDist < 1e-5f ? bestIdx : -1;
	}

	// a forgiving index search, which prevents wall stitching failures
	private int FindClosestIndex(Vector2[] array, Vector2 target)
	{
		int bestIdx = 0;
		float bestDist = float.MaxValue;

		for (int i = 0; i < array.Length; i++)
		{
			float d = Vector2.SqrMagnitude(array[i] - target);

			if (d < bestDist)
			{
				bestDist = d;
				bestIdx = i;
			}
		}

		return bestIdx;
	}

	private Vector2[] DensifyPolygon(Vector2[] polygon, float maxEdgeLength)
	{
		List<Vector2> densePoly = new();

		for (int i = 0; i < polygon.Length; i++)
		{
			Vector2 p1 = polygon[i];
			Vector2 p2 = polygon[(i + 1) % polygon.Length];

			densePoly.Add(p1);

			float dist = Vector2.Distance(p1, p2);
			int segments = Mathf.CeilToInt(dist / maxEdgeLength);

			for (int j = 1; j < segments; j++)
			{
				float t = (float)j / segments;
				densePoly.Add(Vector2.Lerp(p1, p2, t));
			}
		}

		return densePoly.ToArray();
	}

	private void ResetBuilderState()
	{
		DestroyPlanePreviews();
		DestroyPlanePoints();

		// reset main flag and state
		_bUpperJaw = true;

		currentState = BuilderState.MarkOcclusal;

		// update the UI
		UIEvents.RequestUIMessage("Base Builder has been Reset!\nReady for a new scan.\nPlace 3 Occlusal points.");
	}

	private void UpdatePlanePreviews(Transform scanTransform)
	{
		if (scanTransform == null || Config.Instance.planePreviewPrefab == null)
		{
			return;
		}

        // occlusal plane preview
        if (_occlusalPoints.Count == 3 && ((currentState == BuilderState.MarkOcclusal) || (currentState == BuilderState.MarkSagittal && _sagittalPoints.Count == 2)))
		{
			if (_occlusalPlanePreview == null)
			{
				_occlusalPlanePreview = Instantiate(Config.Instance.planePreviewPrefab, scanTransform, false);
				_occlusalPlanePreview.name = "Preview_OcclusalPlane";

				if (Config.Instance.occlusalPlaneMaterial != null && _occlusalPlanePreview.TryGetComponent<MeshRenderer>(out var occlusalRenderer))
				{
					occlusalRenderer.sharedMaterial = Config.Instance.occlusalPlaneMaterial;
				}
			}

			Vector3 upAxis = CalculateUpAxis();

			Vector3 occlusalMolarRight = _occlusalPoints[(int)OcclusalPoint.MolarRight];
			Vector3 occlusalIncisorMiddle = _occlusalPoints[(int)OcclusalPoint.IncisorMiddle];
			Vector3 occlusalMolarLeft = _occlusalPoints[(int)OcclusalPoint.MolarLeft];

			// temp fwd axis, since sagittal points have not been placed yet
			Vector3 tempForward = (occlusalIncisorMiddle - occlusalMolarRight).normalized;
			Vector3 center = (occlusalMolarRight + occlusalIncisorMiddle + occlusalMolarLeft) / 3f;

			_occlusalPlanePreview.transform.SetLocalPositionAndRotation(center, Quaternion.LookRotation(tempForward, upAxis));

			_occlusalPlanePreview.SetActive(true);
		}
		else if (_occlusalPlanePreview != null) // if we removed an occlusal point (undo)
		{
			_occlusalPlanePreview.SetActive(false);
		}

		// sagittal plane preview
		if (_occlusalPoints.Count == 3 && _sagittalPoints.Count == 2)
		{
			if (_sagittalPlanePreview == null)
			{
				_sagittalPlanePreview = Instantiate(Config.Instance.planePreviewPrefab, scanTransform, false);
				_sagittalPlanePreview.name = "Preview_SagittalPlane";

				if (Config.Instance.sagittalPlaneMaterial != null && _sagittalPlanePreview.TryGetComponent<MeshRenderer>(out var sagittalRenderer))
				{
					sagittalRenderer.sharedMaterial = Config.Instance.sagittalPlaneMaterial;
				}
			}

			Quaternion finalRotation = CalculateRotation();

			Vector3 forwardAxis = finalRotation * Vector3.forward;
			Vector3 rightAxis = finalRotation * Vector3.right;

			// update the occlusal plane to snap to the true fwd vector
			if (_occlusalPlanePreview != null)
			{
				_occlusalPlanePreview.transform.localRotation = finalRotation;
			}

			Vector3 sagittalFront = _sagittalPoints[(int)SagittalPoint.Front];
			Vector3 sagittalBack = _sagittalPoints[(int)SagittalPoint.Back];

			Vector3 center = (sagittalFront + sagittalBack) / 2f;

			_sagittalPlanePreview.transform.SetLocalPositionAndRotation(center, Quaternion.LookRotation(forwardAxis, rightAxis));

			_sagittalPlanePreview.SetActive(true);
		}
		else if (_sagittalPlanePreview != null) // if we removed a sagittal point (undo)
		{
			_sagittalPlanePreview.SetActive(false);
		}
	}

	private void DestroyPlanePreviews()
	{
		if (_occlusalPlanePreview != null)
		{
			Destroy(_occlusalPlanePreview);
			_occlusalPlanePreview = null;
		}

		if (_sagittalPlanePreview != null)
		{
			Destroy(_sagittalPlanePreview);
			_sagittalPlanePreview = null;
		}
	}

	private void DestroyPlanePoints()
	{
		foreach (GameObject mark in _occlusalMarks)
		{
			Destroy(mark);
		}

		foreach (GameObject mark in _sagittalMarks)
		{
			Destroy(mark);
		}

		_occlusalMarks.Clear();
		_sagittalMarks.Clear();

		_occlusalPoints.Clear();
		_sagittalPoints.Clear();
	}
}
