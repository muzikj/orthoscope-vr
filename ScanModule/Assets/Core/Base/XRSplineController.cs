using UnityEngine;
using UnityEngine.InputSystem;

public class XRSplineController : MonoBehaviour
{
    [Tooltip("Transform for correct XR pointing when using physical controllers.")]
    public Transform controllerPointerOrigin;
    [Tooltip("Transform for correct XR pointing when using hand controllers.")]
    public Transform handPointerOrigin;

    [Tooltip("InputAction for triggering marker placement.")]
    public InputActionReference triggerAction;
    [Tooltip("InputAction for undoing the last marker.")]
    public InputActionReference undoAction;

    private GameObject _ghostMark;
    private GameObject _closingGhostMark;

    private ScanSpline _currentSpline;

    private bool IsMarkingPlanes => BaseBuilder.Instance != null && (BaseBuilder.Instance.currentState == BaseBuilder.BuilderState.MarkOcclusal || BaseBuilder.Instance.currentState == BaseBuilder.BuilderState.MarkSagittal);

    private Transform ActivePointerOrigin
    {
        get
        {
            if (controllerPointerOrigin != null && controllerPointerOrigin.gameObject.activeInHierarchy)
            {
                return controllerPointerOrigin;
            }

            if (handPointerOrigin != null && handPointerOrigin.gameObject.activeInHierarchy)
            {
                return handPointerOrigin;
            }

            return null;
        }
    }

    private void Awake()
    {
        if (Config.Instance.ghostMarkPrefab != null)
        {
            _ghostMark = Instantiate(Config.Instance.ghostMarkPrefab);
            _ghostMark.transform.localScale = Config.Instance.ghostMarkPrefab.transform.localScale * Config.Instance.scanScale;
            _ghostMark.SetActive(false);
        }

        if (Config.Instance.closingGhostMarkPrefab != null)
        {
            _closingGhostMark = Instantiate(Config.Instance.closingGhostMarkPrefab);
            _closingGhostMark.transform.localScale = Config.Instance.closingGhostMarkPrefab.transform.localScale * Config.Instance.scanScale;
            _closingGhostMark.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (triggerAction != null)
        {
            triggerAction.action.Enable();
            triggerAction.action.performed += OnTriggerPressed;
        }

        if (undoAction != null)
        {
            undoAction.action.Enable();
            undoAction.action.performed += OnUndoPressed;
        }
    }

    private void OnDisable()
    {
        if (triggerAction != null)
        {
            triggerAction.action.performed -= OnTriggerPressed;
            triggerAction.action.Disable();
        }

        if (undoAction != null)
        {
            undoAction.action.performed -= OnUndoPressed;
            undoAction.action.Disable();
        }
    }

    private void Update()
    {
        Transform currentOrigin = ActivePointerOrigin;

        if (currentOrigin == null || _ghostMark == null || _closingGhostMark == null)
        {
            return;
        }

        if (Physics.Raycast(currentOrigin.position, currentOrigin.forward, out RaycastHit hit, Config.Instance.raycastMaxDistance, Config.Instance.scanRaycastLayer))
        {
            // we have hit the correct layer, which is a mere helper child object of the Scan, so we need to get the parent who controls it and then fetch the ScanSpline component on one of its children
            _currentSpline = hit.collider.transform.parent.GetComponentInChildren<ScanSpline>();

            if (_currentSpline != null)
            {
                _ghostMark.transform.localScale = Config.Instance.ghostMarkPrefab.transform.localScale * _currentSpline.transform.lossyScale.x;
                _closingGhostMark.transform.localScale = Config.Instance.closingGhostMarkPrefab.transform.localScale * _currentSpline.transform.lossyScale.x;

                if (!_currentSpline.bClosed)
                {
                    Vector3 potentialPosition = hit.point + (hit.normal * Config.Instance.splineSurfaceOffset);

                    if (_currentSpline.WillSnapToStart(potentialPosition, out Vector3 snapPosition, out Vector3 snapNormal))
                    {
                        _ghostMark.SetActive(false);
                        _closingGhostMark.SetActive(true);

                        _closingGhostMark.transform.SetPositionAndRotation(snapPosition, Quaternion.LookRotation(snapNormal));

                        return;
                    }
                    else // standard hovering
                    {
                        _ghostMark.SetActive(true);
                        _closingGhostMark.SetActive(false);

                        _ghostMark.transform.SetPositionAndRotation(potentialPosition, Quaternion.LookRotation(hit.normal));
                    }
                }
                else
                {
                    _ghostMark.SetActive(false);
                    _closingGhostMark.SetActive(false);
                }

                return;
            }
        }

        // closed spline or no hit
        _currentSpline = null;
        _ghostMark.SetActive(false);
        _closingGhostMark.SetActive(false);
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (_currentSpline != null)
        {
            Transform activeGhost = _closingGhostMark.activeSelf ? _closingGhostMark.transform : _ghostMark.transform;

            if (IsMarkingPlanes)
            {
                BaseBuilder.Instance.AddPoint(activeGhost.position, activeGhost.forward, _currentSpline.transform);
            }
            else if (!_currentSpline.bClosed)
            {
                _currentSpline.AddMark(activeGhost.position, activeGhost.forward);
            }
        }
    }

    private void OnUndoPressed(InputAction.CallbackContext context)
    {
        if (IsMarkingPlanes)
        {
            BaseBuilder.Instance.RemoveLastPoint();
        }
        else if (_currentSpline != null)
        {
            _currentSpline.RemoveLastMark();
        }
    }
}
