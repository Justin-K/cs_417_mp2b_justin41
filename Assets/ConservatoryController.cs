using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConservatoryController : MonoBehaviour
{
    [Header("XR actions (bind Select to each hand's secondary trigger)")]
    public InputActionReference cycleCropAction;
    public InputActionReference leftSelectAction;
    public InputActionReference rightSelectAction;
    public Transform leftAim;
    public Transform rightAim;
    [Min(0.1f)] public float reach = 12f;

    [Header("Conservatory")]
    public CropDefinition[] cropOptions;
    public Sprinkler sprinkler;
    public CompostAltar compostAltar;
    [Min(0f)] public float startingSprinklerWater = 10f;
    public TMP_Text cropLabel;
    public TMP_Text feedbackLabel;

    private int selectedCrop;
    public float OrganicWaste { get; private set; }

    private void OnEnable()
    {
        if (cycleCropAction != null && cycleCropAction.action != null) {
            cycleCropAction.action.performed += CycleCrop;
            cycleCropAction.action.Enable();
        }
        if (leftSelectAction != null && leftSelectAction.action != null) {
            leftSelectAction.action.performed += SelectLeft;
            leftSelectAction.action.Enable();
        }
        if (rightSelectAction != null && rightSelectAction.action != null) {
            rightSelectAction.action.performed += SelectRight;
            rightSelectAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (cycleCropAction != null && cycleCropAction.action != null) cycleCropAction.action.performed -= CycleCrop;
        if (leftSelectAction != null && leftSelectAction.action != null) leftSelectAction.action.performed -= SelectLeft;
        if (rightSelectAction != null && rightSelectAction.action != null) rightSelectAction.action.performed -= SelectRight;
    }

    private void Start()
    {
        // A little starter water makes the existing sprinkler usable in the demo.
        if (sprinkler != null && sprinkler.CurrentResource <= 0f)
            sprinkler.TryRefillResource(startingSprinklerWater);
        DisplayCrop();
    }

    private void CycleCrop(InputAction.CallbackContext context)
    {
        if (cropOptions == null || cropOptions.Length == 0) {
            Say("Assign crop options in the Inspector.");
            return;
        }
        selectedCrop = (selectedCrop + 1) % cropOptions.Length;
        DisplayCrop();
    }

    private void DisplayCrop()
    {
        string name = cropOptions != null && cropOptions.Length > 0 &&
                      cropOptions[selectedCrop] != null
            ? cropOptions[selectedCrop].cropName : "None";
        if (cropLabel != null) cropLabel.text = "Selected seed: " + name;
        Say("Selected seed: " + name);
    }

    private void SelectLeft(InputAction.CallbackContext context) => Select(leftAim);
    private void SelectRight(InputAction.CallbackContext context) => Select(rightAim);

    private void Select(Transform aim)
    {
        if (aim == null) { Say("Assign the controller aim transforms."); return; }

        // RaycastAll finds the soil even when a crop visual has its own collider.
        RaycastHit[] hits = Physics.RaycastAll(aim.position, aim.forward, reach,
                                               ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Collider target = hit.collider;
            if (sprinkler != null && target.GetComponentInParent<Sprinkler>() == sprinkler) {
                ToggleSprinkler();
                return;
            }
            if (compostAltar != null && target.GetComponentInParent<CompostAltar>() == compostAltar) {
                UseCompostAltar();
                return;
            }

            Crop aimedCrop = target.GetComponentInParent<Crop>();
            SoilBed bed = target.GetComponentInParent<SoilBed>();
            if (bed == null) continue;
            Vector3 position = aimedCrop != null ? aimedCrop.transform.position : hit.point;
            if (!FindCell(bed, position, out int row, out int col)) continue;

            SoilPlot plot = bed.GetPlot(row, col);
            if (plot.isEmpty) Plant(bed, row, col, plot);
            else Harvest(plot.crop);
            return;
        }
        Say("Aim at a soil bed, sprinkler, or compost altar.");
    }

    // Maps the hit position to the same local X/Z grid used by SoilBed.BuildGrid.
    private bool FindCell(SoilBed bed, Vector3 worldPosition, out int row, out int col)
    {
        row = col = 0;
        if (bed.upperLeftCornerRef == null || bed.lowerRightCornerRef == null ||
            bed.GetPlot(0, 0) == null) return false;

        Vector3 topLeft = bed.transform.InverseTransformPoint(bed.upperLeftCornerRef.transform.position);
        Vector3 bottomRight = bed.transform.InverseTransformPoint(bed.lowerRightCornerRef.transform.position);
        Vector3 hit = bed.transform.InverseTransformPoint(worldPosition);
        float width = bottomRight.x - topLeft.x;
        float depth = topLeft.z - bottomRight.z;
        if (width <= 0f || depth <= 0f || hit.x < topLeft.x || hit.x > bottomRight.x ||
            hit.z > topLeft.z || hit.z < bottomRight.z) return false;

        int rows = 0, cols = 0;
        while (bed.GetPlot(rows, 0) != null) rows++;
        while (bed.GetPlot(0, cols) != null) cols++;
        row = Mathf.Min(rows - 1, Mathf.FloorToInt((topLeft.z - hit.z) / depth * rows));
        col = Mathf.Min(cols - 1, Mathf.FloorToInt((hit.x - topLeft.x) / width * cols));
        return true;
    }

    private void Plant(SoilBed bed, int row, int col, SoilPlot plot)
    {
        if (cropOptions == null || cropOptions.Length == 0 || cropOptions[selectedCrop] == null) {
            Say("No crop selected.");
            return;
        }
        CropDefinition crop = cropOptions[selectedCrop];
        if (crop.isRare && crop.requiresRarePlot && !plot.isRare) {
            Say(crop.cropName + " needs the rare crop plot.");
            return;
        }
        Say(bed.TryPlant(row, col, crop)
            ? "Planted " + crop.cropName + "."
            : "Planting failed. Check plot and seed prefab.");
    }

    private void Harvest(Crop crop)
    {
        if (!crop.CanHarvest()) {
            Say("Not mature: " + Mathf.RoundToInt(crop.CurrentGrowth * 100f) + "% grown.");
            return;
        }
        string name = crop.definition.cropName;
        crop.OnHarvest(); // Also frees the SoilPlot.
        OrganicWaste += Crop.OrganicWastePerHarvest;
        Say("Harvested " + name + ". OrganicWaste carried: " + OrganicWaste.ToString("0.##"));
    }

    private void ToggleSprinkler()
    {
        bool turnOn = !sprinkler.IsEnabled;
        sprinkler.SetActive(turnOn);
        Say(!turnOn ? "Sprinkler OFF." : sprinkler.active
            ? "Sprinkler ON." : "Sprinkler needs water.");
    }

    private void UseCompostAltar()
    {
        if (OrganicWaste > 0f) {
            float remainder = compostAltar.TryRefillResource(OrganicWaste);
            float deposited = OrganicWaste - remainder;
            OrganicWaste = remainder;
            if (deposited > 0f || compostAltar.CurrentResource > 0f)
                compostAltar.SetActive(true); // Also empties a full altar over time.
            Say(deposited > 0f
                ? "Deposited " + deposited.ToString("0.##") + " OrganicWaste. Remaining: " + OrganicWaste.ToString("0.##")
                : "Compost altar full; activated to make room. Try again later.");
        } else {
            // The altar begins with a little stored waste; this starts first-crop fertility.
            compostAltar.ToggleActive();
            Say(compostAltar.active ? "Compost altar ON." : compostAltar.CurrentResource <= 0f
                ? "No OrganicWaste. Harvest mature crops." : "Compost altar OFF.");
        }
    }

    private void Say(string message)
    {
        if (feedbackLabel != null) feedbackLabel.text = message;
        Debug.Log(message, this);
    }
}
