using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The serve-receive ball machine. While launching, fires a ball every Interval seconds
/// on a projectile arc that arrives at the player's forearms after Flight Time seconds.
/// Uses a small pool of balls so the floor never fills up. Never takes back a held ball.
/// Hook StartLaunching / StopLaunching to the passing spot's PlayerZone events.
/// </summary>
public class BallLauncher : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Volleyball prefab (needs Rigidbody + Volleyball component).")]
    [SerializeField] GameObject ballPrefab;
    [Tooltip("Where balls leave the machine (the barrel tip).")]
    [SerializeField] Transform muzzle;
    [Min(1)] [SerializeField] int poolSize = 6;

    [Header("Timing")]
    [Tooltip("Seconds between balls.")]
    [SerializeField] float interval = 3.5f;
    [Tooltip("Delay before the first ball after the player steps on the spot.")]
    [SerializeField] float firstBallDelay = 1.5f;
    [Tooltip("Seconds each ball spends in the air. Bigger = higher, slower, easier.")]
    [SerializeField] float flightTime = 1.2f;

    [Header("Aim (relative to the player's head)")]
    [Tooltip("How far below the eyes the ball arrives (forearm height).")]
    [SerializeField] float aimBelowHead = 0.75f;
    [Tooltip("How far in front of the player the ball arrives.")]
    [SerializeField] float aimInFront = 0.35f;
    [Tooltip("Random spread of the arrival point, in meters.")]
    [SerializeField] float aimJitter = 0.2f;

    [Header("Cleanup")]
    [Tooltip("Seconds after its last touch before a loose ball goes back into the machine.")]
    [SerializeField] float recycleAfter = 7f;

    [Header("Feedback (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip launchClip;
    [SerializeField] ParticleSystem launchPuff;

    public bool IsLaunching => launching && !shutDown;

    readonly List<Volleyball> pool = new List<Volleyball>();
    bool launching, shutDown;
    float nextShotAt;
    Transform head;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (muzzle == null) muzzle = transform;
        if (ballPrefab == null)
        {
            Debug.LogError("[BallLauncher] No ball prefab assigned.", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(ballPrefab, muzzle.position, Quaternion.identity);
            go.name = $"{ballPrefab.name} (machine {i + 1})";
            var ball = go.GetComponent<Volleyball>();
            if (ball == null) ball = go.AddComponent<Volleyball>();
            go.SetActive(false);
            pool.Add(ball);
        }
    }

    public void StartLaunching()
    {
        if (shutDown || launching) return;
        launching = true;
        nextShotAt = Time.time + firstBallDelay;
    }

    public void StopLaunching() => launching = false;

    /// <summary>Stops the machine for good (puzzle solved).</summary>
    public void Shutdown()
    {
        shutDown = true;
        launching = false;
    }

    void Update()
    {
        RecycleLooseBalls();

        if (!launching || shutDown || !GameManager.Running) return;
        if (Time.time < nextShotAt) return;

        nextShotAt = Time.time + interval;
        Fire();
    }

    void Fire()
    {
        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
        }

        var ball = NextBall();
        if (ball == null) return;

        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.ProjectOnPlane(muzzle.position - head.position, Vector3.up) * -1f;
        flatForward.Normalize();

        Vector2 jitter = Random.insideUnitCircle * aimJitter;
        Vector3 target = head.position + flatForward * aimInFront + Vector3.down * aimBelowHead
                         + new Vector3(jitter.x, 0f, jitter.y);

        Vector3 start = muzzle.position;
        float t = Mathf.Max(0.3f, flightTime);
        Vector3 velocity = (target - start) / t - 0.5f * Physics.gravity * t;

        var go = ball.gameObject;
        go.transform.SetPositionAndRotation(start, Random.rotation);
        go.SetActive(true);
        ball.Body.isKinematic = false;
        ball.Body.linearVelocity = velocity;
        ball.Body.angularVelocity = Random.insideUnitSphere * 4f;
        ball.MarkLaunched();

        if (launchPuff != null) launchPuff.Play();
        if (audioSource != null && launchClip != null) audioSource.PlayOneShot(launchClip);
    }

    Volleyball NextBall()
    {
        Volleyball oldest = null;
        foreach (var b in pool)
        {
            if (b == null) continue;
            if (!b.gameObject.activeSelf) return b;
            if (b.IsHeld) continue;
            if (oldest == null || b.LastTouchTime < oldest.LastTouchTime) oldest = b;
        }
        return oldest; // every ball is out: reuse the one untouched the longest
    }

    void RecycleLooseBalls()
    {
        foreach (var b in pool)
        {
            if (b == null || !b.gameObject.activeSelf || b.IsHeld) continue;
            if (Time.time - b.LastTouchTime > recycleAfter)
            {
                b.ClearTouch();
                b.gameObject.SetActive(false);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (muzzle == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(muzzle.position, 0.12f);
    }
}
