using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CourtZone : MonoBehaviour
{
    [SerializeField] int zone = 1;
    [Tooltip("Number of the jersey that belongs in this zone for rotation 1.")]
    [SerializeField] int expectedJersey = 7;

    public int Zone => zone;
    public int ExpectedJersey => expectedJersey;

    BoxCollider box;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.enabled = false;
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
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
        Gizmos.DrawCube(b.center, new Vector3(b.size.x, 0.02f, b.size.z));
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, 0.02f, b.size.z));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.TransformPoint(b.center) + Vector3.up * 0.3f, $"Zone {zone}  (#{expectedJersey})");
#endif
    }
}
