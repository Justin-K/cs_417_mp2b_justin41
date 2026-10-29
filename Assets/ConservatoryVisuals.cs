using UnityEngine;

public static class ConservatoryVisuals {
    public static void Fit(GameObject visual,float size,Vector3 anchor,bool restOnSoil) {
        if(visual==null) return;
        var renderers=visual.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0) return;
        Bounds b=renderers[0].bounds;
        foreach(var renderer in renderers) b.Encapsulate(renderer.bounds);
        float extent=Mathf.Max(b.size.x,b.size.y,b.size.z);
        if(extent>.001f) visual.transform.localScale*=size/extent;
        b=renderers[0].bounds;
        foreach(var renderer in renderers) b.Encapsulate(renderer.bounds);
        var point=restOnSoil?new Vector3(b.center.x,b.min.y,b.center.z):b.center;
        visual.transform.position+=anchor-point;
    }
}
