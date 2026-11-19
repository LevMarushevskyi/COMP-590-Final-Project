using UnityEngine;

public class ExitDoors : MonoBehaviour
{
    [Tooltip("Hinge of the west door. Left empty, uses the child named \"Door Left Hinge\".")]
    [SerializeField] Transform leftHinge;
    [Tooltip("Hinge of the east door. Left empty, uses the child named \"Door Right Hinge\".")]
    [SerializeField] Transform rightHinge;
    [Range(0f, 90f)] [SerializeField] float openAngle = 90f;
    [Min(0.1f)] [SerializeField] float openSeconds = 1.5f;
    [Tooltip("Open as soon as the player wins.")]
    [SerializeField] bool openOnWin = true;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip openClip;

    public bool IsOpen { get; private set; }

    Quaternion leftClosed, rightClosed;
    float progress = -1f; // -1 = not opening yet
    GameManager subscribedTo;

    void Awake()
    {
        if (leftHinge == null) leftHinge = transform.Find("Door Left Hinge");
        if (rightHinge == null) rightHinge = transform.Find("Door Right Hinge");
        if (leftHinge != null) leftClosed = leftHinge.localRotation;
        if (rightHinge != null) rightClosed = rightHinge.localRotation;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (!openOnWin || GameManager.Instance == null) return;
        subscribedTo = GameManager.Instance;
        subscribedTo.onWin.AddListener(Open);
    }

    void OnDestroy()
    {
        if (subscribedTo != null) subscribedTo.onWin.RemoveListener(Open);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        progress = 0f;
        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
    }

    void Update()
    {
        if (progress < 0f || progress >= 1f) return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / openSeconds);
        float angle = openAngle * Mathf.SmoothStep(0f, 1f, progress);

        if (leftHinge != null) leftHinge.localRotation = leftClosed * Quaternion.Euler(0f, angle, 0f);
        if (rightHinge != null) rightHinge.localRotation = rightClosed * Quaternion.Euler(0f, -angle, 0f);
    }
}
