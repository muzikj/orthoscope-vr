using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(MeshRenderer), typeof(XRGrabInteractable))]
public class ScanController : MonoBehaviour
{
    public ModelTheme modelTheme;

    public bool Selected { get; private set; } = false;

    private MeshRenderer _renderer;
    private XRGrabInteractable _grabInteractable;

    private Vector3 _originalScale;
    private Quaternion _originalRotation;

    private float _triggerStartTime = 0f;
    private bool _triggerHeld = false;
    private bool _triggerHeldForLongEnough = false;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _grabInteractable = GetComponent<XRGrabInteractable>();

        _originalScale = transform.localScale;
        _originalRotation = transform.localRotation;
    }

    private void Start()
    {
        MakeDefault();
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
    }

    private void OnEnable()
    {
        _grabInteractable.activated.AddListener(OnTriggerPulled);
        _grabInteractable.deactivated.AddListener(OnTriggeReleased);

        ScanEvents.OnDeleteRequested += HandleDeleteRequested;
        ScanEvents.OnResetRequested += HandleResetRequested;
        ScanEvents.OnScaleRequested += HandleScaleRequested;
    }

    private void OnDisable()
    {
        _grabInteractable.activated.RemoveListener(OnTriggerPulled);
        _grabInteractable.deactivated.RemoveListener(OnTriggeReleased);

        ScanEvents.OnDeleteRequested -= HandleDeleteRequested;
        ScanEvents.OnResetRequested -= HandleResetRequested;
        ScanEvents.OnScaleRequested -= HandleScaleRequested;

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
            _renderer.sharedMaterial = modelTheme.highlightedMaterial;
        }
    }

    private void MakeDefault()
    {
        if (modelTheme != null && modelTheme.defaultMaterial != null)
        {
            _renderer.sharedMaterial = modelTheme.defaultMaterial;
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

        transform.localScale = _originalScale;
        transform.localRotation = _originalRotation;
    }

    private void HandleScaleRequested(float scaleFactor)
    {
        if (!Selected) return;

        transform.localScale = _originalScale * scaleFactor;
    }
}
