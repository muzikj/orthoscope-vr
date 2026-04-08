using UnityEngine;
using UnityEngine.InputSystem;

public class XRSplineController : MonoBehaviour
{
    [Header("VR Controller Dependencies")]
    [Tooltip("Transform for correct XR pointing.")]
    public Transform pointerOrigin;
    [Tooltip("InputAction for triggering marker placement.")]
    public InputActionReference triggerAction;

    private void OnEnable()
    {
        if (triggerAction != null)
        {
            triggerAction.action.Enable();
            triggerAction.action.performed += OnTriggerPulled;
        }
    }

    private void OnDisable()
    {
        if (triggerAction != null)
        {
            triggerAction.action.performed -= OnTriggerPulled;
            triggerAction.action.Disable();
        }
    }

    private void OnTriggerPulled(InputAction.CallbackContext context)
    {
        if (pointerOrigin == null) return;

        if (Physics.Raycast(pointerOrigin.position, pointerOrigin.forward, out RaycastHit hit, Config.Instance.raycastMaxDistance, Config.Instance.scanRaycastLayer))
        {
            // we have hit the correct layer, which is a mere helper child object of the Scan, so we need to get the parent who controls it and then fetch the ScanSpline component on one of its children
            ScanSpline spline = hit.collider.transform.parent.GetComponentInChildren<ScanSpline>();
            
            if (spline != null)
            {
                spline.AddMark(hit.point, hit.normal);
            }
        }
    }
}
