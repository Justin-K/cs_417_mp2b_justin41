using UnityEngine;
using TMPro;

public sealed class ConservatoryTarget : MonoBehaviour {
    public enum Kind { Bed, Well, Forage, Sprinkler, Altar, Nest, RareSeedTrade, SupplyTrade, Continue, Transition, MoveSprinkler, Action }
    public Kind kind;
    public string title;
    public int requiredGate;
    public int practiceGate;
    public ConservatorySession.ActionMode action;
    public SoilBed bed;
    public Sprinkler sprinkler;
    public CompostAltar altar;
    public TextMeshPro label;
    [HideInInspector] public float readyAt;
    public bool IsPractice => practiceGate > 0;
    private void LateUpdate() {
        if(label==null) return;
        if(bed!=null) label.transform.localPosition=new Vector3(0,1.05f,0);
        var camera=Camera.main;
        bool button=kind==Kind.Action || kind==Kind.Continue || kind==Kind.RareSeedTrade || kind==Kind.SupplyTrade || kind==Kind.Transition || kind==Kind.MoveSprinkler;
        if(camera!=null) {
            if(button) {
                // Turn the entire plate, collider and text together; remain usable from either side.
                var direction=transform.position-camera.transform.position;
                if(direction.sqrMagnitude>.001f) transform.rotation=Quaternion.LookRotation(direction);
                label.transform.localRotation=Quaternion.identity;
            } else {
                var direction=label.transform.position-camera.transform.position;
                if(direction.sqrMagnitude>.001f) label.transform.rotation=Quaternion.LookRotation(direction);
            }
        }
        var session=ConservatorySession.Current;
        bool showPollen=session!=null && session.State!=null && session.State.gate>=4;
        bool nearby=camera!=null && Vector3.Distance(transform.position,camera.transform.position)<3f;
        if(bed!=null) {
            var plot=bed.GetPlot(0,0);
            label.text=DisplayName;
            if(bed.starter) {label.text=DisplayName+"\nMature starter — select HARVEST";return;}
            if(plot!=null && nearby) label.text+="\n"+$"Water {plot.currentMoisture:P0} | Fertility {plot.currentFertility:P0}"+(plot.crop!=null?$"\nGrowth {plot.crop.CurrentGrowth:P0}"+(showPollen?$" | Pollen {plot.crop.CurrentPollination:P0}":""):"\nEmpty bed");
        } else if(kind==Kind.Forage) label.text="Wild Seed Moss"+(nearby?(Time.time<readyAt?"\nRegrowing — return soon":"\nGather Seed + Organic"):"");
    }
    public string DisplayName => string.IsNullOrEmpty(title) ? name : title;
}


