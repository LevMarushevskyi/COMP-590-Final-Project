using UnityEngine;

/// <summary>Scores throws from the scene's rack ball against the two target triggers.</summary>
[RequireComponent(typeof(Volleyball))]
public class RackBallTargets : MonoBehaviour
{
    [SerializeField] Collider backLeft;
    [SerializeField] Collider middleRight;
    [SerializeField] GameObject lineupPuzzle;

    Volleyball ball;

    void Awake() => ball = GetComponent<Volleyball>();

    void OnTriggerEnter(Collider target)
    {
        if ((target != backLeft && target != middleRight) || !target.gameObject.activeSelf)
            return;
        if (!GameManager.Running || ball.IsHeld || ball.Body.isKinematic || ball.LastTouch != BallTouch.Thrown)
            return;

        target.gameObject.SetActive(false);
        if (backLeft.gameObject.activeSelf || middleRight.gameObject.activeSelf)
        {
            GameManager.Say("Target hit! Hit the remaining target.");
            return;
        }

        GameManager.ReportSolved("target-practice");
        lineupPuzzle.SetActive(true);
        GameManager.Say("Puzzle 2 unlocked! Find the jerseys in the corner.");
    }

    public void ShowInstructions() =>
        GameManager.Say("Puzzle 1: Grab the rack ball and throw it at both targets.");
}
