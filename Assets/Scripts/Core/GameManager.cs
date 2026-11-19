using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { NotStarted, Running, Won, Lost }

    public static GameManager Instance { get; private set; }

    public static bool Running => Instance == null || Instance.State == GameState.Running;

    [Header("Rules")]
    [Tooltip("Length of the game in seconds (300 = 5:00). Adjustable from 60 to 900.")]
    [Range(60, 900)] [SerializeField] int countdownSeconds = 300;
    [Tooltip("How many puzzles must be solved before the player can escape.")]
    [Min(1)] [SerializeField] int puzzlesRequired = 3;
    [Tooltip("Seconds taken off the clock by a penalized mistake.")]
    [SerializeField] float mistakePenaltySeconds = 20f;
    [Tooltip("Start the clock as soon as the scene loads.")]
    [SerializeField] bool startOnLoad = false;
    [Tooltip("Start the clock the first time the player moves: teleport, snap turn, stick movement, or a real step. " +
             "Off = start with a trigger pull instead. (Enter also starts it in the Editor.)")]
    [SerializeField] bool startOnFirstMove = true;
    [Tooltip("How far (m) the player has to physically step for it to count as moving.")]
    [SerializeField] float stepDistance = 0.2f;
    [Tooltip("Seconds after the scene loads before movement is watched, so tracking can settle.")]
    [SerializeField] float settleSeconds = 1f;
    [Tooltip("Win as soon as every puzzle is solved. Off = the exit keypad decides the win.")]
    [SerializeField] bool winWhenAllSolved = true;
    [Tooltip("Play the tick sound every second below this many seconds left.")]
    [SerializeField] int tickBelowSeconds = 30;

    [Header("Text (all optional)")]
    [SerializeField] TMP_Text[] timerTexts;

    [Header("Scoreboard exit code")]
    [Tooltip("Scoreboard text that shows HOME/AWAY. It reads 00-00 until every puzzle is solved, then shows the keypad code as the score.")]
    [SerializeField] TMP_Text scoreText;
    [Tooltip("Each round the code is two random scores from 0 to this (inclusive). Shown as HOME and AWAY; " +
             "the keypad code is both, two digits each (HOME 7, AWAY 23 -> 0723).")]
    [Range(1, 99)] [SerializeField] int maxCodeScore = 25;
    [Tooltip("{0} = HOME score, {1} = AWAY score.")]
    [TextArea(2, 3)] [SerializeField] string scoreFormat = "<size=70%>HOME        AWAY</size>\n{0}       {1}";
    [SerializeField] string hiddenScore = "00";

    [Header("Win screen (fixed in the doorway, shows the top-5 leaderboard)")]
    [SerializeField] WinScreen winScreen;

    [Header("Lose screen (optional; also used for a win if there's no Win Screen)")]
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

    [Header("Lose effect (optional)")]
    [Tooltip("Real-time lights that dim and turn red when time runs out.")]
    [SerializeField] Light[] gymLights;
    [SerializeField] Color loseLightColor = new Color(1f, 0.15f, 0.1f);

    [Header("Events")]
    public UnityEvent onGameStarted = new UnityEvent();
    public UnityEvent onWin = new UnityEvent();

    public GameState State { get; private set; } = GameState.NotStarted;
    public float TimeRemaining { get; private set; }
    public int SolvedCount => solved.Count;
    public bool AllSolved => solved.Count >= puzzlesRequired;
    public int HomeScore { get; private set; }
    public int AwayScore { get; private set; }
    public string KeypadCode => $"{HomeScore:00}{AwayScore:00}";
    public bool CheckCode(string entry) => entry == KeypadCode;
    public bool CodeRevealed { get; private set; }

    const string LeaderboardKey = "VolleyballEscape.Leaderboard";
    const int LeaderboardSize = 5;

    readonly HashSet<string> solved = new HashSet<string>();
    int lastTickSecond = -1;
    float restartAllowedAt;
    InputAction startAction;

    // First-move detection
    Transform rig, head;
    bool hasBaseline;
    Vector3 baseRigPos, baseHeadLocal;
    float baseRigYaw;

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

        HomeScore = Random.Range(0, maxCodeScore + 1);
        AwayScore = Random.Range(0, maxCodeScore + 1);
        Debug.Log($"[GameManager] Keypad code this round: {KeypadCode}", this);

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
        RefreshScore();

        if (startOnLoad) StartGame();
        else ShowMessage(startOnFirstMove
            ? $"Clock starts when the player first moves. You have {FormatTime(countdownSeconds)}."
            : $"Pull the trigger to start. You have {FormatTime(countdownSeconds)}.");
    }

    void Update()
    {
        switch (State)
        {
            case GameState.NotStarted:
                if (startOnFirstMove ? PlayerMoved() || EnterPressed() : startAction.WasPressedThisFrame()) StartGame();
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
    }

    // ------------------------------------------------------------------ static helpers for puzzles

    public static void ReportSolved(string puzzleId)
    {
        if (Instance != null) Instance.MarkSolved(puzzleId);
        else Debug.Log($"[GameManager] (none in scene) Puzzle solved: {puzzleId}");
    }

    public static void ReportMistake(string message, bool penalize)
    {
        if (Instance != null) Instance.Mistake(message, penalize);
        else Debug.Log($"[GameManager] (none in scene) Mistake: {message}");
    }

    public static void Say(string message)
    {
        if (Instance != null) Instance.ShowMessage(message);
        else Debug.Log($"[GameManager] (none in scene) {message}");
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

    public void MarkSolved(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || !solved.Add(puzzleId)) return;

        Play(puzzleSolvedClip);
        Debug.Log($"[GameManager] Solved '{puzzleId}' ({solved.Count}/{puzzlesRequired}).", this);

        if (solved.Count == puzzlesRequired)
        {
            CodeRevealed = true;
            RefreshScore();
            ShowMessage(winWhenAllSolved ? "All puzzles solved!" : "All puzzles solved! Check the scoreboard.");
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

    void AdjustTime(float seconds)
    {
        if (State != GameState.Running) return;
        TimeRemaining = Mathf.Max(0.01f, TimeRemaining + seconds);
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
        var (top, rank) = SaveTime(escapeTime);
        Play(winClip);
        if (winScreen != null)
        {
            restartAllowedAt = Time.time + 2f;
            winScreen.Show(escapeTime, top, rank);
        }
        else ShowEnd("YOU ESCAPED!", $"Time: {FormatTime(escapeTime)}{(rank >= 0 ? "  (top 5!)" : "")}\nPull the trigger to play again.");
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
    }

    public void PlayCorrectSound() => Play(puzzleSolvedClip);

    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    public void ShowMessage(string message) => Debug.Log("[GameManager] " + message, this);

    // ------------------------------------------------------------------ internals

    void ShowEnd(string title, string detail)
    {
        restartAllowedAt = Time.time + 2f;
        if (endPanel != null) endPanel.SetActive(true);
        if (endTitleText != null) endTitleText.text = title;
        if (endDetailText != null) endDetailText.text = detail;
        if (endPanel == null && endTitleText == null) ShowMessage(title + " " + detail);
    }

    static bool EnterPressed() => Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;

    bool PlayerMoved()
    {
        if (head == null)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            head = cam.transform;
        }
        if (rig == null)
        {
            var origin = FindAnyObjectByType<XROrigin>();
            rig = origin != null ? origin.transform : head.root;
        }

        Vector3 headLocal = rig.InverseTransformPoint(head.position);
        if (!hasBaseline)
        {
            if (Time.timeSinceLevelLoad < settleSeconds) return false;
            baseRigPos = rig.position;
            baseRigYaw = rig.eulerAngles.y;
            baseHeadLocal = headLocal;
            hasBaseline = true;
            return false;
        }

        if ((rig.position - baseRigPos).sqrMagnitude > 0.0001f) return true;
        if (Mathf.Abs(Mathf.DeltaAngle(rig.eulerAngles.y, baseRigYaw)) > 1f) return true;
        Vector3 step = headLocal - baseHeadLocal;
        step.y = 0f;
        return step.magnitude > stepDistance;
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
        if (CodeRevealed)
        {
            home = HomeScore.ToString("00");
            away = AwayScore.ToString("00");
        }
        scoreText.text = string.Format(scoreFormat, home, away);
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

    static (List<float> top, int rank) SaveTime(float seconds)
    {
        seconds = Mathf.Round(seconds * 100f) / 100f; // same precision as what's saved
        var times = LoadTimes();
        times.Add(seconds);
        times = times.OrderBy(f => f).Take(LeaderboardSize).ToList();
        PlayerPrefs.SetString(LeaderboardKey, string.Join(",", times.Select(f => f.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
        PlayerPrefs.Save();
        return (times, times.LastIndexOf(seconds));
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    void Play(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }
}
