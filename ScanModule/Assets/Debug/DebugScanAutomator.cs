using UnityEngine;

using UnityEngine.InputSystem;

[System.Serializable]
public struct MockTransform
{
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
}

public class DebugScanAutomator : MonoBehaviour
{
    public MockTransform[] mockOcclusalPoints = new MockTransform[3];
    public MockTransform[] mockSagittalPoints = new MockTransform[2];
    public MockTransform[] mockUpperSpline = new MockTransform[4];
    public MockTransform[] mockLowerSpline = new MockTransform[4];

    private enum TargetSystem
    {
        BaseBuilder,
        SplineLoop,
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            InjectMockData(mockOcclusalPoints, "U - Occlusal", TargetSystem.BaseBuilder);
        }

        if (Keyboard.current.iKey.wasPressedThisFrame)
        {
            InjectMockData(mockSagittalPoints, "I - Sagittal", TargetSystem.BaseBuilder);
        }

        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            InjectMockData(mockUpperSpline, "O - Upper Spline", TargetSystem.SplineLoop);
        }

        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            InjectMockData(mockLowerSpline, "P - Lower Spline", TargetSystem.SplineLoop);
        }
    }

    private void InjectMockData(MockTransform[] mockPoints, string debugName, TargetSystem target)
    {
        if (mockPoints == null || mockPoints.Length == 0)
        {
            return;
        }

        if (target == TargetSystem.BaseBuilder && BaseBuilder.Instance == null)
        {
            Debug.LogError($"[DebugAutomator] Cannot inject {debugName}! BaseBuilder.Instance is missing from the scene.");

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
