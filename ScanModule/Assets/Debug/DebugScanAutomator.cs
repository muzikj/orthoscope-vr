using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct DebugMockSet
{
	[Tooltip("Parent object for the planes. Children 0-2 = Occlusal, Children 3-4 = Sagittal")]
	public Transform planarGroup;
	[Tooltip("Parent object for the upper spline points.")]
	public Transform upperSplineGroup;
	[Tooltip("Parent object for the lower spline points.")]
	public Transform lowerSplineGroup;
}

public class DebugScanAutomator : MonoBehaviour
{
	public List<DebugMockSet> debugSets = new();

	private enum TargetSystem
	{
		BaseBuilder,
		SplineLoop,
	}

	private void OnEnable()
	{
		ScanEvents.OnInjectDebugDataRequested += HandleInjectDebugDataRequested;
	}

	private void OnDisable()
	{
		ScanEvents.OnInjectDebugDataRequested -= HandleInjectDebugDataRequested;
	}

	private void HandleInjectDebugDataRequested(int setIndex)
	{
		if (BaseBuilder.Instance == null)
		{
			Debug.LogError($"[DebugAutomator] Cannot inject! BaseBuilder.Instance is missing from the scene.");

			return;
		}

		if (setIndex < 0 || setIndex >= debugSets.Count)
		{
			Debug.LogError($"[DebugAutomator] Requested Set Index {setIndex} is out of bounds!");

			return;
		}

		DebugMockSet currentSet = debugSets[setIndex];

		switch (BaseBuilder.Instance.currentState)
		{
			case BaseBuilder.BuilderState.MarkOcclusal:
			{
				List<Transform> occlusalPoints = ExtractPlanarPoints(currentSet.planarGroup, 0, 3);
				InjectMockData(occlusalPoints, "Occlusal", TargetSystem.BaseBuilder, BaseBuilder.BuilderState.MarkOcclusal);

				break;
			}

			case BaseBuilder.BuilderState.MarkSagittal:
			{
				List<Transform> sagittalPoints = ExtractPlanarPoints(currentSet.planarGroup, 3, 2);
				InjectMockData(sagittalPoints, "Sagittal", TargetSystem.BaseBuilder, BaseBuilder.BuilderState.MarkSagittal);

				break;
			}

			case BaseBuilder.BuilderState.MarkUpperGums:
			{
				List<Transform> upperSpline = ExtractSplinePoints(currentSet.upperSplineGroup);
				InjectMockData(upperSpline, "Upper Gum Spline", TargetSystem.SplineLoop, BaseBuilder.BuilderState.MarkUpperGums);

				break;
			}

			case BaseBuilder.BuilderState.MarkLowerGums:
			{
				List<Transform> lowerSpline = ExtractSplinePoints(currentSet.lowerSplineGroup);
				InjectMockData(lowerSpline, "Lower Gum Spline", TargetSystem.SplineLoop, BaseBuilder.BuilderState.MarkLowerGums);

				break;
			}

			default:
			{
				Debug.LogWarning($"[DebugAutomator] BaseBuilder is in state {BaseBuilder.Instance.currentState}, which is not set up for injection!");

				break;
			}
		}
	}

	private List<Transform> ExtractPlanarPoints(Transform parentGroup, int startIndex, int count)
	{
		List<Transform> points = new();

		if (parentGroup == null)
		{
			Debug.LogWarning("[DebugAutomator] Planar Group is not assigned in the requested Debug Set.");

			return points;
		}

		if (parentGroup.childCount < startIndex + count)
		{
			Debug.LogWarning($"[DebugAutomator] Planar Group '{parentGroup.name}' does not have enough children! Expected at least {startIndex + count}, found {parentGroup.childCount}.");

			return points;
		}

		for (int i = startIndex; i < startIndex + count; i++)
		{
			points.Add(parentGroup.GetChild(i));
		}

		return points;
	}

	private List<Transform> ExtractSplinePoints(Transform parentGroup)
	{
		List<Transform> points = new();

		if (parentGroup == null)
		{
			Debug.LogWarning("[DebugAutomator] Spline Group is not assigned in the requested Debug Set.");

			return points;
		}

		if (parentGroup.childCount < 3)
		{
			Debug.LogWarning($"[DebugAutomator] Spline Group '{parentGroup.name}' does not have enough children for a loop! Expected at least {3}, found {parentGroup.childCount}.");

			return points;
		}

		for (int i = 0; i < parentGroup.childCount; i++)
		{
			points.Add(parentGroup.GetChild(i));
		}

		points.Add(parentGroup.GetChild(0));

		return points;
	}

	private void InjectMockData(List<Transform> mockPoints, string debugName, TargetSystem target, BaseBuilder.BuilderState state)
	{
		if (mockPoints == null || mockPoints.Count == 0)
		{
			return;
		}

		if (BaseBuilder.Instance.currentState != state)
		{
			Debug.LogWarning($"[DebugAutomator] BaseBuilder is not in the correct state for injecting {debugName} points! Current state: {BaseBuilder.Instance.currentState}");

			return;
		}

		ScanSpline activeSpline = GetActiveSpline();

		if (activeSpline == null)
		{
			Debug.LogWarning($"[DebugAutomator] No open ScanSpline found for {debugName}!");

			return;
		}

		Transform scanTransform = activeSpline.transform.parent;

		Debug.Log($"[DebugAutomator] Injecting {debugName} points...");

		foreach (var mockPt in mockPoints)
		{
			Vector3 worldPos = scanTransform.TransformPoint(mockPt.localPosition);

			Vector3 localForward = Quaternion.Euler(mockPt.localEulerAngles) * Vector3.forward;
			Vector3 worldForward = scanTransform.TransformDirection(localForward);

			if (target == TargetSystem.BaseBuilder)
			{
				BaseBuilder.Instance.AddPoint(worldPos, worldForward, activeSpline.transform);
			}
			else if (target == TargetSystem.SplineLoop)
			{
				activeSpline.AddMark(worldPos, worldForward);
			}
		}

		if (target == TargetSystem.SplineLoop)
		{
			activeSpline.bClosed = true;

			Debug.Log($"[DebugAutomator] {debugName} marked as Closed.");
		}
	}

	private ScanSpline GetActiveSpline()
	{
		foreach (var spline in FindObjectsByType<ScanSpline>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
		{
			if (!spline.bClosed)
			{
				return spline;
			}
		}

		return null;
	}
}
