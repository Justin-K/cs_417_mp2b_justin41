using UnityEngine;

public class Crop : MonoBehaviour {
    public CropDefinition definition;



    // Stats
    private float currentHealth; // [0.0, 1.0], current health of crop => 0.0 represents death
    private float currentGrowth; // [0.0, 1.0], percentage of current growth
    private float currentPollination; // [0.0, 1.0], percentage of current pollination
    private float currentGrowthTime; // amount of total time spent growing
    private GameObject currentPlantPrefab; // instantiated avatar representing current growth

    private SoilPlot currentPlot;
    private CropDefinition.GrowthStage displayedStage;
    private bool isPlanted;

    // The SoilBed adds this component before it can assign a definition,
    // so initialization must happen explicitly instead of in Awake or Start.
    public void Initialize(CropDefinition cropDefinition) {
        definition = cropDefinition;
        if (definition == null) { return; }

        currentHealth = definition.initialHealth;
        currentGrowth = definition.initialGrowth;
        currentPollination = definition.initialPollination;
        currentGrowthTime = 0f;
    }

    public float CurrentGrowth => currentGrowth;
    public float CurrentGrowthTime => currentGrowthTime;
    public float CurrentPollination => currentPollination;
    public SoilPlot Plot => currentPlot;
    public bool IsPlanted => isPlanted;
    public event System.Action<Crop> Harvested;

    public void Pollinate(float amount) {
        if (isPlanted && amount > 0f) currentPollination = Mathf.Clamp01(currentPollination + amount);
    }

    public CropDefinition.GrowthStage GetCurrentStage() {
        if (currentGrowth >= 0f && currentGrowth < definition.stage1Threshold) { return CropDefinition.GrowthStage.Seed; }
        else if (currentGrowth >= definition.stage1Threshold && currentGrowth < definition.stage2Threshold) { return CropDefinition.GrowthStage.Seedling; }
        else if (currentGrowth >= definition.stage2Threshold && currentGrowth < 1f) { return CropDefinition.GrowthStage.Growing; }
        else { return CropDefinition.GrowthStage.Mature; }
    }

    public bool CanGrow() {
        if (!isPlanted || definition == null || currentHealth <= 0f || currentGrowth >= 1f) { return false; }
        // A crop requires both moisture and fertility. Both are consumed while it grows.
        // Mana and other requirements can be added later.
        return currentPlot != null && currentPlot.currentMoisture > 0f
            && currentPlot.currentMoisture >= definition.minGrowthMoisture && currentPlot.currentFertility > 0f
            && currentPlot.currentFertility >= definition.minGrowthFertility
            && (!definition.requiresPollination || currentPollination >= definition.requiredPollination);
    }

    public bool CanHarvest() {
        if (currentHealth > 0f && isPlanted) {
            if (definition.requiresPollination && currentPollination < definition.requiredPollination) { return false; }
            if (GetCurrentStage() != CropDefinition.GrowthStage.Mature) { return false; }
            // Other requirements (Mana / astral enhancement) can be added here later.
            return true;
        }
        return false;
    }

    public bool CanPlant(bool requested_plot_is_rare, bool plot_empty) {
        if (definition == null || isPlanted || !plot_empty) { return false; }
        if (definition.isRare && definition.requiresRarePlot && !requested_plot_is_rare) { return false; }
        return true;
    }

    // The seed is separate; growthStagePrefabs[0..2] represent Seedling, Growing, Mature.
    private GameObject GetStagePrefab(CropDefinition.GrowthStage stage) {
        if (stage == CropDefinition.GrowthStage.Seed) { return definition.seedPrefab; }
        int index = (int)stage - 1;
        if (definition.growthStagePrefabs == null || index >= definition.growthStagePrefabs.Length) { return null; }
        return definition.growthStagePrefabs[index];
    }

    private bool SpawnStageVisual(CropDefinition.GrowthStage stage) {
        GameObject prefab = GetStagePrefab(stage);
        if (prefab == null) {
            Debug.LogWarning($"Crop '{definition.cropName}' is missing the prefab for stage {stage}.", this);
            return false;
        }

        GameObject nextVisual = Instantiate(prefab, transform.position, transform.rotation, transform);
        ConservatoryVisuals.Fit(nextVisual, stage == CropDefinition.GrowthStage.Mature ? .55f : .3f, transform.position, true);
        if (currentPlantPrefab != null) {
            currentPlantPrefab.SetActive(false);
            Destroy(currentPlantPrefab);
        }
        currentPlantPrefab = nextVisual;
        displayedStage = stage;
        return true;
    }

