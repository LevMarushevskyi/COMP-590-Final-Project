using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CourtLineup : MonoBehaviour
{
    [SerializeField] string puzzleId = "lineup";
    [Tooltip("The six jerseys. Left empty, it uses every Jersey under this object's parent.")]
    [SerializeField] Jersey[] jerseys;
    [Tooltip("A jersey slower than this (m/s) counts as lying still.")]
    [SerializeField] float restSpeed = 0.05f;
    [SerializeField] float checkInterval = 0.25f;
    [SerializeField] string faultMessage = "Rotation fault! Check the lineup.";

    [Header("Events")]
    public UnityEvent onSolved = new UnityEvent();

    public bool IsSolved { get; private set; }

    CourtZone[] zones;
    XRGrabInteractable[] grabs;
    Rigidbody[] bodies;
    float nextCheck;
    bool faultShown;

    void Awake()
    {
        zones = GetComponentsInChildren<CourtZone>(true);
        if (jerseys == null || jerseys.Length == 0)
            jerseys = transform.parent != null ? transform.parent.GetComponentsInChildren<Jersey>(true) : new Jersey[0];
        if (zones.Length != jerseys.Length)
            Debug.LogWarning($"[CourtLineup] {zones.Length} zones but {jerseys.Length} jerseys.", this);

        grabs = jerseys.Select(j => j.GetComponent<XRGrabInteractable>()).ToArray();
        bodies = jerseys.Select(j => j.GetComponent<Rigidbody>()).ToArray();
    }

    void Update()
    {
        if (IsSolved || !GameManager.Running || Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;
        Check();
    }

    void Check()
    {
        var placed = new Dictionary<CourtZone, Jersey>();
        for (int i = 0; i < jerseys.Length; i++)
        {
            if (grabs[i] != null && grabs[i].isSelected) { faultShown = false; return; }
            if (bodies[i] != null && !bodies[i].isKinematic && bodies[i].linearVelocity.magnitude > restSpeed) return;

            var zone = zones.FirstOrDefault(z => z.Contains(jerseys[i].transform.position));
            if (zone == null || placed.ContainsKey(zone)) return;
            placed[zone] = jerseys[i];
        }
        if (placed.Count < zones.Length) return;

        if (zones.All(z => placed[z].Number == z.ExpectedJersey)) Solve();
        else if (!faultShown) Fault(placed);
    }

    void Solve()
    {
        IsSolved = true;
        foreach (var j in jerseys) j.Lock();
        Debug.Log("[CourtLineup] Puzzle 2 (starting lineup) solved.", this);
        GameManager.ReportSolved(puzzleId);
        onSolved?.Invoke();
    }

    void Fault(Dictionary<CourtZone, Jersey> placed)
    {
        faultShown = true;
        GameManager.ReportMistake(faultMessage, penalize: false);
        var wrong = zones.Where(z => placed[z].Number != z.ExpectedJersey)
                         .Select(z => $"Zone {z.Zone} has #{placed[z].Number} (needs #{z.ExpectedJersey})");
        Debug.Log("[CourtLineup] Rotation fault: " + string.Join(", ", wrong), this);
    }
}
