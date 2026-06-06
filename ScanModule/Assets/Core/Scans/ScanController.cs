using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(MeshRenderer), typeof(XRGrabInteractable))]
public class ScanController : MonoBehaviour
{
    public ModelTheme modelTheme;

    public bool Selected { get; private set; } = false;

    public enum JawType
    {
        Unassigned,
        Upper,
        Lower
    }

    public JawType jawType = JawType.Unassigned;

    private MeshRenderer _renderer;
    private XRGrabInteractable _grabInteractable;

    public Vector3 OriginalPosition { get; private set; }
    public Vector3 OriginalScale { get; private set; }
    public Quaternion OriginalRotation { get; private set; }

    private float _triggerStartTime = 0f;
    private bool _triggerHeld = false;
    private bool _triggerHeldForLongEnough = false;

    private bool _isLeader = false;
    private bool _isFollower = false;

    private Vector3 _offsetPosition;
    private Quaternion _offsetRotation;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _grabInteractable = GetComponent<XRGrabInteractable>();

        OriginalPosition = transform.position;
        OriginalScale = transform.localScale;
        OriginalRotation = transform.localRotation;
    }

    private void Start()
    {
        MakeDefault();

        // make culling double-sided
        if (_renderer.material.HasProperty("_Cull"))
        {
            _renderer.material.SetFloat("_Cull", 0f);
        }
    }

    private void Update()
    {
        if (_triggerHeld && !_triggerHeldForLongEnough)
        {
            float threshold = Config.Instance.holdThreshold;
            float holdDuration = Time.time - _triggerStartTime;

            if (holdDuration >= threshold)
            {
                _triggerHeldForLongEnough = true;
                ToggleOptions();
            }
        }

        if (_isLeader)
        {
            ScanEvents.RequestGroupMove(transform);
        }
    }

    private void OnEnable()
    {
        _grabInteractable.activated.AddListener(OnTriggerPulled);
        _grabInteractable.deactivated.AddListener(OnTriggeReleased);

        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);

        ScanEvents.OnGroupGrabStarted += HandleGroupGrabStarted;
        ScanEvents.OnGroupMoved += HandleGroupMoved;
        ScanEvents.OnGroupGrabEnded += HandleGroupGrabEnded;

        ScanEvents.OnDeleteRequested += HandleDeleteRequested;
        ScanEvents.OnResetRequested += HandleResetRequested;
        ScanEvents.OnScaleRequested += HandleScaleRequested;
        ScanEvents.OnSnapViewRequested += HandleSnapViewRequested;
    }

    private void OnDisable()
    {
        _grabInteractable.activated.RemoveListener(OnTriggerPulled);
        _grabInteractable.deactivated.RemoveListener(OnTriggeReleased);

        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);

        ScanEvents.OnGroupGrabStarted -= HandleGroupGrabStarted;
        ScanEvents.OnGroupMoved -= HandleGroupMoved;
        ScanEvents.OnGroupGrabEnded -= HandleGroupGrabEnded;

        ScanEvents.OnDeleteRequested -= HandleDeleteRequested;
        ScanEvents.OnResetRequested -= HandleResetRequested;
        ScanEvents.OnScaleRequested -= HandleScaleRequested;
        ScanEvents.OnSnapViewRequested -= HandleSnapViewRequested;

        ToggleSelection(false);
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        _triggerStartTime = Time.time;

        _triggerHeld = true;
        _triggerHeldForLongEnough = false;
    }

    private void OnTriggeReleased(DeactivateEventArgs args)
    {
        _triggerHeld = false;

        if (!_triggerHeldForLongEnough)
        {
            ToggleSelection();
        }
    }

    private void ToggleOptions()
    {
        // TODO: implement this as a UI panel
        Debug.Log($"Context Menu toggled ON for: {gameObject.name}!");
    }

    private void ToggleSelection(bool? forceSelected = null)
    {
        Selected = forceSelected ?? !Selected;

        if (Selected)
        {
            MakeHighlighted();

            ScanEvents.NotifyScanSelected(gameObject);
        }
        else
        {
            MakeDefault();

            ScanEvents.NotifyScanDeselected(gameObject);
        }
    }

    private void MakeHighlighted()
    {
        if (modelTheme != null && modelTheme.highlightedMaterial != null)
        {
            float currentCull = _renderer.material.HasProperty("_Cull") ? _renderer.material.GetFloat("_Cull") : 2f;

            _renderer.sharedMaterial = modelTheme.highlightedMaterial;
            _renderer.material.SetFloat("_Cull", currentCull);
        }
    }

    private void MakeDefault()
    {
        if (modelTheme != null && modelTheme.defaultMaterial != null)
        {
            float currentCull = _renderer.material.HasProperty("_Cull") ? _renderer.material.GetFloat("_Cull") : 2f;

            _renderer.sharedMaterial = modelTheme.defaultMaterial;
            _renderer.material.SetFloat("_Cull", currentCull);
        }
    }

    private void HandleDeleteRequested()
    {
        if (!Selected) return;

        Destroy(gameObject);
    }

    private void HandleResetRequested()
    {
        if (!Selected) return;

        transform.position = OriginalPosition;
        transform.localScale = OriginalScale;
        transform.localRotation = OriginalRotation;
    }

    private void HandleScaleRequested(float scaleFactor) // when changing the scale of e.g. upper and lower teeth scans, the gap inbetween will NOT be adequate when scaling up OR down, as we are not scaling relative to the origin of both the scans, but rather their two distinct INDIVIDUAL origins
    {
        if (!Selected) return;

        transform.localScale = OriginalScale * scaleFactor;
    }

    private void HandleSnapViewRequested(OrthoView view)
    {
        if (!Selected) return;

        Quaternion targetRotation = OriginalRotation;

        switch (view)
        {
            case OrthoView.Front:
                targetRotation = OriginalRotation;

                break;

            case OrthoView.Back:
                targetRotation = Quaternion.Euler(0f, 180f, 0f) * OriginalRotation;

                break;

            case OrthoView.Left:
                targetRotation = Quaternion.Euler(0f, -90f, 0f) * OriginalRotation;

                break;

            case OrthoView.Right:
                targetRotation = Quaternion.Euler(0f, 90f, 0f) * OriginalRotation;

                break;

            case OrthoView.Top:
                targetRotation = Quaternion.Euler(90f, 0f, 0f) * OriginalRotation;

                break;

            case OrthoView.Bottom:
                targetRotation = Quaternion.Euler(-90f, 0f, 0f) * OriginalRotation;

                break;
        }

        transform.localRotation = targetRotation;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (!Selected) return;

        _isLeader = true;

        ScanEvents.RequestGroupGrabStart(transform);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (_isLeader)
        {
            _isLeader = false;

            ScanEvents.RequestGroupGrabEnd();
        }
    }

    private void HandleGroupGrabStarted(Transform leaderTransform)
    {
        if (!Selected || _isLeader) return;

        _isFollower = true;

        _offsetPosition = leaderTransform.InverseTransformPoint(transform.position);
        _offsetRotation = Quaternion.Inverse(leaderTransform.rotation) * transform.rotation;
    }

    private void HandleGroupMoved(Transform leaderTransform)
    {
        if (!_isFollower) return;

        transform.SetPositionAndRotation(leaderTransform.TransformPoint(_offsetPosition), leaderTransform.rotation * _offsetRotation);
    }

    private void HandleGroupGrabEnded()
    {
        _isFollower = false;
    }

    public void UpdateOriginalState()
    {
        OriginalPosition = transform.position;
        OriginalRotation = transform.localRotation;
        OriginalScale = transform.localScale;
    }
}
