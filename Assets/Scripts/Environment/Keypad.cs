using TMPro;
using UnityEngine;

public class Keypad : MonoBehaviour
{
    [SerializeField] TMP_Text display;
    [Min(1)] [SerializeField] int codeLength = 4;
    [Tooltip("Take time off the clock for a wrong code.")]
    [SerializeField] bool penalizeWrongCode = false;
    [SerializeField] float wrongShowSeconds = 1f;

    [Header("Display")]
    [SerializeField] string lockedText = "LOCKED";
    [SerializeField] string wrongText = "WRONG";
    [SerializeField] string openText = "OPEN";
    [SerializeField] Color lockedColor = new Color(0.6f, 0.1f, 0.1f);
    [SerializeField] Color readyColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] Color wrongColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] Color openColor = new Color(0.2f, 1f, 0.3f);

    [Header("Audio (optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip keyClip;

    public bool IsActive => !IsUnlocked && GameManager.Instance != null && GameManager.Instance.AllSolved && GameManager.Running;
    public bool IsUnlocked { get; private set; }

    string entry = "";
    float wrongUntil;
    string shown;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update() => RefreshDisplay();

    public void Press(string key)
    {
        if (!IsActive) return;
        Play(keyClip);

        if (key == "C") entry = "";
        else if (key == "OK") Submit();
        else if (key.Length == 1 && char.IsDigit(key[0]) && entry.Length < codeLength)
        {
            entry += key;
            if (entry.Length == codeLength) Submit();
        }
        wrongUntil = 0f;
    }

    void Submit()
    {
        if (entry.Length == 0) return;
        if (GameManager.Instance.CheckCode(entry))
        {
            IsUnlocked = true;
            GameManager.Instance.PlayCorrectSound();
            Debug.Log("[Keypad] Correct code. Opening the exit.", this);
            GameManager.Instance.Win(); // ExitDoors opens on win
        }
        else
        {
            GameManager.ReportMistake($"Wrong code ({entry}).", penalizeWrongCode);
            entry = "";
            wrongUntil = Time.time + wrongShowSeconds;
        }
    }

    void RefreshDisplay()
    {
        if (display == null) return;
        string text; Color color;
        if (IsUnlocked) { text = openText; color = openColor; }
        else if (!IsActive) { text = lockedText; color = lockedColor; }
        else if (Time.time < wrongUntil) { text = wrongText; color = wrongColor; }
        else { text = entry.PadRight(codeLength, '_'); color = readyColor; }

        if (text == shown && display.color == color) return;
        shown = text;
        display.text = text;
        display.color = color;
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
