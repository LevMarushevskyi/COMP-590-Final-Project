using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Judges the six LineupSlots under it once they're all filled.
/// Correct: light goes green, jerseys lock, onSolved fires.
/// Wrong: mistake sound, "ROTATION FAULT!", light goes red.
/// </summary>
public class LineupBoard : MonoBehaviour
{
    [Header("Status light (cube with an emissive Lit material)")]
    [SerializeField] Renderer statusLight;
    [SerializeField] Color idleColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] Color faultColor = Color.red;
    [SerializeField] Color solvedColor = Color.green;
    [SerializeField] float emissionIntensity = 3f;

    [Header("Status text (optional world-space TextMeshPro above the board)")]
    [SerializeField] TMP_Text statusText;
    [SerializeField] string faultMessage = "ROTATION FAULT!";
    [SerializeField] string solvedMessage = "LINEUP LOCKED IN!";

    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip mistakeClip;
    [SerializeField] AudioClip solvedClip;

    [Header("Events")]
    public UnityEvent onSolved;
    public UnityEvent onFault;

    public bool IsSolved { get; private set; }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    LineupSlot[] slots;
    Material lightMaterial;

    void Awake()
    {
        slots = GetComponentsInChildren<LineupSlot>(true);
        if (slots.Length != 6)
            Debug.LogWarning($"[LineupBoard] Expected 6 LineupSlots under {name}, found {slots.Length}.", this);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (statusLight != null)
        {
            lightMaterial = statusLight.material; // instance, so other objects aren't affected
            lightMaterial.EnableKeyword("_EMISSION");
        }

        ShowIdle();
    }

    void OnEnable()
    {
        foreach (var slot in slots) slot.Changed += OnSlotChanged;
    }

    void OnDisable()
    {
        foreach (var slot in slots) slot.Changed -= OnSlotChanged;
    }

    void OnSlotChanged(LineupSlot changed)
    {
        if (IsSolved) return;

        if (!slots.All(s => s.IsFilled))
        {
            ShowIdle();
            return;
        }

        if (slots.All(s => s.IsCorrect)) Solve();
        else Fault();
    }

    void Solve()
    {
        IsSolved = true;
        foreach (var slot in slots) slot.CurrentJersey.Lock();

        SetLight(solvedColor);
        SetText(solvedMessage);
        Play(solvedClip);
        Debug.Log("[LineupBoard] Puzzle 2 solved.", this);
        onSolved?.Invoke();
    }

    void Fault()
    {
        SetLight(faultColor);
        SetText(faultMessage);
        Play(mistakeClip);

        var wrong = slots.Where(s => !s.IsCorrect)
                         .Select(s => $"Zone {s.Zone} has #{s.CurrentJersey.Number} (needs #{s.ExpectedJersey})");
        Debug.Log("[LineupBoard] Rotation fault: " + string.Join(", ", wrong), this);
        onFault?.Invoke();
    }

    void ShowIdle()
    {
        SetLight(idleColor);
        SetText("");
    }

    void SetLight(Color color)
    {
        if (lightMaterial == null) return;
        lightMaterial.SetColor(BaseColorId, color);
        lightMaterial.SetColor(EmissionColorId, color * emissionIntensity);
    }

    void SetText(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
