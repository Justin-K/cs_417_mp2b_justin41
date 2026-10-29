using UnityEngine;
using System.Collections.Generic;

// Visible links mirror the same radius/provenance query used for actual care pulses.
public sealed class ConservatoryInfluenceLinks : MonoBehaviour {
    public Sprinkler sprinkler;
    public CompostAltar altar;
    public Material material;
    private readonly List<LineRenderer> lines=new List<LineRenderer>();
    private float nextCheck;
    private void Update() {
        if(Time.time<nextCheck)return;
        nextCheck=Time.time+.2f;
        bool active=sprinkler!=null?sprinkler.active:altar!=null&&altar.active;
        SoilBed[] beds=!active?null:sprinkler!=null?sprinkler.GetSoilBedsInRadius():altar.GetSoilBedsInRadius();
        int count=beds==null?0:beds.Length;
        while(lines.Count<count) {
            var go=new GameObject("Care connection");go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.positionCount=2;line.useWorldSpace=true;line.startWidth=.012f;line.endWidth=.006f;line.sharedMaterial=material;lines.Add(line);
        }
        for(int i=0;i<lines.Count;i++) {
            lines[i].enabled=i<count;
            if(i<count){lines[i].SetPosition(0,transform.position+Vector3.up*.2f);lines[i].SetPosition(1,beds[i].transform.position+Vector3.up*.25f);}
        }
    }
}
