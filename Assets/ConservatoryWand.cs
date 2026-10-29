using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Right-hand wand reuses the rig's tracked controller transform. Press edges dispatch once.
public sealed class ConservatoryWand : MonoBehaviour {
    public Transform rightHand;
    public ConservatorySession session;
    public LineRenderer beam;
    private bool wasPressed;
    private ConservatoryTarget hovered;
    private void Update() {
        if(session==null) return;
        var device=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool tracked=device.isValid;
        Ray ray;
        bool pressed=false;
        if(tracked && rightHand!=null) {
            ray=new Ray(rightHand.position,rightHand.forward);
            device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton,out pressed);
        } else {
            var camera=Camera.main;
            if(camera==null) return;
            ray=Mouse.current!=null?camera.ScreenPointToRay(Mouse.current.position.ReadValue()):new Ray(camera.transform.position,camera.transform.forward);
            pressed=Mouse.current!=null && Mouse.current.leftButton.isPressed;
            if(Keyboard.current!=null) {
                var key=Keyboard.current;
                float x=(key.rightArrowKey.isPressed?1:0)-(key.leftArrowKey.isPressed?1:0);
                float z=(key.upArrowKey.isPressed?1:0)-(key.downArrowKey.isPressed?1:0);
                transform.position+=new Vector3(x,0,z)*Time.deltaTime*1.5f;
            }
        }
        ConservatoryTarget next=null;
        Vector3 end=ray.origin+ray.direction*12;
        if(Physics.Raycast(ray,out RaycastHit hit,12,~0,QueryTriggerInteraction.Collide)) {
            next=hit.collider.GetComponentInParent<ConservatoryTarget>(); end=hit.point;
        }
        if(beam!=null) {beam.SetPosition(0,ray.origin);beam.SetPosition(1,end);beam.enabled=tracked;beam.startColor=beam.endColor=next!=null?Color.cyan:Color.gray;}
        if(next!=hovered) {hovered=next;session.Inspect(hovered);}
        else if(hovered!=null) session.Inspect(hovered);
        if(pressed && !wasPressed && hovered!=null) session.Act(hovered);
        wasPressed=pressed;
    }
}

