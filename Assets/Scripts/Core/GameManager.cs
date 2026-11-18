using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the escape-room clock, counts solved puzzles by ID, and decides win or lose.
/// One per scene. Puzzles report with GameManager.ReportSolved("id") or, from an
/// Inspector UnityEvent, GameManager > MarkSolved with the ID typed in.
/// Puzzles still work with no GameManager in the scene (Running is true then).
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState { NotStarted, Running, Won, Lost }

    public static GameManager Instance { get; private set; }

    /// <summary>True while the clock runs, or when there's no GameManager (testing a puzzle alone).</summary>
    public static bool Running => Instance == null || Instance.State == GameState.Running;

    [Header("Rules")]
    [Tooltip("Length of the game in seconds (300 = 5:00). Adjustable from 60 to 900.")]
    [Range(60, 900)] [SerializeField] int countdownSeconds = 300;
    [Tooltip("How many puzzles must be solved before the player can escape.")]
    [Min(1)] [SerializeField] int puzzlesRequired = 3;
    [Tooltip("Seconds taken off the clock by a penalized mistake.")]
    [SerializeField] float mistakePenaltySeconds = 20f;
    [Tooltip("Start the clock as soon as the scene loads instead of waiting for a trigger pull.")]
    [SerializeField] bool startOnLoad = false;
    [Tooltip("Win as soon as every puzzle is solved. Turn this off once the exit door calls Win().")]
    [SerializeField] bool winWhenAllSolved = true;
    [Tooltip("Play the tick sound every second below this many seconds left.")]
    [SerializeField] int tickBelowSeconds = 30;

    [Header("Text (all optional)")]
    [SerializeField] TMP_Text[] timerTexts;
    [SerializeField] TMP_Text messageText;
    [SerializeField] float messageSeconds = 3f;
    [SerializeField] TMP_Text leaderboardText;

    [Header("Scoreboard exit code")]
    [Tooltip("Scoreboard text that shows HOME/AWAY. It reads 00-00 until every puzzle is solved, then shows the keypad code as the score.")]
    [SerializeField] TMP_Text scoreText;
    [Tooltip("The exit keypad code. Shown as the score: first half = HOME, second half = AWAY (2523 -> 25 : 23).")]
    [SerializeField] string keypadCode = "2523";
    [Tooltip("{0} = HOME score, {1} = AWAY score.")]
    [TextArea(2, 3)] [SerializeField] string scoreFormat = "<size=70%>HOME        AWAY</size>\n{0}       {1}";
    [SerializeField] string hiddenScore = "00";

    [Header("End screen (optional)")]
    [SerializeField] GameObject endPanel;
    [SerializeField] TMP_Text endTitleText;
    [SerializeField] TMP_Text endDetailText;

    [Header("Audio (all optional)")]
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioClip startClip;
    [SerializeField] AudioClip puzzleSolvedClip;
    [SerializeField] AudioClip mistakeClip;
    [SerializeField] AudioClip tickClip;
    [SerializeField] AudioClip winClip;
    [SerializeField] AudioClip loseClip;
    [SerializeField] AudioClip bonusClip;

    [Header("Lose effect (optional)")]
    [Tooltip("Real-time lights that dim and turn red when time runs out.")]
    [SerializeField] Light[] gymLights;
    [SerializeField] Color loseLightColor = new Color(1f, 0.15f, 0.1f);

    [Header("Events")]
    public UnityEvent onGameStarted = new UnityEvent();
    public UnityEvent onPuzzleSolved = new UnityEvent();
    public UnityEvent onAllPuzzlesSolved = new UnityEvent();
    public UnityEvent onWin = new UnityEvent();
    public UnityEvent onLose = new UnityEvent();

    public GameState State { get; private set; } = GameState.NotStarted;
    public float TimeRemaining { get; private set; }
    public float TimeElapsed => countdownSeconds - TimeRemaining + bonusSecondsEarned - penaltySecondsTaken;
    public int SolvedCount => solved.Count;
    public int PuzzlesRequired => puzzlesRequired;
    public bool AllSolved => solved.Count >= puzzlesRequired;
    public IReadOnlyCollection<string> SolvedIds => solved;
    /// <summary>The exit keypad's code (the keypad should check against this).</summary>
    public string KeypadCode => keypadCode;
    /// <summary>True once the code is showing on the scoreboard.</summary>
    public bool CodeRevealed { get; private set; }

    const string LeaderboardKey = "VolleyballEscape.Leaderboard";
    const int LeaderboardSize = 5;

    readonly HashSet<string> solved = new HashSet<string>();
    float messageClearAt = float.PositiveInfinity;
    int lastTickSecond = -1;
    float restartAllowedAt;
    float bonusSecondsEarned, penaltySecondsTaken;
    InputAction startAction;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] A second GameManager was found and disabled.", this);
            enabled = false;
            return;
        }
        Instance = this;
        TimeRemaining = countdownSeconds;

        // Either trigger (or Enter in the Editor) starts the game and restarts it after it ends.
        startAction = new InputAction("StartOrRestart", InputActionType.Button);
        startAction.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        startAction.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        startAction.AddBinding("<Keyboard>/enter");
    }

    void OnEnable() => startAction?.Enable();
    void OnDisable() => startAction?.Disable();

    void OnDestroy()
    {
        startAction?.Dispose();
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    // In the Editor (not playing), keep the scoreboard and wrist clocks showing the
    // Countdown Seconds value, so the scene always shows the real starting time.
    // Runs when the scene loads and whenever the slider changes.
    void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying || timerTexts == null) return;
            string start = FormatTime(countdownSeconds);
            foreach (var text in timerTexts)
            {
                if (text == null || text.text == start) continue;
                UnityEditor.Undo.RecordObject(text, "Update clock text");
                text.text = start;
                UnityEditor.EditorUtility.SetDirty(text);
            }
        };
    }
