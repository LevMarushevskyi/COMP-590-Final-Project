using Unity.XR.CoreUtils;
using UnityEngine;

public class HeadCollisionBlocker : MonoBehaviour
{
    [Tooltip("Size of the head, in meters. Bigger keeps the eyes further from walls.")]
    [SerializeField] float headRadius = 0.15f;
    [Tooltip("Layers that count as solid.")]
    [SerializeField] LayerMask solidLayers = ~0;
    [Tooltip("Most the rig can be moved in one frame, so a bad overlap can't fling the player.")]
    [SerializeField] float maxPushPerFrame = 0.5f;

    Transform rig, head;
    CharacterController body;
    SphereCollider probe;
    readonly Collider[] hits = new Collider[16];

    void Start()
    {
        var origin = GetComponentInParent<XROrigin>();
        if (origin == null) origin = FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.Camera == null)
        {
            Debug.LogWarning("[HeadCollisionBlocker] No XR Origin with a camera found; disabled.", this);
            enabled = false;
            return;
        }
        rig = origin.transform;
        head = origin.Camera.transform;
        body = rig.GetComponent<CharacterController>();

        var go = new GameObject("Head Probe") { layer = 2 };
        go.transform.SetParent(head, false);
        probe = go.AddComponent<SphereCollider>();
        probe.isTrigger = true;
        probe.radius = headRadius;
    }

    void LateUpdate()
    {
        Vector3 p = head.position;
        int n = Physics.OverlapSphereNonAlloc(p, headRadius, hits, solidLayers, QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;

        for (int i = 0; i < n; i++)
        {
            var c = hits[i];
            if (c == probe || c.attachedRigidbody != null || c.transform.IsChildOf(rig)) continue;
            if (Physics.ComputePenetration(probe, p, head.rotation, c, c.transform.position, c.transform.rotation,
                                           out var dir, out float dist))
                push += dir * dist;
        }

        push.y = 0f;
        if (push.sqrMagnitude < 1e-8f) return;
        push = Vector3.ClampMagnitude(push, maxPushPerFrame);

        if (body != null && body.enabled) body.Move(push);
        else rig.position += push;
    }

    void OnDrawGizmosSelected()
    {
        if (head == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(head.position, headRadius);
    }
}
