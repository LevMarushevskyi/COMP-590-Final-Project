using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RackVolleyball : XRGrabInteractable
{
    [SerializeField] private Transform rackSpot;
    [SerializeField, Min(1f)] private float returnDelay = 6f;

    private Rigidbody ball;
    private float returnAt = float.PositiveInfinity;

    protected override void Awake()
    {
        base.Awake();
        ball = GetComponent<Rigidbody>();
        if (rackSpot != null)
            ReturnToRack();
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        returnAt = float.PositiveInfinity;
        ball.isKinematic = false;
        base.OnSelectEntering(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (!isSelected && rackSpot != null)
            returnAt = Time.time + returnDelay;
    }

    private void FixedUpdate()
    {
        if (Time.time >= returnAt)
            ReturnToRack();
    }

    private void ReturnToRack()
    {
        returnAt = float.PositiveInfinity;
        if (!ball.isKinematic)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
        }
        ball.isKinematic = true;
        ball.position = rackSpot.position;
        ball.rotation = rackSpot.rotation;
    }
}
