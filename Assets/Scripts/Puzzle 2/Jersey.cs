using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

    public void Lock()
    {
        if (IsLocked) return;
        IsLocked = true;
        grab.interactionLayers = InteractionLayerMask.GetMask(jerseyLayerName);
    }
}
