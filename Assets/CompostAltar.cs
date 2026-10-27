using UnityEngine;
using System.Collections.Generic;

// Converts OrganicWaste from harvested crops into fertility for nearby SoilBeds.
// Uses the same basic reservoir / pulse API as Sprinkler.
public class CompostAltar : MonoBehaviour {
    [SerializeField] private bool shouldRun;
    public bool active => shouldRun && currentResource > 0f;

    // OrganicWaste stored on this altar (not a separate inventory item).
    // A little starting waste lets the first crop grow before any harvests exist.
    [SerializeField, Min(0f)] private float currentResource = 1f;
    public float CurrentResource => currentResource;

    [SerializeField, Min(0f)] private float resourceCapacity = 10f;
    public float maxResource => resourceCapacity;

    // The altar collects harvest waste and fertilizes SoilBeds within this radius.
    [Min(0f)] public float effectRadius = 3f;
    // How much OrganicWaste to offer to beds per pulse (1 waste = 1 fertility unit).
    [Min(0f)] public float consumption = 0.6f;
    // Seconds between compost pulses.
    [Min(0.01f)] public float consumptionRate = 1f;

    private float elapsedTime;

    // Called by player interaction (e.g. the wand) or an existing UnityEvent.
    public void SetActive(bool value) {
        shouldRun = value;
        if (shouldRun && currentResource <= 0f) {
            Debug.Log("Compost Altar needs OrganicWaste. Harvest a mature crop nearby to refill it.", this);
        }
    }

    // Parameterless interaction hook for the Unity Inspector / UnityEvents.
    public void ToggleActive() {
        SetActive(!shouldRun);
    }

    // Optional desktop test: clicking the prefab toggles it when it has a Collider.
    private void OnMouseDown() {
        ToggleActive();
    }

    // Add OrganicWaste, up to maxResource. Returns the amount that did not fit.
    public float TryRefillResource(float amount) {
        if (amount <= 0f) { return 0f; }
        float accepted = Mathf.Min(amount, Mathf.Max(0f, maxResource - currentResource));
        currentResource += accepted;
        return amount - accepted;
    }

    // Called only by a successful harvest. Deposit into the nearest altar in range.
    // Returns any waste that could not be stored (no nearby altar / no capacity).
    public static float TryDepositHarvestWaste(Vector3 harvestPosition, float amount) {
        if (amount <= 0f) { return 0f; }

        CompostAltar nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (CompostAltar altar in FindObjectsOfType<CompostAltar>()) {
            if (!altar.isActiveAndEnabled || altar.currentResource >= altar.maxResource) { continue; }
            float distanceSquared = (altar.transform.position - harvestPosition).sqrMagnitude;
            if (distanceSquared <= altar.effectRadius * altar.effectRadius && distanceSquared < nearestDistanceSquared) {
                nearest = altar;
                nearestDistanceSquared = distanceSquared;
            }
        }
        return nearest == null ? amount : nearest.TryRefillResource(amount);
    }

    // Reuse Sprinkler's collider-based lookup of nearby SoilBeds.
    public SoilBed[] GetSoilBedsInRadius() {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, effectRadius, ~0, QueryTriggerInteraction.Collide);
        List<SoilBed> beds = new List<SoilBed>();
        foreach (Collider collider in colliders) {
            SoilBed bed = collider.GetComponentInParent<SoilBed>();
            if (bed != null && !beds.Contains(bed)) { beds.Add(bed); }
        }
        return beds.Count == 0 ? null : beds.ToArray();
    }

    // Distribute fertilizer to nearby beds; spend only OrganicWaste they accept.
    // Returns the number of SoilBeds that actually received fertility.
    public int TryAffectInscribedSoilBeds() {
        if (!active || consumption <= 0f) { return 0; }
        SoilBed[] beds = GetSoilBedsInRadius();
        if (beds == null) { return 0; }

        float offered = Mathf.Min(consumption, currentResource);
        float perBed = offered / beds.Length;
        float used = 0f;
        int affected = 0;
        foreach (SoilBed bed in beds) {
            float remainder = bed.TryAddFertility(perBed);
            float accepted = Mathf.Clamp(perBed - remainder, 0f, perBed);
            used += accepted;
            if (accepted > 0f) { affected++; }
        }
        currentResource = Mathf.Max(0f, currentResource - used);
        return affected;
    }

    void Start() {
        currentResource = Mathf.Clamp(currentResource, 0f, Mathf.Max(0f, maxResource));
        SetActive(shouldRun);
    }

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
