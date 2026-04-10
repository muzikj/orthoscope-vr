using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

public class BaseBuilder : MonoBehaviour
{
    public static BaseBuilder Instance { get; private set; }

    public enum BuilderState { MarkOcclusal, MarkSagittal, MarkGums, Meshing }
    public BuilderState currentState = BuilderState.MarkOcclusal;

    private List<Vector3> _occlusalPoints = new();
    private List<Vector3> _sagittalPoints = new();

    private List<GameObject> _occlusalMarks = new();
    private List<GameObject> _sagittalMarks = new();

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

    private void Update()
    {
        // TODO: remove debug input and replace with proper UI buttons
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            AdvanceState();
        }
    }

    private void AdvanceState()
    {
        if (currentState == BuilderState.MarkOcclusal)
        {
            currentState = BuilderState.MarkSagittal;
            Debug.Log("State Advanced: Now marking Sagittal Plane (2 points on upper palate).");
        }
        else if (currentState == BuilderState.MarkSagittal)
        {
            currentState = BuilderState.MarkGums;
            Debug.Log("State Advanced: Now marking Gum Splines. TubeRenderer active!");
        }
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
            Debug.Log($"Occlusal point added. ({_occlusalPoints.Count}/3)");
        }
        else if (currentState == BuilderState.MarkSagittal)
        {
            _sagittalPoints.Add(localPosition);
            if (mark != null) _sagittalMarks.Add(mark);

            UpdateMarkColors(_sagittalMarks, 2);
            Debug.Log($"Sagittal point added. ({_sagittalPoints.Count}/2)");
        }
    }

    public void RemoveLastPoint()
    {
        if (currentState == BuilderState.MarkOcclusal && _occlusalPoints.Count > 0)
        {
            _occlusalPoints.RemoveAt(_occlusalPoints.Count - 1);
            RemoveLastMark(_occlusalMarks);

            UpdateMarkColors(_occlusalMarks, 3);
            Debug.Log($"Last occlusal point removed. ({_occlusalPoints.Count}/3)");
        }
        else if (currentState == BuilderState.MarkSagittal && _sagittalPoints.Count > 0)
        {
            _sagittalPoints.RemoveAt(_sagittalPoints.Count - 1);
            RemoveLastMark(_sagittalMarks);

            UpdateMarkColors(_sagittalMarks, 2);
            Debug.Log($"Last sagittal point removed. ({_sagittalPoints.Count}/2)");
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
}
