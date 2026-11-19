using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PassTarget : MonoBehaviour
{
    [SerializeField] string puzzleId = "serve-receive";
    [Min(1)] [SerializeField] int passesRequired = 3;
    [Tooltip("Ball machine to switch off once the puzzle is solved.")]
    [SerializeField] BallLauncher launcher;
    [Tooltip("Inner radius of the ring (m). The ball's center has to drop through inside this.")]
    [SerializeField] float openingRadius = 0.53f;

    [Header("Feedback (optional)")]
    [Tooltip("Pass count above the hoop. Hidden except for a moment after each pass that goes in.")]
    [SerializeField] TMP_Text counterText;
    [SerializeField] float counterShowSeconds = 1f;
    [SerializeField] ParticleSystem successBurst;

    public int Passes { get; private set; }
    public bool IsSolved { get; private set; }

    float hideCounterAt = float.PositiveInfinity;

    readonly Dictionary<Volleyball, Vector3> tracked = new Dictionary<Volleyball, Vector3>();
    readonly List<Volleyball> scratch = new List<Volleyball>();

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        RefreshCounter();
        if (counterText != null) counterText.enabled = false;
    }

    void Update()
    {
        if (Time.time < hideCounterAt) return;
        hideCounterAt = float.PositiveInfinity;
        if (counterText != null) counterText.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        var ball = other.GetComponentInParent<Volleyball>();
        if (ball != null) tracked[ball] = Local(ball);
    }

    void OnTriggerExit(Collider other)
    {
        var ball = other.GetComponentInParent<Volleyball>();
        if (ball != null) tracked.Remove(ball);
    }

    void FixedUpdate()
    {
        if (tracked.Count == 0) return;
        scratch.Clear();
        scratch.AddRange(tracked.Keys);
        foreach (var ball in scratch)
        {
            if (ball == null || !ball.isActiveAndEnabled) { tracked.Remove(ball); continue; } // recycled by the machine

            Vector3 prev = tracked[ball];
            Vector3 now = Local(ball);
            tracked[ball] = now;
            if (prev.y <= 0f || now.y > 0f) continue;

            Vector3 cross = Vector3.Lerp(prev, now, prev.y / (prev.y - now.y));
            if (new Vector2(cross.x, cross.z).magnitude <= openingRadius) DroppedThrough(ball);
        }
    }

    Vector3 Local(Volleyball ball) =>
        transform.InverseTransformPoint(ball.Body != null ? ball.Body.position : ball.transform.position);

    void DroppedThrough(Volleyball ball)
    {
        if (IsSolved || !GameManager.Running) return;

        if (ball.LastTouch == BallTouch.Passed)
        {
            ball.ClearTouch(); // each pass counts once
            Passes++;
            RefreshCounter();
            FlashCounter();
            if (successBurst != null) successBurst.Play();

            if (Passes >= passesRequired) Solve();
            else
            {
                GameManager.Say("Nice pass!");
            }
        }
        else if (ball.LastTouch == BallTouch.Thrown || ball.LastTouch == BallTouch.Held)
        {
            ball.ClearTouch();
            GameManager.ReportMistake("Lift violation! Pass it with the trigger, don't throw it.", penalize: false);
        }
    }

    void Solve()
    {
        IsSolved = true;
        if (launcher != null) launcher.Shutdown();
        Debug.Log("[PassTarget] Puzzle 3 (serve receive) solved.", this);
        GameManager.ReportSolved(puzzleId);
    }

    void RefreshCounter()
    {
        if (counterText != null) counterText.text = $"{Passes}/{passesRequired}";
    }

    void FlashCounter()
    {
        if (counterText == null) return;
        counterText.enabled = true;
        hideCounterAt = Time.time + counterShowSeconds;
    }
}
