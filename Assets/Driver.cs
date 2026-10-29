using UnityEngine;

public class Driver : MonoBehaviour
{
    public SoilBed bed;
    public CropDefinition cropDefinition;

    private void Start() {
        if (bed != null && cropDefinition != null) {
                Debug.Log("Planted: " + bed.TryPlant(0, 0, cropDefinition));
                Debug.Log("Planted: " + bed.TryPlant(0, 1, cropDefinition));
                Debug.Log("Planted: " + bed.TryPlant(0, 2, cropDefinition));
                Debug.Log("Planted: " + bed.TryPlant(1, 0, cropDefinition));
                Debug.Log("Planted: " + bed.TryPlant(1, 1, cropDefinition));
                Debug.Log("Planted: " + bed.TryPlant(1, 2, cropDefinition));
                
        }
    }
}