    // Helpers for stage transitions
    private void OnSeedToSeedling() {
        SpawnStageVisual(CropDefinition.GrowthStage.Seedling);
    }

    private void OnSeedlingToGrowing() {
        SpawnStageVisual(CropDefinition.GrowthStage.Growing);
    }

    private void OnGrowingToMature() {
        SpawnStageVisual(CropDefinition.GrowthStage.Mature);
    }

    public bool OnPlant(SoilPlot plot, Transform parent) {
        // Check if planting is possible
        if (plot == null || !CanPlant(plot.isRare, plot.isEmpty)) { return false; }

        transform.SetParent(parent, true);
        transform.position = plot.plotLocation;

        // Spawn seed (or the visual matching nonzero initialGrowth).
        if (!SpawnStageVisual(GetCurrentStage())) { return false; }

        // Finish planting by associating this Crop with its SoilPlot.
        currentPlot = plot;
        plot.crop = this;
        isPlanted = true;
        return true;
    }

    public void OnHarvest() {
        TryHarvest();
    }

    public bool TryHarvest() {
        // Check if harvest is possible
        if (!CanHarvest()) { return false; }

        // Claim before notifying observers so repeated/reentrant input cannot reward twice.
        isPlanted = false;
        Harvested?.Invoke(this);

        // The owning session receives Harvested and awards local yield using bed provenance.
        currentHealth = 0f;
        OnDeath();
        return true;
    }

    public void OnDeath() {
        // Check if health > 0
        if (currentHealth > 0f) { return; }

        // Release the SoilPlot immediately so it can be replanted.
        if (currentPlot != null && currentPlot.crop == this) { currentPlot.crop = null; }
        currentPlot = null;
        isPlanted = false;

        // Destroy the entire Crop object, including its stage visual.
        Destroy(gameObject);
    }

    // Also release the plot if the Crop is removed by some external system.
    private void OnDestroy() {
        if (currentPlot != null && currentPlot.crop == this) { currentPlot.crop = null; }
    }

    public void OnGrow() {
        TickGrowth(Time.deltaTime);
    }

    public void TickGrowth(float seconds) {
        // Check if growing is possible
        if (seconds <= 0f || !CanGrow()) { return; }

        // Bound progress to available care even on long frames; never grow through a deficit.
        seconds = Mathf.Min(seconds, (1f - currentGrowth) * Mathf.Max(1, definition.growthDuration));
        if (definition.moistureConsumptionRate > 0f)
            seconds = Mathf.Min(seconds, Mathf.Max(0f, currentPlot.currentMoisture - definition.minGrowthMoisture) / definition.moistureConsumptionRate);
        if (definition.fertilityConsumptionRate > 0f)
            seconds = Mathf.Min(seconds, Mathf.Max(0f, currentPlot.currentFertility - definition.minGrowthFertility) / definition.fertilityConsumptionRate);

        // growthDuration is the time for growth to advance from 0 to 1.
        // initialGrowth, if nonzero, provides a head start.
        currentGrowthTime += seconds;
        currentGrowth = Mathf.Clamp01(currentGrowth + seconds / Mathf.Max(1, definition.growthDuration));
        if (currentGrowth > 0.99999f) currentGrowth = 1f;
        currentPlot.currentMoisture = Mathf.Max(0f, currentPlot.currentMoisture - definition.moistureConsumptionRate * seconds);
        currentPlot.currentFertility = Mathf.Max(0f, currentPlot.currentFertility - definition.fertilityConsumptionRate * seconds);
        OnStageTransistion();
    }

    // Retain the original name for compatibility with callers.
    public void OnStageTransistion() {
        if (!isPlanted || definition == null) { return; }

        // Compare the last displayed stage against the growth-derived stage.
        // The loop also handles very large frame times crossing multiple thresholds.
        while ((int)displayedStage < (int)GetCurrentStage()) {
            CropDefinition.GrowthStage previousStage = displayedStage;
            switch (displayedStage) {
                case CropDefinition.GrowthStage.Seed: { OnSeedToSeedling(); break; }
                case CropDefinition.GrowthStage.Seedling: { OnSeedlingToGrowing(); break; }
                case CropDefinition.GrowthStage.Growing: { OnGrowingToMature(); break; }
                case CropDefinition.GrowthStage.Mature: { return; }
            }
            // Avoid an infinite loop if a required stage prefab was not assigned.
            if (displayedStage == previousStage) { return; }
        }
    }

    // Update is called once per frame
    void Update() {
        OnGrow();
    }
}



