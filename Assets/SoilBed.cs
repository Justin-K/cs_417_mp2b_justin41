using UnityEngine;
using System;

public class SoilBed : MonoBehaviour {
    // Marks "start" of bed's available room bounding box
    public GameObject upperLeftCornerRef;
    // Marks "end" of bed's available room bounding box
    public GameObject lowerRightCornerRef;
    // Grid of SoilPlots
    private SoilPlot[,] grid;

    [SerializeField, Min(1)] private int gridRows = 2;
    [SerializeField, Min(1)] private int gridCols = 3;

    // Using references to upper-left and lower-right corners of a bounding box,
    // divide the bounding box into rows x cols cells.
    // Each SoilPlot is positioned at the center of its cell.
    public void BuildGrid(int rows, int cols) {
        if (upperLeftCornerRef == null || lowerRightCornerRef == null) { return; }
        if (rows <= 0 || cols <= 0) { return; }

        // Do not discard references to planted crops by rebuilding an occupied grid.
        if (grid != null) {
            foreach (SoilPlot existingPlot in grid) {
                if (existingPlot != null && !existingPlot.isEmpty) {
                    Debug.LogWarning("Cannot rebuild a SoilBed while it contains crops.", this);
                    return;
                }
            }
        }

        // Convert reference positions into SoilBed's local coordinate space
        Vector3 upperLeft = transform.InverseTransformPoint(upperLeftCornerRef.transform.position);
        Vector3 lowerRight = transform.InverseTransformPoint(lowerRightCornerRef.transform.position);

        // Determine usable dimensions in local space
        (float w, float h) gridBoxSize = (
            lowerRight.x - upperLeft.x,
            upperLeft.z - lowerRight.z
        );

        if (gridBoxSize.w <= 0 || gridBoxSize.h <= 0) { return; }

        // Columns divide width (X), rows divide depth (Z)
        (float w, float h) soilPlotSize = (
            gridBoxSize.w / (float)cols,
            gridBoxSize.h / (float)rows
        );

        if (soilPlotSize.w < 0.05f || soilPlotSize.h < 0.05f) { return; }

        grid = new SoilPlot[rows, cols];

        // The only rare crop plot will have 1 cell (preserves original convention).
        bool rare = rows == 1 && cols == 1;

        for (int i = 0; i < rows; i++) {
            for (int j = 0; j < cols; j++) {
                // Calculate the center of this cell in local space
                Vector3 localCenter = new Vector3(
                    upperLeft.x + (j + 0.5f) * soilPlotSize.w,
                    upperLeft.y,
                    upperLeft.z - (i + 0.5f) * soilPlotSize.h
                );

                // Transform local center into world coordinates
                Vector3 worldCenter = transform.TransformPoint(localCenter);

                // Instantiate and define SoilPlot
                grid[i, j] = new SoilPlot(rare, worldCenter, (i, j));
            }
        }
    }

    public SoilPlot GetPlot(int row, int col) {
        if (grid == null || row < 0 || col < 0 || row >= grid.GetLength(0) || col >= grid.GetLength(1)) {
            return null;
        }
        return grid[row, col];
    }

    // One entry point for planting into a specific SoilPlot.
    public bool TryPlant(int row, int col, CropDefinition definition) {
        if (grid == null) { BuildGrid(gridRows, gridCols); }
        SoilPlot plot = GetPlot(row, col);
        if (plot == null || definition == null || !plot.isEmpty) { return false; }

        GameObject cropObject = new GameObject($"{definition.cropName} (Crop)");
        Crop plantedCrop = cropObject.AddComponent<Crop>();
        plantedCrop.Initialize(definition);

        if (!plantedCrop.OnPlant(plot, transform)) {
            Destroy(cropObject);
            return false;
        }
        return true;
    }
    // Distribute water evenly across plots (including empty plots).
    // Moisture is [0, 1] per plot; return any water that does not fit.
    public float TryAddWater(float amount) {
        if (amount <= 0f) { return 0f; }
        if (grid == null) { BuildGrid(gridRows, gridCols); }
        if (grid == null) { return amount; }

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        float perPlot = amount / (rows * cols);
        float remaining = amount;

        for (int i = 0; i < rows; i++) {
            for (int j = 0; j < cols; j++) {
                SoilPlot p = grid[i, j];
                float added = Mathf.Min(perPlot, Mathf.Max(0f, 1f - p.currentMoisture));
                p.currentMoisture = Mathf.Clamp01(p.currentMoisture + added);
                remaining -= added;
            }
        }
        return Mathf.Max(0f, remaining);
    }
    // Distribute fertility like TryAddWater, with the same [0, 1] cap per plot.
    // Returns any unused fertility so an altar only spends accepted OrganicWaste.
    public float TryAddFertility(float amount) {
        if (amount <= 0f) { return 0f; }
        if (grid == null) { BuildGrid(gridRows, gridCols); }
        if (grid == null) { return amount; }

        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        float perPlot = amount / (rows * cols);
        float remaining = amount;

        for (int i = 0; i < rows; i++) {
            for (int j = 0; j < cols; j++) {
                SoilPlot p = grid[i, j];
                float added = Mathf.Min(perPlot, Mathf.Max(0f, 1f - p.currentFertility));
                p.currentFertility = Mathf.Clamp01(p.currentFertility + added);
                remaining -= added;
            }
        }
        return Mathf.Max(0f, remaining);
    }

    // Start is called once before the first execution of Update
    void Start() {
        if (grid == null) { BuildGrid(gridRows, gridCols); }
    }
}
