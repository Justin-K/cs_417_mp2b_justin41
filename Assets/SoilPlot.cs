using UnityEngine;

public class SoilPlot {
    public bool isRare { get; }
    public Vector3 plotLocation { get; }
    public float currentFertility; // [0.0, 1.0], percentage of current fertility
    public float currentMoisture; // [0.0, 1.0], percentage of current water
    private (int row, int col) gridPosition { get; }
    public Crop crop;
    public bool isEmpty => crop == null; // always agrees with crop occupancy

    public SoilPlot(bool is_rare, Vector3 plot_location, (int, int) grid_position) {
        isRare = is_rare;
        plotLocation = plot_location;
        currentFertility = 0f;
        currentMoisture = 0f;
        crop = null;
        gridPosition = grid_position;
    }
}