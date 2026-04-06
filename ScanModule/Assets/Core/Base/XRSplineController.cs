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
            // we hit the correct layer, maintained by the helper child object of the Scan, so we need to get the parent who controls it
            ScanSpline spline = hit.collider.GetComponentInParent<ScanSpline>();
            
            if (spline != null)
            {
                spline.AddMark(hit.point, hit.normal);
            }
        }
    }
}
