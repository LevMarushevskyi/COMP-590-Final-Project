using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class KeypadKey : MonoBehaviour
{
    [SerializeField] string key = "1";
    [Tooltip("Left empty, uses the Keypad on a parent.")]
    [SerializeField] Keypad keypad;
    [Tooltip("Visual part that sinks in when pressed.")]
    [SerializeField] Transform cap;
    [SerializeField] float pressDepth = 0.006f;
    [SerializeField] float pressSeconds = 0.12f;

    XRSimpleInteractable interactable;
    Vector3 capRest;
    float releaseAt;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (keypad == null) keypad = GetComponentInParent<Keypad>();
        if (cap != null) capRest = cap.localPosition;
    }

    void OnEnable() => interactable.selectEntered.AddListener(OnPressed);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnPressed);

    void OnPressed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        if (keypad != null) keypad.Press(key);
        if (cap != null)
        {
            cap.localPosition = capRest + Vector3.forward * pressDepth; // +Z = into the wall
            releaseAt = Time.time + pressSeconds;
        }
    }

    void Update()
    {
        if (cap != null && releaseAt > 0f && Time.time >= releaseAt)
        {
            releaseAt = 0f;
            cap.localPosition = capRest;
        }
    }
}
