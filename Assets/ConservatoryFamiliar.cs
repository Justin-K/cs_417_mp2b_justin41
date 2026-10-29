using UnityEngine;

// One pollinator per nest, sustained until scene transition. It visits live plants.
public sealed class ConservatoryFamiliar : MonoBehaviour {
    public bool practice;
    public float speed = 1.5f;
    public float radius = 12f;
    private Vector3 home;
    private Crop target;
    public bool HasPollinated { get; private set; }
    public event System.Action<Crop> Pollinated;
    private void Start() { home = transform.position; }
    private void Update() {
        if (target == null || !target.IsPlanted || target.CurrentPollination >= 1f) {
            target = null;
            foreach (Crop crop in FindObjectsByType<Crop>(FindObjectsSortMode.None)) {
                SoilBed bed = crop.GetComponentInParent<SoilBed>();
                if (bed == null || bed.practice != practice || bed.starter || !crop.IsPlanted || crop.CurrentPollination >= 1f) continue;
                if (Vector3.Distance(home, crop.transform.position) <= radius) { target=crop; break; }
            }
        }
        Vector3 destination = target == null ? home : target.transform.position + Vector3.up * 0.3f;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        if (target != null && Vector3.Distance(transform.position, destination) < 0.05f) {
            Crop reached=target;
            reached.Pollinate(1f);
            HasPollinated=true;
            target=null;
            Pollinated?.Invoke(reached);
        }
    }
}