#endif

    void Start()
    {
        if (endPanel != null) endPanel.SetActive(false);
        RefreshTimer();
        RefreshLeaderboard();
        RefreshScore();

        if (startOnLoad) StartGame();
        else ShowMessage($"Pull the trigger to start. You have {FormatTime(countdownSeconds)}.", sticky: true);
    }

    void Update()
    {
        switch (State)
        {
            case GameState.NotStarted:
                if (startAction.WasPressedThisFrame()) StartGame();
                break;

            case GameState.Running:
                TimeRemaining -= Time.deltaTime;
                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    RefreshTimer();
                    Lose();
                    break;
                }
                int whole = Mathf.CeilToInt(TimeRemaining);
                if (whole <= tickBelowSeconds && whole != lastTickSecond)
                {
                    lastTickSecond = whole;
                    Play(tickClip);
                }
                RefreshTimer();
                break;

            case GameState.Won:
            case GameState.Lost:
                if (Time.time >= restartAllowedAt && startAction.WasPressedThisFrame()) Restart();
                break;
        }

        if (Time.time >= messageClearAt) ClearMessage();
    }

    // ------------------------------------------------------------------ static helpers for puzzles

    /// <summary>Mark a puzzle solved. Safe to call more than once and with no GameManager.</summary>
    public static void ReportSolved(string puzzleId)
    {
        if (Instance != null) Instance.MarkSolved(puzzleId);
        else Debug.Log($"[GameManager] (none in scene) Puzzle solved: {puzzleId}");
    }

    /// <summary>Show a mistake message, play the buzz, and optionally take time off.</summary>
    public static void ReportMistake(string message, bool penalize)
    {
        if (Instance != null) Instance.Mistake(message, penalize);
        else Debug.Log($"[GameManager] (none in scene) Mistake: {message}");
    }

    /// <summary>Show a short message to the player.</summary>
    public static void Say(string message)
    {
        if (Instance != null) Instance.ShowMessage(message);
        else Debug.Log($"[GameManager] (none in scene) {message}");
    }

    /// <summary>Add (or with a negative value, remove) seconds from the clock.</summary>
    public static void AddTime(float seconds)
    {
        if (Instance != null) Instance.AdjustTime(seconds);
    }

    // ------------------------------------------------------------------ Inspector-callable methods

    public void StartGame()
    {
        if (State != GameState.NotStarted) return;
        State = GameState.Running;
        TimeRemaining = countdownSeconds;
        Play(startClip);
        ShowMessage("Escape the gym before the alarm arms!");
        onGameStarted?.Invoke();
    }

    /// <summary>For UnityEvents: type the puzzle's ID in the string box.</summary>
    public void MarkSolved(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || !solved.Add(puzzleId)) return;

        Play(puzzleSolvedClip);
        Debug.Log($"[GameManager] Solved '{puzzleId}' ({solved.Count}/{puzzlesRequired}).", this);
        onPuzzleSolved?.Invoke();

        if (solved.Count == puzzlesRequired)
        {
            CodeRevealed = true;
            RefreshScore();
            ShowMessage(winWhenAllSolved ? "All puzzles solved!" : "All puzzles solved! Check the scoreboard.");
            onAllPuzzlesSolved?.Invoke();
            if (winWhenAllSolved) Win();
        }
        else
        {
            ShowMessage("Puzzle solved!");
        }
    }

    public void Mistake(string message, bool penalize)
    {
        Play(mistakeClip);
        if (penalize && State == GameState.Running)
        {
            AdjustTime(-mistakePenaltySeconds);
            message += $"  -{mistakePenaltySeconds:0}s";
        }
        ShowMessage(message);
    }

    public void AdjustTime(float seconds)
    {
        if (State != GameState.Running) return;
        TimeRemaining = Mathf.Max(0.01f, TimeRemaining + seconds);
        if (seconds > 0f) { bonusSecondsEarned += seconds; Play(bonusClip); }
        else penaltySecondsTaken += -seconds;
        RefreshTimer();
    }

    public void Win()
    {
        if (State != GameState.Running) return;
        if (!AllSolved)
        {
            ShowMessage("Locked. Solve every puzzle first.");
            return;
        }
        State = GameState.Won;
        float escapeTime = countdownSeconds - TimeRemaining;
        bool newBest = SaveTime(escapeTime);
        RefreshLeaderboard();
        Play(winClip);
        ShowEnd("YOU ESCAPED!", $"Time: {FormatTime(escapeTime)}{(newBest ? "  (top 5!)" : "")}\nPull the trigger to play again.");
        onWin?.Invoke();
    }

    public void Lose()
    {
        if (State != GameState.Running) return;
        State = GameState.Lost;
        foreach (var l in gymLights)
            if (l != null) { l.color = loseLightColor; l.intensity *= 0.6f; }
        Play(loseClip);
        ShowEnd("TIME'S UP!", "The alarm is armed.\nPull the trigger to try again.");
        onLose?.Invoke();
    }

    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    public void ShowMessage(string message) => ShowMessage(message, sticky: false);

    // ------------------------------------------------------------------ internals

    void ShowMessage(string message, bool sticky)
    {
        if (messageText == null) { Debug.Log("[GameManager] " + message, this); return; }
        messageText.text = message;
        messageText.gameObject.SetActive(true);
        messageClearAt = sticky ? float.PositiveInfinity : Time.time + messageSeconds;
    }

    void ClearMessage()
    {
        messageClearAt = float.PositiveInfinity;
        if (messageText != null) messageText.text = "";
    }

    void ShowEnd(string title, string detail)
    {
        ClearMessage();
        restartAllowedAt = Time.time + 2f; // so a pass/trigger pull mid-game doesn't skip the screen
        if (endPanel != null) endPanel.SetActive(true);
        if (endTitleText != null) endTitleText.text = title;
        if (endDetailText != null) endDetailText.text = detail;
        if (endPanel == null && endTitleText == null) ShowMessage(title + " " + detail, sticky: true);
    }

    void RefreshTimer()
    {
        if (timerTexts == null) return;
        string t = FormatTime(TimeRemaining);
        bool low = State == GameState.Running && TimeRemaining <= tickBelowSeconds;
        foreach (var text in timerTexts)
        {
            if (text == null) continue;
            text.text = t;
            text.color = low ? new Color(1f, 0.3f, 0.25f) : Color.white;
        }
    }

    void RefreshScore()
    {
        if (scoreText == null) return;
        string home = hiddenScore, away = hiddenScore;
        if (CodeRevealed && !string.IsNullOrEmpty(keypadCode))
        {
            int half = (keypadCode.Length + 1) / 2;
            home = keypadCode.Substring(0, half);
            away = keypadCode.Substring(half);
        }
        scoreText.text = string.Format(scoreFormat, home, away);
    }

    void RefreshLeaderboard()
    {
        if (leaderboardText == null) return;
        var times = LoadTimes();
        leaderboardText.text = times.Count == 0
            ? "FASTEST ESCAPES\n—"
            : "FASTEST ESCAPES\n" + string.Join("\n", times.Select((t, i) => $"{i + 1}. {FormatTime(t)}"));
    }

    static List<float> LoadTimes()
    {
        string raw = PlayerPrefs.GetString(LeaderboardKey, "");
        return raw.Split(',')
                  .Select(s => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : -1f)
                  .Where(f => f > 0f)
                  .OrderBy(f => f)
                  .Take(LeaderboardSize)
                  .ToList();
    }

    /// <summary>Adds a time to the top-5 list. Returns true if it made the list.</summary>
    static bool SaveTime(float seconds)
    {
        var times = LoadTimes();
        times.Add(seconds);
        times = times.OrderBy(f => f).Take(LeaderboardSize).ToList();
        PlayerPrefs.SetString(LeaderboardKey, string.Join(",", times.Select(f => f.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
        PlayerPrefs.Save();
        return times.Contains(seconds);
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    void Play(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }
}
