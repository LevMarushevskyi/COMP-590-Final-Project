using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A grabbable jersey with a number. Lock() removes every interaction layer except
/// "Jersey", so hands (Default) can no longer grab it but the socket keeps holding it.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Jersey : MonoBehaviour
{
    [SerializeField] int number = 7;

    [Tooltip("TextMeshPro labels that show the number (front, back, ...).")]
    [SerializeField] TMP_Text[] numberLabels;

    [Tooltip("Interaction layer used by the lineup sockets.")]
    [SerializeField] string jerseyLayerName = "Jersey";

    public int Number => number;
    public bool IsLocked { get; private set; }

    XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    void Start()
    {
        RefreshLabels();
    }

#if UNITY_EDITOR
    // Update the labels in the editor when Number changes (delayed to avoid OnValidate warnings).
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) RefreshLabels(); };
    }
#endif

    void RefreshLabels()
    {
        if (numberLabels == null) return;
        foreach (var label in numberLabels)
            if (label != null) label.text = number.ToString();
    }

    /// <summary>Stops hands from grabbing this jersey. The socket holding it keeps it.</summary>
    public void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;
        grab.interactionLayers = InteractionLayerMask.GetMask(jerseyLayerName);
    }
}
