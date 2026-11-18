using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Trigger inside the setter's hoop. Counts balls that arrive from a pass (PassButton).
/// A ball that was caught and thrown in gets "Lift violation!" and doesn't count.
/// At Passes Required it stops the ball machine, reports "serve-receive" to the GameManager,
/// and fires onSolved.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PassTarget : MonoBehaviour
{
    [SerializeField] string puzzleId = "serve-receive";
    [Min(1)] [SerializeField] int passesRequired = 3;
    [Tooltip("Ball machine to switch off once the puzzle is solved.")]
    [SerializeField] BallLauncher launcher;

    [Header("Feedback (optional)")]
    [Tooltip("Pass count above the hoop. Hidden except for a moment after each pass that goes in.")]
    [SerializeField] TMP_Text counterText;
    [SerializeField] float counterShowSeconds = 1f;
    [SerializeField] ParticleSystem successBurst;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip goodPassClip;
    [SerializeField] AudioClip solvedClip;

    [Header("Events")]
    public UnityEvent onGoodPass = new UnityEvent();
    public UnityEvent onSolved = new UnityEvent();

    public int Passes { get; private set; }
    public bool IsSolved { get; private set; }

    float hideCounterAt = float.PositiveInfinity;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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
        if (IsSolved || !GameManager.Running) return;
        var ball = other.GetComponentInParent<Volleyball>();
        if (ball == null) return;

        if (ball.LastTouch == BallTouch.Passed)
        {
            ball.ClearTouch(); // each pass counts once
            Passes++;
            RefreshCounter();
            FlashCounter();
            if (successBurst != null) successBurst.Play();
            onGoodPass?.Invoke();

            if (Passes >= passesRequired) Solve();
            else
            {
                Play(goodPassClip);
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
        Play(solvedClip != null ? solvedClip : goodPassClip);
        if (launcher != null) launcher.Shutdown();
        Debug.Log("[PassTarget] Puzzle 3 (serve receive) solved.", this);
        GameManager.ReportSolved(puzzleId);
        onSolved?.Invoke();
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

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
