using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// One zone on the lineup board. Tracks which jersey is in its socket and whether it's the right one.
/// </summary>
[RequireComponent(typeof(XRSocketInteractor))]
public class LineupSlot : MonoBehaviour
{
    [Range(1, 6)]
    [SerializeField] int zone = 1;

    [Tooltip("Jersey number that belongs in this zone.")]
    [SerializeField] int expectedJersey = 7;

    public int Zone => zone;
    public int ExpectedJersey => expectedJersey;
    public Jersey CurrentJersey { get; private set; }
    public bool IsFilled => CurrentJersey != null;
    public bool IsCorrect => IsFilled && CurrentJersey.Number == expectedJersey;

    /// <summary>Fires whenever a jersey is placed in or removed from this slot.</summary>
    public event Action<LineupSlot> Changed;

    XRSocketInteractor socket;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
    }

    void OnEnable()
    {
        socket.selectEntered.AddListener(OnSelectEntered);
        socket.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnSelectEntered);
        socket.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        CurrentJersey = args.interactableObject.transform.GetComponentInParent<Jersey>();
        Changed?.Invoke(this);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        CurrentJersey = null;
        Changed?.Invoke(this);
    }
}
