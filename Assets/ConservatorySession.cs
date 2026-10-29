using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ConservatorySession : MonoBehaviour {
    public enum ActionMode { Inspect, Plant, Water, Fertilize, Harvest, PlantRare }
    public CropDefinition basicDefinition, rareDefinition, starterDefinition;
    public GameObject familiarPrefab;
    public WorldInfoPanel book;
    public SoilBed starterBed, productionBed, rareBed, practiceBed;
    public Sprinkler productionSprinkler, practiceSprinkler;
    public CompostAltar productionAltar, practiceAltar;
    public Transform productionNest, practiceNest;
    public GameObject[] trophies;
    public bool advanced;
    public int generationSeed = 417;
    public ConservatoryTarget[] targets;
    public Transform[] foragePatches;
    public GameObject foragePrefab;
    public string advancedScene = "ConservatoryGarden";
    public static ConservatorySession Current { get; private set; }
    public sealed class Progress {
        public ConservatoryInventory inventory = new ConservatoryInventory();
        public int gate, harvests, rareHarvests;
        public bool[] practice = new bool[5];
        public bool transitioned;
        public string rank="D";
    }
    private static Progress carry;
    public Progress State { get; private set; }
    public ActionMode SelectedAction { get; private set; }
    public int PendingReveal { get; private set; }
    public string Feedback { get; private set; }
    public string TargetInfo { get; private set; }
    public bool Transitioning { get; private set; }
    public bool AwaitingTransitionConfirmation { get; private set; }
    public ConservatoryFamiliar ProductionFamiliar { get; private set; }
    private ConservatoryFamiliar practiceFamiliar;
    private float refreshAt;
    private bool sprinklerAway;
    private Vector3 sprinklerHome;
    private string tally;
    private readonly string[] reveals = {
        "A BED IS A SEED'S HOME\nPlanting places a living Seed in soil. Water and fertility let it grow.",
        "A NEIGHBOR THAT SHARES RAIN\nThe Moon Sprinkler spends stored Water on nearby beds. Distance matters.",
        "LEFTOVERS FEED THE SOIL\nThe Compost Altar turns Organic material into neighboring soil fertility.",
        "A NECTAR-FED HOME FOR A HELPER\nFeed the Familiar Nest. A pollinator flies to your plants and leaves pollen.",
        "THE RARE CROP COMBINES YOUR SKILLS\nA Rare Seed needs water, fertile soil and a Familiar's pollen before it grows. Harvest and repeat."
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { carry=null; Current=null; }
    private void Awake() {
        Current=this;
        State=advanced && carry!=null ? carry : new Progress();
        SelectedAction=ActionMode.Inspect;
        Feedback="Right trigger: activate the pointed target. Left stick: move; right stick: turn. Select HARVEST, then point at the STARTER plant and press the trigger.";
        sprinklerHome=productionSprinkler.InfluenceTransform.position;
    }
    private void Start() {
        if(advanced && carry==null) { SceneManager.LoadScene("ConservatoryNursery"); return; }
        GenerateForage();
        InitializeBed(starterBed); InitializeBed(productionBed); InitializeBed(rareBed); InitializeBed(practiceBed);
        if (!advanced) starterBed.TryPlant(0,0,starterDefinition);
        else Feedback="Welcome to the Moonlit Garden. Your unlocks and carried supplies remain. Tend Rare Crops indefinitely.";
        RefreshVisibility(); RefreshBook();
    }
    private static void InitializeBed(SoilBed bed) { if (bed!=null) bed.BuildGrid(1,1); }
    private void GenerateForage() {
        if (foragePrefab==null || foragePatches==null) return;
        var random=new System.Random(generationSeed);
        int[] order=Enumerable.Range(0,foragePatches.Length).OrderBy(i=>random.Next()).ToArray();
        for(int i=0;i<Mathf.Min(3,order.Length);i++) {
            var instance=Instantiate(foragePrefab,foragePatches[order[i]].position,Quaternion.identity);
            instance.name="Wild Seed Moss "+i;
            var target=instance.GetComponent<ConservatoryTarget>();
            if(target!=null) { target.title="Wild Seed Moss | gather Seed + Organic"; target.readyAt=0; }
        }
        targets=FindObjectsByType<ConservatoryTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    }
    public void Inspect(ConservatoryTarget target) {
        if(target==null) { TargetInfo="Point at a labelled garden object."; return; }
        TargetInfo=target.DisplayName;
        if(target.bed!=null) {
            var p=target.bed.GetPlot(0,0);
            if(p!=null) TargetInfo+=$"\nWater {p.currentMoisture:P0} / Fertility {p.currentFertility:P0}";
            if(p!=null && p.crop!=null) TargetInfo+=$" / Growth {p.crop.CurrentGrowth:P0}\nNeeds water/fertility >= 10%"+(State.gate>=4?$"; pollen {p.crop.CurrentPollination:P0}":"")+(p.crop.definition.requiresPollination?" (needs 100%)":"");
        }
        if(target.sprinkler!=null) TargetInfo+=$"\nStored Water {target.sprinkler.CurrentResource:F1}; range {target.sprinkler.effectRadius:F1}m";
        if(target.altar!=null) TargetInfo+=$"\nStored Organic {target.altar.CurrentResource:F1}; range {target.altar.effectRadius:F1}m";
    }
    public bool Act(ConservatoryTarget target) {
        if(target==null || !target.gameObject.activeInHierarchy) return false;
        if(Transitioning) {
            if(AwaitingTransitionConfirmation && target.kind==ConservatoryTarget.Kind.Continue) { AwaitingTransitionConfirmation=false;return true; }
            return false;
        }
        Inspect(target);
        if(PendingReveal>0 && target.kind!=ConservatoryTarget.Kind.Continue) return Say("Read the reveal, then select CONTINUE to open the new mechanic.");
        if(target.kind==ConservatoryTarget.Kind.Action) { SelectedAction=target.action; Feedback=$"Selected {SelectedAction}. Point at a bed and press the trigger."; RefreshBook(); return true; }
        bool result=false;
        switch(target.kind) {
            case ConservatoryTarget.Kind.Continue: result=AcceptReveal(); break;
            case ConservatoryTarget.Kind.Bed: result=ActBed(target); break;
            case ConservatoryTarget.Kind.Well:
                if(Time.time<target.readyAt) return Say("The well is refilling. Try again in two seconds.");
                result=State.inventory.Add(ConservatoryInventory.Resource.Water,4)>0;
                if(result) {target.readyAt=Time.time+2; Feedback="Collected 4 Water (up to capacity). The well renews in 2 seconds.";} else Say("Water pouch full (99). Water a bed or fill the sprinkler first."); break;
            case ConservatoryTarget.Kind.Forage:
                if(Time.time<target.readyAt) return Say("This patch is regrowing. Try another patch or return in five seconds.");
                if(State.inventory.Seeds==99 && State.inventory.Organic==99) return Say("Seed and Organic pouches full. Plant or fertilize first.");
                State.inventory.Add(ConservatoryInventory.Resource.Seed,1); State.inventory.Add(ConservatoryInventory.Resource.Organic,1);
                target.readyAt=Time.time+5; Feedback="Gathered 1 Seed + 1 Organic. This living patch renews in 5 seconds."; result=true; break;
            case ConservatoryTarget.Kind.Sprinkler: result=RefillSprinkler(target); break;
            case ConservatoryTarget.Kind.Altar: result=RefillAltar(target); break;
            case ConservatoryTarget.Kind.Nest: result=Summon(target.IsPractice); break;
            case ConservatoryTarget.Kind.RareSeedTrade:
                result=State.inventory.BuyRareSeed(); Feedback=result?"Traded 1 Nectar for 1 Rare Seed.":"Need 1 Nectar and Rare Seed pouch space. Grow and harvest a basic crop for Nectar."; break;
            case ConservatoryTarget.Kind.SupplyTrade:
                result=State.inventory.ExchangeProduce(); Feedback=result?"Spent 1 Produce: +2 Seeds, +4 Water, +2 Organic (up to capacity).":"Need 1 Enchanted Produce and supply space. Harvest a Rare Crop, or use free moss/well to recover."; break;
            case ConservatoryTarget.Kind.MoveSprinkler:
                sprinklerAway=!sprinklerAway; productionSprinkler.InfluenceTransform.position=sprinklerHome+(sprinklerAway?Vector3.right*5:Vector3.zero);
                Feedback=sprinklerAway?"Sprinkler moved to the distant stand. Beds outside its radius stop receiving water.":"Sprinkler returned beside the production beds."; result=true; break;
            case ConservatoryTarget.Kind.Transition:
                if(State.rareHarvests<1 || State.practice.Any(p=>!p)) return Say("Complete all five practices and harvest a production Rare Crop before entering the garden.");
                StartCoroutine(Transition()); result=true; break;
        }
        RefreshBook(); return result;
    }
    private bool ActBed(ConservatoryTarget target) {
        SoilBed bed=target.bed;
        if(bed==null) return Say("This is not a compatible soil bed. Select a labelled empty bed.");
        var plot=bed.GetPlot(0,0);
        if(plot==null) return Say("This bed is not initialized.");
        if(SelectedAction==ActionMode.Inspect) {Feedback="Inspected. Choose PLANT, WATER, FERTILIZE or HARVEST on the spellbook."; return true;}
        if(SelectedAction==ActionMode.Plant || SelectedAction==ActionMode.PlantRare) {
            if(!plot.isEmpty) return Say("Planting rejected: this bed is occupied. Harvest its mature crop or use an empty bed.");
            if(bed.starter) return Say("This starter display is not a planting bed. Use the production or practice bed.");
            if(!bed.practice && State.gate==1 && !State.practice[0]) return Say("First complete planting in the protected practice bed. Then plant this production bed.");
            bool rare=SelectedAction==ActionMode.PlantRare;
            if(rare && !plot.isRare) return Say("Incompatible Seed: a Rare Seed needs the Moon Blossom plot. Use the Rare production bed or the protected Rare practice bed. Nothing spent.");
            var resource=rare?ConservatoryInventory.Resource.RareSeed:ConservatoryInventory.Resource.Seed;
            if(!bed.practice && State.inventory.Count(resource)<1) return Say(rare?"No Rare Seed. Trade 1 Nectar from a basic harvest at the seed exchange.":"No Seed. Gather renewable Wild Seed Moss beside the path.");
            if(!bed.TryPlant(0,0,rare?rareDefinition:basicDefinition)) return Say("Incompatible planting. Use an empty compatible Conservatory bed.");
            if(!bed.practice) State.inventory.Spend(resource,1);
            plot.crop.Harvested+=OnHarvested;
            Feedback="Seed planted. Supply water and fertility"+(rare?" and summon a Familiar for pollen.":".");
            if(bed.practice && State.gate==1) CompletePractice(1,"You gave the Seed a home.");
            if(!bed.practice && State.gate==1 && State.practice[0]) Reveal(2);
            return true;
        }
        if(SelectedAction==ActionMode.Water || SelectedAction==ActionMode.Fertilize) {
            bool water=SelectedAction==ActionMode.Water;
            if(bed.starter) return Say("The starter is already mature. Select HARVEST.");
            if(bed.practice && ((State.gate==2 && water)||(State.gate==3 && !water))) return Say(water?"For this practice, use the practice sprinkler to share rain.":"For this practice, use the practice altar to convert Organic.");
            if((water?plot.currentMoisture:plot.currentFertility)>=.999f) return Say("This bed is already full. No supply spent.");
            var resource=water?ConservatoryInventory.Resource.Water:ConservatoryInventory.Resource.Organic;
            if(!bed.practice && State.inventory.Count(resource)<1) return Say(water?"No Water. Collect renewable water from the well.":"No Organic. Gather Wild Seed Moss or harvest a basic crop.");
            if(water) bed.TryAddWater(1); else bed.TryAddFertility(1);
            if(!bed.practice) State.inventory.Spend(resource,1);
            Feedback=water?"Water supplied. The bed now has moisture for growth.":"Organic worked into the soil. Fertility restored."; return true;
        }
        if(plot.crop==null) return Say("Nothing to harvest. Plant a Seed in this empty bed.");
        if(!plot.crop.CanHarvest()) return Say($"Harvest rejected: growth {plot.crop.CurrentGrowth:P0}/100%. Needs water and fertility >=10%"+(plot.crop.definition.requiresPollination?" and pollen 100%.":".")+" Restore the missing care and wait.");
        if(!bed.practice && !bed.starter && State.gate<4 && (State.gate!=3 || !State.practice[2])) return Say("Finish the revealed practice before harvesting this production crop. Your mature crop will wait.");
        if(bed.starter) {
            if(!plot.crop.TryHarvest()) return false;
            Reveal(1); return true;
        }
        return plot.crop.TryHarvest();
    }
    private void OnHarvested(Crop crop) {
        var bed=crop.GetComponentInParent<SoilBed>();
        if(bed==null) return;
        if(bed.practice) {if(State.gate==5 && crop.definition.isRare) CompletePractice(5,"Rare practice complete. Produce and replant in the production Rare bed.");return;}
        State.harvests++;
        if(crop.definition.isRare) {
            State.rareHarvests++; State.inventory.Add(ConservatoryInventory.Resource.Produce,Mathf.Max(1,crop.definition.harvestYield)); State.inventory.Add(ConservatoryInventory.Resource.RareSeed,1);
            Feedback="Rare harvest: +3 Produce and your next Rare Seed. Refill care, plant and keep cultivating.";
        } else {
            State.inventory.Add(ConservatoryInventory.Resource.Seed,2); State.inventory.Add(ConservatoryInventory.Resource.Organic,2); State.inventory.Add(ConservatoryInventory.Resource.Nectar,2);
            Feedback="Basic harvest: +2 Seeds, +2 Organic, +2 Nectar.";
            if(State.gate==3 && State.practice[2]) Reveal(4);
        }
    }
    private bool RefillSprinkler(ConservatoryTarget target) {
        var sprinkler=target.sprinkler;
        if(sprinkler==null) return false;
        if(sprinkler.CurrentResource>=sprinkler.maxResource-.01f) return Say("Sprinkler full; let it water nearby beds before adding more.");
        if(!target.IsPractice && State.inventory.Water<1) return Say("Empty sprinkler supply: need Water. Collect it from the renewable well, then retry.");
        float amount=Mathf.Min(1,sprinkler.maxResource-sprinkler.CurrentResource);
        sprinkler.TryRefillResource(amount);
        if(!target.IsPractice) State.inventory.Spend(ConservatoryInventory.Resource.Water,1);
        sprinkler.SetActive(true); sprinkler.TryAffectInscribedSoilBeds();
        Feedback="Sprinkler supplied: stored Water flows only to compatible neighbors in range."; return true;
    }
    private bool RefillAltar(ConservatoryTarget target) {
        var altar=target.altar;
        if(altar==null) return false;
        if(altar.CurrentResource>=altar.maxResource-.01f) return Say("Altar full. Let neighboring soil accept fertility first.");
        if(!target.IsPractice && State.inventory.Organic<1) return Say("Altar needs Organic. Gather Wild Seed Moss or harvest a basic crop.");
        altar.TryRefillResource(Mathf.Min(1,altar.maxResource-altar.CurrentResource));
        if(!target.IsPractice) State.inventory.Spend(ConservatoryInventory.Resource.Organic,1);
        altar.SetActive(true); altar.TryAffectInscribedSoilBeds(); Feedback="Organic converted into neighboring soil fertility."; return true;
    }
    private bool Summon(bool practice) {
        if((practice?practiceFamiliar:ProductionFamiliar)!=null) return Say("This nest already has a Familiar. It will visit new plants; no Nectar spent.");
        if(!practice && State.inventory.Nectar<1) return Say("Missing Nectar: grow and harvest a basic crop for 2 Nectar, then feed this nest.");
        if(familiarPrefab==null) return Say("Familiar prefab is missing.");
        var home=practice?practiceNest:productionNest;
        var go=new GameObject(practice?"Practice pollinator":"Production pollinator");
        go.transform.position=home.position+Vector3.up*.25f;
        var visual=Instantiate(familiarPrefab,go.transform);
        ConservatoryVisuals.Fit(visual,.4f,go.transform.position,false);
        var familiar=go.AddComponent<ConservatoryFamiliar>(); familiar.practice=practice;
        foreach(var collider in go.GetComponentsInChildren<Collider>()) collider.enabled=false;
        familiar.Pollinated+=crop=> {
            if(practice && State.gate==4) CompletePractice(4,"The Familiar reached the plant and delivered pollen.");
            if(!practice && State.gate==4 && State.practice[3]) Reveal(5);
        };
        if(practice) practiceFamiliar=familiar;
        else {ProductionFamiliar=familiar; State.inventory.Spend(ConservatoryInventory.Resource.Nectar,1);}
        Feedback="Familiar attracted. Watch it fly to a plant and deliver pollen."; return true;
    }
    private void Reveal(int gate) {
        if(PendingReveal!=0 || gate!=State.gate+1) return;
        PendingReveal=gate; Feedback=reveals[gate-1]; RefreshBook();
    }
    private bool AcceptReveal() {
        if(PendingReveal==0) return Say("No reveal waiting. Follow the current objective.");
        State.gate=PendingReveal; PendingReveal=0;
        SetupPractice(); RefreshVisibility(); Feedback="New mechanic available. "+PracticeInstruction(); return true;
    }
    private void SetupPractice() {
        var plot=practiceBed.GetPlot(0,0);
        if(plot.crop!=null) { Destroy(plot.crop.gameObject); plot.crop=null; }
        if(practiceFamiliar!=null) { Destroy(practiceFamiliar.gameObject); practiceFamiliar=null; }
        practiceSprinkler.SetActive(false); practiceAltar.SetActive(false);
        plot.currentMoisture=0; plot.currentFertility=0;
        if(State.gate==4) {
            practiceBed.TryPlant(0,0,basicDefinition); plot.currentMoisture=1; plot.currentFertility=1;
        }
    }
    private void CompletePractice(int gate,string message) {
        if(State.practice[gate-1]) return;
        State.practice[gate-1]=true; Feedback="PRACTICE COMPLETE: "+message; RefreshVisibility();
    }
    private void Update() {
        var plot=productionBed.GetPlot(0,0);
        if(PendingReveal==0 && State.gate==2 && State.practice[1] && plot?.crop!=null && plot.crop.CurrentGrowth>=basicDefinition.stage1Threshold) Reveal(3);
        var practice=practiceBed.GetPlot(0,0);
        if(practice!=null && State.gate==2 && practice.currentMoisture>=.6f) CompletePractice(2,"The sprinkler brought soil moisture to at least 60%.");
        if(practice!=null && State.gate==3 && practice.currentFertility>=.6f) CompletePractice(3,"The altar raised fertility to at least 60%.");
        if(State.gate==4 && State.practice[3] && ProductionFamiliar!=null && ProductionFamiliar.HasPollinated) Reveal(5);
        if(trophies!=null && trophies.Length>=3) {
            trophies[0].SetActive(State.harvests>=1); trophies[1].SetActive(State.rareHarvests>=1); trophies[2].SetActive(State.rareHarvests>=3);
        }
        if(Time.time>=refreshAt) {refreshAt=Time.time+.15f; RefreshBook();}
    }
    private void RefreshVisibility() {
        foreach(var target in targets) {
            if(target==null) continue;
            bool visible=State.gate>=target.requiredGate;
            if(target.IsPractice) visible=State.gate==target.practiceGate || (target.kind==ConservatoryTarget.Kind.Bed && State.gate>=1);
            if(target.IsPractice && target.kind==ConservatoryTarget.Kind.Nest && State.gate==5) visible=true;
            if(target.bed==starterBed && State.gate>0) visible=false;
            if(advanced && (target.IsPractice || target.bed==starterBed || target.kind==ConservatoryTarget.Kind.Transition)) visible=false;
            target.gameObject.SetActive(visible);
        }
    }
    private string Goal() {
        switch(State.gate) {
            case 0: return "Select HARVEST, then collect the STARTER plant.";
            case 1: return "Complete practice planting, then plant a production Seed.";
            case 2: return "Practice sprinkler (60%), then water/fertilize production to first growth stage.";
            case 3: return "Practice compost (60%), then mature and harvest a production crop.";
            case 4: return "Practice Familiar pollination, then plant and pollinate a production crop.";
            default: return "Practice Rare cultivation, then keep growing and harvesting production Rare Crops.";
        }
    }
    private string PracticeInstruction() {
        if(State.gate==0) return "";
        if(State.practice[State.gate-1]) return "Practice completed. Continue the production objective.";
        switch(State.gate) {
            case 1:return "Practice: select PLANT and use the practice bed. Its Seed is protected.";
            case 2:return "Practice: activate the practice sprinkler until the practice bed reaches 60% moisture.";
            case 3:return "Practice: activate the practice altar until the practice bed reaches 60% fertility.";
            case 4:return "Practice: feed the practice nest and watch its Familiar pollinate the practice plant.";
            default:return "Practice: select PLANT RARE and plant; supply WATER and FERTILIZE; feed the practice nest, wait, then HARVEST.";
        }
    }
    public void RefreshBook() {
        if(book==null || State==null) return;
        string resources=State.gate==0 ? "" : $"Seeds {State.inventory.Seeds}  Water {State.inventory.Water}  Organic {State.inventory.Organic}";
        if(State.gate>=4) resources+=$"\nNectar {State.inventory.Nectar}";
        if(State.gate>=5) resources+=$"  Rare Seeds {State.inventory.RareSeeds}  Produce {State.inventory.Produce}";
        string content=PendingReveal>0 ? $"UNLOCK REVEAL {PendingReveal}/5\n{reveals[PendingReveal-1]}\n\nSelect CONTINUE to enter the practice." : $"{(advanced?"MOONLIT GARDEN":"CONSERVATORY NURSERY")} | {SelectedAction}\n{Goal()}\n{PracticeInstruction()}\n\n{resources}\n\n{Feedback}\n\n{TargetInfo}";
        if(Transitioning) content=tally;
        book.SetMessage(content);
    }
    private bool Say(string message) { Feedback=message; RefreshBook(); return false; }
    public static string RankFor(int produce) => produce>=15?"S":produce>=9?"A":produce>=6?"B":produce>=3?"C":"D";
    private IEnumerator Transition() {
        if(State.transitioned || Transitioning) yield break;
        Transitioning=true; AwaitingTransitionConfirmation=true;
        int water=State.inventory.Water, organic=State.inventory.Organic;
        int crops=FindObjectsByType<Crop>(FindObjectsSortMode.None).Count(c=>{var b=c.GetComponentInParent<SoilBed>(); return b!=null&&!b.practice&&!b.starter&&c.IsPlanted;});
        int familiar=ProductionFamiliar!=null?1:0;
        int conversion=water/4+organic/2+crops+familiar;
        State.rank=RankFor(State.inventory.AccruedProduce);
        tally=$"ENTER THE MOONLIT GARDEN\nRank {State.rank}: {State.inventory.AccruedProduce} accrued Produce\nCarry: Seeds {State.inventory.Seeds}, Rare Seeds {State.inventory.RareSeeds}, Nectar {State.inventory.Nectar}, Produce {State.inventory.Produce}\nReset: Water {water}, Organic {organic}, planted crops {crops}, Familiar {familiar}\nConversion: floor(Water/4) + floor(Organic/2) + crops + Familiar = {conversion} Produce\nUnlocks and achievements carry. Renewable moss/well remain.\nSelect CONTINUE after reviewing this tally.";
        RefreshBook(); yield return new WaitUntil(()=>!AwaitingTransitionConfirmation);
        if(water>0) State.inventory.Spend(ConservatoryInventory.Resource.Water,water);
        if(organic>0) State.inventory.Spend(ConservatoryInventory.Resource.Organic,organic);
        int credited=0;
        for(int i=0;i<conversion;i++) {credited+=State.inventory.Add(ConservatoryInventory.Resource.Produce,1); tally=$"LIQUIDATION\n{ i+1}/{conversion} conversion units tallied; {credited} credited\nCarried Produce {State.inventory.Produce} (cap 99)\nRank {State.rank}\nSeeds, Nectar, Rare Seeds, unlocks and trophies carry.";RefreshBook();yield return new WaitForSeconds(.5f);}
        State.transitioned=true; carry=State;
        SceneManager.LoadScene(advancedScene);
    }
}








