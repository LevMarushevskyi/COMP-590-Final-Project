using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class WinScreen : MonoBehaviour
{
    [Tooltip("Everything that should appear on a win (the panel and its text).")]
    [SerializeField] GameObject content;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text timeText;
    [SerializeField] TMP_Text leaderboardText;
    [SerializeField] TMP_Text footerText;

    [SerializeField] string title = "CONGRATULATIONS!\n<size=60%>You escaped the gym!</size>";
    [SerializeField] string footer = "Pull the trigger to play again";
    [SerializeField] Color highlightColor = new Color(0.25f, 1f, 0.35f);
    [SerializeField] int slots = 5;

    void Awake()
    {
        if (content != null) content.SetActive(false);
    }

    public void Show(float escapeSeconds, IReadOnlyList<float> top, int rank)
    {
        if (content != null) content.SetActive(true);
        if (titleText != null) titleText.text = title;
        if (footerText != null) footerText.text = footer;
        if (timeText != null)
            timeText.text = $"Your time: {GameManager.FormatTime(escapeSeconds)}" +
                            (rank == 0 ? "  <color=#" + ColorUtility.ToHtmlStringRGB(highlightColor) + ">New record!</color>" : "");

        if (leaderboardText == null) return;
        string green = ColorUtility.ToHtmlStringRGB(highlightColor);
        var sb = new StringBuilder("<b>TOP 5 ESCAPES</b>\n");
        for (int i = 0; i < slots; i++)
        {
            string line = i < top.Count ? $"{i + 1}.  {GameManager.FormatTime(top[i])}" : $"{i + 1}.  -:--";
            sb.Append(i == rank ? $"<color=#{green}><b>{line}   (you)</b></color>" : line);
            if (i < slots - 1) sb.Append('\n');
        }
        leaderboardText.text = sb.ToString();
    }
}
