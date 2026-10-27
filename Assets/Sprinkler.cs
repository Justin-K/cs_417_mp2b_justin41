using UnityEngine;
using System.Collections.Generic;

public class Sprinkler : MonoBehaviour {
    // Whether watering is enabled; being "active" also requires some stored resource.
    [SerializeField] private bool shouldRun;
    public bool active => shouldRun && currentResource > 0f;
    public bool IsEnabled => shouldRun; // Unlike active, stays true when water runs out.

    // Amount of available water (or water-equivalent resource), in moisture units.
    [SerializeField, Min(0f)] private float currentResource;
    public float CurrentResource => currentResource;

    // Maximum resource the sprinkler can hold.
    [SerializeField, Min(0f)] private float resourceCapacity = 10f;
    public float maxResource => resourceCapacity;

    // Radius within which SoilBed colliders can receive water.
    [Min(0f)] public float effectRadius = 3f;
    // Amount of stored resource offered to nearby beds per watering pulse.
    [Min(0f)] public float consumption = 0.6f;
    // Seconds between watering pulses.
    [Min(0.01f)] public float consumptionRate = 1f;

    public ParticleSystem psNorth;
    public ParticleSystem psEast;
    public ParticleSystem psSouth;
    public ParticleSystem psWest;

    private float elapsedTime;
    private bool allPSEnabled;

    // Allows an interaction or other system to enable/disable watering.
    public void SetActive(bool value) {
        shouldRun = value;
        UpdateParticleSystems(true);
    }

    // Keep visuals in sync with the actual operating state (enabled AND fueled).
    // Force the initial sync even if particles were set to Play On Awake.
    private void UpdateParticleSystems(bool force = false) {
        bool shouldEmit = active;
        if (!force && allPSEnabled == shouldEmit) { return; }

        allPSEnabled = shouldEmit;
        if (shouldEmit) {
            if (psNorth != null) { psNorth.Play(); }
            if (psEast != null) { psEast.Play(); }
            if (psSouth != null) { psSouth.Play(); }
            if (psWest != null) { psWest.Play(); }
        } else {
            // Pause would freeze droplets in mid-air; Stop also clears them.
            if (psNorth != null) { psNorth.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            if (psEast != null) { psEast.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            if (psSouth != null) { psSouth.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            if (psWest != null) { psWest.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }
    }

    // Try to refill `currentResource` with respect to `maxResource`.
    // Returns the amount that did not fit in the sprinkler.
    public float TryRefillResource(float amount) {
        if (amount <= 0f) { return 0f; }

        float accepted = Mathf.Min(amount, Mathf.Max(0f, maxResource - currentResource));
        currentResource += accepted;
        UpdateParticleSystems(); // Restart automatically if enabled and no longer empty.
        return amount - accepted;
    }

    // Find unique SoilBeds in radius; null if none are found.
    public SoilBed[] GetSoilBedsInRadius() {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, effectRadius, ~0, QueryTriggerInteraction.Collide);
        List<SoilBed> beds = new List<SoilBed>();
        foreach (Collider collider in colliders) {
            // SoilBed may sit on a parent while colliders are on child meshes.
            SoilBed bed = collider.GetComponentInParent<SoilBed>();
            if (bed != null && !beds.Contains(bed)) { beds.Add(bed); }
        }
        return beds.Count == 0 ? null : beds.ToArray();
    }

    // Distribute up to `consumption` resource among nearby SoilBeds.
    // Only water accepted by SoilPlots is removed from the reservoir.
    // Returns the number of beds that actually received water.
    public int TryAffectInscribedSoilBeds() {
        if (!active || consumption <= 0f) { return 0; }
        SoilBed[] beds = GetSoilBedsInRadius();
        if (beds == null) { return 0; }

        float offered = Mathf.Min(consumption, currentResource);
        float waterPerBed = offered / beds.Length;
        float used = 0f;
        int affected = 0;
        foreach (SoilBed bed in beds) {
            float remainder = bed.TryAddWater(waterPerBed);
            float accepted = Mathf.Clamp(waterPerBed - remainder, 0f, waterPerBed);
            used += accepted;
            if (accepted > 0f) { affected++; }
        }
        currentResource = Mathf.Max(0f, currentResource - used);
        UpdateParticleSystems(); // Stop immediately if this pulse drained the reservoir.
        return affected;
    }

    // Start is called once before the first execution of Update.
    void Start() {
        currentResource = Mathf.Clamp(currentResource, 0f, Mathf.Max(0f, maxResource));
        SetActive(shouldRun);
    }

    // Water on a fixed interval; no need to make particle collisions drive gameplay.
    void Update() {
        if (!active) { return; }
        
        elapsedTime += Time.deltaTime;
        float interval = Mathf.Max(0.01f, consumptionRate);
        while (elapsedTime >= interval) {
            elapsedTime -= interval;
            TryAffectInscribedSoilBeds();
        }
    }
}
