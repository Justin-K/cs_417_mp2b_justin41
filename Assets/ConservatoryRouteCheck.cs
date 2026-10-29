#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

// Editor-only route driver. Uses the same Act entry point as the wand; no resource grants,
// gate writes, shortened growth or timestep injection. HMD input/comfort remain untested.
public sealed class ConservatoryRouteCheck : MonoBehaviour {
    public static string Report="NOT RUN";
    private readonly StringBuilder log=new StringBuilder();
    private ConservatorySession S => ConservatorySession.Current;
    private ConservatoryTarget Target(ConservatoryTarget.Kind kind, bool practice=false) => S.targets.First(t=>t!=null&&t.kind==kind&&t.IsPractice==practice&&t.gameObject.activeInHierarchy);
    private void Check(bool value,string message) { if(!value)throw new Exception(message+" | "+S.Feedback); }
    private void Act(ConservatoryTarget target) {Check(S.Act(target),"Action rejected: "+target.DisplayName);}
    private void Mode(ConservatorySession.ActionMode mode) {Act(S.targets.First(t=>t.kind==ConservatoryTarget.Kind.Action&&t.action==mode));}
    private void Bed(SoilBed bed,ConservatorySession.ActionMode mode) {Mode(mode);Act(bed.GetComponent<ConservatoryTarget>());}
    private void Note(string text) {log.AppendLine(Time.time.ToString("F1")+"s "+text);Report=log.ToString();}
    private IEnumerator Until(Func<bool> predicate,string description,float timeout=50) {
        float end=Time.time+timeout;
        while(!predicate()) {if(Time.time>end)throw new Exception("Timeout: "+description+" | "+S.Feedback);yield return null;}
        Note("PASS "+description);
    }
    private void Reveal(int gate) {Check(S.PendingReveal==gate,"Expected reveal "+gate);Check(S.State.gate==gate-1,"Reveal must precede gate");Act(Target(ConservatoryTarget.Kind.Continue));Check(S.State.gate==gate,"Gate "+gate);}
    private IEnumerator Supplies(int seedCount=1,int organicCount=1,int waterCount=2) {
        while(S.State.inventory.Seeds<seedCount||S.State.inventory.Organic<organicCount) {
            var moss=S.targets.FirstOrDefault(t=>t.kind==ConservatoryTarget.Kind.Forage&&t.gameObject.activeInHierarchy&&Time.time>=t.readyAt);
            if(moss==null){yield return new WaitForSeconds(1);continue;} Act(moss);
        }
        while(S.State.inventory.Water<waterCount){var well=Target(ConservatoryTarget.Kind.Well);if(Time.time>=well.readyAt)Act(well);else yield return new WaitForSeconds(1);}
    }
    private IEnumerator Route() {
        Note("START normal-tuning route; only player actions; seed "+S.generationSeed);
        Check(S.State.gate==0,"Fresh state");
        Bed(S.starterBed,ConservatorySession.ActionMode.Harvest);
        Check(!S.productionBed.gameObject.activeInHierarchy,"No pre-reveal production exposure");Reveal(1);
        Mode(ConservatorySession.ActionMode.Plant);Check(!S.Act(S.productionBed.GetComponent<ConservatoryTarget>()),"production cannot strand progression before practice");
        Bed(S.practiceBed,ConservatorySession.ActionMode.Plant);Check(S.State.practice[0],"Practice planting");
        Mode(ConservatorySession.ActionMode.Plant);Check(!S.Act(S.productionBed.GetComponent<ConservatoryTarget>()),"Missing seed catch");
        yield return Supplies();
        Bed(S.productionBed,ConservatorySession.ActionMode.Plant);Reveal(2);
        Act(Target(ConservatoryTarget.Kind.Sprinkler,true));yield return Until(()=>S.State.practice[1],"practice sprinkler");
        Bed(S.productionBed,ConservatorySession.ActionMode.Water);Bed(S.productionBed,ConservatorySession.ActionMode.Fertilize);
        Mode(ConservatorySession.ActionMode.Plant);int seedBefore=S.State.inventory.Seeds;Check(!S.Act(S.productionBed.GetComponent<ConservatoryTarget>())&&S.State.inventory.Seeds==seedBefore,"occupied catch preserves seed");
        Mode(ConservatorySession.ActionMode.Harvest);Check(!S.Act(S.productionBed.GetComponent<ConservatoryTarget>()),"immature catch");
        yield return Until(()=>S.PendingReveal==3,"production growth stage");Reveal(3);
        Act(Target(ConservatoryTarget.Kind.Altar,true));yield return Until(()=>S.State.practice[2],"practice altar");
        yield return Until(()=>S.productionBed.GetPlot(0,0).crop.CanHarvest(),"basic maturity at normal tuning");
        Bed(S.productionBed,ConservatorySession.ActionMode.Harvest);Reveal(4);
        Check(S.State.harvests==1 && S.State.inventory.Nectar==2,"basic rewards");
        Act(Target(ConservatoryTarget.Kind.Nest,true));yield return Until(()=>S.State.practice[3],"practice Familiar physically reaches plant");
        yield return Supplies();Bed(S.productionBed,ConservatorySession.ActionMode.Plant);
        Act(Target(ConservatoryTarget.Kind.Nest));yield return Until(()=>S.PendingReveal==5,"production Familiar physically pollinates");Reveal(5);
        Bed(S.practiceBed,ConservatorySession.ActionMode.PlantRare);Bed(S.practiceBed,ConservatorySession.ActionMode.Water);Bed(S.practiceBed,ConservatorySession.ActionMode.Fertilize);Act(Target(ConservatoryTarget.Kind.Nest,true));
        yield return Until(()=>S.practiceBed.GetPlot(0,0).crop.CanHarvest(),"practice Rare maturity",60);
        Bed(S.practiceBed,ConservatorySession.ActionMode.Harvest);Check(S.State.practice.All(v=>v),"five practices");Check(S.State.rareHarvests==0,"practice cannot award production trophy");
        Mode(ConservatorySession.ActionMode.PlantRare);Check(!S.Act(S.rareBed.GetComponent<ConservatoryTarget>()),"missing Rare seed catch");
        Bed(S.productionBed,ConservatorySession.ActionMode.Harvest);
        Act(Target(ConservatoryTarget.Kind.RareSeedTrade));
        Mode(ConservatorySession.ActionMode.PlantRare);
        int rareBefore=S.State.inventory.RareSeeds;
        Check(!S.Act(S.productionBed.GetComponent<ConservatoryTarget>()) && S.State.inventory.RareSeeds==rareBefore && S.productionBed.GetPlot(0,0).isEmpty,"incompatible Rare planting preserves Seed and empty bed");
        Note("PASS incompatible Rare Seed lesson and preserved transaction");
        while(S.State.inventory.Nectar>0) Act(Target(ConservatoryTarget.Kind.RareSeedTrade));
        int stableObjectCount=-1;
        for(int cycle=1;cycle<=3;cycle++) {
            yield return Supplies(1,1,1);
            Bed(S.rareBed,ConservatorySession.ActionMode.PlantRare);Bed(S.rareBed,ConservatorySession.ActionMode.Water);Bed(S.rareBed,ConservatorySession.ActionMode.Fertilize);
            yield return Until(()=>S.rareBed.GetPlot(0,0).crop.CanHarvest(),"production Rare cycle "+cycle+" maturity",60);
            Bed(S.rareBed,ConservatorySession.ActionMode.Harvest);
            Check(S.State.rareHarvests==cycle,"one rare reward");Check(!S.Act(S.rareBed.GetComponent<ConservatoryTarget>()),"duplicate harvest rejected");
            yield return null;
            int objects=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            if(stableObjectCount<0)stableObjectCount=objects;else Check(objects==stableObjectCount,"object count stable across completed Rare cycles");
            Note("BALANCE cycle "+cycle+": "+S.State.inventory+" | objects="+objects);
        }
        Check(S.State.inventory.RareSeeds>=1,"next Rare seed remains");
        Act(Target(ConservatoryTarget.Kind.SupplyTrade));
        Note("PASS five gates, five practices, three production Rare cycles, renewable acquisition, produce exchange");
        int expectedWater=S.State.inventory.Water,expectedOrganic=S.State.inventory.Organic;
        int beforeProduce=S.State.inventory.Produce;
        int cropCount=FindObjectsByType<Crop>(FindObjectsSortMode.None).Count(c=>{var b=c.GetComponentInParent<SoilBed>();return b!=null&&!b.practice&&!b.starter&&c.IsPlanted;});
        int expected=beforeProduce+expectedWater/4+expectedOrganic/2+cropCount+1;
        Act(Target(ConservatoryTarget.Kind.Transition));
        Check(!S.Act(Target(ConservatoryTarget.Kind.Transition)),"repeat transition rejected");
        Check(S.AwaitingTransitionConfirmation && S.State.inventory.Water==expectedWater && S.State.inventory.Produce==beforeProduce,"preview preserves balances until confirmation");
        Act(Target(ConservatoryTarget.Kind.Continue));
        yield return Until(()=>S!=null&&S.advanced,"scene transition",40);
        yield return null;
        Check(S.State.inventory.Water==0&&S.State.inventory.Organic==0,"reset supplies");
        Check(S.State.inventory.Produce==Mathf.Min(99,expected),"liquidation arithmetic");
        Check(S.State.gate==5&&S.State.practice.All(v=>v)&&S.State.rareHarvests==3,"carry progression");
        Note("PASS transition, once-only liquidation, carry/reset; rank "+S.State.rank);
        // Destination naturally has depleted water/organic and zero Nectar: prove recovery.
        Check(!S.Act(Target(ConservatoryTarget.Kind.Sprinkler)),"empty sprinkler supply catch");
        Check(!S.Act(Target(ConservatoryTarget.Kind.Altar)),"empty Organic catch");
        Check(!S.Act(Target(ConservatoryTarget.Kind.Nest)),"missing Nectar catch");
        yield return Supplies(1,2,3);
        Act(Target(ConservatoryTarget.Kind.Sprinkler));Act(Target(ConservatoryTarget.Kind.Altar));
        Bed(S.productionBed,ConservatorySession.ActionMode.Plant);
        Bed(S.productionBed,ConservatorySession.ActionMode.Water);Bed(S.productionBed,ConservatorySession.ActionMode.Fertilize);
        yield return Until(()=>S.productionBed.GetPlot(0,0).crop.CanHarvest(),"Nectar recovery basic crop",40);
        Bed(S.productionBed,ConservatorySession.ActionMode.Harvest);
        Act(Target(ConservatoryTarget.Kind.Nest));
        Note("PASS depleted Water/Organic/Nectar catches and ordinary local recovery");
        yield return Supplies(1,1,1);
        Bed(S.rareBed,ConservatorySession.ActionMode.PlantRare);Bed(S.rareBed,ConservatorySession.ActionMode.Water);Bed(S.rareBed,ConservatorySession.ActionMode.Fertilize);
        yield return Until(()=>S.rareBed.GetPlot(0,0).crop.CanHarvest(),"destination Rare recovery cycle",60);
        Bed(S.rareBed,ConservatorySession.ActionMode.Harvest);
        Note("PASS destination fourth Rare harvest, normal renewable recovery after liquidation");
        Note("ROUTE PASSED. Editor action-dispatch evidence only. HMD NOT RUN.");
    }
    private IEnumerator Start() {
        DontDestroyOnLoad(gameObject);
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Route());
        while(stack.Count>0) {
            IEnumerator current=stack.Peek();bool next=false;object value=null;
            try {next=current.MoveNext();if(next)value=current.Current;} catch(Exception ex){Note("FAIL "+ex);yield break;}
            if(!next){stack.Pop();continue;}
            if(value is IEnumerator child)stack.Push(child);else yield return value;
        }
    }
}

#endif




