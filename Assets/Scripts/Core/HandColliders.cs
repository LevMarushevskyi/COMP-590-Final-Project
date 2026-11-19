using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class HandColliders : MonoBehaviour
{
    [Tooltip("Left empty, uses the child named \"Left Controller\".")]
    [SerializeField] Transform leftController;
    [Tooltip("Left empty, uses the child named \"Right Controller\".")]
    [SerializeField] Transform rightController;
    [Tooltip("Size of each hand, in meters.")]
    [SerializeField] float handRadius = 0.06f;
    [Tooltip("Seconds the hand stays non-solid after letting go of something.")]
    [SerializeField] float afterReleaseDelay = 0.4f;
    [Tooltip("Layers the hands can hit. Nothing selected = the \"Ball\" layer.")]
    [SerializeField] LayerMask hitLayers;

    class Hand
    {
        public Transform controller;
        public Rigidbody body;
        public Collider collider;
        public XRBaseInteractor[] interactors;
        public float solidAgainAt;
    }

    Hand[] hands;

    void Start()
    {
        var origin = GetComponentInParent<XROrigin>();
        Transform root = origin != null ? origin.transform : transform;
        if (leftController == null) leftController = FindChild(root, "Left Controller");
        if (rightController == null) rightController = FindChild(root, "Right Controller");
        if (hitLayers.value == 0) hitLayers = LayerMask.GetMask("Ball");

        foreach (var ball in FindObjectsByType<Volleyball>(FindObjectsInactive.Include))
            if ((hitLayers.value & (1 << ball.gameObject.layer)) == 0)
                Debug.LogWarning($"[HandColliders] \"{ball.name}\" isn't on a Hit Layer, so hands can't hit it.", ball);

        hands = new[] { leftController, rightController }
            .Where(c => c != null)
            .Select(Make)
            .ToArray();
        if (hands.Length == 0) Debug.LogWarning("[HandColliders] No controllers found; nothing to do.", this);
    }

    Hand Make(Transform controller)
    {
        var go = new GameObject($"{controller.name} Hand Collider");
        go.transform.SetPositionAndRotation(controller.position, controller.rotation);

        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var sphere = go.AddComponent<SphereCollider>();
        sphere.radius = handRadius;
        sphere.excludeLayers = ~hitLayers;

        return new Hand
        {
            controller = controller,
            body = body,
            collider = sphere,
            interactors = controller.GetComponentsInChildren<XRBaseInteractor>(true),
        };
    }

    void FixedUpdate()
    {
        if (hands == null) return;
        foreach (var h in hands)
        {
            if (h.controller == null) continue;

            bool holding = h.interactors.Any(i => i != null && i.isActiveAndEnabled && i.hasSelection);
            if (holding) h.solidAgainAt = Time.time + afterReleaseDelay;
            bool solid = Time.time >= h.solidAgainAt && h.controller.gameObject.activeInHierarchy;

            if (h.collider.enabled != solid)
            {
                h.collider.enabled = solid;
                if (solid) h.body.position = h.controller.position;
            }
            h.body.MovePosition(h.controller.position);
            h.body.MoveRotation(h.controller.rotation);
        }
    }

    void OnDestroy()
    {
        if (hands == null) return;
        foreach (var h in hands)
            if (h.body != null) Destroy(h.body.gameObject);
    }

    static Transform FindChild(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
