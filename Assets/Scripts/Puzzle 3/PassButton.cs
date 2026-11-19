using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PassButton : MonoBehaviour
{
    [Header("Where it works")]
    [Tooltip("Passing only works while the player stands in this zone. Leave empty to allow anywhere.")]
    [SerializeField] PlayerZone onlyInZone;

    [Header("Input")]
    [Tooltip("Leave with no bindings to use either trigger plus P on the keyboard.")]
    [SerializeField] InputActionProperty passAction;

    [Header("Timing")]
    [Tooltip("Seconds your arms stay out after a press.")]
    [SerializeField] float timingWindow = 0.3f;
    [Tooltip("Contact this soon after the press = perfect pass.")]
    [SerializeField] float perfectWindow = 0.1f;
    [Tooltip("Seconds before you can press again, so mashing doesn't help.")]
    [SerializeField] float cooldown = 0.5f;

    [Header("Contact (relative to the player's head)")]
    [SerializeField] float contactRadius = 0.7f;
    [SerializeField] float forearmBelowHead = 0.75f;
    [SerializeField] float forearmInFront = 0.35f;

    [Header("Where the pass goes")]
    [Tooltip("The hoop's trigger. Facing within the angle below sends the pass here.")]
    [SerializeField] Transform aimAssistTarget;
    [SerializeField] float aimAssistAngle = 20f;
    [Tooltip("Seconds the pass is in the air.")]
    [SerializeField] float passFlightTime = 1f;
    [Tooltip("Without aim assist: how far ahead the pass lands, and how high above the forearms.")]
    [SerializeField] float freePassDistance = 4f;
    [SerializeField] float freePassHeight = 1.5f;
    [Tooltip("Largest miss, in meters, for a press made at the very start of the timing window.")]
    [SerializeField] float maxAimError = 1.2f;

    [Header("Feedback (optional)")]
    [Range(0f, 1f)] [SerializeField] float hapticAmplitude = 0.7f;
    [SerializeField] float hapticDuration = 0.12f;
    [Tooltip("Optional 'arms' visual shown at the forearm spot for a moment on each press.")]
    [SerializeField] GameObject platformVisual;
    [SerializeField] float platformShowSeconds = 0.25f;

    InputAction action;
    bool ownsAction;
    Transform head;
    bool armed;
    float armedAt, cooldownUntil, hidePlatformAt;

    void Awake()
    {
        if (platformVisual != null) platformVisual.SetActive(false);

        action = passAction.action;
        if (action == null || action.bindings.Count == 0)
        {
            action = new InputAction("Pass", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            action.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            action.AddBinding("<Keyboard>/p");
            ownsAction = true;
        }
    }

    void OnEnable() => action?.Enable();

    void OnDisable()
    {
        if (ownsAction) action?.Disable();
        armed = false;
    }

    void OnDestroy()
    {
        if (ownsAction) action?.Dispose();
    }

    void Update()
    {
        if (platformVisual != null && platformVisual.activeSelf && Time.time >= hidePlatformAt)
            platformVisual.SetActive(false);

        if (head == null)
        {
            if (Camera.main == null) return;
            head = Camera.main.transform;
        }

        bool allowed = GameManager.Running && (onlyInZone == null || onlyInZone.PlayerInside);
        bool pressed = action.WasPressedThisFrame();
        if (!allowed) { armed = false; return; }

        if (pressed && Time.time >= cooldownUntil)
        {
            armed = true;
            armedAt = Time.time;
            cooldownUntil = Time.time + cooldown;
            ShowPlatform();
        }

        if (!armed) return;

        float sinceArmed = Time.time - armedAt;
        var ball = FindBallInReach();
        if (ball != null)
        {
            armed = false;
            Pass(ball, sinceArmed);
        }
        else if (sinceArmed > timingWindow)
        {
            armed = false;
            GameManager.Say("Whiff! Time it as the ball reaches you.");
        }
    }

    Vector3 ForearmSpot()
    {
        return head.position + FlatForward() * forearmInFront + Vector3.down * forearmBelowHead;
    }

    Vector3 FlatForward()
    {
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
    }

    Volleyball FindBallInReach()
    {
        Vector3 spot = ForearmSpot();
        Volleyball best = null;
        float bestDist = contactRadius;
        foreach (var b in Volleyball.Active)
        {
            if (b == null || b.IsHeld || b.LastTouch == BallTouch.Passed) continue;
            float d = Vector3.Distance(b.transform.position, spot);
            if (d <= bestDist) { best = b; bestDist = d; }
        }
        return best;
    }

    void Pass(Volleyball ball, float secondsAfterPress)
    {
        Vector3 from = ball.transform.position;
        Vector3 forward = FlatForward();

        Vector3 target;
        bool assisted = false;
        if (aimAssistTarget != null)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(aimAssistTarget.position - head.position, Vector3.up);
            if (Vector3.Angle(forward, toTarget) <= aimAssistAngle)
            {
                target = aimAssistTarget.position;
                assisted = true;
            }
            else target = from + forward * freePassDistance + Vector3.up * freePassHeight;
        }
        else target = from + forward * freePassDistance + Vector3.up * freePassHeight;

        bool perfect = secondsAfterPress <= perfectWindow;
        float error = perfect ? 0f : Mathf.InverseLerp(perfectWindow, timingWindow, secondsAfterPress) * maxAimError;
        Vector2 miss = Random.insideUnitCircle.normalized * error;
        target += new Vector3(miss.x, 0f, miss.y);

        float t = Mathf.Max(0.3f, passFlightTime);
        Vector3 velocity = (target - from) / t - 0.5f * Physics.gravity * t;

        ball.Body.isKinematic = false;
        ball.Body.linearVelocity = velocity;
        ball.Body.angularVelocity = Vector3.Cross(Vector3.up, velocity.normalized) * -3f; // a little backspin
        ball.MarkPassed();

        Buzz();
        if (perfect) GameManager.Say(assisted ? "Perfect timing!" : "Perfect timing! Face the hoop to aim.");
        else GameManager.Say("A little early...");
    }

    void ShowPlatform()
    {
        if (platformVisual == null) return;
        platformVisual.transform.SetPositionAndRotation(ForearmSpot(), Quaternion.LookRotation(FlatForward(), Vector3.up));
        platformVisual.SetActive(true);
        hidePlatformAt = Time.time + platformShowSeconds;
    }

    void Buzz()
    {
        Buzz(XRNode.LeftHand);
        Buzz(XRNode.RightHand);
    }

    void Buzz(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, hapticAmplitude, hapticDuration);
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || head == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(ForearmSpot(), contactRadius);
    }
}
