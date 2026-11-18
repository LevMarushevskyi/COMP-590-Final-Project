using UnityEngine;

/// <summary>
/// Keeps a world-space text or panel floating in front of the player's eyes and
/// turned to face them. Eases toward its spot so it doesn't feel glued to the head.
/// </summary>
public class FollowHead : MonoBehaviour
{
    [SerializeField] float distance = 1.5f;
    [Tooltip("Up/down from eye height. Negative = a little below the eyes.")]
    [SerializeField] float heightOffset = -0.2f;
    [Tooltip("Higher = follows faster.")]
    [SerializeField] float followSpeed = 4f;

    Transform head;

    void OnEnable()
    {
        head = Camera.main != null ? Camera.main.transform : null;
        if (head != null) Snap();
    }

    void LateUpdate()
    {
        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
            Snap();
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, TargetPosition(), t);
        Face();
    }

    void Snap()
    {
        transform.position = TargetPosition();
        Face();
    }

    Vector3 TargetPosition()
    {
        Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(head.up, Vector3.up);
        return head.position + flat.normalized * distance + Vector3.up * heightOffset;
    }

    // TextMeshPro reads correctly when its forward points away from the viewer.
    void Face() => transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
}
