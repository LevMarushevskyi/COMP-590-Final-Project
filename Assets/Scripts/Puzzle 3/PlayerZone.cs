using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class PlayerZone : MonoBehaviour
{
    public UnityEvent onPlayerEnter = new UnityEvent();
    public UnityEvent onPlayerExit = new UnityEvent();

    public bool PlayerInside { get; private set; }

    BoxCollider box;
    Transform head;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.enabled = false;
    }

    void Update()
    {
        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
        }

        bool inside = Contains(head.position);
        if (inside == PlayerInside) return;
        PlayerInside = inside;
        if (inside) onPlayerEnter?.Invoke();
        else onPlayerExit?.Invoke();
    }

    void OnDisable()
    {
        if (!PlayerInside) return;
        PlayerInside = false;
        onPlayerExit?.Invoke();
    }

    public bool Contains(Vector3 worldPoint)
    {
        if (box == null) box = GetComponent<BoxCollider>();
        Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = PlayerInside ? new Color(0.2f, 1f, 0.3f, 0.35f) : new Color(1f, 0.8f, 0.2f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
    }
}
