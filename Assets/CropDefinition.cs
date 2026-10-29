using UnityEngine;

[CreateAssetMenu(menuName = "Conservatory/Crop")]
public class CropDefinition : ScriptableObject{
	public enum GrowthStage : int {
		Seed = 0,
		Seedling = 1,
		Growing = 2,
		Mature = 3
	}
	[Header("Identity")]
    public string cropName;
    public bool isRare; // is this the rare crop? yes => can only be planted in Rare Crop Plot, no => can be planted in Soil Bed
    public bool requiresRarePlot; // is this the rare crop? yes => true, no => false
	
	[Header("Growth")]
	[Range(0f, 1f)]
	public float initialGrowth; // [0.0, 1.0], percentage starting growth
	[Min(1)]
    public int growthDuration; // required maturation duration, seconds
    [Range(0f, 1f)]
    public float stage1Threshold; // [0.0, 1.0], minimum required growth before stage 0 (seed prefab) to stage 1 (seedling) transition
    [Range(0f, 1f)]
    public float stage2Threshold; // [0.0, 1.0], minimum required growth before stage 1 (seedling prefab) to stage 2 (growing prefab) transition
    public GameObject seedPrefab; // prefab representing a planted seed
    public GameObject[] growthStagePrefabs; // the prefabs representing each of the 3 growth stages

    [Header("Cultivation")]
    [Range(0.01f, 1f)]
	public float initialHealth; // [0.0, 1.0], crop's starting health percentage
	[Range(0f, 1f)]
	public float initialPollination; // [0.0, 1.0], crop's starting pollenation percentage
	[Range(0f, 1f)]
    public float minGrowthMoisture; // [0.0, 1.0], minimum moisture percentage required to grow
    [Range(0f, 1f)]
    public float minGrowthFertility; // [0.0, 1.0], minimum fertility percentage required to grow
    [Range(0f, 1f)]
    public float requiredPollination; // [0.0, 1.0], pollination required before harvest
    [Range(0f, 1f)]
    public float moistureConsumptionRate; // [0.0, 1.0], rate of moisture consumption per second
    [Range(0f, 1f)]
    public float fertilityConsumptionRate; // [0.0, 1.0], rate of fertility consumption per second

    [Header("Harvest")]
    public ScriptableObject harvestResource; // resource yielded upon harvest
    [Min(1)]
    public int harvestYield; // how much of `harvestResource` resource is produced upon harvest
    

   

    [Header("Rare Crop Requirements")]
    public bool requiresMana; // is mana required for maturation?
    public bool requiresPollination; // is pollination required before harvest?
    public bool requiresAstralEnhancement; // requires resource from Lucus' area?
}