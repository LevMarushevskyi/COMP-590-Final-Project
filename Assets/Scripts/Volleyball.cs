using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum BallTouch { None, Held, Thrown, Launched, Passed }

[RequireComponent(typeof(Rigidbody))]
public class Volleyball : MonoBehaviour
{
    public static readonly List<Volleyball> Active = new List<Volleyball>();

    public BallTouch LastTouch { get; private set; } = BallTouch.None;
    public float LastTouchTime { get; private set; }
    public Rigidbody Body { get; private set; }
    public bool IsHeld => grab != null && grab.isSelected;

    XRGrabInteractable grab;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        Active.Add(this);
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    void OnDisable()
    {
        Active.Remove(this);
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnGrabbed(SelectEnterEventArgs _) => SetTouch(BallTouch.Held);

    void OnReleased(SelectExitEventArgs _)
    {
        if (!IsHeld) SetTouch(BallTouch.Thrown);
    }

    public void MarkLaunched() => SetTouch(BallTouch.Launched);
    public void MarkPassed() => SetTouch(BallTouch.Passed);
    public void ClearTouch() => SetTouch(BallTouch.None);

    void SetTouch(BallTouch touch)
    {
        LastTouch = touch;
        LastTouchTime = Time.time;
    }
}
